# Testing Summary: F2 — Robustez, Ciclo B

## Scope

Cobertura de tests para el código nuevo/modificado, aún sin confirmar
(`commit`), de F2 Ciclo B (issue única **#13** — US-1 `config.json`
intervalo/posición/chime, US-2 arrastrar y persistir posición), a partir de
`docs/sdlc/development/f2-robustez-ciclo-b.md` (detalla exactamente qué se
implementó, sus desviaciones respecto al diseño, y que
`UsagePageTests.cs` compilaba pero fallaría en runtime) y de
`docs/sdlc/requirements/f2-robustez-ciclo-b.md` (para la tabla de
trazabilidad AC → test). Se ha leído directamente el código de producción y
de los tests ya existentes antes de escribir ningún test nuevo, sin asumir
firmas/nombres de la descripción de la tarea.

Piezas nuevas/modificadas cubiertas:

- `src/ClaudeMeter.Domain/Usage/ThresholdTransition.cs` (nuevo) —
  `EnteredCritical(previous, current)`, función pura.
- `src/ClaudeMeter.Desktop/Configuration/AppConfig.cs`/`AppConfigStore.cs`
  (nuevos) — modelo y lector/escritor de `config.json`.
- `src/ClaudeMeter.Desktop/Windowing/WindowPositionResolver.cs` (nuevo) —
  función pura de resolución de posición con monitores simulados.
- `src/ClaudeMeter.Desktop/Windowing/WindowDragService.cs` (nuevo) —
  orquestador del gesto de arrastre.
- `src/ClaudeMeter.Desktop/Audio/IChimePlayer.cs`/`SystemSoundChimePlayer.cs`
  (nuevos) — abstracción de reproducción de chime.
- `src/ClaudeMeter.Desktop/Pages/UsagePage.razor` — intervalo de poll desde
  `Config.PollingInterval`, listener de arrastre vía JS interop, disparo de
  chime en transición a Crítico.

## Arreglo mecánico de compilación/runtime (paso previo obligatorio)

`test/ClaudeMeter.Desktop.Tests/Pages/UsagePageTests.cs` ya compilaba
(confirmado de nuevo con `dotnet build ClaudeMeter.sln -c Debug` al empezar
esta fase) pero fallaba en tiempo de ejecución en los 10 `[Fact]`/`[Theory]`
existentes: su `Services` de bUnit solo registraba `IUsageDataSource`, y
`UsagePage` exige además `AppConfig`, `IChimePlayer`, `WindowDragService` e
`IJSRuntime` vía `@inject` — exactamente lo anticipado por Development. Se
corrigió centralizando el registro en un helper privado
`RegisterCoreServices(IUsageDataSource, AppConfig?, IChimePlayer?)` que:
registra el `IUsageDataSource` recibido; registra `AppConfig.Default` salvo
que el test pase uno distinto (así los 10 tests preexistentes, que no
prueban nada de config/chime, conservan exactamente su comportamiento
original); registra un `FakeChimePlayer` (nuevo test double) por defecto;
registra un `WindowDragService` real (con un `AppConfigStore` real detrás,
nunca invocado en estos tests porque no hay gesto de ratón real que dispare
`BeginDrag()`); y pone `JSInterop.Mode = JSRuntimeMode.Loose` (bUnit) para
que la llamada real a `claudeMeterDrag.init` en `OnAfterRenderAsync` no
rompa el montaje. Las 10 llamadas `Services.AddSingleton<IUsageDataSource>(...)`
existentes se sustituyeron por `RegisterCoreServices(...)` sin tocar
ninguna otra línea de esos 10 tests — arreglo puramente mecánico de
compilación/wiring, no un cambio de comportamiento a testear de nuevo
(mismo patrón ya usado en F2 Ciclo A con `NullLogger<T>.Instance`).

## Tests Added/Modified

