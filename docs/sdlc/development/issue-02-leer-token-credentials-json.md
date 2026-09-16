# Implementation Summary: [F0] Leer token desde .credentials.json

## Design Reference
`docs/sdlc/design/issue-02-leer-token-credentials-json.md` (Issue #2, milestone "F0 — Núcleo de validación"). Documento cerrado, implementado literalmente, sin decisiones abiertas.

## Files Changed

- `src/ClaudeMeter.Domain/Authentication/TokenResult.cs` — **Nuevo.** `TokenResultStatus` (enum: `Success`, `FileNotFound`, `InvalidJson`, `TokenMissing`) y `TokenResult` (record sellado con constructor privado, propiedades `Status`/`AccessToken`/`IsSuccess`, y cuatro fábricas estáticas `Success`/`FileNotFound`/`InvalidJson`/`TokenMissing`). Sin dependencias externas, tal y como exige Domain.

- `src/ClaudeMeter.Application/Abstractions/ITokenProvider.cs` — **Nuevo.** Puerto `ITokenProvider` con el único método `Task<TokenResult> GetTokenAsync(CancellationToken cancellationToken = default)`. Solo referencia `ClaudeMeter.Domain.Authentication.TokenResult`; no referencia ningún tipo de Infrastructure ni de E/S.

- `src/ClaudeMeter.Infrastructure/Authentication/CredentialsFileTokenProvider.cs` — **Nuevo.** Implementación de `ITokenProvider`:
  - Constructor público sin parámetros que resuelve la ruta fija `%USERPROFILE%\.claude\.credentials.json` vía `Environment.SpecialFolder.UserProfile`.
  - Constructor `internal` que acepta la ruta del fichero, reservado para los tests de Infrastructure (vía `InternalsVisibleTo`).
  - `GetTokenAsync`: `File.Exists` → `FileNotFound` si no existe; `File.ReadAllTextAsync` envuelto en `try/catch (IOException or UnauthorizedAccessException)` → `FileNotFound` (condición de carrera); `JsonSerializer.Deserialize<CredentialsFileDto>` envuelto en `try/catch (JsonException)` → `InvalidJson`; si `claudeAiOauth.accessToken` es `null`/vacío/blanco → `TokenMissing`; en caso contrario → `Success(accessToken)` con el valor exacto leído. DTOs privados `CredentialsFileDto`/`OAuthSectionDto` con `System.Text.Json` y `JsonPropertyName` para mapear los nombres `camelCase` del fichero real. No realiza llamadas HTTP ni cálculo de rate limits — responsabilidad limitada estrictamente a leer/parsear el fichero, según exige `CLAUDE.md`.

- `src/ClaudeMeter.Infrastructure/ClaudeMeter.Infrastructure.csproj` — **Modificado.** Se añadió `<InternalsVisibleTo Include="ClaudeMeter.Infrastructure.Tests" />` para que la fase de testing (`sdlc-testing`) pueda usar el constructor `internal` de `CredentialsFileTokenProvider` sin exponerlo públicamente. Ningún cambio de `ProjectReference` fue necesario: las referencias ya declaradas en el scaffolding F0 cubrían este issue.

- `src/ClaudeMeter.Desktop/App.xaml.cs` — **Modificado (efecto colateral necesario, fuera del alcance directo del diseño).** Se cambió `public partial class App : Application` por `public partial class App : System.Windows.Application`. Ver sección de desviaciones para el porqué.

## Deviations from the Design

- **Fix no descrito en el diseño: colisión de nombres `Application` en `ClaudeMeter.Desktop`.** Al crear el namespace `ClaudeMeter.Application.Abstractions` (exigido literalmente por el diseño y por `CLAUDE.md`), el proyecto `ClaudeMeter.Desktop` —que ya referenciaba `ClaudeMeter.Application.csproj` desde el scaffolding F0— dejó de compilar: `App.xaml.cs` declaraba `public partial class App : Application`, y el compilador empezó a resolver el identificador `Application` contra el namespace `ClaudeMeter.Application` (visible como namespace anidado bajo `ClaudeMeter` a través de la referencia de proyecto) en lugar de la clase `System.Windows.Application`, produciendo `CS0118`. Esto es un efecto colateral inevitable de introducir el namespace que el diseño exige explícitamente, no una decisión de diseño alternativa. La corrección mínima y no invasiva fue cualificar completamente el tipo base en el único punto afectado (`System.Windows.Application`), sin tocar ninguna otra lógica de `Desktop`. No se ha modificado ningún fichero `.xaml` generado (`App.g.cs`/`MainWindow.g.cs`) porque esos ya cualificaban `System.Windows.Application` explícitamente y no se vieron afectados.
- No hay más desviaciones: el resto del código sigue el diseño literalmente (namespaces, rutas de fichero, firmas, catálogo de excepciones capturadas, DTOs, comentarios XMLDoc en español).

## How to Verify

1. **Compilación** (ya ejecutada como parte de esta fase): `dotnet build ClaudeMeter.sln` y `dotnet build ClaudeMeter.sln -c Release` — ambas terminan en "Compilación correcta", 0 advertencias, 0 errores.
2. **Verificación manual exploratoria** (no automatizada, tal y como indica el paso 8 del plan de diseño): en una máquina con `%USERPROFILE%\.claude\.credentials.json` real (el que genera el CLI de Claude Code), instanciar `new CredentialsFileTokenProvider()` y llamar a `GetTokenAsync()` debería devolver `TokenResult.Success` con el `accessToken` real. No se ha comprometido ningún token real en este repositorio.
3. Para verificar los casos de fallo manualmente sin depender de los tests (que no forman parte de esta fase): renombrar temporalmente `.credentials.json` (→ `FileNotFound`), o editar una copia con JSON corrupto (→ `InvalidJson`), o con `claudeAiOauth.accessToken` vacío/ausente (→ `TokenMissing`), usando el constructor `internal` desde un pequeño programa de consola de prueba manual si se desea (no incluido aquí).

## Follow-ups / Known Limitations

- **Tests unitarios pendientes**: el diseño especifica 6 casos de test para `CredentialsFileTokenProviderTests` en `test/ClaudeMeter.Infrastructure.Tests/Authentication/` (éxito, fichero ausente, JSON mal formado, token ausente, token vacío/blanco en `[Theory]`, e `IsSuccess` uniforme sobre los tres fallos), más un test opcional de `TokenResult` en `ClaudeMeter.Domain.Tests`. Ninguno de estos tests se ha escrito ni ejecutado en esta fase — corresponde exclusivamente a `sdlc-testing` (QA Engineer), conforme a las reglas de este agente.
- **Composition root / DI**: el cableado de `ITokenProvider` → `CredentialsFileTokenProvider` en el contenedor de DI de `ClaudeMeter.Desktop` no existe todavía (no hay `Program.cs`/`App.xaml.cs` con contenedor DI); queda fuera del alcance de este issue, tal y como indica el propio diseño.
- **Logging del caso `InvalidJson`**: por diseño explícito, no se añade ningún framework de logging en F0 (llega en F2 con Serilog); el bloque `catch (JsonException)` ya es el único punto de inserción necesario para F2.
- El fix de `App.xaml.cs` documentado arriba conviene revisarlo en la fase de documentación técnica (`sdlc-documentation`) o en un futuro issue de scaffolding, ya que es un efecto colateral estructural (namespace `ClaudeMeter.Application` vs. `System.Windows.Application`) que puede repetirse si se añaden más namespaces raíz en Application/Infrastructure que colisionen con tipos de WPF.

Nada se ha confirmado (`commit`) ni enviado (`push`) al repositorio: todos los cambios quedan en el árbol de trabajo, listos para revisión. Próximos pasos sugeridos del pipeline: `sdlc-testing` (escribir los tests unitarios descritos en el diseño) y `sdlc-documentation` (documentación técnica/funcional de este componente).
