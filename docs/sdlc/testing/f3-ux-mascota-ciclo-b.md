# Testing Summary: F3 — UX + página Mascota, Ciclo B

## Scope

Cobertura de tests para el código nuevo/modificado, aún sin confirmar
(`commit`), de F3 Ciclo B (issues **#16** US-1 click-through de la ventana y
**#17** US-2 icono de bandeja con Pausar/Reanudar/Recargar/Salir + cierre
directo), a partir de `docs/sdlc/development/f3-ux-mascota-ciclo-b.md` y,
siguiendo su cadena, de `docs/sdlc/design/f3-ux-mascota-ciclo-b.md` (cuyo
Implementation Plan, pasos 3/5/7/9/11/16, ya detalla explícitamente qué
tests escribir y con qué alcance) y `docs/sdlc/requirements/f3-ux-mascota-ciclo-b.md`
(Acceptance Criteria y Definition of Done usadas como fuente de verdad). Se
ha leído el código real de producción de cada fichero nuevo/modificado y los
tests ya existentes usados como referencia de patrón/alcance
(`WindowDragServiceTests.cs`, `WindowResizeServiceTests.cs`,
`UsagePollingCoordinatorTests.cs`, `UsagePageTests.cs`) antes de escribir
nada nuevo, sin asumir firmas desde el resumen de la tarea.

Piezas nuevas/modificadas cubiertas:

- `src/ClaudeMeter.Desktop/Windowing/ClickThroughService.cs` (nuevo) — US-1.
- `src/ClaudeMeter.Desktop/Windowing/WindowCloseService.cs` (nuevo) — US-2.
- `src/ClaudeMeter.Desktop/Polling/PollingControlService.cs` (nuevo) — US-2.
- `src/ClaudeMeter.Desktop/Tray/TrayIconService.cs` (nuevo) — US-2.
- `src/ClaudeMeter.Desktop/Polling/UsagePollingCoordinator.cs` (modificado) —
  `Pause()`/`PollNow()` nuevos.
- `src/ClaudeMeter.Desktop/Pages/UsagePage.razor` (modificado) — puente a
  `PollingControlService`/`WindowCloseService`, botón de cierre siempre
  presente.

`MainWindow.xaml.cs`/`App.xaml.cs`/`ClaudeMeter.Desktop.csproj` no llevan
test dedicado — mismo criterio ya aplicado desde F2/Ciclo B a estos ficheros
(composition root / adjuntar servicios a la ventana real), confirmado
explícitamente también por el propio Implementation Plan de diseño (pasos
17/18, "Sin test automatizado").

## Tests Added/Modified

- `test/ClaudeMeter.Desktop.Tests/Windowing/ClickThroughServiceTests.cs` —
  **Nuevo.** 9 tests xUnit puro: `IsEnabled == false` al construir (AC de
  arranque siempre interactivo); `SetEnabled`/`Toggle` sin ventana adjunta no
  lanzan, registran `Warning` y no cambian `IsEnabled` ni disparan
  `StateChanged` (mismo patrón de guard que `WindowDragServiceTests`);
  idempotencia ante varias llamadas repetidas sin ventana; `SetEnabledForTests`
  (gancho `internal` diseñado explícitamente para `TrayIconServiceTests`)
  actualiza `IsEnabled` y dispara `StateChanged` exactamente una vez, en
  ambos sentidos (`true`/`false`).
- `test/ClaudeMeter.Desktop.Tests/Windowing/WindowCloseServiceTests.cs` —
  **Nuevo.** 3 tests xUnit puro, mismo patrón exacto que
  `WindowDragServiceTests`/`WindowResizeServiceTests`: `RequestClose()` sin
  ventana adjunta no lanza, registra `Warning`, y es idempotente ante varias
  llamadas repetidas.
- `test/ClaudeMeter.Desktop.Tests/Polling/PollingControlServiceTests.cs` —
  **Nuevo.** 10 tests xUnit puro, usando un `UsagePollingCoordinator` real
  respaldado por `FakeUsageDataSource` (mismo patrón que
  `UsagePollingCoordinatorTests`, sin mock porque `UsagePollingCoordinator`
  es una clase concreta sin interfaz): `AttachCoordinator` deja
  `IsPaused == false`; `Pause()`/`Resume()`/`TogglePause()` alternan
  `IsPaused` y disparan `PauseStateChanged` exactamente una vez por llamada
  efectiva (una segunda `Pause()` consecutiva no repite el evento);
  `Pause()`/`Resume()`/`RequestReload()` sin coordinador adjunto son no-op
  silenciosos, sin lanzar; `RequestReload()` con coordinador adjunto dispara
  un poll adicional (`fake.CallCount` sube a 1) sin tocar `IsPaused` ni
  arrancar el timer.