- `test/ClaudeMeter.Domain.Tests/Usage/ThresholdTransitionTests.cs` —
  **Nuevo.** 16 casos (`[Theory]`) cubriendo `EnteredCritical`: cualquier
  combinación no-Crítico→Crítico (incluido `null`→Crítico, "primera lectura
  ya en rojo") devuelve `true`; Crítico→Crítico devuelve `false` (el AC más
  importante: no repetir mientras se mantenga en rojo); Crítico→cualquier
  no-Crítico (incluido Crítico→`null`) devuelve `false`; y todas las
  combinaciones donde intervienen `null` sin que `current` sea Crítico.
- `test/ClaudeMeter.Desktop.Tests/Configuration/AppConfigStoreTests.cs` —
  **Nuevo.** 17 tests con un directorio temporal propio por test class
  (`IDisposable`, nunca `%LOCALAPPDATA%` real): fichero inexistente →
  `AppConfig.Default` sin crear nada; JSON sintácticamente inválido y JSON
  literalmente `null` → default completo sin lanzar; `pollingIntervalSeconds`
  ≤0 (`[Theory]`: 0 y -5) → solo ese campo cae a 60s, `chimeEnabled`/
  `windowPosition` válidos en el mismo fichero se respetan; campo ausente
  vs. presente-pero-inválido para intervalo/posición/chime por separado;
  `windowPosition` con un solo eje presente → posición completa a `null`;
  round-trip `Save`→`Load`; `Save` crea el directorio destino si no existe;
  escritura atómica confirmada (sin `.tmp` residual tras completar, único
  fichero en el directorio); dos `Save()` consecutivos dejan siempre el
  valor del último (simula 2 arrastres rápidos, AC de US-2); `Save` nunca
  lanza ante un fallo de E/S real (directorio destino bloqueado por un
  fichero con el mismo nombre → `IOException` capturada); `SavePosition`
  actualiza solo la posición conservando intervalo/chime ya persistidos, y
  también funciona partiendo de cero (sin config previo).
- `test/ClaudeMeter.Desktop.Tests/Windowing/WindowPositionResolverTests.cs`
  — **Nuevo.** 8 tests con monitores simulados (`Rect` a mano, sin
  `Win32ScreenInfo` real): posición que cabe en el monitor principal se
  respeta; posición que cabe solo en un monitor secundario se respeta;
  posición fuera de todos los monitores conectados (con 2 monitores
  simulados) cae al monitor **principal**, no a cualquier otro disponible;
  sin posición configurada → mismo fallback; lista de monitores vacía →
  mismo fallback; fronteras exactas (cabe justo en el borde inferior
  derecho, no cabe un píxel más allá); coordenadas negativas fuera de
  pantalla.
- `test/ClaudeMeter.Desktop.Tests/Windowing/WindowDragServiceTests.cs` —
  **Nuevo.** 3 tests limitados a la única rama alcanzable sin una
  `System.Windows.Window` real en un hilo STA con sesión de escritorio real
  (ver Gaps): `BeginDrag()` sin `AttachWindow()` previo no lanza, registra
  `Warning` (vía un `CapturingLogger<T>` nuevo, mismo patrón que
  `ClaudeMeter.Infrastructure.Tests.Usage.CapturingLogger<T>` de F2 Ciclo A)
  y es idempotente si se llama varias veces seguidas.
- `test/ClaudeMeter.Desktop.Tests/TestDoubles/FakeChimePlayer.cs` —
  **Nuevo.** Test double de `IChimePlayer` con contador `PlayCount`, nunca
  reproduce sonido real.
- `test/ClaudeMeter.Desktop.Tests/TestDoubles/CapturingLogger.cs` —
  **Nuevo.** Mismo test double genérico ya usado en
  `ClaudeMeter.Infrastructure.Tests` (reimplementado aquí porque los test
  doubles `internal` no se comparten entre ensamblados de test distintos).
