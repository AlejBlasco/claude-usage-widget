# Implementation Summary: F2 — Robustez, Ciclo B

## Design Reference

`docs/sdlc/design/f2-robustez-ciclo-b.md` (Requirements:
`docs/sdlc/requirements/f2-robustez-ciclo-b.md`). Cubre US-1 (`config.json`:
intervalo, posición inicial, chime) y US-2 (arrastrar el widget y persistir
la nueva posición), ambas dentro de la issue única **#13**. Se ha seguido el
Implementation Plan del documento de diseño paso a paso para todo el código
de producción (pasos 1, 3, 5, 7, 8, 9, 10, 12, 14, 15, 16, 18); los pasos de
test (2, 4, 6, 11, 13, 17) quedan fuera de esta fase — ver Deviations y
Follow-ups.

## Files Changed

- `src/ClaudeMeter.Domain/Usage/ThresholdTransition.cs` (nuevo) —
  `ThresholdTransition.EnteredCritical(UsageThreshold? previous, UsageThreshold? current)`,
  función pura, sin dependencias, tal cual el Data Model del diseño.
- `src/ClaudeMeter.Desktop/Configuration/AppConfig.cs` (nuevo) — records
  `WindowPosition(double Left, double Top)` y
  `AppConfig(TimeSpan PollingInterval, WindowPosition? Position, bool ChimeEnabled)`
  con `AppConfig.Default` (60s / sin posición / chime desactivado).
- `src/ClaudeMeter.Desktop/Configuration/AppConfigStore.cs` (nuevo) — lector/
  escritor único de `config.json`. `Load()` nunca lanza (JSON corrupto →
  `AppConfig.Default` completo; campo concreto fuera de rango → solo ese
  campo cae a su valor por defecto con `Warning`); `Save()`/`SavePosition()`
  escriben de forma atómica (`.tmp` + `File.Move(overwrite: true)`) y nunca
  lanzan (`Error` en fallo de E/S). Ruta por defecto
  `%LOCALAPPDATA%\ClaudeMeter\config.json`.
- `src/ClaudeMeter.Desktop/Windowing/WindowPositionResolver.cs` (nuevo) —
  función pura `Resolve(...)` que decide `(Left, Top)` a partir de la
  posición configurada y las áreas de trabajo de las pantallas conectadas,
  con fallback a la esquina inferior derecha del monitor principal.
- `src/ClaudeMeter.Desktop/Windowing/Win32ScreenInfo.cs` (nuevo) — adaptador
  `internal` sobre `EnumDisplayMonitors`/`GetMonitorInfo` de `user32.dll`
  (P/Invoke directo, sin `UseWindowsForms`). Corregido, tal como pedía la
  nota de implementación del diseño, para pasar el `hMonitor` real (primer
  parámetro del delegado) a `GetMonitorInfo`, no el `RECT`.
- `src/ClaudeMeter.Desktop/Windowing/WindowDragService.cs` (nuevo) —
  singleton sin `Window` en el constructor; `AttachWindow(Window)` se llama
  desde `MainWindow`; `[JSInvokable] BeginDrag()` comprueba el hilo de UI
  (`Dispatcher.CheckAccess`/`Invoke`), llama a `Window.DragMove()`
  (bloqueante), atrapa la `InvalidOperationException` benigna de "botón ya
  no pulsado", y persiste `Left`/`Top` vía `AppConfigStore.SavePosition`.
- `src/ClaudeMeter.Desktop/Audio/IChimePlayer.cs` /
  `SystemSoundChimePlayer.cs` (nuevos) — abstracción de una línea sobre
  `System.Media.SystemSounds.Exclamation.Play()`.
- `src/ClaudeMeter.Desktop/wwwroot/js/drag.js` (nuevo) —
  `window.claudeMeterDrag.init(dotNetDragService)` registra un listener
  `mousedown` a nivel de `document` y reenvía a `BeginDrag` vía
  `invokeMethodAsync`.