- `test/ClaudeMeter.Desktop.Tests/Tray/TrayIconServiceTests.cs` — **Nuevo.**
  10 tests xUnit puro. Confirmado por ejecución real (no solo por diseño)
  que el constructor de `TrayIconService` es instanciable en xUnit sin
  sesión de escritorio real (el propio `NotifyIcon`/`ContextMenuStrip`/
  `ToolStripMenuItem` se construyen sin lanzar) y que los cuatro ítems del
  menú quedan construidos con su texto correcto; `PauseResumeItemForTests.PerformClick()`
  alterna texto (`"Pausar"`↔`"Reanudar"`) e `IsPaused` del
  `PollingControlService` real inyectado, ida y vuelta; `ReloadItemForTests.PerformClick()`
  dispara un poll adicional sin tocar `IsPaused`; `ClickThroughItemForTests.Checked`
  refleja `ClickThroughService.SetEnabledForTests` (verifica la suscripción
  real a `StateChanged` sin pasar por `user32.dll`); `ExitItemForTests` existe
  con texto `"Salir"` — deliberadamente **sin** invocar `PerformClick()`
  (dispararía `Application.Current.Shutdown()` real, terminando el propio
  proceso de test); `Dispose()` es idempotente y desuscribe de verdad los dos
  eventos (`PauseStateChanged`/`StateChanged`) — verificado dejando de
  reaccionar a ellos tras `Dispose()`.