- `test/ClaudeMeter.Desktop.Tests/Pages/UsagePageTests.cs` — **Modificado.**
  Arreglo mecánico de los 10 tests existentes (arriba) + 8 tests nuevos:
  intervalo de poll tomado de `Config.PollingInterval` en vez de un valor
  hardcodeado (intervalo corto inyectado + espera de un segundo
  `GetUsageAsync()`, algo que un intervalo de 60s real nunca completaría
  dentro del timeout del test); transición Normal→Crítico con chime
  habilitado dispara el chime exactamente una vez; **el AC más importante
  de US-1** — el chime NO se repite en ciclos sucesivos mientras el estado
  se mantenga en Crítico sin una transición nueva (verificado con 3 ciclos
  consecutivos en Crítico, no solo 2); chime deshabilitado en `AppConfig`
  nunca suena aunque haya transición a Crítico; el primer snapshot recibido
  ya en Crítico también dispara el chime (transición `null`→Crítico); un
  fallo transitorio (`RequestFailed`) mientras el último estado era Crítico
  no resetea la detección de transición al recuperarse (el chime no vuelve
  a sonar) — verifica textualmente el rationale ya documentado en el propio
  código de producción; Crítico→Normal→Crítico sí dispara el chime de
  nuevo (transición nueva); y la transición a Crítico de la ventana
  **semanal** por sí sola (sesión normal) también dispara el chime, no solo
  la de sesión.

No se ha modificado `test/ClaudeMeter.Desktop.Tests/Pages/UsageBarTests.cs`,
`ReauthNoticeTests.cs`, `Polling/UsagePollingCoordinatorTests.cs` ni
`Logging/SerilogConfiguratorTests.cs` (no les afecta este ciclo), ni
`ClaudeMeter.Application.Tests`/`ClaudeMeter.Console.Tests`/
`ClaudeMeter.Infrastructure.Tests` (F2 Ciclo B no toca ninguna de esas
capas).

## Trazabilidad AC → Test

