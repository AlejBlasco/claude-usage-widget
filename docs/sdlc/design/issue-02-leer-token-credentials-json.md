# Design: [F0] Leer token desde .credentials.json

## Requirements Reference
`docs/sdlc/requirements/issue-02-leer-token-credentials-json.md` (Issue #2, milestone "F0 — Núcleo de validación"). Documento cerrado, sin preguntas abiertas pendientes.

## Resumen / Alcance

Este diseño cubre exclusivamente la lectura y el parseo del token OAuth desde el fichero fijo `%USERPROFILE%\.claude\.credentials.json`, expuesto al resto de la aplicación a través de un puerto pequeño (`ITokenProvider`) en Application, implementado por un adaptador (`CredentialsFileTokenProvider`) en Infrastructure. El "resultado sin datos" se modela como un tipo de dominio explícito (`TokenResult`) en vez de `string?` o excepciones.

Fuera de alcance (explícitamente, según Technical Notes del documento de requisitos y el roadmap de `CLAUDE.md`):
- Llamadas a la API de Anthropic, cálculo de countdown/porcentajes de rate limit, renderizado — clases separadas, fases posteriores.
- Refresco de OAuth propio (F2 gestiona 401/403 con un aviso claro).
- Configurabilidad de la ruta del fichero (llega en F2 con `config.json`).
- Integración con Windows Credential Manager (no está en el roadmap MVP para F0).

El diseño se apoya en el scaffolding F0 ya mergeado (`ClaudeMeter.sln` + 4 proyectos en `src/` y sus tests en `test/`, actualmente vacíos de lógica de negocio) y respeta sus referencias de proyecto tal y como están declaradas hoy:

- `ClaudeMeter.Domain` — sin `ProjectReference` (núcleo puro, confirmado inspeccionando su `.csproj`).
- `ClaudeMeter.Application` → referencia `ClaudeMeter.Domain`.
- `ClaudeMeter.Infrastructure` → referencia `ClaudeMeter.Application` (y transitivamente `Domain`).
- `ClaudeMeter.Infrastructure.Tests` → referencia `ClaudeMeter.Infrastructure` (y transitivamente `Application`/`Domain`).
- `ClaudeMeter.Domain.Tests` → referencia `ClaudeMeter.Domain`.

No se necesita ningún cambio en los `.csproj` existentes ni nuevas `ProjectReference`: las dependencias que este issue necesita ya están declaradas.

## Arquitectura

### Ubicación exacta de cada pieza

| Tipo | Proyecto | Namespace | Fichero propuesto |
|---|---|---|---|
| `TokenResult` (record) + `TokenResultStatus` (enum) | `ClaudeMeter.Domain` | `ClaudeMeter.Domain.Authentication` | `src/ClaudeMeter.Domain/Authentication/TokenResult.cs` |
| `ITokenProvider` (puerto) | `ClaudeMeter.Application` | `ClaudeMeter.Application.Abstractions` | `src/ClaudeMeter.Application/Abstractions/ITokenProvider.cs` |
| `CredentialsFileTokenProvider` (adaptador) | `ClaudeMeter.Infrastructure` | `ClaudeMeter.Infrastructure.Authentication` | `src/ClaudeMeter.Infrastructure/Authentication/CredentialsFileTokenProvider.cs` |
| `CredentialsFileTokenProviderTests` | `ClaudeMeter.Infrastructure.Tests` | `ClaudeMeter.Infrastructure.Tests.Authentication` | `test/ClaudeMeter.Infrastructure.Tests/Authentication/CredentialsFileTokenProviderTests.cs` |

**Rationale de nombres de carpeta/namespace**: se usa `Authentication` como sub-namespace vertical (feature folder) en Domain e Infrastructure, y `Abstractions` en Application para los puertos — convención habitual en plantillas Clean Architecture de .NET (p. ej. Jason Taylor / Ardalis templates), coherente con la guía `dotnet-solution-structure.md` ("Folders mirror namespaces"). Alternativa considerada: namespace raíz plano (`ClaudeMeter.Domain`, sin subcarpeta) — rechazada porque el roadmap ya anticipa más tipos de dominio (`UsageSnapshot`, `RateLimitWindow`, `MascotState`) que no tienen relación con autenticación; separar por feature evita que `Domain`/`Application` se conviertan en una carpeta plana según crezcan las fases F1-F4.

### Flujo de dependencias (Clean Architecture, dependencias apuntan hacia dentro)

```
ClaudeMeter.Infrastructure.Tests
        │  (ProjectReference)
        ▼
ClaudeMeter.Infrastructure  ── implementa ──►  ITokenProvider (Application)
   CredentialsFileTokenProvider                       │
        │  usa                                        │ referencia como tipo de retorno
        ▼                                              ▼
   System.IO / System.Text.Json              ClaudeMeter.Domain
   (detalles de infraestructura)                 TokenResult / TokenResultStatus
                                                        ▲
                                                        │ (sin dependencias externas)
```

- `ClaudeMeter.Domain` no conoce `ITokenProvider` ni `CredentialsFileTokenProvider` — solo expone el value object `TokenResult`, sin ninguna dependencia de paquete NuGet ni de E/S.
- `ClaudeMeter.Application` define `ITokenProvider` y lo tipa con `TokenResult` (Domain) como retorno; **no referencia `System.IO`, `System.Text.Json` ni ningún tipo de `ClaudeMeter.Infrastructure`** — cumple el primer criterio de aceptación literalmente.
- `ClaudeMeter.Infrastructure` implementa `ITokenProvider` mediante `CredentialsFileTokenProvider`, que es la única pieza que toca el sistema de ficheros y el parseo JSON.
- El futuro composition root (Desktop, F1) resuelve `ITokenProvider` → `CredentialsFileTokenProvider` vía DI; este issue no incluye ese cableado (no hay `Program.cs`/`App.xaml.cs` con contenedor DI todavía, y no es parte del alcance de Issue #2).

Este flujo preserva las dos reglas de `CLAUDE.md` más relevantes aquí: (a) las fuentes de "datos" (aquí, de token) devuelven "sin datos" de forma consistente en vez de lanzar excepciones para el caso esperado/vacío; (b) leer el token queda en una clase separada de las que llamarán a la API/calcularán el countdown/renderizarán.

## Modelo de Datos / Interfaces

### `TokenResult` (Domain)

```csharp
namespace ClaudeMeter.Domain.Authentication;

/// <summary>
/// Estados explícitos en los que puede resolverse el intento de obtener
/// el token OAuth de Claude Code desde su fuente de credenciales.
/// </summary>
public enum TokenResultStatus
{
    /// <summary>Se obtuvo un token utilizable.</summary>
    Success,

    /// <summary>El fichero de credenciales no existe en la ruta esperada.</summary>
    FileNotFound,

    /// <summary>El fichero existe pero su contenido no es JSON válido.</summary>
    InvalidJson,

    /// <summary>
    /// El JSON es válido pero falta el objeto/campo del token, o el token
    /// está vacío o en blanco.
    /// </summary>
    TokenMissing
}

/// <summary>
/// Representa el resultado de intentar obtener el token OAuth. Modela el
/// caso "sin datos" como un tipo de dominio explícito en vez de
/// <c>string?</c> o un <c>Result&lt;string&gt;</c> genérico, de forma que
/// los consumidores nunca necesitan lanzar ni capturar excepciones para
/// distinguir éxito de los distintos fallos esperados.
/// </summary>
public sealed record TokenResult
{
    /// <summary>Estado explícito del resultado.</summary>
    public TokenResultStatus Status { get; }

    /// <summary>
    /// Valor del token cuando <see cref="Status"/> es
    /// <see cref="TokenResultStatus.Success"/>; <c>null</c> en cualquier
    /// otro estado.
    /// </summary>
    public string? AccessToken { get; }

    /// <summary>
    /// Comprobación uniforme de si el resultado contiene un token
    /// utilizable, sin necesidad de inspeccionar <see cref="Status"/>.
    /// </summary>
    public bool IsSuccess => Status == TokenResultStatus.Success;

    private TokenResult(TokenResultStatus status, string? accessToken)
    {
        Status = status;
        AccessToken = accessToken;
    }

    /// <summary>Crea un resultado de éxito con el token leído, sin transformarlo.</summary>
    public static TokenResult Success(string accessToken) =>
        new(TokenResultStatus.Success, accessToken);

    /// <summary>Crea un resultado de "fichero no encontrado".</summary>
    public static TokenResult FileNotFound() =>
        new(TokenResultStatus.FileNotFound, accessToken: null);

    /// <summary>Crea un resultado de "JSON inválido".</summary>
    public static TokenResult InvalidJson() =>
        new(TokenResultStatus.InvalidJson, accessToken: null);

    /// <summary>Crea un resultado de "token ausente o vacío".</summary>
    public static TokenResult TokenMissing() =>
        new(TokenResultStatus.TokenMissing, accessToken: null);
}
```

Los constructores de fábrica estáticos (`Success`/`FileNotFound`/`InvalidJson`/`TokenMissing`) sustituyen a un constructor público: evitan que se pueda construir un `TokenResult` en estado inconsistente (p. ej. `Success` sin token) y dan a los tests/consumidores nombres autoexplicativos en vez de un enum + valor sueltos.

### `ITokenProvider` (Application)

```csharp
namespace ClaudeMeter.Application.Abstractions;

using ClaudeMeter.Domain.Authentication;

/// <summary>
/// Puerto de Application para obtener el token OAuth de Claude Code sin
/// conocer su origen ni formato de almacenamiento (fichero, futuro
/// Credential Manager, etc.).
/// </summary>
public interface ITokenProvider
{
    /// <summary>
    /// Obtiene el token OAuth actual. Nunca lanza una excepción para los
    /// casos esperados de "sin datos" (fichero ausente, JSON inválido,
    /// token ausente/vacío) — esos casos se representan en el
    /// <see cref="TokenResult"/> devuelto.
    /// </summary>
    Task<TokenResult> GetTokenAsync(CancellationToken cancellationToken = default);
}
```

**Decisión — síncrono vs. asíncrono**: `GetTokenAsync` es asíncrono. Rationale: aunque el fichero es pequeño y la E/S es local, (1) el resto de Application se construye sobre MediatR con handlers asíncronos (`CLAUDE.md`: "use cases via MediatR queries/commands"), y un puerto síncrono forzaría a los consumidores a bloquear un hilo o a envolver la llamada en `Task.Run` innecesariamente; (2) mantiene la puerta abierta a que una implementación futura del mismo puerto (p. ej. una que consulte Windows Credential Manager en una fase posterior) necesite E/S realmente asíncrona, sin tener que cambiar la firma del puerto ni los consumidores ya escritos contra él. Alternativa considerada: `TokenResult GetToken()` síncrono — rechazada por lo anterior; el coste de `async`/`Task` aquí es marginal y evita un cambio de contrato disruptivo más adelante.

## Diseño de `CredentialsFileTokenProvider`

```csharp
namespace ClaudeMeter.Infrastructure.Authentication;

using System.Text.Json;
using System.Text.Json.Serialization;
using ClaudeMeter.Application.Abstractions;
using ClaudeMeter.Domain.Authentication;

/// <summary>
/// Implementación de <see cref="ITokenProvider"/> que lee el token OAuth
/// en texto plano desde el fichero de credenciales del CLI de Claude Code
/// (<c>%USERPROFILE%\.claude\.credentials.json</c> en Windows). Su única
/// responsabilidad es leer y parsear ese fichero: no realiza llamadas a la
/// API de Anthropic ni calcula countdowns o porcentajes de rate limit.
/// </summary>
public sealed class CredentialsFileTokenProvider : ITokenProvider
{
    private readonly string _credentialsFilePath;

    /// <summary>
    /// Constructor de producción: usa siempre la ruta fija
    /// <c>%USERPROFILE%\.claude\.credentials.json</c>. No expone forma
    /// alguna de configurar la ruta en runtime (F0 la deja hardcodeada
    /// por diseño; la configurabilidad llega en F2 con <c>config.json</c>).
    /// </summary>
    public CredentialsFileTokenProvider()
        : this(GetDefaultCredentialsFilePath())
    {
    }

    /// <summary>
    /// Constructor interno usado únicamente por
    /// <c>ClaudeMeter.Infrastructure.Tests</c> (vía
    /// <c>InternalsVisibleTo</c>) para apuntar a un fichero de fixture
    /// temporal en los tests, sin tocar el fichero real del usuario.
    /// No forma parte de la superficie pública del ensamblado.
    /// </summary>
    internal CredentialsFileTokenProvider(string credentialsFilePath)
    {
        _credentialsFilePath = credentialsFilePath;
    }

    private static string GetDefaultCredentialsFilePath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".claude",
            ".credentials.json");

    /// <inheritdoc />
    public async Task<TokenResult> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_credentialsFilePath))
        {
            return TokenResult.FileNotFound();
        }

        string json;
        try
        {
            json = await File.ReadAllTextAsync(_credentialsFilePath, cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // El fichero existía en el check anterior pero dejó de ser
            // accesible (borrado/bloqueado por otro proceso justo entre
            // medias, permisos, etc.). Para el consumidor de ITokenProvider
            // el efecto es indistinguible de "no hay fichero disponible".
            return TokenResult.FileNotFound();
        }

        CredentialsFileDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<CredentialsFileDto>(json);
        }
        catch (JsonException)
        {
            return TokenResult.InvalidJson();
        }

        var accessToken = dto?.ClaudeAiOauth?.AccessToken;
        return string.IsNullOrWhiteSpace(accessToken)
            ? TokenResult.TokenMissing()
            : TokenResult.Success(accessToken);
    }

    private sealed record CredentialsFileDto(
        [property: JsonPropertyName("claudeAiOauth")] OAuthSectionDto? ClaudeAiOauth);

    private sealed record OAuthSectionDto(
        [property: JsonPropertyName("accessToken")] string? AccessToken);
}
```

### Mapeo GIVEN-WHEN-THEN → estado de `TokenResult`

| Escenario del documento de requisitos | Camino en el código | Estado resultante |
|---|---|---|
| Fichero válido, `claudeAiOauth.accessToken` no vacío | `File.Exists` → true, deserializa OK, `accessToken` no vacío | `Success(accessToken)` — valor exacto leído, sin transformar |
| Fichero no existe en la ruta fija | `File.Exists` → false | `FileNotFound()` |
| Fichero existe pero JSON mal formado | `JsonSerializer.Deserialize` lanza `JsonException`, capturada | `InvalidJson()` |
| JSON válido pero sin `claudeAiOauth` o sin `accessToken` | `dto?.ClaudeAiOauth?.AccessToken` es `null` | `TokenMissing()` |
| JSON válido, `accessToken` presente pero vacío/blanco | `string.IsNullOrWhiteSpace(accessToken)` → true | `TokenMissing()` (mismo estado que el caso anterior, según AC) |
| Consumidor solo necesita saber si el token es utilizable | `result.IsSuccess` | N/A — ya cubierto por el diseño de `TokenResult` |

Caso límite no descrito explícitamente en los AC pero cubierto por el diseño: un fichero cuyo contenido es JSON válido pero no es un objeto (p. ej. `null`, un array, o un escalar) deserializa a `dto == null`; `dto?.ClaudeAiOauth?.AccessToken` se evalúa a `null` de forma segura vía null-conditional, cayendo en `TokenMissing()` sin excepción. Se documenta aquí para que el desarrollador no necesite tomar esa decisión de forma ad-hoc durante la implementación.

### Ninguna excepción no controlada escapa

Puntos exactos envueltos en `try/catch`:
1. `File.ReadAllTextAsync` → captura `IOException` y `UnauthorizedAccessException` (condición de carrera tras el `File.Exists`), mapeadas a `FileNotFound`.
2. `JsonSerializer.Deserialize<CredentialsFileDto>` → captura `JsonException` (JSON mal formado), mapeada a `InvalidJson`.

`File.Exists` en sí no lanza excepciones (devuelve `false` ante casi cualquier error de acceso), por lo que no necesita try/catch. No se añade un `catch (Exception)` genérico adicional: el catálogo de excepciones no controladas que puede producir esta clase se limita a E/S y parseo JSON, ambas ya cubiertas explícitamente; un catch-all genérico ocultaría fallos de programación reales (p. ej. un `NullReferenceException` por un cambio futuro mal hecho) bajo un estado de dominio que no les corresponde. `OperationCanceledException` (por cancelación del `CancellationToken`) se deja propagar sin capturar, siguiendo la convención estándar de .NET para cancelación cooperativa.

### Logging / diagnosticabilidad del caso "JSON inválido"

El AC pide que el caso de JSON inválido "quede registrado internamente de forma que sea diagnosticable (p. ej. mediante logging, **cuando exista en fases posteriores**)". F0 no incorpora todavía ningún framework de logging (Serilog llega en F2, según `CLAUDE.md`). Decisión: **no añadir una dependencia de logging en este issue**. En su lugar:
- El propio `TokenResultStatus.InvalidJson` ya hace el fallo diagnosticable de forma estructurada para cualquier consumidor o test, sin necesidad de logging.
- El bloque `catch (JsonException)` es el único punto de inserción necesario para que F2 añada una línea `_logger.LogWarning(ex, "...")` sin rediseñar la clase; no se registra ni el contenido del fichero ni el token en ningún caso (cumple el riesgo de seguridad del documento de requisitos).
- Alternativa considerada: inyectar ya `ILogger<CredentialsFileTokenProvider>` (Microsoft.Extensions.Logging.Abstractions) con una implementación no-op por defecto — rechazada por ahora: añadiría una dependencia y un parámetro de constructor que ningún consumidor de F0 usaría todavía (el roadmap no cablea Serilog hasta F2), aumentando superficie sin beneficio inmediato. Se revisita en el issue de F2 que introduzca Serilog.

## Estrategia de Testing

**Ubicación**: `test/ClaudeMeter.Infrastructure.Tests/Authentication/CredentialsFileTokenProviderTests.cs` (el `.csproj` de este proyecto ya referencia `ClaudeMeter.Infrastructure`, sin cambios necesarios).

**Cómo se simula el fichero**: se usa la ruta real del sistema de ficheros, pero **nunca la ruta fija de producción ni un token real**. Cada test:
1. Genera una ruta única bajo `Path.GetTempPath()` (p. ej. `Path.Combine(Path.GetTempPath(), $"claudemeter-tests-{Guid.NewGuid()}.json")`).
2. Escribe (o deliberadamente no escribe, para el caso "fichero ausente") contenido de fixture con `File.WriteAllTextAsync`, con un `accessToken` ficticio como `"fake-access-token-for-tests"`.
3. Construye `CredentialsFileTokenProvider` con el **constructor interno** que acepta la ruta (visible al proyecto de test vía `InternalsVisibleTo`, añadido al `.csproj` de `ClaudeMeter.Infrastructure`: `<InternalsVisibleTo Include="ClaudeMeter.Infrastructure.Tests" />`).
4. Llama a `GetTokenAsync()` y verifica `Status`/`AccessToken`/`IsSuccess`.
5. Borra el fichero temporal en un `finally` (o implementando `IDisposable`/`IAsyncLifetime` de xUnit por test) para no dejar residuos en `%TEMP%`.

**Decisión — por qué no una abstracción `IFileSystem`**: se rechaza introducir `System.IO.Abstractions` (o una interfaz propia de filesystem) para este issue. Escribir un fichero temporal real es determinista, no depende de mocks frágiles de E/S, y evita una dependencia/indirección nueva para una única clase que lee un único fichero. Si F2 introduce más necesidades de lectura de ficheros (`config.json`) y la duplicación de este patrón empieza a doler, se puede reconsiderar entonces (YAGNI). Se rechaza igualmente escribir/leer sobre la ruta real de producción en los tests: destruiría potencialmente las credenciales reales del desarrollador que ejecuta la suite y violaría la regla de "nunca un token real en tests".

**Casos cubiertos (mapeo 1:1 con los GIVEN-WHEN-THEN)**:

| Test | GIVEN-WHEN-THEN cubierto |
|---|---|
| `GetTokenAsync_ConFicheroValido_DevuelveSuccessConElTokenExacto` | Fichero válido con `accessToken` no vacío → `Success` con el valor exacto |
| `GetTokenAsync_SinFichero_DevuelveFileNotFound` | Fichero no existe en la ruta → `FileNotFound` |
| `GetTokenAsync_ConJsonMalFormado_DevuelveInvalidJson` | Contenido no es JSON válido → `InvalidJson` |
| `GetTokenAsync_SinCampoAccessToken_DevuelveTokenMissing` | JSON válido sin `claudeAiOauth`/`accessToken` → `TokenMissing` |
| `GetTokenAsync_ConAccessTokenVacioOEnBlanco_DevuelveTokenMissing` (`[Theory]` con `""` y `"   "`) | `accessToken` presente pero vacío/blanco → mismo `TokenMissing` |
| `GetTokenAsync_ParaCualquierEstadoDeFallo_IsSuccessEsFalse` (`[Theory]` sobre los tres fallos) | Comprobación uniforme `IsSuccess` sin excepciones, para los 3 estados de fallo |

Estos 6 casos (con las 2 variantes `[Theory]`) cubren los cuatro estados de `TokenResultStatus` y el criterio de aceptación de "comprobación uniforme", satisfaciendo el requisito mínimo del documento de requisitos y contribuyendo al objetivo de cobertura del 70% (`testingCoverage`) para Infrastructure/Application.

Adicionalmente, en `ClaudeMeter.Domain.Tests` se puede (opcional, no bloqueante para este issue) añadir un test puro de `TokenResult` que verifique que cada fábrica estática produce el `Status`/`AccessToken`/`IsSuccess` esperado — es lógica de dominio sin E/S y barata de cubrir al 100%.

## Cross-Cutting Concerns

- **Seguridad**: el token nunca se registra ni se incluye en mensajes de excepción propagados; los estados de fallo (`FileNotFound`, `InvalidJson`, `TokenMissing`) no llevan el contenido del fichero ni fragmentos del JSON. El fichero se lee en texto plano tal y como decide el roadmap para F0 (sin Credential Manager); no se introduce cifrado ni ofuscación adicional en este issue.
- **Rendimiento**: N/A — lectura de un fichero local pequeño; el uso de `async`/`await` evita bloquear el hilo llamante pero no busca optimizar throughput (no aplica en este caso de uso).
- **Manejo de errores**: contrato "nunca excepción no controlada para los casos esperados" cumplido mediante los dos `try/catch` descritos arriba; `OperationCanceledException` se propaga sin capturar (comportamiento estándar de cancelación en .NET). No hay reintentos aquí — el retry con backoff de F2 aplica a las llamadas HTTP a la API, no a la lectura local del token.
- **Retrocompatibilidad**: no aplica todavía (primera implementación del puerto); el diseño de `ITokenProvider` como interfaz asíncrona minimiza el riesgo de romper firmas cuando F2 añada nuevas implementaciones o F5 requiera múltiples cuentas.
- **Sustituibilidad (Liskov)**: cualquier otra implementación futura de `ITokenProvider` (p. ej., una futura integración con Credential Manager) debe seguir devolviendo `TokenResult` con los mismos cuatro estados semánticos en vez de lanzar para casos esperados, preservando el contrato ya fijado por este diseño.

## Implementation Plan

1. Crear `src/ClaudeMeter.Domain/Authentication/TokenResult.cs` con `TokenResultStatus` (enum) y `TokenResult` (record sellado, fábricas estáticas), documentado con XMLDoc en español.
2. Crear `src/ClaudeMeter.Application/Abstractions/ITokenProvider.cs` con el método `Task<TokenResult> GetTokenAsync(CancellationToken cancellationToken = default)`, documentado con XMLDoc en español.
3. Crear `src/ClaudeMeter.Infrastructure/Authentication/CredentialsFileTokenProvider.cs` implementando `ITokenProvider` según el diseño de la sección anterior (constructor público sin parámetros + constructor `internal` con la ruta, DTOs privados `CredentialsFileDto`/`OAuthSectionDto` con `System.Text.Json`).
4. Añadir `<InternalsVisibleTo Include="ClaudeMeter.Infrastructure.Tests" />` en `src/ClaudeMeter.Infrastructure/ClaudeMeter.Infrastructure.csproj` (o un `AssemblyInfo.cs` equivalente) para permitir que los tests usen el constructor interno.
5. Crear `test/ClaudeMeter.Infrastructure.Tests/Authentication/CredentialsFileTokenProviderTests.cs` con los 6 casos descritos en la sección de Testing, usando ficheros temporales reales y limpieza determinista tras cada test.
6. (Opcional, no bloqueante) Añadir un test puro de `TokenResult` en `test/ClaudeMeter.Domain.Tests/` verificando las cuatro fábricas estáticas.
7. Ejecutar `dotnet test` sobre la solución completa y comprobar que no se introducen warnings nuevos bajo `Release` (el `Directory.Build.props` ya trata warnings como errores en Release).
8. Verificar manualmente en un entorno con `.claude\.credentials.json` real (sin commitear nada de ese fichero) que `CredentialsFileTokenProvider` (constructor público, ruta por defecto) devuelve `Success` con el token real — validación exploratoria, no parte de la suite automatizada.

## Risks & Open Decisions

None — no se han identificado decisiones de diseño genuinamente abiertas que requieran validación humana antes de pasar a desarrollo. Las cuestiones de diseño que sí requerían una decisión (síncrono vs. asíncrono, dónde vive la constante de ruta, cómo simular el fichero en los tests sin `IFileSystem`, si añadir logging ya en F0) se han resuelto explícitamente en las secciones anteriores, con su rationale y alternativa rechazada correspondiente.

Riesgos heredados del documento de requisitos (no bloquean el diseño, pero conviene que el desarrollador los tenga presentes):
- El fichero puede no existir en máquinas donde nunca se ha usado el CLI de Claude Code — ya cubierto por el estado `FileNotFound`.
- El fichero puede quedar en un estado parcial/corrupto si el proceso que lo escribe se interrumpe — cubierto por `InvalidJson` (JSON truncado no parsea) y por el catch de `IOException`/`UnauthorizedAccessException` (fichero bloqueado a medio escribir).