- `src/ClaudeMeter.Desktop/wwwroot/index.html` — añadido
  `<script src="js/drag.js"></script>` tras `blazor.webview.js`.
- `src/ClaudeMeter.Desktop/App.xaml.cs` — composition root: registra
  `AppConfigStore` (singleton), `AppConfig` (singleton, resuelto
  perezosamente vía `AppConfigStore.Load()` la primera vez que se pide del
  contenedor), `IChimePlayer`→`SystemSoundChimePlayer` y `WindowDragService`
  (singleton). Ningún cambio en el registro de `IUsageDataSource`/Serilog ya
  hecho en Ciclo A.
- `src/ClaudeMeter.Desktop/MainWindow.xaml.cs` — sustituye el cálculo fijo
  de esquina inferior derecha por
  `WindowPositionResolver.Resolve(config.Position, Width, Height, ScreenMargin, SystemParameters.WorkArea, Win32ScreenInfo.GetAllWorkAreas())`;
  resuelve `WindowDragService` del contenedor y le llama
  `AttachWindow(this)`.
- `src/ClaudeMeter.Desktop/Pages/UsagePage.razor` — tres cambios de US-1/
  US-2: (a) `@inject AppConfig Config` y el intervalo del
  `UsagePollingCoordinator` pasa de `RefreshInterval` (constante hardcodeada
  eliminada) a `Config.PollingInterval`; (b) `@inject WindowDragService DragService` /
  `@inject IJSRuntime JS`, nuevo `OnAfterRenderAsync(firstRender)` que
  registra `claudeMeterDrag.init` con un `DotNetObjectReference<WindowDragService>`
  liberado en `Dispose()`; (c) `@inject IChimePlayer ChimePlayer`, nuevos
  campos `_previousSessionThreshold`/`_previousWeeklyThreshold`, y en la
  rama `snapshot.IsSuccess` de `Apply()` se clasifica el umbral de
  sesión/semana y, si `Config.ChimeEnabled` y
  `ThresholdTransition.EnteredCritical` es cierto para cualquiera de las
  dos, se llama a `ChimePlayer.Play()` — evaluado únicamente sobre
  snapshots con éxito, para que un fallo transitorio no resetee la
  detección de transición (tal cual Technology Choices del diseño).
- `src/ClaudeMeter.Desktop/_Imports.razor` — añadidos
  `@using Microsoft.JSInterop`, `@using ClaudeMeter.Desktop.Audio`,
  `@using ClaudeMeter.Desktop.Configuration` y
  `@using ClaudeMeter.Desktop.Windowing` (necesarios para los `@inject`
  nuevos de `UsagePage.razor` sin nombres totalmente cualificados).

Ningún `PackageReference` nuevo en ningún `.csproj` — confirmado en el paso
18 del Implementation Plan (`System.Text.Json`, `Microsoft.JSInterop` y
`System.Media` ya llegan transitivamente vía los frameworks/paquetes
compartidos ya referenciados desde F1/Ciclo A).

## Deviations from the Design