| Acceptance Criteria (resumen) | Test(s) que lo cubre |
|---|---|
| US-1 GIVEN sin `config.json` WHEN arranca THEN defaults (60s/sin posición/chime off), sin excepción ni fichero creado | `AppConfigStoreTests.Load_ConFicheroInexistente_DevuelveDefaultSinCrearNada` |
| US-1 GIVEN `config.json` con intervalo distinto de 60s WHEN se construye el coordinador THEN usa el intervalo configurado, no la constante hardcodeada | `AppConfigStoreTests.Load_ConPollingIntervalSecondsValido_LoRespeta` (lectura) + `UsagePageTests.UsagePage_ConIntervaloCortoEnAppConfig_LoUsaEnVezDeUnValorHardcodeado` (uso real por `UsagePage`) |
| US-1 GIVEN `config.json` con posición inicial dentro de un monitor WHEN se crea `MainWindow` THEN aparece en esa posición | `WindowPositionResolverTests.Resolve_ConPosicionConfiguradaQueCabeEnElMonitorPrincipal_...`, `_QueCabeSoloEnUnMonitorSecundario_...` |
| US-1 GIVEN chime activado y transición a Crítico WHEN se procesa THEN suena; si desactivado, no suena | `UsagePageTests.UsagePage_ConTransicionDeNormalACriticalYChimeHabilitado_DisparaElChimeExactamenteUnaVez`, `_ConChimeDeshabilitadoEnAppConfig_NuncaSuenaAunEnTransicionACritical` |
| **US-1 GIVEN chime ya sonó y el estado sigue en Crítico sin transición nueva WHEN se procesa el siguiente ciclo THEN NO vuelve a sonar** (AC más crítico de esta fase) | `ThresholdTransitionTests.EnteredCritical_ConPreviousYCurrentAmbosCritical_DevuelveFalse` (dominio puro) + `UsagePageTests.UsagePage_ConElEstadoCriticalQuePersisteEnElSiguienteCiclo_NoRepiteElChime` (integración con `UsagePage`, 3 ciclos consecutivos) |
| US-1 GIVEN `config.json` corrupto o con valores fuera de rango WHEN se lee THEN defaults por campo afectado, sin abortar, `Warning` en log | `AppConfigStoreTests.Load_ConJsonSintacticamenteInvalido_...`, `_ConContenidoLiteralmenteNull_...`, `_ConPollingIntervalSecondsFueraDeRango_SoloEseCampoCaeADefault_RestoSeRespeta` (`[Theory]` 0/-5), `_ConWindowPositionConSoloUnCampoPresente_CaeANuloSinLanzar` |
| US-1 GIVEN posición configurada fuera de todos los monitores conectados WHEN se crea `MainWindow` THEN fallback a esquina inferior derecha del monitor **principal** | `WindowPositionResolverTests.Resolve_ConPosicionConfiguradaFueraDeTodosLosMonitoresConectados_CaeAEsquinaInferiorDerechaDelMonitorPrincipal`, `_SinNingunMonitorConectado_...`, `_ConPosicionQueSeSaleUnPixelDelBordeDelMonitor_...`, `_ConCoordenadasNegativas_...` |
| US-2 GIVEN arrastre en curso WHEN se suelta THEN la ventana sigue el puntero | No automatizable (`Window.DragMove()` real, hilo STA con sesión de escritorio) — ver Gaps |
| US-2 GIVEN se completa un arrastre WHEN se procesa THEN la nueva posición se persiste en `config.json` | `AppConfigStoreTests.SavePosition_ActualizaSoloLaPosicionYConservaIntervaloYChimeYaPersistidos`, `_SinConfigPrevio_...` (persistencia en sí) + `WindowDragServiceTests` (guard de "sin ventana", ver Gaps para la invocación real de `SavePosition` tras un `DragMove()` real) |
| US-2 GIVEN posición persistida de una sesión anterior WHEN reinicia THEN `MainWindow` aparece ahí | `AppConfigStoreTests.Save_EscribeUnFicheroQueLoadPuedeLeerDeVuelta` (round-trip) + `WindowPositionResolverTests` (uso de esa posición al resolver) |
| US-2 GIVEN dos arrastres consecutivos WHEN se completan THEN `config.json` refleja siempre el último, nunca una mezcla | `AppConfigStoreTests.Save_LlamadoDosVecesSeguidas_ElFicheroFinalRespetaElUltimoValorSinQuedarACorruptoAMedias`, `_EscrituraAtomica_NoDejaNingunFicheroTemporalTrasCompletar` |
| US-2 GIVEN fallo de E/S al persistir tras soltar WHEN se intenta guardar THEN no crashea, `Warning`/`Error` en log, valor en memoria no se revierte | `AppConfigStoreTests.Save_ConRutaCuyoDirectorioEsUnFicheroExistente_NoLanzaYNoEscribeNada` (`Save` nunca lanza ante IOException real) + `WindowDragServiceTests.BeginDrag_SinVentanaAdjunta_RegistraWarningYSeIgnoraElGesto` (nivel de log del guard análogo) |

## Coverage Result

- Target (`testingCoverage` en `.claude/sdlc.config.yaml`): **70%**
- Comando: `dotnet test <proyecto> --collect:"XPlat Code Coverage"`
  (Coverlet, ya presente en los `.csproj`, sin herramienta nueva).
- Resultado de ejecución (proyectos tocados por este ciclo):
  **77/77 correctos** en `ClaudeMeter.Domain.Tests` (61 preexistentes + 16
  nuevos de `ThresholdTransition`), **75/75** en `ClaudeMeter.Desktop.Tests`
  (39 preexistentes ya arreglados + 17 de `AppConfigStore` + 8 de
  `WindowPositionResolver` + 3 de `WindowDragService` + 8 nuevos en
  `UsagePage`).
