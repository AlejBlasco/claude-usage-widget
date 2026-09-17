# Implementation Summary: F2 — Robustez, Ciclo A

## Design Reference

`docs/sdlc/design/f2-robustez-ciclo-a.md` (Requirements: `docs/sdlc/requirements/f2-robustez-ciclo-a.md`).
Cubre US-1 (#10, aviso de reautenticación), US-2 (#11, retry con backoff) y
US-3 (#12, logging estructurado con Serilog), como un único ciclo SDLC con
cadena de dependencia interna #10 → #11 → #12. Se ha seguido el
Implementation Plan del documento de diseño paso a paso (pasos 1, 3, 4, 6,
8, 10-13; los pasos de test — 2, 5, 7, 9, 14 — quedan fuera de esta fase,
ver más abajo).

## Files Changed

- `src/ClaudeMeter.Domain/Usage/UsageSnapshot.cs` — nuevo miembro
  `UsageSnapshotStatus.MalformedResponse` (categoría 3 del modelo de
  reintento: 2xx sin cabeceras `anthropic-ratelimit-unified-*`) y su
  fábrica `UsageSnapshot.MalformedResponse()`; XMLDoc de `RequestFailed`/
  `RequestFailed()` actualizado para reflejar su alcance reducido (solo
  fallo transitorio: red, timeout, 5xx).
- `src/ClaudeMeter.Console/Polling/UsagePollingLoop.cs` — nuevo
  `case UsageSnapshotStatus.MalformedResponse` en el `switch` de `Render`,
  reutilizando `IUsagePollingRenderer.RenderRequestFailed` (sin método de
  renderer nuevo), tal cual el diseño.
- `src/ClaudeMeter.Infrastructure/Usage/AnthropicApiUsageDataSource.cs` —
  nuevo parámetro `ILogger<AnthropicApiUsageDataSource>` en el constructor;
  el caso "2xx sin cabeceras" ahora devuelve `MalformedResponse()` (antes
  `RequestFailed()`); logging por cada intento HTTP individual
  (`Warning` para `TokenUnavailable`/fallo de red/timeout/no-2xx,
  `Error` para `Unauthorized`/`MalformedResponse`, `Debug` para éxito).
- `src/ClaudeMeter.Infrastructure/Usage/RetryPolicyOptions.cs` (nuevo) —
  `record` de configuración con `Default` (3 intentos, 2s→4s, techo 10s,
  valores confirmados por el usuario en el diseño).
- `src/ClaudeMeter.Infrastructure/Usage/RetryingUsageDataSource.cs` (nuevo)
  — decorator de `IUsageDataSource`; reintenta únicamente
  `UsageSnapshotStatus.RequestFailed` con backoff exponencial hecho a mano
  (`Task.Delay` vía función de espera inyectable); `Success`,
  `TokenUnavailable`, `Unauthorized` y `MalformedResponse` se devuelven de
  inmediato. Constructor público (`Task.Delay` real) + constructor
  `internal` con función de espera inyectable (visible vía
  `InternalsVisibleTo` hacia `ClaudeMeter.Infrastructure.Tests`, ya
  declarado en el `.csproj`).
- `src/ClaudeMeter.Infrastructure/ClaudeMeter.Infrastructure.csproj` —
  añadido `PackageReference Microsoft.Extensions.Logging.Abstractions
  8.0.3`; comentario de `InternalsVisibleTo` actualizado para mencionar el
  nuevo constructor `internal` de `RetryingUsageDataSource`.
- `src/ClaudeMeter.Desktop/Logging/SerilogConfigurator.cs` (nuevo) —
  fábrica estática `CreateLogger(string? logDirectory = null)` (directorio
  inyectable para tests, nunca escribe en `%LOCALAPPDATA%` real si se pasa
  uno) y `DefaultLogDirectory()` → `%LOCALAPPDATA%\ClaudeMeter\logs`; sink
  de fichero con rotación diaria y retención de 7 días.
- `src/ClaudeMeter.Desktop/App.xaml.cs` — `OnStartup` inicializa Serilog
  (`Log.Logger = SerilogConfigurator.CreateLogger()`), lo conecta a
  `Microsoft.Extensions.Logging` (`AddLogging(b => b.AddSerilog(...))`), y
  compone la cadena real de `IUsageDataSource`:
  `AnthropicApiUsageDataSource` registrado como tipo concreto,
  `RetryingUsageDataSource` (con `RetryPolicyOptions.Default`) como el
  único `IUsageDataSource` expuesto. `OnExit` llama a `Log.CloseAndFlush()`.
- `src/ClaudeMeter.Desktop/Polling/UsagePollingCoordinator.cs` — nuevo
  parámetro `ILogger<UsagePollingCoordinator>` en el constructor; log
  `Information` en `Start()` y heartbeat `Information` por tick en
  `PollAsync()` (`Status={Status}`); el `catch` de última instancia (antes
  silencioso) ahora registra `LogError` con la excepción.
- `src/ClaudeMeter.Desktop/Pages/UsagePage.razor` — nuevo campo `_status`
  (`UsageSnapshotStatus?`), `@inject ILogger<UsagePollingCoordinator> Logger`
  (requerido por el nuevo parámetro del constructor del coordinador), rama
  `@if (_status == UsageSnapshotStatus.Unauthorized)` que sustituye las dos
  `UsageBar` por `ReauthNotice`, nueva rama en `Apply()` que nunca marca
  `_isStale = true` para `Unauthorized` (AC 3 de US-1), y `StatusForTests`
  para los futuros tests bUnit.
- `src/ClaudeMeter.Desktop/Pages/ReauthNotice.razor` (nuevo) — componente
  que sustituye por completo las barras cuando `Status == Unauthorized`,
  icono + mensaje de reautenticación, sin sufijo "(desactualizado)".
- `src/ClaudeMeter.Desktop/wwwroot/css/app.css` — clases `.usage-reauth`,
  `.usage-reauth__icon`, `.usage-reauth__message` (reutiliza el rojo ya
  usado para el umbral crítico).
- `src/ClaudeMeter.Desktop/_Imports.razor` — añadido
  `@using Microsoft.Extensions.Logging` (necesario para `@inject ILogger<...>`
  en `UsagePage.razor`).
- `src/ClaudeMeter.Desktop/ClaudeMeter.Desktop.csproj` — añadidos
  `PackageReference` para `Serilog` (4.4.0), `Serilog.Sinks.File` (7.0.0),
  `Serilog.Extensions.Logging` (8.0.0) y `Microsoft.Extensions.Logging`
  (8.0.1). Ver sección de versiones más abajo para el porqué de cada
  número exacto.

## Deviations from the Design

1. **Versiones NuGet: 2 de los 5 paquetes no siguen la serie 8.0.x asumida
   por el diseño.** El diseño delegaba explícitamente en Development
   "confirmar el último patch estable de la serie 8.0.x disponible en
   api.nuget.org en el momento de implementar" para los 5 paquetes nuevos.
   Al verificarlo contra `api.nuget.org/v3-flatcontainer/<paquete>/index.json`
   se encontró que **`Serilog`** (núcleo) y **`Serilog.Sinks.File`**
   versionan de forma independiente al ciclo de .NET (a diferencia de los
   paquetes `Microsoft.Extensions.*`, que sí siguen 8.0.x/9.0.x/10.0.x):
   - `Serilog`: nunca ha publicado una versión `8.x` — su serie más
     reciente estable es `4.x` (última: **4.4.0**). Se usa esta versión;
     es multi-target y compatible con `net8.0`.
   - `Serilog.Sinks.File`: la versión `8.0.0` únicamente existe como
     prerelease (`8.0.0-dev-*`, `8.0.0-nblumhardt-*`); nunca se publicó
     estable. La última versión estable real es **7.0.0**. Se usa esta.
   - Los otros 3 paquetes sí siguen la serie 8.0.x tal cual asumía el
     diseño: `Serilog.Extensions.Logging` → **8.0.0** (única versión
     estable de esa serie — el resto salta directo a 9.x/10.x),
     `Microsoft.Extensions.Logging` → **8.0.1**,
     `Microsoft.Extensions.Logging.Abstractions` → **8.0.3**.

   Esto es una comprobación mecánica de packaging, no un cambio de
   arquitectura (mismo criterio que el propio diseño fijó para esta
   decisión) — documentado con un comentario junto a cada
   `PackageReference` en los `.csproj` correspondientes, con la URL exacta
   de verificación.

2. **`src/ClaudeMeter.Console/Program.cs` (composition root de F0) —
   consumidor no listado en la sección "Compatibilidad hacia atrás" del
   diseño.** El diseño identificaba como consumidores del constructor
   modificado de `AnthropicApiUsageDataSource` únicamente "el composition
   root de Desktop y ambos proyectos de test", pero `ClaudeMeter.Console`
   (F0) tiene su propio composition root (`Program.cs`) que también
   instancia esta clase directamente y no estaba en esa lista. Es una
   omisión menor del análisis de impacto del diseño, no una decisión de
   arquitectura a revisar — se corrigió pasando
   `NullLogger<AnthropicApiUsageDataSource>.Instance`
   (`Microsoft.Extensions.Logging.Abstractions`, ya disponible
   transitivamente vía `ClaudeMeter.Infrastructure`), ya que F0/Console no
   tiene ninguna infraestructura de logging propia (Serilog se introdujo en
   F2 únicamente para `ClaudeMeter.Desktop`, ver `CLAUDE.md`). Sin este
   cambio el build de `ClaudeMeter.Console` no habría compilado.

3. **`using System.IO;` y `using Serilog;` añadidos explícitamente en
   `SerilogConfigurator.cs`**, no mostrados en el snippet del diseño. Igual
   que ya documentó el resumen de F1 para `System.Net.Http`, el conjunto de
   *implicit usings* de `Microsoft.NET.Sdk.Razor` (usado por
   `ClaudeMeter.Desktop`) no incluye `System.IO`, y sin `using Serilog;`
   explícito el método de extensión `LoggerSinkConfiguration.File(...)` no
   se resuelve. Corrección mecánica de compilación detectada en el primer
   intento de build, no un cambio de diseño.

4. **Ningún test creado ni modificado, incluyendo los ya existentes que el
   Implementation Plan del diseño (pasos 2, 5, 7, 9, 14) esperaba
   actualizar.** Regla dura de este agente: nunca crear, modificar ni
   ejecutar ficheros de test. Como consecuencia, y tal como el propio
   diseño anticipaba en su sección "Compatibilidad hacia atrás" (cambios de
   firma en `AnthropicApiUsageDataSource` y `UsagePollingCoordinator`),
   **dos ficheros de test ya existentes dejan de compilar** hasta que
   `sdlc-testing` los actualice:
   - `test/ClaudeMeter.Infrastructure.Tests/Usage/AnthropicApiUsageDataSourceTests.cs`
     (11 sitios de instanciación de `AnthropicApiUsageDataSource` con 2
     argumentos; el constructor ahora exige 3 — `CS7036`). Además, el test
     `GetUsageAsync_Con200SinCabecerasUnified_DevuelveRequestFailed` (y el
     de "solo cabecera de 5h") pasarán a fallar en tiempo de ejecución una
     vez recompilados, porque ahora se espera `MalformedResponse()` en vez
     de `RequestFailed()` — cambio de comportamiento ya anticipado y
     documentado por el propio diseño (paso 5 del Implementation Plan).
   - `test/ClaudeMeter.Desktop.Tests/Polling/UsagePollingCoordinatorTests.cs`
     (9 sitios de instanciación de `UsagePollingCoordinator` con 2
     argumentos; el constructor ahora exige 3 — `CS7036`).

   Verificado explícitamente con `dotnet build ClaudeMeter.sln -c Debug`:
   exactamente 19 errores `CS7036`, todos en esos dos ficheros — ningún
   otro proyecto de test se ve afectado (`ClaudeMeter.Domain.Tests`,
   `ClaudeMeter.Application.Tests`, `ClaudeMeter.Console.Tests` y el resto
   de `ClaudeMeter.Desktop.Tests` — `UsagePageTests.cs`, `UsageBarTests.cs`,
   `FakeUsageDataSource.cs` — compilan sin cambios). `sdlc-testing` deberá
   abordar esto como parte de sus pasos 5/9/14 ya previstos por el diseño,
   además de escribir los tests nuevos de `RetryingUsageDataSource`,
   `UsageSnapshot.MalformedResponse()`, `SerilogConfigurator` y
   `ReauthNotice`/`UsagePage` con `Unauthorized`.

Ninguna otra desviación: el resto del código (`UsageSnapshotStatus`/
`UsageSnapshot`, `AnthropicApiUsageDataSource`, `RetryPolicyOptions`,
`RetryingUsageDataSource`, `SerilogConfigurator`, `App.xaml.cs`,
`UsagePollingCoordinator`, `UsagePage.razor`, `ReauthNotice.razor`, CSS,
`UsagePollingLoop.cs`) se implementó tal cual la sección Data Model /
Implementation Plan del documento de diseño, incluyendo los niveles de log
exactos (`Warning` para `TokenUnavailable`, `Error` para `Unauthorized`/
`MalformedResponse`/reintentos agotados/excepción no controlada) y los
valores de `RetryPolicyOptions.Default` (3 intentos, 2s→4s, techo 10s).

## Versiones exactas de los 5 paquetes NuGet nuevos

| Paquete | Versión usada | Cómo se verificó |
|---|---|---|
| `Serilog` | **4.4.0** | `curl https://api.nuget.org/v3-flatcontainer/serilog/index.json` — última versión estable (sin sufijo de prerelease) de toda la lista; no existe ninguna serie `8.x` para este paquete. |
| `Serilog.Sinks.File` | **7.0.0** | `curl https://api.nuget.org/v3-flatcontainer/serilog.sinks.file/index.json` — última estable; `8.0.0` solo existe como `-dev-*`/`-nblumhardt-*` (prerelease), nunca publicado como estable. |
| `Serilog.Extensions.Logging` | **8.0.0** | `curl https://api.nuget.org/v3-flatcontainer/serilog.extensions.logging/index.json` — única versión estable de la serie 8.0.x (el resto de esa franja son `8.0.1-dev-*`; la siguiente estable ya es `9.0.0`). |
| `Microsoft.Extensions.Logging` | **8.0.1** | `curl https://api.nuget.org/v3-flatcontainer/microsoft.extensions.logging/index.json` — última estable de la serie 8.0.x (`8.0.0`, `8.0.1`). |
| `Microsoft.Extensions.Logging.Abstractions` | **8.0.3** | `curl https://api.nuget.org/v3-flatcontainer/microsoft.extensions.logging.abstractions/index.json` — última estable de la serie 8.0.x (`8.0.0`…`8.0.3`). |

## How to Verify

1. **Build (verificado en esta fase):** `dotnet build` por proyecto en
   Debug y Release para los 4 proyectos de producción — todos compilan sin
   advertencias ni errores:
   - `dotnet build src/ClaudeMeter.Domain/ClaudeMeter.Domain.csproj -c Debug|Release`
   - `dotnet build src/ClaudeMeter.Infrastructure/ClaudeMeter.Infrastructure.csproj -c Debug|Release`
   - `dotnet build src/ClaudeMeter.Console/ClaudeMeter.Console.csproj -c Debug|Release`
   - `dotnet build src/ClaudeMeter.Desktop/ClaudeMeter.Desktop.csproj -c Debug|Release`

   `dotnet build ClaudeMeter.sln` fallará hoy con 19 errores `CS7036`
   (ver Deviations §4) hasta que `sdlc-testing` actualice los dos ficheros
   de test afectados — es el estado esperado en este punto del pipeline,
   no una regresión de esta fase.
2. **Validación manual (US-1):** invalidar el token local o simular un
   401/403 contra un mock de larga duración; confirmar que `ReauthNotice`
   aparece en la UI real sustituyendo ambas barras y es visualmente
   distinguible de "No disponible" — no automatizable por este pipeline
   (WPF/WebView2 real).
3. **Validación manual (US-2):** cortar la conectividad de red brevemente
   (menos de ~6s) y confirmar que el widget se recupera solo tras el
   reintento sin mostrar error ni parpadeo visible; cortarla más de ~6s y
   confirmar que tras 3 intentos se muestra el fallo. Revisar
   `%LOCALAPPDATA%\ClaudeMeter\logs\claudemeter-*.log` para confirmar que
   la secuencia de reintentos queda trazada.
4. **Validación manual (US-3):** ejecutar `ClaudeMeter.Desktop` en Windows
   real y confirmar que aparece `%LOCALAPPDATA%\ClaudeMeter\logs\claudemeter-*.log`
   con contenido legible, con `Information` de arranque/heartbeat,
   `Warning` para `TokenUnavailable`/reintentos, y `Error` para
   `Unauthorized`/`MalformedResponse`/reintentos agotados.
5. Cuando `sdlc-testing` actualice
   `AnthropicApiUsageDataSourceTests.cs`/`UsagePollingCoordinatorTests.cs`
   (pasando `NullLogger<T>.Instance` en cada instanciación existente) y
   escriba los tests nuevos descritos en el Implementation Plan del diseño
   (pasos 7 y 9: `RetryingUsageDataSourceTests.cs` con función de espera
   falsa, `SerilogConfiguratorTests.cs` con directorio temporal), el build
   completo de `ClaudeMeter.sln` debería volver a compilar sin errores.

## Follow-ups / Known Limitations

- `test/ClaudeMeter.Infrastructure.Tests/Usage/AnthropicApiUsageDataSourceTests.cs`
  y `test/ClaudeMeter.Desktop.Tests/Polling/UsagePollingCoordinatorTests.cs`
  no compilan hasta que `sdlc-testing` los actualice (ver Deviations §4) —
  es explícitamente responsabilidad de esa fase, no de esta.
- Ningún test nuevo para `RetryingUsageDataSource`,
  `UsageSnapshot.MalformedResponse()`, `SerilogConfigurator` ni
  `ReauthNotice`/`UsagePage` con `Unauthorized` — pendiente de
  `sdlc-testing` (pasos 2, 7, 9, 14 del Implementation Plan del diseño).
- `RetryPolicyOptions.Default` no es configurable desde `config.json`
  (issue #13, explícitamente fuera de alcance de este ciclo).
- Las 3 validaciones manuales de la Definition of Done (US-1/US-2/US-3)
  siguen pendientes de un equipo Windows real — no automatizables por este
  pipeline, ya marcadas como tales en Requirements/Design.
- La franja horaria/costo de cuota real de los reintentos de US-2 contra la
  API de Anthropic real sigue sin validar en producción (mitigado por
  diseño con los valores de `RetryPolicyOptions.Default`, pero la
  validación manual de la Definition of Done sigue pendiente).