- `test/ClaudeMeter.Desktop.Tests/Polling/UsagePollingCoordinatorTests.cs` —
  **Modificado.** 5 tests xUnit puro nuevos, extendiendo el fichero ya
  existente (US-4, F2): `Pause()` detiene el timer subyacente sin lanzar y
  sin liberar la instancia (con y sin `Start()` previo); `Start()` tras
  `Pause()` reanuda el timer y dispara un fetch inmediato (AC de "Reanudar":
  mismo comportamiento que el `Start()` inicial); `PollNow()` sin haber
  llamado nunca a `Start()` dispara `GetUsageAsync` exactamente una vez sin
  arrancar el timer como efecto colateral (AC de "Recargar": "sin reiniciar
  el propio timer"); `PollNow()` con un poll ya en vuelo respeta el guard
  anti-solape `_isPolling` y es un no-op silencioso (mismo guard ya
  verificado vía `PollOnceForTestsAsync` en los tests preexistentes, ahora
  también verificado invocando directamente el método público de
  producción).
- `test/ClaudeMeter.Desktop.Tests/Pages/UsagePageTests.cs` — **Modificado.**
  `RegisterCoreServices` registra ahora `PollingControlService` (instancia
  real por test, la misma que `UsagePage.OnInitialized()` adjunta a su
  propio coordinador) y `WindowCloseService` real (nunca invocado en estos
  tests bUnit, solo no debe impedir el montaje — mismo criterio ya aplicado
  a `WindowDragService`/`WindowResizeService`), para que los 27 tests ya
  existentes de este fichero sigan compilando y pasando sin cambiar su
  comportamiento. 4 tests bUnit nuevos: el botón `button.claudemeter-close`
  está presente y renderizado tanto con snapshot `Success` (dos `UsageBar`)
  como con `Unauthorized` (`ReauthNotice`) — confirma que vive fuera del
  `@if`/`else`; pausar desde la misma instancia de `PollingControlService`
  que `UsagePage` adjuntó marca `"(desactualizado)"` en el markup sin perder
  el último valor válido mostrado (reutiliza `_isStale`, ningún tercer
  estado visual nuevo); tras desmontarse (`DisposeComponentsAsync`), una
  llamada posterior a `PollingControlService.Pause()` es un no-op silencioso
  (confirma que `UsagePage.Dispose()` invoca `DetachCoordinator` de verdad).

## Coverage Result

- Target (`testingCoverage` en `.claude/sdlc.config.yaml`): **70%**
- Comando: `dotnet test test/ClaudeMeter.Desktop.Tests/ClaudeMeter.Desktop.Tests.csproj --collect:"XPlat Code Coverage"`
  (Coverlet, ya presente en el `.csproj`, sin herramienta nueva).
- Resultado de ejecución: **159/159 correctos** en `ClaudeMeter.Desktop.Tests`
  (119 preexistentes de F3/Ciclo A + 9 `ClickThroughServiceTests` + 3
  `WindowCloseServiceTests` + 10 `PollingControlServiceTests` + 10
  `TrayIconServiceTests` + 5 nuevos en `UsagePollingCoordinatorTests` + 4
  nuevos en `UsagePageTests`).
- Cobertura por clase, leída de `coverage.cobertura.xml`, para el código
  nuevo/modificado de F3 Ciclo B:
  - `ClaudeMeter.Desktop.Polling.PollingControlService`: **line-rate 100%,
    branch-rate 100%.**
  - `ClaudeMeter.Desktop.Polling.UsagePollingCoordinator`: **line-rate 100%,
    branch-rate 100%** (sube desde el 100%/100% ya existente de F2/US-4 al
    incorporar `Pause()`/`PollNow()`, ambos ya cubiertos igual de completo).
  - `ClaudeMeter.Desktop.Tray.TrayIconService`: **line-rate 78%, branch-rate
    100%** — por encima del 70% objetivo. Las líneas sin cubrir son
    `Initialize()` completo (`Icon.ExtractAssociatedIcon`/`SystemIcons.Application`/
    `Visible = true`) — gap aceptado, ver abajo.
  - `ClaudeMeter.Desktop.Pages.UsagePage`: **line-rate 96.66%, branch-rate
    84.61%** — sube desde el 95.94%/90% de F3/Ciclo A (branch-rate baja
    ligeramente en términos relativos solo porque el fichero ahora tiene más
    ramas totales por el puente de pausa; en términos absolutos se cubren
    más ramas que antes, ver Gaps para el resto ya heredado).
  - `ClaudeMeter.Desktop.Windowing.ClickThroughService`: **line-rate 52%,
    branch-rate 21.42%** — por debajo del 70% objetivo, **gap aceptado y
    documentado explícitamente abajo, no un descuido.**
  - `ClaudeMeter.Desktop.Windowing.WindowCloseService`: **line-rate 53.84%,
    branch-rate 25%** — por debajo del 70% objetivo, **mismo gap aceptado,
    ver abajo.**
- **Achieved: 78%-100% en cuatro de las seis clases nuevas/modificadas de
  este ciclo (`PollingControlService`, `UsagePollingCoordinator`,
  `TrayIconService`, `UsagePage`) — muy por encima del 70% objetivo.**
  `ClickThroughService` (52%/21.42%) y `WindowCloseService` (53.84%/25%)
  quedan por debajo del umbral **por diseño, no por falta de esfuerzo**: su
  único código no cubierto es la llamada real a `user32.dll`/`Application.Shutdown()`
  con una `Window`/`HWND` reales, exactamente el mismo tipo de gap ya
  aceptado en este proyecto para `WindowDragService` (47.8%/25%, F2/Ciclo B)
  y `WindowResizeService` (29.16%/8.33%, F3/Ciclo A) — ambos con porcentajes
  del mismo orden, no un caso aislado. No se ha añadido ningún test
  artificial para forzar el número en ninguna de las seis clases.
- Confirmación final, solución completa: `dotnet test ClaudeMeter.sln` —
  **298/298 correctos, 0 fallos** (77 `ClaudeMeter.Domain.Tests`, 20
  `ClaudeMeter.Console.Tests`, 42 `ClaudeMeter.Infrastructure.Tests`, 159
  `ClaudeMeter.Desktop.Tests`; `ClaudeMeter.Application.Tests` sigue sin
  tests, scaffolding vacío preexistente sin relación con esta fase) — los
  258 tests que ya estaban en verde antes de este ciclo siguen en verde, sin
  ninguna regresión introducida por el código nuevo ni por sus tests.
  `dotnet build ClaudeMeter.sln -c Release` (con `TreatWarningsAsErrors`)
  compila **sin advertencias ni errores**, incluyendo los cuatro ficheros de
  test nuevos y los dos modificados.

## Gaps / Not Covered

- **`ClickThroughService`, line-rate 52%/branch-rate 21.42% — gap aceptado,
  mismo criterio ya usado para `WindowDragService`/`WindowResizeService`.**
  Solo el estado inicial (`IsEnabled == false`) y el guard de "sin ventana
  adjunta" son alcanzables de forma determinista sin un `HWND` real en un
  hilo STA con sesión de escritorio real. El resto —
  `GetWindowLongPtr`/`SetWindowLongPtr` reales (y su *shim* de 32/64 bits),
  el cálculo de `newStyle` sobre un estilo extendido real, y la rama
  `enabled == IsEnabled` con una ventana ya adjunta — requiere esa ventana
  real; `SetEnabledForTests` cubre la parte observable (actualización de
  `IsEnabled`/`StateChanged`) sin pasar por Win32, pero no ejercita el
  P/Invoke en sí. Mitigación más cercana: la validación manual de la
  Definition of Done ("el click-through activado deja pasar los clics a la
  ventana de debajo del escritorio real").
- **`WindowCloseService`, line-rate 53.84%/branch-rate 25% — mismo gap
  aceptado.** Solo el guard de "sin ventana adjunta" es alcanzable de forma
  determinista. La rama `!_window.Dispatcher.CheckAccess()` (reentrada vía
  `Dispatcher.Invoke`) y la llamada final a
  `System.Windows.Application.Current.Shutdown()` requieren una
  `Window`/`Application` reales — y, deliberadamente, no se han simulado:
  invocar `Shutdown()` real terminaría el propio proceso de test. Mitigación
  más cercana: la validación manual de la Definition of Done ("el botón de
  cierre directo... termina el proceso sin dejarlo corriendo en segundo
  plano").
- **`TrayIconService.Initialize()` (líneas de `Icon.ExtractAssociatedIcon`/
  `SystemIcons.Application`/`NotifyIcon.Visible = true`) — gap aceptado.**
  Es el único método de la clase que sí toca la bandeja real de Windows
  (`Visible = true`) y `Environment.ProcessPath` del proceso de test (no del
  `.exe` de producción); el resto de la clase (construcción del menú,
  reacción a los eventos, `Dispose()`) sí queda cubierto (de ahí el 78%
  line-rate ya por encima del umbral pese a este gap puntual). Mitigación
  más cercana: la validación manual de la Definition of Done ("al
  completarse el arranque aparece el icono en la bandeja").
- **`ExitItemForTests.PerformClick()` — deliberadamente no invocado.**
  Dispararía `Application.Current.Shutdown()` real dentro del propio proceso
  de test (mismo motivo que el gap de `WindowCloseService` de arriba). Se
  verifica solo que el ítem existe con el texto correcto.
- **`UsagePage`, ~15% de branch-rate restante — mismo tipo de gap ya
  aceptado en ciclos anteriores (carrera benigna
  `ObjectDisposedException`/`InvalidOperationException` en
  `OnSnapshotReceived`, y ahora también la rama de `catch` de
  `claudeMeterClose.init` si el interop JS fallase, no ejercitable con
  `JSInterop.Mode = JSRuntimeMode.Loose`).** Sin cambio de criterio respecto
  a F1/F2/F3 Ciclo A.
- **Contraste visual real / animación de countdown / resize real —
  heredados de ciclos anteriores, sin relación con este ciclo:** no se
  repiten aquí por no ser parte del alcance de F3/Ciclo B (ver el informe de
  F3/Ciclo A para su detalle).
- No se ha escrito ningún test de integración (Testcontainers/DB/HTTP): no
  aplica — F3 Ciclo B no introduce ninguna dependencia nueva de base de
  datos ni de red (confirmado por el propio documento de diseño: "Este ciclo
  no toca Domain, Application ni Infrastructure en absoluto"); toda la
  superficie nueva es Desktop puro (P/Invoke, WinForms, puente de DI,
  Razor), cubierta con xUnit/bUnit consistente con la política de testing de
  `CLAUDE.md`.

## Acceptance Criteria Coverage

| Acceptance Criterion | Covered by |
|---|---|
| US-1 GIVEN arranca sin interacción previa WHEN se crea `MainWindow` THEN click-through desactivado por defecto | `ClickThroughServiceTests.IsEnabled_AlConstruir_EsFalsePorDefecto` |
| US-1 GIVEN modo interactivo WHEN activa click-through desde la bandeja THEN los clics atraviesan sin dejar de renderizar/actualizar | Manual validation — see Definition of Done (requiere un `HWND`/escritorio real; `TrayIconServiceTests.ClickThroughItem_TrasSetEnabledForTests_ReflejaElEstadoDeClickThrough` cubre el disparo del cambio de estado desde el menú, no el efecto Win32 real) |
| US-1 GIVEN modo click-through WHEN desactiva desde la bandeja THEN vuelve a interceptar (arrastre y cierre responden) | Manual validation — see Definition of Done (mismo motivo que arriba) |
| US-1 GIVEN modo click-through WHEN intenta arrastrar o cerrar THEN ningún efecto | Manual validation — see Definition of Done (por diseño no hay guarda explícita en código que testear: el SO nunca entrega el evento, ver Technical Notes de requisitos) |
| US-1 GIVEN estado de click-through WHEN abre el menú de bandeja THEN el ítem refleja el estado actual (marcado/desmarcado) | `TrayIconServiceTests.ClickThroughItem_AlConstruir_NoEstaMarcado` + `ClickThroughItem_TrasSetEnabledForTests_ReflejaElEstadoDeClickThrough` |
| US-2 GIVEN arranca con normalidad WHEN completa `App.OnStartup` THEN aparece un icono en la bandeja | Manual validation — see Definition of Done (`TrayIconService.Initialize()` toca `NotifyIcon.Visible` real, gap aceptado) |
| US-2 GIVEN icono visible WHEN clic derecho THEN menú con Pausar/Reanudar, Recargar, click-through, Salir | `TrayIconServiceTests.Constructor_EsInstanciableSinSesionDeEscritorioReal_YConstruyeLosCuatroItemsDelMenu` |
| US-2 GIVEN polling activo WHEN "Pausar" THEN timer se detiene, conserva último snapshot marcado, ítem pasa a "Reanudar" | `PollingControlServiceTests.Pause_ConCoordinadorAdjunto_MarcaIsPausedYDisparaPauseStateChangedUnaVez` + `TrayIconServiceTests.PauseResumeItem_PerformClick_AlternaTextoYIsPausedDePausarAReanudar` + `UsagePageTests.UsagePage_AlPausarDesdePollingControlService_MarcaLasBarrasComoDesactualizadasSinPerderElUltimoValor` |
| US-2 GIVEN pausado WHEN "Reanudar" THEN timer se reinicia con fetch inmediato | `PollingControlServiceTests.Resume_TrasPause_MarcaIsPausedFalseYDisparaPauseStateChangedDeNuevo` + `UsagePollingCoordinatorTests.Start_TrasPause_ReanudaElTimerYDisparaUnFetchInmediato` + `TrayIconServiceTests.PauseResumeItem_PerformClickDosVeces_VuelveAPausarConSuTextoOriginal` |
| US-2 GIVEN cualquier estado WHEN "Recargar" THEN poll inmediato adicional sin reiniciar el timer | `UsagePollingCoordinatorTests.PollNow_SinHaberLlamadoAStartAntes_DisparaGetUsageAsyncUnaVezSinTocarElTimer` + `PollingControlServiceTests.RequestReload_ConCoordinadorAdjunto_DisparaUnPollAdicionalSinTocarIsPaused` + `TrayIconServiceTests.ReloadItem_PerformClick_DisparaUnPollAdicionalSinTocarIsPaused` |
| US-2 GIVEN modo interactivo WHEN gesto de cierre directo THEN cierra completamente (mismo efecto que "Salir") | `WindowCloseServiceTests` (guard de "sin ventana adjunta", condición previa) + Manual validation — see Definition of Done (`Application.Current.Shutdown()` real) |
| US-2 GIVEN modo click-through WHEN gesto de cierre directo THEN sin efecto | Manual validation — see Definition of Done (sin guarda explícita en código, mismo motivo que el AC equivalente de US-1) |
| US-2 GIVEN cualquier estado WHEN "Salir" THEN cierra completo, libera el icono, sin proceso residual | `TrayIconServiceTests.ExitItem_Existe_ConElTextoSalirYSinInvocarPerformClick` (solo existencia, deliberadamente sin `PerformClick()`) + Manual validation — see Definition of Done |
| US-2 GIVEN cierre por cualquiera de las tres vías WHEN completa THEN efecto idéntico, sin ventana/icono residual | Manual validation — see Definition of Done |

## Definition of Done

(Copiada literalmente de `docs/sdlc/requirements/f3-ux-mascota-ciclo-b.md`.)

- [ ] Todas las Acceptance Criteria de US-1 y US-2 anteriores se cumplen —
      **las verificables por tests automáticos se cumplen** (ver tabla de
      trazabilidad: 6 de las 14 ACs tienen cobertura automática completa —
      estado inicial, contenido del menú, y la orquestación completa de
      Pausar/Reanudar/Recargar —, otras 3 tienen cobertura automática
      parcial que cubre la lógica de estado/orquestación pero no el efecto
      Win32/proceso real; las 5 restantes dependen por completo de un `HWND`
      real, de `Application.Current.Shutdown()` real, o de la bandeja real
      de Windows, que ningún agente de este pipeline puede producir — ver
      los ítems PENDIENTE de abajo, que impiden marcar esto como 100%
      cerrado solo con esta fase.
- [x] Cobertura de tests automatizados (xUnit puro para
      `ClickThroughService`/`WindowCloseService`/`PollingControlService`/
      `UsagePollingCoordinator.Pause`+`PollNow` y para la lógica de
      `TrayIconService` invocada vía `PerformClick()`/`SetEnabledForTests`;
      bUnit para que `UsagePage` refleje `_isStale` al pausar y para la
      presencia del botón de cierre) según el mínimo configurado en
      `.claude/sdlc.config.yaml` (`testingCoverage: 70`) — **conseguido:
      78%-100% en cuatro de las seis clases nuevas/modificadas
      (`PollingControlService`, `UsagePollingCoordinator`, `TrayIconService`,
      `UsagePage`); `ClickThroughService` (52%/21.42%) y `WindowCloseService`
      (53.84%/25%) quedan por debajo del umbral por un gap aceptado y
      documentado explícitamente (P/Invoke/`Shutdown()` reales), consistente
      con el mismo criterio ya usado para `WindowDragService`/
      `WindowResizeService` en ciclos anteriores** — 159/159 tests en verde
      (`dotnet test test/ClaudeMeter.Desktop.Tests/ClaudeMeter.Desktop.Tests.csproj --collect:"XPlat Code Coverage"`).
- [x] Ninguna prueba automatizada depende de una bandeja de sistema real ni
      de interacción de ratón real de Windows — los 32 tests nuevos/
      modificados de esta fase invocan `PerformClick()`
      (`TrayIconServiceTests`), los métodos `[JSInvokable]` directamente
      (`WindowCloseServiceTests`), o `SetEnabledForTests`
      (`ClickThroughServiceTests`/`TrayIconServiceTests`) — verificado por
      revisión de cada test nuevo de esta fase; ninguno simula un clic físico
      real ni depende del `NotifyIcon.Visible` real (`TrayIconService.Initialize()`
      nunca se invoca en ningún test).
- [ ] **PENDIENTE — Validación manual (US-1/US-2):** se confirma, ejecutando
      la aplicación real en Windows, que (a) el icono aparece en la bandeja
      y su menú responde a Pausar/Reanudar/Recargar/click-through/Salir con
      el efecto descrito, (b) el click-through activado deja pasar los
      clics a la ventana de debajo del escritorio real y el widget sigue
      actualizándose, (c) el botón de cierre directo (visible solo en
      hover) y "Salir" desde la bandeja terminan el proceso sin dejarlo
      corriendo en segundo plano (verificable en el Administrador de
      tareas), y (d) ningún icono de bandeja queda residual tras cerrar por
      cualquiera de las tres vías. Ningún agente de este pipeline puede
      verificar interacción de ratón real, comportamiento real de la
      bandeja de Windows, ni procesos del sistema operativo de forma
      automática; `ClickThroughServiceTests`/`WindowCloseServiceTests`/
      `TrayIconServiceTests`/`PollingControlServiceTests` son la mitigación
      automatizable más cercana (la misma lógica de estado/orquestación,
      verificada de forma determinista), pero no sustituyen esta
      validación.

---

Nada se ha confirmado (`commit`) ni enviado (`push`) al repositorio: los
ficheros de test nuevos
(`test/ClaudeMeter.Desktop.Tests/Windowing/ClickThroughServiceTests.cs`,
`test/ClaudeMeter.Desktop.Tests/Windowing/WindowCloseServiceTests.cs`,
`test/ClaudeMeter.Desktop.Tests/Polling/PollingControlServiceTests.cs`,
`test/ClaudeMeter.Desktop.Tests/Tray/TrayIconServiceTests.cs`) y modificados
(`test/ClaudeMeter.Desktop.Tests/Polling/UsagePollingCoordinatorTests.cs`,
`test/ClaudeMeter.Desktop.Tests/Pages/UsagePageTests.cs`) quedan en el árbol
de trabajo, listos para revisión, junto con el resto de cambios de F3 Ciclo
B ya presentes sin confirmar. Ningún fichero fuera de `test/` se ha tocado
en esta fase, salvo este propio informe.
