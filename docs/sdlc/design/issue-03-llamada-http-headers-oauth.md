# Design: [F0] Llamada HTTP con headers OAuth correctos

## Requirements Reference
`docs/sdlc/requirements/issue-03-llamada-http-headers-oauth.md` (Issue #3, milestone "F0 — Núcleo de validación"). Documento cerrado, sin preguntas bloqueantes; contiene una única decisión explícitamente delegada a la fase de Diseño (estrategia de inyección de `HttpClient`), que se resuelve en este documento (ver "Decisiones Técnicas Clave" y "Diseño de `AnthropicApiUsageDataSource`").

## Resumen / Alcance

Este diseño cubre exclusivamente la llamada HTTP a la API de Anthropic (`POST /v1/messages`) para obtener, en crudo, las cabeceras `anthropic-ratelimit-unified-5h-*`/`-7d-*`, expuesta al resto de la aplicación a través de un puerto (`IUsageDataSource`) en Application, implementado por un adaptador (`AnthropicApiUsageDataSource`) en Infrastructure que consume el `ITokenProvider` ya existente (Issue #2, mergeado). El "resultado sin datos"/error se modela como un tipo de dominio explícito (`UsageSnapshot`), análogo a `TokenResult`.

Fuera de alcance (explícitamente, según Technical Notes del documento de requisitos y el roadmap de `CLAUDE.md`):
- Parseo de las cabeceras `anthropic-ratelimit-unified-*` a minutos/porcentaje, cálculo de countdown, construcción de `RateLimitWindow` — clase/issue separado, consumidor de `UsageSnapshot`.
- Refresco de OAuth propio ante 401/403 (nunca se implementa en este proyecto).
- Retry/backoff ante fallos transitorios (F2).
- Polling periódico (F1) y cableado del composition root de Desktop (no existe todavía ningún contenedor DI en `ClaudeMeter.Desktop` — `App.xaml.cs` es la plantilla WPF por defecto, sin `OnStartup` ni DI; este issue no lo introduce).
- Renderizado / UI (F1).

El diseño se apoya en el scaffolding F0 ya mergeado y en lo construido por Issue #2 (`ITokenProvider`, `TokenResult`, `CredentialsFileTokenProvider`), respetando las referencias de proyecto ya declaradas:

- `ClaudeMeter.Domain` — sin `ProjectReference` (núcleo puro).
- `ClaudeMeter.Application` → referencia `ClaudeMeter.Domain`.
- `ClaudeMeter.Infrastructure` → referencia `ClaudeMeter.Application` (y transitivamente `Domain`).
- `ClaudeMeter.Infrastructure.Tests` → referencia `ClaudeMeter.Infrastructure` (y transitivamente `Application`/`Domain`).

No se necesita ninguna `ProjectReference` nueva. Tampoco se necesita ningún paquete NuGet nuevo (ver "Decisiones Técnicas Clave" — se descarta `Microsoft.Extensions.Http`): `System.Net.Http` y `System.Text.Json` ya están disponibles vía el BCL de .NET 8, igual que en `CredentialsFileTokenProvider`.

## Arquitectura

### Ubicación exacta de cada pieza

| Tipo | Proyecto | Namespace | Fichero propuesto |
|---|---|---|---|
| `UsageSnapshot` (record) + `UsageSnapshotStatus` (enum) + `RawRateLimitHeaders` (record) | `ClaudeMeter.Domain` | `ClaudeMeter.Domain.Usage` | `src/ClaudeMeter.Domain/Usage/UsageSnapshot.cs` |
| `IUsageDataSource` (puerto) | `ClaudeMeter.Application` | `ClaudeMeter.Application.Abstractions` | `src/ClaudeMeter.Application/Abstractions/IUsageDataSource.cs` |
| `AnthropicApiUsageDataSource` (adaptador) | `ClaudeMeter.Infrastructure` | `ClaudeMeter.Infrastructure.Usage` | `src/ClaudeMeter.Infrastructure/Usage/AnthropicApiUsageDataSource.cs` |
| `AnthropicApiUsageDataSourceTests` | `ClaudeMeter.Infrastructure.Tests` | `ClaudeMeter.Infrastructure.Tests.Usage` | `test/ClaudeMeter.Infrastructure.Tests/Usage/AnthropicApiUsageDataSourceTests.cs` |
| `StubHttpMessageHandler` (test double) | `ClaudeMeter.Infrastructure.Tests` | `ClaudeMeter.Infrastructure.Tests.Usage` | `test/ClaudeMeter.Infrastructure.Tests/Usage/StubHttpMessageHandler.cs` |
| `FakeTokenProvider` (test double) | `ClaudeMeter.Infrastructure.Tests` | `ClaudeMeter.Infrastructure.Tests.Usage` | `test/ClaudeMeter.Infrastructure.Tests/Usage/FakeTokenProvider.cs` |

**Rationale de namespace/carpeta**: se usa `Usage` como sub-namespace vertical, siguiendo el mismo patrón por feature ya adoptado en Issue #2 con `Authentication` (feature folders, no namespace raíz plano) — coherente con el roadmap, que ya anticipa que `Usage` crecerá con `RateLimitWindow`/`PeakOffPeakAnalyzer` en F1/F4. Alternativa considerada: reutilizar el namespace `Authentication` para `UsageSnapshot` ya que depende conceptualmente del token — rechazada porque `UsageSnapshot` no es un concepto de autenticación sino de uso/rate-limit; mezclar ambos degradaría la cohesión de la carpeta a medida que crecen F1-F4.

**No se introduce un `IUsageService` monolítico** — `IUsageDataSource` es un puerto pequeño y específico (una sola responsabilidad: obtener el snapshot crudo), tal y como exige `CLAUDE.md` ("deliberadamente no single catch-all `IUsageService`") y el primer Acceptance Criteria del documento de requisitos.

### Flujo de dependencias (Clean Architecture, dependencias apuntan hacia dentro)

```
ClaudeMeter.Infrastructure.Tests
        │  (ProjectReference)
        ▼
ClaudeMeter.Infrastructure ── implementa ──► IUsageDataSource (Application)
   AnthropicApiUsageDataSource                      │
        │  usa                                      │ referencia como tipo de retorno
        │  ┌─────────────────────┐                  ▼
        ├─►│ ITokenProvider       │  (Application, ya existente — Issue #2)
        │  └─────────────────────┘
        │  usa
        ▼
   System.Net.Http.HttpClient              ClaudeMeter.Domain
   (detalle de infraestructura,               UsageSnapshot / UsageSnapshotStatus
    inyectado por constructor)                 RawRateLimitHeaders
                                                        ▲
                                                        │ (sin dependencias externas)
```

- `ClaudeMeter.Domain` no conoce `IUsageDataSource` ni `AnthropicApiUsageDataSource` — solo expone los value objects `UsageSnapshot`/`RawRateLimitHeaders`, sin dependencia de paquete NuGet ni de E/S.
- `ClaudeMeter.Application` define `IUsageDataSource` tipado con `UsageSnapshot` (Domain) como retorno; **no referencia `System.Net.Http`, `System.Text.Json` ni ningún tipo de `ClaudeMeter.Infrastructure`** — cumple literalmente el primer Acceptance Criteria del documento de requisitos.
- `ClaudeMeter.Infrastructure` implementa `IUsageDataSource` mediante `AnthropicApiUsageDataSource`, que es la única pieza que construye/envía la petición HTTP y traduce la respuesta cruda.
- `AnthropicApiUsageDataSource` **consume** `ITokenProvider` (no lo reimplementa ni lee el fichero de credenciales directamente) — preserva la regla de `CLAUDE.md` de que "leer el token, llamar a la API, calcular el countdown y renderizar son clases distintas".
- El futuro composition root (Desktop, F1) resuelve `IUsageDataSource` → `AnthropicApiUsageDataSource` y crea el `HttpClient` compartido; este issue no incluye ese cableado (no hay `OnStartup`/contenedor DI en `App.xaml.cs` todavía). Se documenta el snippet recomendado para cuando llegue (ver "Decisiones Técnicas Clave").

## Decisiones Técnicas Clave

| Decisión | Elección | Rationale | Alternativa(s) consideradas y rechazadas |
|---|---|---|---|
| **Estrategia de inyección de `HttpClient`** (decisión delegada por el documento de requisitos) | Constructor-injected: `AnthropicApiUsageDataSource` recibe un `HttpClient` ya construido, creado **una sola vez** como singleton en el futuro composition root de Desktop (no en este issue) | (1) `Desktop` es una app WPF sin `Microsoft.Extensions.Hosting`/contenedor DI todavía (`App.xaml.cs` es la plantilla por defecto) — introducir `Microsoft.Extensions.Http` + `IServiceCollection` solo para registrar un `IHttpClientFactory` con un único cliente con nombre es una dependencia y un nivel de indirección desproporcionados para el alcance de F0 (regla explícita del enunciado: "no over-engineer"). (2) El problema clásico que resuelve `IHttpClientFactory` — agotamiento de sockets por crear un `HttpClient` nuevo en cada petición — no aplica aquí: hay **un único** `HttpClient`, creado una vez y reutilizado en cada ciclo de polling (60 s en F1), nunca instanciado por llamada. (3) Testabilidad: un `HttpClient` inyectado por constructor se construye trivialmente en los tests como `new HttpClient(stubHandler)`, sin necesidad de mockear `IHttpClientFactory` ni registrar handlers con nombre — más simple que el patrón de fábrica. (4) El riesgo real de un `HttpClient` singleton de larga vida (staleness de DNS si el proceso vive días/semanas, relevante para F6 con autostart) se mitiga sin `IHttpClientFactory`, configurando el `SocketsHttpHandler` subyacente con `PooledConnectionLifetime` acotado (ver snippet abajo) — mitigación estándar de Microsoft para este escenario exacto, sin añadir un contenedor DI. | (a) `IHttpClientFactory` vía `Microsoft.Extensions.Http` registrado manualmente con un `ServiceCollection` ad-hoc — rechazada: exige montar un mini contenedor DI en Desktop antes de que exista ningún otro consumidor de DI, solo para una llamada HTTP; añade una `PackageReference` y superficie (política de reintentos/handlers con nombre) que este issue no necesita y que F2 puede introducir si el resto de la app termina adoptando DI de verdad. (b) `AnthropicApiUsageDataSource` crea y posee su propio `HttpClient` internamente (`new HttpClient()` en el constructor sin parámetros) — rechazada: imposibilita inyectar un `HttpMessageHandler` fake en los tests sin añadir un constructor `internal` adicional (como se hizo en Issue #2 para la ruta del fichero), y si se crea una instancia nueva en cada llamada (en vez de como campo) reintroduce el problema de agotamiento de sockets que se quiere evitar. |
| **Modelo usado en el payload mínimo** | `claude-haiku-4-5-20251001` (Claude Haiku 4.5, constante), como valor de `model` en el body de `/v1/messages` | El documento de requisitos confirma el payload mínimo (`max_tokens: 1`, un mensaje trivial) pero no especifica qué `model` usar — el endpoint de la API exige igualmente el campo `model`. Se elige el modelo vigente más barato/rápido disponible para minimizar el coste de cuota real que cada llamada consume (Risk "Cost/Quota" del documento de requisitos, aceptado pero a minimizar). | Usar un modelo por defecto más capaz (p. ej. Sonnet/Opus) — rechazado: desperdicia cuota real del usuario en una llamada cuyo único propósito es leer cabeceras de rate-limit, contradiciendo el riesgo de coste ya documentado. Este valor concreto de `model` es una asunción de implementación, no un contrato confirmado por ingeniería inversa como sí lo son endpoint/headers; debe verificarse en la validación manual ya obligatoria por el Acceptance Criteria del issue original y ajustarse si la API la rechaza (o si Anthropic retira este model id, como ya ocurrió con `claude-3-5-haiku-20241022`, retirado el 19/02/2026). |
| **Valor literal de `User-Agent`** | Constante `claude-code/0.1.0` (versión de ensamblado hardcodeada por ahora) | El formato `claude-code/<versión>` está confirmado como obligatorio por el documento de requisitos; el número de versión exacto no está documentado como funcionalmente relevante (solo el prefijo/formato lo es, según las fuentes de ingeniería inversa citadas). `Nerdbank.GitVersioning` (que daría un número de versión real) no llega hasta F6 según el roadmap; introducirlo ya en F0 solo para este header sería adelantar trabajo de una fase posterior sin necesidad. | Leer `Assembly.GetExecutingAssembly().GetName().Version` en runtime — rechazada para F0: sin `Nerdbank.GitVersioning` todos los ensamblados quedan en la versión por defecto de .NET SDK (`1.0.0.0`), que no aporta más información real que una constante y añade una llamada de reflexión innecesaria; se revisita en el issue de F6 que introduzca versionado real. |
| **Criterio de éxito cuando la respuesta es 200** | Éxito solo si están presentes `anthropic-ratelimit-unified-5h-status` **y** `anthropic-ratelimit-unified-7d-status`; el resto de sufijos (`-utilization`, `-remaining`, `-reset`) se capturan si están presentes pero no son obligatorios para considerar la respuesta un éxito | El documento de requisitos deja explícitamente ambigua la nomenclatura exacta de la cabecera "cantidad" (`-utilization` **o** `-remaining`, según la sección Technical Notes) — no está garantizado cuál de las dos aparece realmente. Exigir la presencia de *todos* los sufijos como condición de éxito arriesgaría clasificar como `RequestFailed` una respuesta real y válida solo porque usa `-remaining` en vez de `-utilization` (o viceversa). `-status` es el único sufijo que el documento de requisitos da por confirmado sin ambigüedad en ambas ventanas, por lo que es el criterio mínimo razonable; el resto de campos quedan como `string?` y simplemente valen `null` si la cabecera concreta no aparece — sin romper el AC de "expone esas cabeceras en crudo". | Exigir los 4 sufijos por ventana como condición de éxito — rechazada por el riesgo de falso negativo descrito arriba. Exigir solo *alguna* cabecera `anthropic-ratelimit-unified-*` presente (sin distinguir sufijo) — rechazada por ser demasiado laxa: aceptaría como "éxito" una respuesta que, por ejemplo, solo trajera la ventana de 7 días sin la de 5 horas, ocultando una regresión real de la API. |
| **Representación de las cabeceras crudas** | Tipo anidado `RawRateLimitHeaders` (record posicional con 4 `string?`: `Status`, `Utilization`, `Remaining`, `Reset`), con una instancia para `Session` (5h) y otra para `Weekly` (7d) en `UsageSnapshot` | Agrupar por ventana (en vez de 8 propiedades planas en `UsageSnapshot`) refleja el dominio real (dos ventanas de rate-limit) y facilita que la futura clase de parseo (F1, fuera de alcance aquí) itere `new[] { snapshot.Session, snapshot.Weekly }` en vez de repetir lógica para 8 campos sueltos. Usar `string?` nullable en vez de forzar valores no-nulos deja espacio, sin excepción ni caso especial, para el sufijo (`Utilization` vs `Remaining`) que pueda no aparecer según la decisión anterior. | 8 propiedades planas en `UsageSnapshot` (`FiveHourStatus`, `FiveHourUtilization`, ...) — rechazada por duplicar la estructura dos veces y dificultar iterar sobre "ambas ventanas" genéricamente. Un `IReadOnlyDictionary<string, string>` de cabeceras crudas sin tipar — rechazada: pierde el estilo explícito de value object ya establecido por `TokenResult` que el propio documento de requisitos pide imitar, y oculta en tiempo de compilación qué claves se esperan. |

## Modelo de Datos / Interfaces

### `UsageSnapshot` (Domain)

```csharp
namespace ClaudeMeter.Domain.Usage;

/// <summary>
/// Estados explícitos en los que puede resolverse el intento de obtener
/// el snapshot de uso/rate-limit real de la cuenta desde la API de
/// Anthropic.
/// </summary>
public enum UsageSnapshotStatus
{
    /// <summary>Se obtuvo el snapshot con las cabeceras de rate-limit esperadas.</summary>
    Success,

    /// <summary>
    /// No había un token OAuth utilizable (<see cref="ClaudeMeter.Application.Abstractions.ITokenProvider"/>
    /// no devolvió éxito); no se llegó a realizar ninguna llamada HTTP.
    /// </summary>
    TokenUnavailable,

    /// <summary>
    /// La API respondió 401 o 403: el token fue rechazado. Nunca se
    /// intenta refrescar el token por cuenta propia (regla de <c>CLAUDE.md</c>).
    /// </summary>
    Unauthorized,

    /// <summary>
    /// La petición falló por un motivo distinto a 401/403: error de red,
    /// timeout, código 5xx, u otra respuesta 2xx sin las cabeceras
    /// <c>anthropic-ratelimit-unified-*</c> mínimas esperadas.
    /// </summary>
    RequestFailed
}

/// <summary>
/// Cabeceras de rate-limit "en crudo" (sin parsear a minutos/porcentaje)
/// de una única ventana (sesión de 5h o semanal de 7d). Cualquier
/// propiedad puede ser <c>null</c> si la cabecera concreta no vino en la
/// respuesta — el nombre exacto de la cabecera "cantidad" (<c>utilization</c>
/// vs <c>remaining</c>) no está confirmado oficialmente (ver documento de
/// requisitos, Technical Notes).
/// </summary>
public sealed record RawRateLimitHeaders(
    string? Status,
    string? Utilization,
    string? Remaining,
    string? Reset);

/// <summary>
/// Representa el resultado de intentar obtener el snapshot de uso/rate-limit
/// real de la cuenta. Modela el caso "sin datos" como un tipo de dominio
/// explícito, análogo a <c>TokenResult</c>, de forma que los consumidores
/// nunca necesitan lanzar ni capturar excepciones para distinguir éxito de
/// los distintos fallos esperados. No calcula countdowns ni porcentajes —
/// esa responsabilidad queda fuera de alcance de este tipo.
/// </summary>
public sealed record UsageSnapshot
{
    /// <summary>Estado explícito del resultado.</summary>
    public UsageSnapshotStatus Status { get; }

    /// <summary>
    /// Cabeceras crudas de la ventana de sesión (5h) cuando
    /// <see cref="Status"/> es <see cref="UsageSnapshotStatus.Success"/>;
    /// <c>null</c> en cualquier otro estado.
    /// </summary>
    public RawRateLimitHeaders? Session { get; }

    /// <summary>
    /// Cabeceras crudas de la ventana semanal (7d) cuando
    /// <see cref="Status"/> es <see cref="UsageSnapshotStatus.Success"/>;
    /// <c>null</c> en cualquier otro estado.
    /// </summary>
    public RawRateLimitHeaders? Weekly { get; }

    /// <summary>
    /// Comprobación uniforme de si el snapshot contiene datos utilizables,
    /// sin necesidad de inspeccionar <see cref="Status"/>.
    /// </summary>
    public bool IsSuccess => Status == UsageSnapshotStatus.Success;

    private UsageSnapshot(UsageSnapshotStatus status, RawRateLimitHeaders? session, RawRateLimitHeaders? weekly)
    {
        Status = status;
        Session = session;
        Weekly = weekly;
    }

    /// <summary>Crea un resultado de éxito con las cabeceras crudas de ambas ventanas.</summary>
    public static UsageSnapshot Success(RawRateLimitHeaders session, RawRateLimitHeaders weekly) =>
        new(UsageSnapshotStatus.Success, session, weekly);

    /// <summary>Crea un resultado de "sin token disponible"; no hubo llamada HTTP.</summary>
    public static UsageSnapshot TokenUnavailable() =>
        new(UsageSnapshotStatus.TokenUnavailable, session: null, weekly: null);

    /// <summary>Crea un resultado de "no autorizado" (401/403).</summary>
    public static UsageSnapshot Unauthorized() =>
        new(UsageSnapshotStatus.Unauthorized, session: null, weekly: null);

    /// <summary>Crea un resultado de "fallo de la petición" (red, 5xx, cabeceras ausentes).</summary>
    public static UsageSnapshot RequestFailed() =>
        new(UsageSnapshotStatus.RequestFailed, session: null, weekly: null);
}
```

Igual que en `TokenResult`, los constructores de fábrica estáticos sustituyen a un constructor público: evitan construir un `UsageSnapshot` en estado inconsistente (p. ej. `Success` sin `Session`/`Weekly`) y dan nombres autoexplicativos a tests y consumidores.

### `IUsageDataSource` (Application)

```csharp
namespace ClaudeMeter.Application.Abstractions;

using ClaudeMeter.Domain.Usage;

/// <summary>
/// Puerto de Application para obtener el snapshot de uso/rate-limit real
/// de la cuenta sin conocer el origen de los datos (API real de
/// Anthropic, futura fuente simulada, futuro <c>BleUsageSink</c>, etc.).
/// </summary>
public interface IUsageDataSource
{
    /// <summary>
    /// Obtiene el snapshot de uso actual. Nunca lanza una excepción para
    /// los casos esperados de "sin datos" (sin token, no autorizado,
    /// fallo de la petición) — esos casos se representan en el
    /// <see cref="UsageSnapshot"/> devuelto, de forma que cualquier
    /// implementación (real, simulada, BLE) sea sustituible por otra sin
    /// cambiar el comportamiento observable de este contrato (regla de
    /// sustituibilidad tipo Liskov, igual que <c>ITokenProvider</c>).
    /// </summary>
    Task<UsageSnapshot> GetUsageAsync(CancellationToken cancellationToken = default);
}
```

**Decisión — forma de la firma**: idéntica en espíritu a `ITokenProvider.GetTokenAsync` (asíncrona, `CancellationToken` opcional con valor por defecto, devuelve un tipo de dominio explícito en vez de `HttpResponseMessage`/`string?`/`Result<T>` genérico). Rationale: consistencia de puerto a puerto en Application (un desarrollador que ya conoce `ITokenProvider` reconoce el patrón inmediatamente) y cumplimiento literal del primer Acceptance Criteria del documento de requisitos. No se considera una alternativa síncrona por las mismas razones ya fijadas en el diseño de Issue #2 (MediatR es asíncrono en todo Application; una futura fuente BLE necesitará E/S real).

## Diseño de `AnthropicApiUsageDataSource`

```csharp
namespace ClaudeMeter.Infrastructure.Usage;

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClaudeMeter.Application.Abstractions;
using ClaudeMeter.Domain.Usage;

/// <summary>
/// Implementación de <see cref="IUsageDataSource"/> que llama a
/// <c>POST https://api.anthropic.com/v1/messages</c> con el token OAuth
/// obtenido de <see cref="ITokenProvider"/> y traduce la respuesta cruda
/// (código de estado + cabeceras <c>anthropic-ratelimit-unified-*</c>) a
/// un <see cref="UsageSnapshot"/>. Su única responsabilidad es construir
/// la petición, enviarla y traducir la respuesta: no calcula countdowns
/// ni porcentajes, ni lee el token directamente de ningún fichero (eso
/// sigue siendo responsabilidad exclusiva de <see cref="ITokenProvider"/>),
/// ni reintenta ante fallos transitorios (alcance de F2), ni intenta
/// refrescar el token OAuth ante 401/403.
/// </summary>
public sealed class AnthropicApiUsageDataSource : IUsageDataSource
{
    private const string MessagesEndpoint = "https://api.anthropic.com/v1/messages";
    private const string AnthropicVersion = "2023-06-01";
    private const string AnthropicBetaOAuth = "oauth-2025-04-20";

    // Modelo vigente más barato/rápido disponible: minimiza la cuota real
    // consumida por cada llamada (ver Risk "Cost/Quota" del documento de
    // requisitos). Valor provisional — confirmar en la validación manual
    // ya exigida por el Acceptance Criteria del issue original.
    private const string PingModel = "claude-haiku-4-5-20251001";

    // Formato "claude-code/<versión>" confirmado como obligatorio (evita
    // un bucket de rate-limit más agresivo). El número de versión exacto
    // es un literal hasta que F6 introduzca Nerdbank.GitVersioning.
    private const string UserAgentValue = "claude-code/0.1.0";

    private readonly ITokenProvider _tokenProvider;
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Recibe tanto <see cref="ITokenProvider"/> como el <see cref="HttpClient"/>
    /// ya construido (compartido, de larga vida) por constructor. La
    /// creación/configuración del <see cref="HttpClient"/> (incluida su
    /// vida útil como singleton) es responsabilidad del composition root
    /// (Desktop, fuera de alcance de este issue) — ver Decisiones Técnicas
    /// Clave para el rationale de esta elección frente a
    /// <c>IHttpClientFactory</c> o un <see cref="HttpClient"/> propio.
    /// </summary>
    public AnthropicApiUsageDataSource(ITokenProvider tokenProvider, HttpClient httpClient)
    {
        _tokenProvider = tokenProvider;
        _httpClient = httpClient;
    }

    /// <inheritdoc />
    public async Task<UsageSnapshot> GetUsageAsync(CancellationToken cancellationToken = default)
    {
        var tokenResult = await _tokenProvider.GetTokenAsync(cancellationToken);
        if (!tokenResult.IsSuccess)
        {
            // Regla de CLAUDE.md / AC: sin token utilizable, nunca se
            // realiza ninguna llamada HTTP.
            return UsageSnapshot.TokenUnavailable();
        }

        using var request = BuildRequest(tokenResult.AccessToken!);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException)
        {
            // Error de red / DNS / conexión rechazada, etc.
            return UsageSnapshot.RequestFailed();
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Timeout interno de HttpClient (no cancelación pedida por el
            // llamante) — se traduce como fallo de la petición. La
            // OperationCanceledException genuina (cancellationToken
            // solicitado) se propaga sin capturar, igual que en
            // CredentialsFileTokenProvider.
            return UsageSnapshot.RequestFailed();
        }

        using (response)
        {
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                // Nunca se intenta refrescar el token — regla de CLAUDE.md.
                return UsageSnapshot.Unauthorized();
            }

            if (!response.IsSuccessStatusCode)
            {
                return UsageSnapshot.RequestFailed();
            }

            var session = ReadWindow(response.Headers, "5h");
            var weekly = ReadWindow(response.Headers, "7d");

            if (session.Status is null || weekly.Status is null)
            {
                // 200 pero sin las cabeceras unified-* mínimas esperadas.
                return UsageSnapshot.RequestFailed();
            }

            return UsageSnapshot.Success(session, weekly);
        }
    }

    private static HttpRequestMessage BuildRequest(string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, MessagesEndpoint);

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Add("anthropic-version", AnthropicVersion);
        request.Headers.Add("anthropic-beta", AnthropicBetaOAuth);
        request.Headers.UserAgent.ParseAdd(UserAgentValue);

        var body = new CreateMessageRequestDto(
            Model: PingModel,
            MaxTokens: 1,
            Messages: [new MessageDto(Role: "user", Content: "ping")]);

        request.Content = new StringContent(
            JsonSerializer.Serialize(body),
            Encoding.UTF8,
            "application/json");

        return request;
    }

    private static RawRateLimitHeaders ReadWindow(HttpResponseHeaders headers, string windowSuffix) =>
        new(
            Status: GetHeaderValue(headers, $"anthropic-ratelimit-unified-{windowSuffix}-status"),
            Utilization: GetHeaderValue(headers, $"anthropic-ratelimit-unified-{windowSuffix}-utilization"),
            Remaining: GetHeaderValue(headers, $"anthropic-ratelimit-unified-{windowSuffix}-remaining"),
            Reset: GetHeaderValue(headers, $"anthropic-ratelimit-unified-{windowSuffix}-reset"));

    private static string? GetHeaderValue(HttpResponseHeaders headers, string name) =>
        headers.TryGetValues(name, out var values) ? values.FirstOrDefault() : null;

    private sealed record CreateMessageRequestDto(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("max_tokens")] int MaxTokens,
        [property: JsonPropertyName("messages")] IReadOnlyList<MessageDto> Messages);

    private sealed record MessageDto(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);
}
```

### Mapeo GIVEN-WHEN-THEN → estado de `UsageSnapshot`

| Escenario del documento de requisitos | Camino en el código | Estado resultante |
|---|---|---|
| `ITokenProvider` devuelve éxito, API responde 200 con las 8 cabeceras `unified-5h-*`/`unified-7d-*` | `tokenResult.IsSuccess` → true, `SendAsync` OK, `response.IsSuccessStatusCode` → true, ambos `Status` no nulos | `Success(session, weekly)` con las cabeceras exactas leídas, sin transformar |
| `ITokenProvider` devuelve cualquier estado de fallo (fichero ausente, JSON inválido, token ausente/vacío) | `!tokenResult.IsSuccess` → true, **no se construye ni envía ninguna petición HTTP** | `TokenUnavailable()` |
| API responde 401 | `response.StatusCode == Unauthorized` | `Unauthorized()` |
| API responde 403 | `response.StatusCode == Forbidden` | `Unauthorized()` |
| Error de red / timeout | `HttpRequestException` o `TaskCanceledException` (sin cancelación pedida) capturada | `RequestFailed()` |
| API responde 5xx | `!response.IsSuccessStatusCode` (y no es 401/403) | `RequestFailed()` |
| API responde 200 sin `anthropic-ratelimit-unified-5h-status`/`-7d-status` | `session.Status is null \|\| weekly.Status is null` | `RequestFailed()` |
| Consumidor solo necesita saber si el snapshot es utilizable | `result.IsSuccess` | N/A — ya cubierto por el diseño de `UsageSnapshot` |

### Ninguna excepción no controlada escapa

Puntos exactos envueltos en `try/catch`:
1. `_httpClient.SendAsync` → captura `HttpRequestException` (red/DNS/conexión) y `TaskCanceledException` filtrada por `when (!cancellationToken.IsCancellationRequested)` (timeout interno de `HttpClient.Timeout`), ambas mapeadas a `RequestFailed`.

No se añade un `catch (Exception)` genérico adicional, por la misma razón documentada en el diseño de Issue #2: ocultaría fallos de programación reales bajo un estado de dominio que no les corresponde. `OperationCanceledException` genuina (cancelación pedida por el consumidor vía `cancellationToken`) se propaga sin capturar, igual que en `CredentialsFileTokenProvider`. `JsonSerializer.Serialize` sobre `CreateMessageRequestDto` no puede lanzar en este caso (DTO de escritura, sin datos externos de entrada) por lo que no necesita try/catch.

### Snippet de referencia para el futuro composition root (F1, fuera de alcance de este issue)

Se documenta aquí únicamente como guía para quien cablee el composition root de Desktop, de forma que la elección de `HttpClient` compartido (ver Decisiones Técnicas Clave) se aplique de forma consistente cuando llegue ese momento:

```csharp
var httpClient = new HttpClient(new SocketsHttpHandler
{
    PooledConnectionLifetime = TimeSpan.FromMinutes(15) // evita staleness de DNS en procesos de larga vida (F6: autostart)
})
{
    Timeout = TimeSpan.FromSeconds(30)
};

IUsageDataSource usageDataSource = new AnthropicApiUsageDataSource(tokenProvider, httpClient);
```

## Estrategia de Testing

**Ubicación**: `test/ClaudeMeter.Infrastructure.Tests/Usage/AnthropicApiUsageDataSourceTests.cs` (el `.csproj` de este proyecto ya referencia `ClaudeMeter.Infrastructure`, sin cambios necesarios; no hace falta ningún `InternalsVisibleTo` nuevo porque el constructor de `AnthropicApiUsageDataSource` es enteramente público).

**Cómo se simula el `HttpMessageHandler`**: siguiendo el patrón ya usado en el proyecto (sin `Moq`/librería de mocking — ninguno de los `.csproj` de test la referencia; se usan test doubles escritos a mano, igual que el resto de la suite), se crea un `StubHttpMessageHandler : HttpMessageHandler` en el propio proyecto de test:

```csharp
namespace ClaudeMeter.Infrastructure.Tests.Usage;

/// <summary>
/// Test double de <see cref="HttpMessageHandler"/> que devuelve una
/// respuesta pre-configurada sin realizar ninguna llamada de red real, y
/// captura la última petición enviada para poder aserciones sobre sus
/// headers/URL/método.
/// </summary>
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

    public HttpRequestMessage? LastRequest { get; private set; }

    public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        _responder = responder;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        return Task.FromResult(_responder(request));
    }
}
```

Y un `FakeTokenProvider : ITokenProvider` mínimo:

```csharp
namespace ClaudeMeter.Infrastructure.Tests.Usage;

internal sealed class FakeTokenProvider : ITokenProvider
{
    private readonly TokenResult _result;

    public FakeTokenProvider(TokenResult result) => _result = result;

    public Task<TokenResult> GetTokenAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_result);
}
```

Cada test construye `new HttpClient(new StubHttpMessageHandler(...))` y lo inyecta junto con un `FakeTokenProvider` en `AnthropicApiUsageDataSource` — **nunca una llamada de red real ni un token real**, cumpliendo el Acceptance Criteria correspondiente y la regla de QA de `CLAUDE.md`.

**Decisión — por qué no `WireMock.Net`**: `CLAUDE.md` lo menciona como alternativa aceptable ("fake `HttpMessageHandler` or WireMock.Net"), pero para un único endpoint con un puñado de respuestas sintéticas, un `HttpMessageHandler` a mano es más simple, no añade una `PackageReference` nueva, y sigue el mismo principio YAGNI ya aplicado en Issue #2 al rechazar `System.IO.Abstractions`. Si en F1/F2 aparecen más escenarios HTTP (retry/backoff, múltiples endpoints), se puede reconsiderar entonces.

**Casos cubiertos (mapeo 1:1 con los GIVEN-WHEN-THEN)**:

| Test | GIVEN-WHEN-THEN cubierto |
|---|---|
| `GetUsageAsync_ConTokenDeExitoY200ConCabecerasCompletas_DevuelveSuccessConLosValoresExactos` | Token OK + 200 con las 8 cabeceras `unified-5h-*`/`unified-7d-*` → `Success` con los valores exactos leídos |
| `GetUsageAsync_ConTokenEnEstadoDeFallo_DevuelveTokenUnavailableSinLlamarAlHandler` (`[Theory]` sobre `FileNotFound`/`InvalidJson`/`TokenMissing`) | Cualquier fallo de `ITokenProvider` → `TokenUnavailable`, **verificando que el `StubHttpMessageHandler` nunca recibió una petición** (`LastRequest is null`) |
| `GetUsageAsync_Con401_DevuelveUnauthorized` | 401 → `Unauthorized` |
| `GetUsageAsync_Con403_DevuelveUnauthorized` | 403 → `Unauthorized` |
| `GetUsageAsync_Con500_DevuelveRequestFailed` | 5xx → `RequestFailed` |
| `GetUsageAsync_Con200SinCabecerasUnified_DevuelveRequestFailed` | 200 sin `anthropic-ratelimit-unified-*` esperadas → `RequestFailed` |
| `GetUsageAsync_AnteHttpRequestException_DevuelveRequestFailedSinLanzar` | Handler que lanza `HttpRequestException` → `RequestFailed`, sin excepción no controlada |
| `GetUsageAsync_ConTokenDeExito_EnviaLosCincoHeadersEsperados` | Inspecciona `StubHttpMessageHandler.LastRequest` tras una llamada exitosa: verifica `Authorization`, `anthropic-version`, `anthropic-beta`, `User-Agent` y `Content-Type`, con énfasis en `User-Agent` por su impacto en el bucket de rate-limit (test explícito exigido por el Acceptance Criteria) |
| `GetUsageAsync_ConTokenDeExito_EnviaPostALaUrlYPayloadEsperados` | Verifica método `POST`, URL `https://api.anthropic.com/v1/messages` y que el body deserializado tiene `max_tokens == 1` y un único mensaje |

Estos 9 casos (con la variante `[Theory]`) cubren los cuatro estados de `UsageSnapshotStatus`, el camino "sin llamada HTTP" del caso `TokenUnavailable`, y los dos tests explícitos de forma de la petición exigidos por el Acceptance Criteria, contribuyendo al objetivo de cobertura del 70% (`testingCoverage`) para Infrastructure.

Adicionalmente, en `ClaudeMeter.Domain.Tests` se puede (opcional, no bloqueante para este issue) añadir un test puro de `UsageSnapshot`/`RawRateLimitHeaders` verificando que cada fábrica estática produce el `Status`/`Session`/`Weekly`/`IsSuccess` esperado — lógica de dominio sin E/S, barata de cubrir al 100%.

## Cross-Cutting Concerns

- **Seguridad**: el token nunca se registra ni se incluye en mensajes de excepción propagados; ninguno de los estados de fallo (`TokenUnavailable`, `Unauthorized`, `RequestFailed`) lleva el valor del token ni el cuerpo de la respuesta de la API. El header `Authorization` se construye con `AuthenticationHeaderValue`, nunca concatenado en texto libre que pudiera acabar en un log futuro. F0 no incorpora todavía ningún framework de logging (Serilog llega en F2, igual que se decidió en Issue #2); no se añade una dependencia de logging en este issue.
- **Rendimiento**: el `HttpClient` es un singleton de larga vida, reutilizado en cada llamada (60 s de polling en F1) — nunca se crea una instancia nueva por invocación. `SocketsHttpHandler.PooledConnectionLifetime` (ver snippet del composition root) mitiga la staleness de DNS para procesos de larga vida (F6, autostart), sin necesidad de `IHttpClientFactory`.
- **Manejo de errores**: contrato "nunca excepción no controlada para los casos esperados" cumplido mediante el único `try/catch` descrito arriba; `OperationCanceledException` genuina se propaga sin capturar. No hay reintentos aquí — el retry con backoff de F2 se añadirá alrededor de esta clase (o de su consumidor), no dentro de ella, para no adelantar alcance de F2.
- **Retrocompatibilidad**: primera implementación del puerto `IUsageDataSource`; su forma asíncrona con `UsageSnapshot` explícito minimiza el riesgo de romper firmas cuando aparezcan futuras implementaciones (`SimulatedUsageDataSource`, `BleUsageSink` en F7).
- **Sustituibilidad (Liskov)**: cualquier otra implementación futura de `IUsageDataSource` (simulada, BLE) debe seguir devolviendo `UsageSnapshot` con los mismos cuatro estados semánticos en vez de lanzar para casos esperados — el propio `AnthropicApiUsageDataSource` ya lo demuestra al traducir sin excepción tanto el caso "sin token" como los fallos HTTP, preservando el contrato fijado por `TokenResult`/`ITokenProvider` en Issue #2.
- **Coste/cuota**: cada llamada consume una cantidad mínima de cuota real (`max_tokens: 1`, modelo más barato disponible) — riesgo aceptado explícitamente por el documento de requisitos, no mitigable dentro de esta clase (no existe endpoint de "solo estado"); a tener en cuenta al definir la frecuencia de polling en F1.

## Implementation Plan

1. Crear `src/ClaudeMeter.Domain/Usage/UsageSnapshot.cs` con `UsageSnapshotStatus` (enum), `RawRateLimitHeaders` (record posicional) y `UsageSnapshot` (record sellado, fábricas estáticas), documentado con XMLDoc en español.
2. Crear `src/ClaudeMeter.Application/Abstractions/IUsageDataSource.cs` con el método `Task<UsageSnapshot> GetUsageAsync(CancellationToken cancellationToken = default)`, documentado con XMLDoc en español.
3. Crear `src/ClaudeMeter.Infrastructure/Usage/AnthropicApiUsageDataSource.cs` implementando `IUsageDataSource` según el diseño de la sección anterior (constructor público `(ITokenProvider, HttpClient)`, DTOs privados `CreateMessageRequestDto`/`MessageDto` con `System.Text.Json`, métodos privados `BuildRequest`/`ReadWindow`/`GetHeaderValue`).
4. Crear `test/ClaudeMeter.Infrastructure.Tests/Usage/StubHttpMessageHandler.cs` y `test/ClaudeMeter.Infrastructure.Tests/Usage/FakeTokenProvider.cs` (test doubles reutilizables).
5. Crear `test/ClaudeMeter.Infrastructure.Tests/Usage/AnthropicApiUsageDataSourceTests.cs` con los 9 casos descritos en la sección de Testing.
6. (Opcional, no bloqueante) Añadir un test puro de `UsageSnapshot`/`RawRateLimitHeaders` en `test/ClaudeMeter.Domain.Tests/` verificando las fábricas estáticas.
7. Ejecutar `dotnet test` sobre la solución completa y comprobar que no se introducen warnings nuevos bajo `Release` (el `Directory.Build.props` ya trata warnings como errores en Release).
8. Verificar manualmente — construyendo `AnthropicApiUsageDataSource` con un `CredentialsFileTokenProvider()` real (ruta por defecto) y un `HttpClient` real, en un entorno con `.claude\.credentials.json` real (sin commitear nada de ese fichero ni del resultado) — que la API responde 200 con las cabeceras `anthropic-ratelimit-unified-5h-*`/`-7d-*` esperadas, y anotar el nombre/formato exacto observado (`utilization` vs `remaining`, formato de `reset`) por si difiere de lo documentado aquí. Esta validación manual es **obligatoria antes de cerrar el issue**, según el Acceptance Criteria original heredado del documento de requisitos — no es parte de la suite automatizada.

## Risks & Open Decisions

None — no quedan decisiones de diseño abiertas que requieran validación humana antes de pasar a desarrollo. La única decisión que el documento de requisitos delegaba explícitamente a esta fase (estrategia de inyección de `HttpClient`) se resuelve arriba (constructor-injected, `HttpClient` compartido de larga vida, sin `IHttpClientFactory`), con su rationale y alternativas rechazadas documentadas en "Decisiones Técnicas Clave". Las demás cuestiones de diseño que requerían una elección propia de esta fase (modelo a usar en el payload, valor literal de `User-Agent`, criterio exacto de "éxito" ante ambigüedad de nomenclatura de cabeceras, forma del tipo `RawRateLimitHeaders`) también quedan resueltas explícitamente arriba.

Riesgos heredados del documento de requisitos (no bloquean el diseño, pero conviene que el desarrollador los tenga presentes):
- **Data quality**: las cabeceras `anthropic-ratelimit-unified-5h-*`/`-7d-*` no son un contrato oficial documentado por Anthropic; su nombre, presencia o formato exacto pueden diferir de lo aquí documentado — de ahí que el paso 8 del Implementation Plan (validación manual) sea obligatorio y su resultado prevalezca sobre este documento si hay discrepancias.
- **Compliance/Adoption**: Anthropic podría cambiar en el futuro, sin aviso, el comportamiento no documentado del que depende esta llamada (incluido el header `User-Agent`), rompiendo la integración.
- **Cost/Quota**: cada llamada (incluido cada ciclo de polling en F1) consume una cantidad mínima de cuota real — trade-off ya aceptado, no un pendiente de este issue.
