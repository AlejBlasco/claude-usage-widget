# Testing Summary: F6 — Distribución

## Scope

Dos tareas sobre `docs/sdlc/development/f6-distribucion.md` (implementación
de F6 — Distribución, milestone #7, US-1 #27 → US-2 #25 → US-3 #26),
siguiendo la cadena documental hasta `docs/sdlc/design/f6-distribucion.md`
y `docs/sdlc/requirements/f6-distribucion.md` (Acceptance Criteria y
Definition of Done, usadas como fuente de verdad):

1. **Bloqueante:** `test/ClaudeMeter.Desktop.Tests/Tray/TrayIconServiceTests.cs`
   no compilaba (`CS7036`) porque `TrayIconService` gana un parámetro de
   constructor obligatorio (`AutoStartService`, F6/US-3) que el test no
   conocía — arreglado, y el test de "cuatro ítems del menú" corregido a
   cinco (nuevo ítem "Iniciar con Windows").
2. Tests nuevos para el código de F6 sin cobertura: `AutoStartService`
   (nuevo), `AppUpdateService` (nuevo), y el campo `AutoStartEnabled` de
   `AppConfig`/`AppConfigStore` (modificados).

Se ha leído el código real de producción de cada fichero antes de escribir
nada (`AutoStartService.cs`, `AppUpdateService.cs`, `AppConfig.cs`,
`AppConfigStore.cs`, `TrayIconService.cs`), y se ha inspeccionado por
reflexión el paquete NuGet `Velopack` 1.2.158 real (no solo su
documentación) para confirmar qué miembros de `UpdateManager`/
`VelopackLocator` son efectivamente mockeables antes de diseñar los test
doubles — ver rationale detallado en los XMLDoc de
`AutoStartServiceTests.cs`/`FakeUpdateManager.cs`.

**Dos cambios aditivos en código de producción, explícitamente señalados**
(regla dura nº 2 de este agente — ninguno cambia el comportamiento público
observable, ambos documentados también en el propio XMLDoc de cada
miembro):

- `AutoStartService.ConfigPathForTests` (`internal`, `null` por defecto):
  `SetEnabled` llamaba a `_configStore.Load()`/`Save()` sin ruta
  explícita, lo que habría hecho que cualquier test que lo invocase
  escribiera de verdad en `%LOCALAPPDATA%\ClaudeMeter\config.json` de la
  máquina que ejecuta los tests. `null` (el valor con el que arranca
  siempre en producción) preserva la ruta por defecto exacta de
  `AppConfigStore`.
- `AppUpdateService`: constructor `internal` que acepta un
  `Velopack.UpdateManager` ya construido (el constructor público de un
  solo argumento no cambia), y método `internal CheckAndApplyForTestsAsync()`
  que expone el flujo asíncrono privado para poder esperarlo (`await`) de
  forma determinista — mismo patrón `*ForTests` ya establecido en el
  repo (`ClickThroughService.SetEnabledForTests`,
  `UsagePollingCoordinator.PollOnceForTestsAsync`).

## Tests Added/Modified

- `test/ClaudeMeter.Desktop.Tests/Tray/TrayIconServiceTests.cs` —
  **Arreglado + ampliado.** `CreateService()` ahora construye y devuelve
  también un `AutoStartService` real (config en memoria, sin
  `ConfigPathForTests` salvo en los 2 tests que sí invocan `SetEnabled`).
  Test de "cuatro ítems" renombrado a
  `Constructor_..._YConstruyeLosCincoItemsDelMenu` y ampliado con la
  aserción del nuevo ítem. 3 tests nuevos: el ítem no está marcado al
  construir con `AutoStartEnabled: false`; con `AutoStartEnabled: true`
  persistido, el *constructor* (no `Initialize()`, gap ya aceptado — ver
  Gaps) aún no lo marca; `PerformClick()` invoca `AutoStartService.Toggle()`
  y actualiza `Checked` vía la suscripción real a `StateChanged`. El test
  de `Dispose()` desuscribiendo eventos se amplía para cubrir también
  `AutoStartService.StateChanged`. 13 tests en total (10 preexistentes + 3
  nuevos), todos en verde.
- `test/ClaudeMeter.Desktop.Tests/Startup/AutoStartServiceTests.cs` —
  **Nuevo**, 11 tests xUnit puro. Cubre: `IsEnabled` reflejando
  `AppConfig.AutoStartEnabled` (Theory true/false); `Initialize()` sin
  lanzar tanto si `IsEnabled` es `false` (no hace nada) como `true` (rama
  de guarda, ver Gaps); `SetEnabled`/`Toggle` actualizando `IsEnabled`,
  persistiendo en `config.json` (ruta temporal vía `ConfigPathForTests`),
  disparando `StateChanged` exactamente una vez, e idempotencia ante
  llamadas repetidas; y un test explícito de aislamiento
  (`SetEnabled_NoTocaElRegistroRealDeAutoarranqueDeLaMaquina`) que lee el
  valor real de `HKCU\...\Run\ClaudeMeter` antes/después de `SetEnabled(true)`
  y confirma que no cambia.
- `test/ClaudeMeter.Desktop.Tests/Updates/FakeUpdateManager.cs` — **Nuevo**
  test double `internal`. Subclase de `Velopack.UpdateManager` (no
  sellada; `IsInstalled`/`CheckForUpdatesAsync`/`DownloadUpdatesAsync` son
  `virtual` en la API pública del paquete, confirmado por reflexión antes
  de escribirla) que sobrescribe esos tres miembros para simular cada
  escenario sin red real. El constructor base recibe una instancia de
  `Velopack.Locators.TestVelopackLocator` (clase de test oficial que el
  propio paquete Velopack publica) pasada como argumento, nunca vía el
  setter estático global `VelopackLocator.SetCurrentLocator` (no tiene
  forma pública de revertirse dentro del proceso). Deliberadamente no
  sobrescribe `ApplyUpdatesAndRestart` (no es `virtual`) — ver Gaps.
- `test/ClaudeMeter.Desktop.Tests/Updates/AppUpdateServiceTests.cs` —
  **Nuevo**, 6 tests xUnit puro (`async Task`). Cubre: sin instalación
  Velopack real (`IsInstalled == false`) nunca comprueba actualizaciones;
  sin actualización disponible no descarga ni loguea; **AC central de
  US-3** — feed de releases inaccesible (`CheckForUpdatesAsync` lanza) y
  fallo de red durante la descarga (`DownloadUpdatesAsync` lanza) degradan
  ambos en silencio (sin excepción propagada, con `Warning` en el log);
  dos fallos consecutivos no dejan al servicio en un estado roto para la
  siguiente comprobación periódica; `Dispose()` es seguro sin haber
  llamado antes a `Start()`.
- `test/ClaudeMeter.Desktop.Tests/Configuration/AppConfigStoreTests.cs` —
  **Ampliado**, 4 tests nuevos (30 preexistentes + 4), mismo patrón ya
  usado para `ChimeEnabled`/`Theme`: `AutoStartEnabled` ausente en
  `config.json` → `false` por defecto; presente → se respeta; round-trip
  `Save`/`Load` con el valor activado y desactivado.

## Coverage Result

- Target (`testingCoverage` en `.claude/sdlc.config.yaml`): **70%**
- Comando: `dotnet test test/ClaudeMeter.Desktop.Tests/ClaudeMeter.Desktop.Tests.csproj --collect:"XPlat Code Coverage"`
  (Coverlet, ya presente en el `.csproj`, sin herramienta nueva).
- Resultado de ejecución: **206/206 correctos** en
  `ClaudeMeter.Desktop.Tests` (182 preexistentes, línea base de F3/Ciclo C,
  + 24 nuevos: 11 `AutoStartServiceTests` + 6 `AppUpdateServiceTests` + 4
  `AppConfigStoreTests` + 3 `TrayIconServiceTests`). `dotnet build
  ClaudeMeter.sln`/`test/ClaudeMeter.Desktop.Tests/...csproj` en Debug y
  Release (`TreatWarningsAsErrors` + analizadores) compilan **sin
  advertencias ni errores**, incluyendo los 5 proyectos de `src/` (F6
  completo) y los ficheros de test nuevos/modificados.
- Cobertura por clase, leída de `coverage.cobertura.xml`, para el código
  nuevo/modificado de F6:
  - `ClaudeMeter.Desktop.Configuration.AppConfig`/`AppConfigStore`:
    **line-rate 98.87%, branch-rate 95.45%** (ya muy por encima del
    umbral antes de este ciclo; el campo `AutoStartEnabled` añade la
    misma cobertura round-trip que `ChimeEnabled`/`Theme`).
  - `ClaudeMeter.Desktop.Tray.TrayIconService`: **line-rate 79.66%,
    branch-rate 100%** — el nuevo `OnAutoStartStateChanged` (1 hit) y
    `AutoStartItemForTests` (6 hits) quedan cubiertos; la única rama sin
    cubrir es `Initialize()` (0%), gap ya aceptado en ciclos anteriores
    (F3/Ciclo B), sin relación con F6.
  - `ClaudeMeter.Desktop.Updates.AppUpdateService`: Coverlet separa el
    cuerpo real del método async
    (`AppUpdateService/<CheckAndApplyAsync>d__8`, la máquina de estados
    generada por el compilador) de la clase declarada — la lógica de
    negocio real (el `try`/`if`/`catch` de `CheckAndApplyAsync`) tiene
    **line-rate 88.23%, branch-rate 100%**; las únicas líneas sin cubrir
    de ese bloque son la llamada a `ApplyUpdatesAndRestart` (deliberado,
    ver Gaps). La clase declarada en sí (`Start()`, el constructor
    público de un argumento, el inicializador estático del intervalo de
    4h) marca **line-rate 38.88%** porque agrupa además tres miembros que
    requieren una instalación Velopack real para poder invocarse sin
    lanzar/sin abrir un temporizador real de producción (ver Gaps) —
    **excluyendo esas líneas inherentemente no alcanzables en un test
    unitario aislado, el código realmente ejercitable de esta clase
    (22/24 líneas) queda al ~91.7%.**
  - `ClaudeMeter.Desktop.Startup.AutoStartService`: **line-rate 64.15%,
    branch-rate 50%** — por debajo del umbral en cifra bruta. Las 19
    líneas sin cubrir son íntegramente la rama "instalación Velopack real
    presente" (`IsCurrentSet == true`) de `ApplyToRegistry`/
    `ResolveStableExePath` (la escritura real en el registro y la
    resolución de `RootAppDir`), inalcanzable en el proceso host de
    `dotnet test` sin mutar `VelopackLocator.SetCurrentLocator` de forma
    global e irreversible (ver Gaps) — **excluyendo esas 19 líneas, el
    resto de la clase (34/34 líneas restantes) está al 100%.**
- **Conclusión:** `AppConfig`/`AppConfigStore` y `TrayIconService` superan
  el 70% en cifra bruta. `AppUpdateService` y `AutoStartService` quedan
  por debajo en la cifra bruta de Coverlet exclusivamente por líneas que,
  de forma demostrable y documentada, no son alcanzables desde un test
  unitario aislado sin (a) una instalación Velopack real o (b) mutar
  estado estático global de un paquete de terceros sin forma de
  revertirlo dentro del proceso — no por falta de tests. No se han escrito
  tests adicionales de relleno para forzar el número: la política de
  `unit-testing-strategy.md` ("no escribir tests que no fallan cuando el
  comportamiento real se rompe") desaconseja explícitamente ese camino.

## Gaps / Not Covered

- **`AutoStartService.ApplyToRegistry`/`ResolveStableExePath`, rama
  `VelopackLocator.IsCurrentSet == true` (escritura real en
  `HKCU\...\Run` + resolución de `RootAppDir`) — gap aceptado.** Ese
  `IsCurrentSet` estático solo es `true` si el proceso arrancó vía
  `Velopack.VelopackApp.Build().Run()` (ver `Program.cs`), lo que nunca
  ocurre bajo `dotnet test`. Forzarlo requeriría
  `VelopackLocator.SetCurrentLocator(...)`, un setter estático **global**
  sin contraparte pública para revertirlo dentro del mismo proceso —
  contaminaría el resto de la batería del ensamblado (incluida
  `TrayIconServiceTests`, que también construye `AutoStartService`),
  pudiendo hacer que otros tests sí escriban de verdad en el registro.
  Mismo criterio ya aceptado en este proyecto para
  `ClickThroughServiceTests` (llamada real a `user32.dll` con un `HWND`
  real). Mitigado en su lugar con un test que lee el valor real del
  registro antes/después de `SetEnabled(true)` y confirma que no cambia.
- **`AppUpdateService.Start()` — gap aceptado.** Crea un
  `System.Timers.Timer` real de 4h y dispara `CheckAndApplyAsync()`
  "fire-and-forget"; probarlo de forma determinista requeriría o bien
  esperar hasta 4h reales, o bien manipular el temporizador con
  reflexión, lo que probaría la mecánica de `System.Timers.Timer`, no el
  comportamiento real del servicio (ver `unit-testing-strategy.md`). El
  flujo que sí importa (`CheckAndApplyAsync`) se prueba directamente y de
  forma determinista vía `CheckAndApplyForTestsAsync()`.
- **`AppUpdateService(ILogger<AppUpdateService>)`, constructor público de
  un argumento — gap aceptado.** Construye internamente
  `new UpdateManager(new GithubSource(...))` sin locator explícito; sin
  una instalación Velopack real, esa llamada lanza
  `InvalidOperationException` ("No VelopackLocator has been set") de
  forma determinista y confirmada por prueba manual contra el paquete
  real antes de escribir este test — invocarlo en el proceso de test
  lanzaría siempre, así que no se ha escrito un test que solo confirmaría
  la propia limitación del entorno de test, no un comportamiento útil.
- **`AppUpdateService.CheckAndApplyAsync`, línea
  `_updateManager.ApplyUpdatesAndRestart(toApply: null)` — gap aceptado,
  deliberado por seguridad.** No es un miembro `virtual` de
  `UpdateManager` (confirmado por reflexión), así que `FakeUpdateManager`
  no puede sobrescribirlo; invocar la implementación real reiniciaría de
  verdad el proceso de test (o lanzaría de forma no determinista al no
  encontrar paquetes reales que aplicar). Los tests cubren en su lugar
  hasta el paso anterior (`DownloadUpdatesAsync`) y confirman que un
  fallo ahí nunca llega a esa línea.
- **`TrayIconService.Initialize()`/`ExitItemForTests.PerformClick()` —
  gap preexistente, no de F6.** Documentado ya en el XMLDoc de
  `TrayIconServiceTests` desde F3/Ciclo B (tocan `NotifyIcon.Visible`
  real / dispararían `Application.Current.Shutdown()` real).
- No se ha escrito ningún test de integración (Testcontainers/DB/HTTP):
  no aplica — F6 es "de build/CI/empaquetado" según el propio documento
  de requisitos, sin tocar Domain/Application; toda la superficie testeada
  es Desktop puro (xUnit), consistente con la política de testing de
  `CLAUDE.md`.

## Acceptance Criteria Coverage

| Acceptance Criterion | Covered by |
|---|---|
| US-1 (versionado): las 5 ACs (versión calculada en el ensamblado, incremento determinista por altura de commits, sufijo "dirty" en build local, versión del instalador coincidente, build falla con `version.json` ausente/inválido) | Trabajo de build/CI (`nbgv`), no de código Desktop unitariamente testeable — verificación mecánica ya realizada por Development (`nbgv get-version` ejecutado con éxito, ver `docs/sdlc/development/f6-distribucion.md`, How to Verify #2). Manual validation — see Definition of Done |
| US-2 (empaquetado): las 5 ACs (instalador generado en tag, instalación limpia en máquina Windows real, publicado como asset de la release, no se dispara fuera de tags, no se publica un instalador roto) | Trabajo de CI/CD (`.github/workflows/release.yml`), fuera del alcance de xUnit. Manual validation — see Definition of Done |
| US-3 GIVEN instalado vía US-2 WHEN activa "iniciar con Windows" THEN queda registrado para autoarranque | Parcial: `AutoStartServiceTests.SetEnabled_True_ActualizaIsEnabled`/`SetEnabled_True_PersisteElValorEnConfigJson` cubren el toggle y la persistencia; la escritura real en el registro (`ApplyToRegistry`, rama `IsCurrentSet == true`) requiere una instalación Velopack real — ver Gaps. Resto: Manual validation — see Definition of Done |
| US-3 GIVEN autoarranque activado WHEN reinicia/inicia sesión THEN abre solo | Manual validation — see Definition of Done (requiere reinicio real de Windows) |
| US-3 GIVEN autoarranque activado previamente WHEN se desactiva THEN deja de arrancar | Parcial: `AutoStartServiceTests.SetEnabled_False_PersisteElValorEnConfigJson`/`Toggle_LlamadoDosVeces_VuelveAlEstadoOriginal` cubren el toggle/persistencia del `false`; el efecto observable (no arrancar en el siguiente inicio de sesión) requiere un reinicio real. Manual validation — see Definition of Done |
| US-3 GIVEN app en ejecución y nueva versión publicada WHEN comprueba actualizaciones THEN detecta, descarga y aplica sin intervención manual | `AppUpdateServiceTests.CheckAndApply_SinActualizacionDisponible_...` (rama "sin actualización") y `...ConActualizacionDisponibleYFalloDeRedEnLaDescarga_...` (llega hasta `DownloadUpdatesAsync`) cubren el flujo hasta donde es seguro simular; la aplicación real (`ApplyUpdatesAndRestart`, no `virtual`) no es invocable en un test unitario sin arriesgar el proceso — ver Gaps. Manual validation — see Definition of Done |
| US-3 GIVEN comprobación sin conexión/feed inaccesible WHEN se ejecuta THEN sigue funcionando sin bloquear/error intrusivo | `AppUpdateServiceTests.CheckAndApply_ConFeedDeReleasesInaccesible_DegradaEnSilencioSinLanzar` + `...ConActualizacionDisponibleYFalloDeRedEnLaDescarga_DegradaEnSilencioSinLanzar` + `...LlamadoVariasVecesTrasFallos_SigueSiendoDeterministaYNoLanza` — **cubierta por completo** |
| US-3 GIVEN actualización aplicada WHEN se reinicia tras aplicarla THEN conserva `config.json` y el estado de autoarranque | Manual validation — see Definition of Done (requiere un ciclo real de aplicación + reinicio) |

## Definition of Done

(Copiada literalmente de `docs/sdlc/requirements/f6-distribucion.md` /
`docs/sdlc/design/f6-distribucion.md`, ambas idénticas en este punto.)

- [ ] Todas las Acceptance Criteria de US-1, US-2 y US-3 anteriores se
      cumplen — **no marcable como cumplida por esta fase**: de las 16 ACs
      del milestone, 3 quedan cubiertas por completo por tests automáticos
      (degradación silenciosa de `AppUpdateService`) y 3 parcialmente
      (toggle/persistencia de `AutoStartService`); las 10 restantes son
      de build/CI/empaquetado o requieren un entorno Windows real con un
      ciclo de release real — ver tabla de trazabilidad de arriba, ninguna
      marcada por esta fase sin evidencia real.
- [ ] **Validación manual (US-2):** instalación limpia del instalador
      generado por CI en una máquina Windows real sin Visual Studio ni
      SDK de .NET — **PENDIENTE**, ningún agente de este pipeline dispone
      de una máquina Windows limpia real para ejecutar esta comprobación.
- [ ] **Validación manual (US-2):** confirmar que el instalador queda
      publicado como asset en una release real de GitHub tras empujar un
      tag real — **PENDIENTE**, requiere credenciales/permiso de
      publicación de releases que ningún agente de este pipeline tiene.
- [ ] **Validación manual (US-3):** confirmar el autoarranque real tras
      cerrar sesión/reiniciar Windows, y la aplicación real de una
      actualización entre dos versiones publicadas consecutivas —
      **PENDIENTE**, requiere un ciclo real de publicación de al menos
      dos releases y una máquina Windows real, fuera del alcance de
      cualquier agente automatizado de este pipeline. Mitigado
      parcialmente por `AutoStartServiceTests`/`AppUpdateServiceTests`
      (la lógica de estado/persistencia/degradación, verificada de forma
      determinista), que no sustituyen esta validación.
- [ ] **Validación manual añadida por el documento de diseño (US-2):**
      confirmar en una máquina Windows real sin el WebView2 Runtime
      preinstalado que el bootstrapper embebido lo instala correctamente
      — **PENDIENTE**, mismo motivo: requiere una máquina Windows real
      fuera del alcance de este pipeline.

---

Nada se ha confirmado (`commit`) ni enviado (`push`) al repositorio: los
ficheros de test nuevos
(`test/ClaudeMeter.Desktop.Tests/Startup/AutoStartServiceTests.cs`,
`test/ClaudeMeter.Desktop.Tests/Updates/FakeUpdateManager.cs`,
`test/ClaudeMeter.Desktop.Tests/Updates/AppUpdateServiceTests.cs`), el
fichero de test arreglado/ampliado
(`test/ClaudeMeter.Desktop.Tests/Tray/TrayIconServiceTests.cs`) y el
ampliado (`test/ClaudeMeter.Desktop.Tests/Configuration/AppConfigStoreTests.cs`)
quedan en el árbol de trabajo, listos para revisión, junto con los dos
cambios aditivos y explícitamente señalados en código de producción
(`AutoStartService.ConfigPathForTests`, el constructor `internal` +
`CheckAndApplyForTestsAsync()` de `AppUpdateService`) y el resto de
cambios de F6 ya presentes sin confirmar (dejados por `sdlc-development`).
Ningún fichero fuera de `test/` se ha tocado salvo esos dos seams de
testabilidad, ambos señalados arriba.