1. **Ningún test creado ni modificado** (pasos 2, 4, 6, 11, 13, 17 del
   Implementation Plan). Regla dura de este agente: nunca crear, modificar
   ni ejecutar ficheros de test — responsabilidad exclusiva de
   `sdlc-testing`, igual que ya se documentó en el resumen de Ciclo A. A
   diferencia de Ciclo A, aquí **no se rompe la compilación de ningún test
   ya existente**: `UsagePollingCoordinator` conserva su firma de
   constructor sin cambios (solo cambia qué `TimeSpan` le pasa
   `UsagePage.razor`), así que `UsagePollingCoordinatorTests.cs` sigue
   compilando tal cual. Sin embargo, `test/ClaudeMeter.Desktop.Tests/Pages/UsagePageTests.cs`
   (ya existente, de Ciclo A) **compila** pero **fallará en tiempo de
   ejecución** con todos sus `[Fact]`/`[Theory]` que montan `UsagePage`: el
   `Services` de bUnit de ese fichero solo registra `IUsageDataSource`, y
   `UsagePage` ahora exige además `AppConfig`, `IChimePlayer`,
   `WindowDragService` e `IJSRuntime` vía `@inject` (`GetRequiredService`
   lanzará `InvalidOperationException` al montar el componente). Esto es
   exactamente lo que el propio Implementation Plan anticipa en sus pasos 11
   y 17 (registrar `AppConfig`/un `FakeChimePlayer`/`WindowDragService`, y
   configurar el modo `Loose` del JSInterop de bUnit) — se deja documentado
   como Follow-up para `sdlc-testing`, verificado con `dotnet build
   ClaudeMeter.sln` (compila sin errores en Debug y Release; el fallo sería
   solo en tiempo de ejecución de los tests, que esta fase no ejecuta).
2. **`Win32ScreenInfo.cs`: struct `Rect_` renombrada a `RectNative`.** El
   propio documento de diseño marca `Rect_`/`lprcMonitor.ToHandleUnused()`
   como pseudocódigo deliberadamente inválido, con una nota de
   implementación explícita indicando que Development debe corregir la
   firma (pasar el `hMonitor` real del delegado a `GetMonitorInfo`, ya
   aplicado). De paso, se renombró `Rect_` a `RectNative` — mismo tipo y
   layout, solo un nombre más descriptivo que evita el sufijo `_` (no es una
   decisión de arquitectura, es limpieza menor sobre un fragmento que el
   propio diseño marcó como no definitivo).
3. **`ChimePlayedForTests` (mencionado en el excerpt de `UsagePage.razor`
   del diseño) no se implementó.** El excerpt del diseño declara
   `internal bool ChimePlayedForTests { get; private set; }` pero no lo
   asigna en ningún punto de `Apply()` dentro del propio excerpt, y el
   Implementation Plan (paso 11) describe el mecanismo de test real como un
   `FakeChimePlayer : IChimePlayer` con contador `PlayCount` inyectado en el
   contenedor bUnit — ese doble ya cubre por completo la verificación
   ("¿sonó el chime?") sin necesitar ningún flag adicional en la página. Se
   omite por parecer un artefacto no utilizado del propio excerpt, no una
   pieza de datos que otra parte del diseño consuma; si `sdlc-testing`
   necesitara esa propiedad de todos modos, es una adición trivial de una
   línea sobre `UsagePage.razor`.

Ninguna otra desviación: el resto del código (`ThresholdTransition`,
`AppConfig`/`AppConfigStore`, `WindowPositionResolver`, `WindowDragService`,
`IChimePlayer`/`SystemSoundChimePlayer`, `drag.js`, `App.xaml.cs`,
`MainWindow.xaml.cs`, `UsagePage.razor`) se implementó tal cual la sección
Data Model / Implementation Plan del documento de diseño, incluyendo rutas
(`%LOCALAPPDATA%\ClaudeMeter\config.json`), niveles de log (`Warning` para
JSON corrupto/campo fuera de rango/`BeginDrag` sin ventana adjunta, `Error`
para fallo de escritura tras un arrastre) y el esquema de `config.json`
(3 campos opcionales de forma independiente).

## How to Verify

1. **Build (verificado en esta fase):**
   `dotnet build ClaudeMeter.sln -c Debug` y `-c Release` — ambos compilan
   sin advertencias ni errores (Release incluye `TreatWarningsAsErrors`).
   Confirmado incluyendo los 4 proyectos de producción y los 5 de test
   (estos últimos compilan; su comportamiento en tiempo de ejecución no se
   ha verificado en esta fase, ver Deviations §1).