- Cobertura por clase, leída de `coverage.cobertura.xml`, para el código
  nuevo/modificado de F2 Ciclo B:
  - `ClaudeMeter.Domain.Usage.ThresholdTransition`: **line-rate 100%,
    branch-rate 100%.**
  - `ClaudeMeter.Desktop.Configuration.AppConfig`/`WindowPosition`:
    **100%/100%** (records, sin lógica más allá de la construcción).
  - `ClaudeMeter.Desktop.Configuration.AppConfigStore`: **line-rate 98.6%,
    branch-rate 94.4%.**
  - `ClaudeMeter.Desktop.Windowing.WindowPositionResolver`: **line-rate
    100%, branch-rate 100%.**
  - `ClaudeMeter.Desktop.Pages.UsagePage`: **line-rate 95.9%, branch-rate
    90%** (consistente con el 95.3%/91.7% de F1 y el 95.3%/91.7% de F2
    Ciclo A; el resto del gap es el mismo ya aceptado en F1/Ciclo A, ver
    Gaps).
  - `ClaudeMeter.Desktop.Windowing.WindowDragService`: **line-rate 47.8%,
    branch-rate 25%** — por debajo del 70% objetivo, **gap aceptado y
    documentado explícitamente abajo**, no un descuido.
  - `ClaudeMeter.Desktop.Audio.SystemSoundChimePlayer`: **line-rate 0%** —
    gap aceptado, ver Gaps (mitigado indirectamente por los 8 tests de
    chime de `UsagePageTests` vía `FakeChimePlayer`).
  - `ClaudeMeter.Desktop.Windowing.Win32ScreenInfo`/`MainWindow`/`App`:
    **0%/0%** — exclusiones ya aceptadas por F1/F2 Ciclo A, sin cambios de
    alcance en este ciclo (composition root/chrome WPF real).
- **Achieved: 94.4%-100% en las cuatro clases de lógica pura/de negocio
  nuevas de este ciclo (`ThresholdTransition`, `AppConfig`, `AppConfigStore`,
  `WindowPositionResolver`) y 90%-95.9% en `UsagePage`  — muy por encima del
  70% objetivo.** `WindowDragService` y `SystemSoundChimePlayer` quedan por
  debajo del 70%, pero es un gap explícitamente autorizado por el propio
  alcance de esta fase (no testear `Window.DragMove()`/audio real) y
  documentado a continuación, no un vacío de esfuerzo — no se ha añadido
  ningún test artificial para forzar el número.
- Confirmación final: `dotnet test ClaudeMeter.sln` sobre la solución
  completa en Debug — **214 tests correctos, 0 fallos** (20
  `ClaudeMeter.Console.Tests`, 77 `ClaudeMeter.Domain.Tests`, 42
  `ClaudeMeter.Infrastructure.Tests`, 75 `ClaudeMeter.Desktop.Tests`;
  `ClaudeMeter.Application.Tests` sigue sin tests, scaffolding vacío
  preexistente sin relación con F2). `dotnet build ClaudeMeter.sln -c
  Release` (con `TreatWarningsAsErrors`) compila sin advertencias ni
  errores, incluyendo los ficheros de test nuevos/modificados.

## Gaps / Not Covered

- **`WindowDragService`, branch-rate 25% — gap aceptado, autorizado
  explícitamente por el alcance de esta fase.** Solo la rama de "sin
  ventana adjunta" (`_window is null` → `Warning` + `return`) es alcanzable
  de forma determinista sin una `System.Windows.Window` real. Las ramas de
  "guard de `Dispatcher.CheckAccess()`", el `try`/`catch` alrededor de
  `Window.DragMove()` y la llamada final a `AppConfigStore.SavePosition`
  tras un arrastre completado requieren una ventana WPF real en un hilo STA
  con sesión de escritorio real y un gesto de ratón real — no hay forma
  determinista de simular `DragMove()` (es una API bloqueante que exige
  `MouseLeftButtonDown` real, y lanza de inmediato si se invoca fuera de
  ese contexto, que es justo la excepción benigna que el propio código ya
  atrapa). Mismo criterio ya aceptado por Development/Design para
  `MainWindow`/`Win32ScreenInfo` en este mismo ciclo, y por F1 para el
  chrome WPF real.
