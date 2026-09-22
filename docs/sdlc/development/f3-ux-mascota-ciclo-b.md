# Implementation Summary: F3 — UX + página Mascota, Ciclo B

## Design Reference

`docs/sdlc/design/f3-ux-mascota-ciclo-b.md` (US-1 click-through, issue #16;
US-2 icono de bandeja + cierre directo, issue #17). Se ha seguido su
Implementation Plan paso a paso (pasos 1, 2, 4, 6, 8, 10, 12, 13, 14, 15, 17,
18 y 19; los pasos 3, 5, 7, 9, 11 y 16 son de tests xUnit/bUnit y quedan
fuera de esta fase — ver Deviations).

## Files Changed

- `src/ClaudeMeter.Desktop/ClaudeMeter.Desktop.csproj` — añade
  `<UseWindowsForms>true</UseWindowsForms>` (sin `PackageReference` nuevo:
  `System.Windows.Forms`/`System.Drawing.Common` llegan con el framework
  compartido ya referenciado por `UseWPF=true`).
- `src/ClaudeMeter.Desktop/Windowing/ClickThroughService.cs` (nuevo) — US-1.
  Alterna `WS_EX_TRANSPARENT` sobre `GWL_EXSTYLE` del HWND real de
  `MainWindow` vía P/Invoke a `user32.dll`, con *shim* de 32/64 bits
  (`GetWindowLongPtr`/`SetWindowLongPtr` en 64 bits, `GetWindowLong`/
  `SetWindowLong` en 32 bits). `IsEnabled = false` por defecto, no toca la
  ventana hasta el primer `SetEnabled(true)`; expone `Toggle()` y el evento
  `StateChanged` para que `TrayIconService` sincronice el `Checked` del
  ítem de menú. `SetEnabledForTests` interno para xUnit sin HWND real.
- `src/ClaudeMeter.Desktop/Windowing/WindowCloseService.cs` (nuevo) — US-2
  (cierre directo). Expone `[JSInvokable] RequestClose()`, invocado desde
  `close.js`; llama a `System.Windows.Application.Current.Shutdown()`, el
  mismo punto de salida que "Salir" desde la bandeja.
- `src/ClaudeMeter.Desktop/Polling/UsagePollingCoordinator.cs` — añade
  `Pause()` (detiene el timer sin `Dispose()`) y `PollNow()` (dispara un
  ciclo de poll fuera de la cadencia del timer, reutilizando el guard
  `_isPolling` existente); `PollOnceForTestsAsync()` ahora delega en
  `PollNow()` en vez de duplicar la llamada a `PollAsync()`.
- `src/ClaudeMeter.Desktop/Polling/PollingControlService.cs` (nuevo) —
  puente de DI singleton entre el icono de bandeja (fuera de Blazor) y la
  instancia real de `UsagePollingCoordinator` que posee cada `UsagePage`
  (`AttachCoordinator`/`DetachCoordinator`, mismo verbo que `AttachWindow`).
  Expone `TogglePause()`/`Pause()`/`Resume()`/`RequestReload()`, `IsPaused`
  y el evento `PauseStateChanged`.
- `src/ClaudeMeter.Desktop/Tray/TrayIconService.cs` (nuevo) — única clase
  del proyecto que referencia `System.Windows.Forms`. Construye
  `NotifyIcon`+`ContextMenuStrip` (Pausar/Reanudar, Recargar,
  Click-through, Salir) en el constructor sin tocar `Visible` (instanciable
  en tests); `Initialize()` (llamado desde `App.OnStartup`) resuelve el
  icono vía `Icon.ExtractAssociatedIcon(Environment.ProcessPath!)` con
  `SystemIcons.Application` como *fallback*, y activa `Visible = true`.
  `Dispose()` oculta y libera el `NotifyIcon`.
- `src/ClaudeMeter.Desktop/Pages/UsagePage.razor` — inyecta
  `PollingControlService`/`WindowCloseService`; `OnInitialized()` adjunta
  su coordinador recién creado a `PollingControl` y se suscribe a
  `PauseStateChanged`; `OnAfterRenderAsync` registra
  `claudeMeterClose.init`; nuevo `<button class="claudemeter-close">`
  siempre presente (fuera del `@if`/`else`, también sobre `ReauthNotice`);
  `OnPauseStateChanged` marca `_isStale = true` al pausar (reutiliza el
  criterio visual existente, sin forzarlo a `false` al reanudar); `Dispose()`
  desuscribe `PauseStateChanged`, llama a `DetachCoordinator` y libera
  `_closeServiceRef`.
- `src/ClaudeMeter.Desktop/wwwroot/js/close.js` (nuevo) — registra el
  listener de clic sobre `.claudemeter-close` y reenvía el gesto a
  `WindowCloseService.RequestClose()`; hace `stopPropagation()` en su
  propio `pointerdown` para no colisionar con el listener de arrastre de
  `drag.js` (que escucha en `document.documentElement`).
- `src/ClaudeMeter.Desktop/wwwroot/index.html` — añade
  `<script src="js/close.js"></script>`.
- `src/ClaudeMeter.Desktop/wwwroot/css/app.css` — añade
  `position: relative` a `.claudemeter-root` y las reglas de
  `.claudemeter-close` (botón discreto, visible solo en hover/foco).
- `src/ClaudeMeter.Desktop/MainWindow.xaml.cs` — adjunta
  `ClickThroughService`/`WindowCloseService` a `this`, mismo patrón ya
  usado para `WindowDragService`/`WindowResizeService`.
- `src/ClaudeMeter.Desktop/App.xaml.cs` — registra los 4 servicios nuevos
  como singletons (`ClickThroughService`, `WindowCloseService`,
  `PollingControlService`, `TrayIconService`); llama a
  `TrayIconService.Initialize()` al final de `OnStartup` y a
  `TrayIconService.Dispose()` al principio de `OnExit`, antes de
  `_httpClient?.Dispose()`/`Log.CloseAndFlush()`.

## Deviations from the Design

- Ninguna respecto al Data Model/Implementation Plan del documento de
  diseño en los ficheros de producción — se han creado/modificado tal
  cual se especificaba.
- Único ajuste no previsto explícitamente por el Data Model: se añadió
  `using System.IO;` en `TrayIconService.cs` (el `catch (... or
  IOException)` de `Initialize()` no compilaba porque este proyecto solo
  trae `System.Drawing`/`System.Windows.Forms` como *global usings*
  implícitos vía `UseWindowsForms=true`, no `System.IO`). Cambio de una
  línea, sin impacto en el comportamiento diseñado.
- Los pasos 3, 5, 7, 9, 11 y 16 del Implementation Plan (tests xUnit de
  `ClickThroughService`/`WindowCloseService`/`PollingControlService`/
  `UsagePollingCoordinator`/`TrayIconService`, y tests bUnit de
  `UsagePage`) quedan **fuera de esta fase** por restricción explícita de
  este agente (Development nunca escribe ni ejecuta tests) — es
  responsabilidad de `sdlc-testing`.

## How to Verify

1. `dotnet build ClaudeMeter.sln -c Debug` y
   `dotnet build ClaudeMeter.sln -c Release` — ambos compilan sin
   advertencias ni errores (verificado en esta sesión; Release incluye
   analizadores + `TreatWarningsAsErrors`).
2. Ejecutar `ClaudeMeter.Desktop.exe` en Windows real y confirmar
   manualmente (Definition of Done del documento de diseño, fuera del
   alcance de este agente):
   - Al completar el arranque aparece el icono en la bandeja, con menú
     Pausar/Recargar/Click-through/Salir.
   - "Pausar" detiene el polling y marca el widget como
     "(desactualizado)"; "Reanudar" (mismo ítem, texto cambiado) lo
     reactiva; "Recargar" dispara un fetch inmediato sin tocar el estado
     de pausa.
   - Activar "Click-through" deja pasar los clics a la ventana de debajo
     mientras el widget sigue renderizándose/actualizándose;
     desactivarlo restaura la interacción normal.
   - El botón de cierre (visible solo en hover, esquina superior derecha)
     y "Salir" desde la bandeja terminan el proceso (verificable en el
     Administrador de tareas) sin dejar ningún icono de bandeja residual.

## Follow-ups / Known Limitations

- Cobertura de tests automatizados (xUnit para los 4 servicios nuevos y
  para `UsagePollingCoordinator.Pause`/`PollNow`, bUnit para el reflejo de
  `_isStale` al pausar y la presencia del botón de cierre) pendiente de
  `sdlc-testing`, según el Implementation Plan del documento de diseño.
- Gaps de cobertura ya aceptados explícitamente por el propio documento de
  diseño (Risks & Open Decisions): la llamada real a `user32.dll` con un
  HWND real, la llamada real a `Application.Current.Shutdown()`, y
  `NotifyIcon.Visible = true` real quedan fuera de xUnit/bUnit — cubiertos
  solo por la validación manual descrita arriba.