2. **Validación manual (US-1, no automatizable — requiere Windows real):**
   - Crear `%LOCALAPPDATA%\ClaudeMeter\config.json` con
     `{"pollingIntervalSeconds": 10, "chimeEnabled": true, "windowPosition": {"left": 100.0, "top": 100.0}}`,
     lanzar `ClaudeMeter.Desktop` y confirmar que el widget aparece en
     `(100, 100)` y refresca cada ~10s (en vez de 60s).
   - Editar el fichero con un JSON roto (`{"pollingIntervalSeconds":`) y
     reiniciar: el widget debe arrancar igualmente en la esquina inferior
     derecha por defecto, sin crashear; revisar
     `%LOCALAPPDATA%\ClaudeMeter\logs\claudemeter-*.log` para el `Warning`
     correspondiente.
   - Simular (o esperar) que sesión/semana crucen el 90% con
     `chimeEnabled: true`: debe sonar el chime del sistema exactamente una
     vez al cruzar, no en cada ciclo posterior mientras siga en rojo.
   - Con `windowPosition` apuntando fuera de cualquier monitor conectado
     (p. ej. `{"left": 5000, "top": 5000}` en un equipo de un solo monitor),
     confirmar que el widget cae a la esquina inferior derecha del monitor
     principal en vez de quedar inaccesible.
3. **Validación manual (US-2, no automatizable — requiere ratón real en
   Windows):**
   - Arrastrar el widget desde cualquier punto de su contenido (clic
     izquierdo + mover) y soltar: la ventana debe seguir el puntero de
     forma fluida (comportamiento estándar de `DragMove()`), sin que
     WebView2 "robe" el gesto.
   - Tras soltar, comprobar (`type %LOCALAPPDATA%\ClaudeMeter\config.json`)
     que `windowPosition` refleja las nuevas coordenadas.
   - Cerrar y reabrir la aplicación: el widget debe reaparecer exactamente
     donde se soltó el último arrastre.
   - Repetir dos arrastres consecutivos rápidos: el fichero final debe
     reflejar siempre la posición del último arrastre completado, nunca una
     escritura a medias (abrir el `.json` con un editor de texto para
     confirmar que es JSON válido).

## Follow-ups / Known Limitations

- `test/ClaudeMeter.Desktop.Tests/Pages/UsagePageTests.cs` compila pero
  fallará en tiempo de ejecución hasta que `sdlc-testing` registre
  `AppConfig`, un `FakeChimePlayer`, `WindowDragService` (con un
  `AppConfigStore` apuntando a un fichero temporal) e `IJSRuntime` (modo
  `Loose` de bUnit, o la invocación explícita de `claudeMeterDrag.init`) en
  el contenedor de cada test — ver Deviations §1 e Implementation Plan
  (pasos 11/17 del diseño).
- Ningún test nuevo para `ThresholdTransition`, `AppConfigStore`,
  `WindowPositionResolver` ni `WindowDragService` (pasos 2, 4, 6, 13 del
  Implementation Plan) — pendiente de `sdlc-testing`.
- Las 2 validaciones manuales de la Definition of Done (US-1/US-2) siguen
  pendientes de un equipo Windows real — no automatizables por este
  pipeline, ya marcadas como tales en Requirements/Design.
- Limitación de plataforma conocida y ya aceptada por el usuario en el
  documento de diseño (no requiere trabajo adicional): el escalado DPI en
  configuraciones multi-monitor mixtas puede hacer impreciso el chequeo
  "¿la posición configurada cabe en este monitor?" cerca de los bordes
  entre monitores — nunca deja la ventana inaccesible, cae al fallback del
  monitor principal.
- `Win32ScreenInfo`/`MainWindow.xaml.cs` quedan, por diseño, fuera de la
  cobertura automatizada de este ciclo (no hay monitor real en CI) — la
  lógica de decisión testeable vive por completo en
  `WindowPositionResolver`.
- La interacción entre el mecanismo de arrastre (listener JS a nivel de
  `document`) y el futuro click-through de F3 (#16) queda documentada como
  riesgo a revisar en el diseño de F3, no en este ciclo.