- **`SystemSoundChimePlayer`, line-rate 0% — gap aceptado.** Reproducir
  `System.Media.SystemSounds.Exclamation.Play()` real en un test emitiría
  sonido real en la máquina que ejecute la suite (incluida CI); mitigado
  por completo por `FakeChimePlayer` + los 8 tests de `UsagePageTests` que
  verifican el AC de negocio real (cuándo debe sonar) a través de la
  abstracción `IChimePlayer`, que es la interfaz que `UsagePage` realmente
  consume.
- **`Win32ScreenInfo`/`MainWindow.xaml.cs`/`App.xaml.cs` — exclusión
  deliberada, ya aceptada por F1/F2 Ciclo A, sin cambios de alcance en este
  ciclo.** Composition root y adaptador P/Invoke sobre monitores reales:
  ninguno es ejercitable de forma significativa por xUnit/bUnit sin un
  monitor/sesión de escritorio real. La lógica de decisión testeable que
  antes vivía implícitamente ahí ya se extrajo por completo a
  `WindowPositionResolver` (100%/100%), que sí está cubierta.
- **`UsagePage`, mismo ~4-5% de gap de branch-rate ya aceptado por F1/F2
  Ciclo A** (líneas de la carrera benigna `ObjectDisposedException`/
  `InvalidOperationException` en `OnSnapshotReceived`, y la guarda
  `_coordinator is null` de `IsPollingActiveForTests`) — se reitera aquí
  solo para dejar constancia de que sigue siendo el mismo gap ya evaluado,
  no uno nuevo introducido por F2 Ciclo B.
- **`AppConfigStore`, ~1.4% de line-rate/5.6% de branch-rate restante.**
  Las ramas defensivas de `ResolveInterval`/`ResolvePosition` para
  `double.IsNaN`/`double.IsInfinity` no son alcanzables desde un
  `config.json` real vía `System.Text.Json` (por defecto no acepta
  literales `NaN`/`Infinity` en JSON, así que esas ramas son inalcanzables
  desde el único punto de entrada real de esta clase) — gap defensivo
  aceptado, no un descuido: se prefirió no forzar un DTO/deserialización
  artificial solo para alcanzar esas líneas.
- **US-2, "la ventana sigue el puntero de forma fluida durante el
  arrastre"** — no automatizable por ningún agente de este pipeline
  (`Window.DragMove()` real, hilo STA, sesión de escritorio real); ya
  marcado como tal en Requirements/Design/Development.
- **Las 2 validaciones manuales de la Definition of Done (US-1/US-2) siguen
  pendientes de un equipo Windows real** — no automatizables por este
  pipeline (WPF/WebView2 real, chime audible real, gesto de ratón real), ya
  marcadas como tales en Requirements/Design/Development.
- No se ha escrito ningún test de integración (Testcontainers/DB): no
  aplica — F2 Ciclo B no introduce ninguna dependencia nueva de base de
  datos; toda la superficie nueva es lógica pura, E/S de fichero local
  (`AppConfigStore`, cubierta con ficheros temporales reales, no mocks) o
  un componente Razor ya cubierto con bUnit, consistente con la política de
  testing de `CLAUDE.md`.

## Definition of Done

(Copiada literalmente de `docs/sdlc/requirements/f2-robustez-ciclo-b.md`.)

- [ ] Todas las Acceptance Criteria de US-1 y US-2 se cumplen — **las
      verificables por tests automáticos se cumplen** (ver tabla de
      trazabilidad); los puntos de interacción de ratón/audio real
      (`DragMove()` fluido, chime audible) dependen de un equipo Windows
      real, ver los ítems PENDIENTE de abajo, que impiden marcar esto como
      100% cerrado solo con esta fase.
- [x] Cobertura de tests automatizados (xUnit puro para el lector/parser de
      `config.json` y para la resolución de posición con monitores
      simulados; bUnit para el intervalo de poll y el ciclo completo del
      chime en `UsagePage.razor`) según el mínimo configurado en
      `.claude/sdlc.config.yaml` (`testingCoverage: 70`) — **conseguido:
      94.4%-100% en las cuatro clases de lógica pura/de negocio nuevas
      (`ThresholdTransition`, `AppConfig`, `AppConfigStore`,
      `WindowPositionResolver`) y 90%-95.9% en `UsagePage`.**
      `WindowDragService`/`SystemSoundChimePlayer` quedan por debajo del
      70%, con el gap explícitamente documentado arriba y autorizado por
      el propio alcance de esta fase (no testear `DragMove()`/audio real).
- [x] Ninguna prueba automatizada depende de un `config.json` real del
      usuario ni de un monitor/resolución real: `AppConfigStoreTests` usa
      un directorio temporal propio por test (`IDisposable`, nunca
      `%LOCALAPPDATA%`), y `WindowPositionResolverTests` usa `Rect`
      simulados a mano, nunca `Win32ScreenInfo`/`SystemParameters.WorkArea`
      reales — verificado por revisión de cada test nuevo de esta fase.
- [x] Disparador del chime (transición a umbral Crítico ≥90%, una sola vez
      por transición) — ya confirmado por el usuario en
      Requirements/Dependencies; verificado aquí con tests explícitos,
      incluido el AC más crítico (no repetir mientras se mantenga en
      Crítico) y el caso simétrico de re-entrada tras bajar del umbral.
- [ ] **PENDIENTE — Validación manual (US-1):** se confirma, ejecutando la
      aplicación real en Windows, que un `config.json` con intervalo,
      posición y chime personalizados cambia efectivamente el
      comportamiento del widget (poll al ritmo configurado, ventana en la
      posición indicada, chime audible/silencioso según el interruptor).
      Ningún agente de este pipeline puede verificar renderizado ni audio
      real de WPF/WebView2; `AppConfigStoreTests`/`WindowPositionResolverTests`/
      `UsagePageTests` son la mitigación automatizable más cercana (la
      misma lógica de decisión, verificada de forma determinista), pero no
      sustituyen esta validación.
- [ ] **PENDIENTE — Validación manual (US-2):** se confirma, arrastrando el
      widget con el ratón en un equipo Windows real y reiniciando la
      aplicación después, que la ventana reaparece exactamente en el punto
      donde se soltó el arrastre. `WindowDragServiceTests`/
      `AppConfigStoreTests.SavePosition_*` verifican por separado el guard
      de "sin ventana" y la persistencia atómica en sí, pero no el gesto de
      ratón real ni `Window.DragMove()` (ver Gaps) — no automatizable por
      este pipeline.

---

Nada se ha confirmado (`commit`) ni enviado (`push`) al repositorio: los
ficheros de test nuevos/modificados
(`test/ClaudeMeter.Domain.Tests/Usage/ThresholdTransitionTests.cs`,
`test/ClaudeMeter.Desktop.Tests/Configuration/AppConfigStoreTests.cs`,
`test/ClaudeMeter.Desktop.Tests/Windowing/WindowPositionResolverTests.cs`,
`test/ClaudeMeter.Desktop.Tests/Windowing/WindowDragServiceTests.cs`,
`test/ClaudeMeter.Desktop.Tests/TestDoubles/FakeChimePlayer.cs`,
`test/ClaudeMeter.Desktop.Tests/TestDoubles/CapturingLogger.cs`,
`test/ClaudeMeter.Desktop.Tests/Pages/UsagePageTests.cs`) quedan en el árbol
de trabajo, listos para revisión, junto con el resto de cambios de F2 Ciclo
B ya presentes sin confirmar. Ningún fichero fuera de `test/` se ha tocado
en esta fase, salvo este propio informe.
