# Testing Summary: F3 — UX + página Mascota, Ciclo C

## Scope

Cobertura de tests para el código nuevo/modificado, aún sin confirmar
(`commit`), de F3 Ciclo C (issues **#18** US-1 `ScreenNavigator` +
`IWidgetScreen`, y **#19** US-2 `MascotPage.razor`), a partir de
`docs/sdlc/development/f3-ux-mascota-ciclo-c.md`, que deja explícitamente
pendientes de esta fase los pasos 2, 5, 12, 13 y 14 de su Implementation
Plan. Siguiendo la cadena documental se ha leído también
`docs/sdlc/design/f3-ux-mascota-ciclo-c.md` (Implementation Plan, Data
Model y Technology Choices) y `docs/sdlc/requirements/f3-ux-mascota-ciclo-c.md`
(Acceptance Criteria y Definition of Done, usadas como fuente de verdad).
Se ha leído el código real de producción de cada fichero nuevo/modificado
(`MascotState.cs`, `IWidgetScreen.cs`, `ScreenCycle.cs`,
`WidgetUsageState.cs`, `ScreenNavigator.razor`, `UsagePage.razor`
reescrito, `MascotPage.razor`) y el fichero de test que este ciclo obliga a
migrar (`UsagePageTests.cs` anterior, ya roto con 68 errores `CS1061`)
antes de escribir nada nuevo, sin asumir firmas desde el resumen de la
tarea.

Piezas nuevas/modificadas cubiertas:

- `src/ClaudeMeter.Domain/Usage/MascotState.cs` (nuevo) — US-2. Enum
  `MascotState` + `MascotStateClassifier.Classify`.
- `src/ClaudeMeter.Desktop/Navigation/ScreenCycle.cs` (nuevo) — US-1.
- `src/ClaudeMeter.Desktop/Navigation/IWidgetScreen.cs` (nuevo) — US-1
  (contrato, sin lógica propia que testear de forma aislada — cubierto por
  el hecho de que `UsagePage`/`MascotPage` lo implementan).
- `src/ClaudeMeter.Desktop/Navigation/WidgetUsageState.cs` (nuevo) — DTO,
  cubierto indirectamente por todos los tests que lo construyen/leen.
- `src/ClaudeMeter.Desktop/Navigation/ScreenNavigator.razor` (nuevo) —
  nuevo `RootComponent` real: polling, chime, tema, botón de cierre,
  ciclado de pantallas, corrección de la rama `Unauthorized`.
- `src/ClaudeMeter.Desktop/Pages/UsagePage.razor` (reescrito en
  profundidad) — ahora puramente presentacional.
- `src/ClaudeMeter.Desktop/Pages/MascotPage.razor` (nuevo) — US-2.

`MainWindow.xaml`/`_Imports.razor`/`wwwroot/css/app.css` no llevan test
dedicado — mismo criterio ya aplicado en ciclos anteriores a composition
root/XAML/CSS puro (sin lógica propia que un test unitario pueda verificar
de forma significativa); su efecto visual queda en la validación manual de
la Definition of Done.

## Tests Added/Modified

- `test/ClaudeMeter.Domain.Tests/Usage/MascotStateClassifierTests.cs` —
  **Nuevo.** 14 tests xUnit puro (Theory incluidas): ambas ventanas Normal
  → `Calm`; al menos una en Warning y ninguna en Critical → `Alert`
  (incluyendo el caso simétrico semana/sesión); al menos una en Critical →
  `NearLimit` con independencia de la otra (5 combinaciones, incluyendo
  ambas simétricas y ambas en Critical); ambas sin dato interpretable →
  `NoData`; una ventana sin dato (`percentage == null`) y la otra con dato
  real → usa el umbral de la que sí tiene dato, sin caer a `NoData` (los
  tres umbrales, en ambos sentidos Session/Weekly); frontera exacta 90%
  (Critical) verificada consistente con `UsageThresholdClassifier`.
- `test/ClaudeMeter.Desktop.Tests/Navigation/ScreenCycleTests.cs` —
  **Nuevo.** 6 tests xUnit puro, sin ningún componente Razor real: el
  constructor lanza `ArgumentException` con lista vacía; `Current` es el
  primer elemento antes de cualquier `Next()`; `ScreenCount` refleja el
  número de pantallas; `Next()` avanza en orden y vuelve a la primera tras
  la última (probado con 2 y con 3 tipos cualesquiera —
  `typeof(object)`/`typeof(string)`/`typeof(int)`); con una única pantalla
  registrada, `Next()` no lanza y siempre devuelve el mismo tipo (AC de
  caso límite de US-1).
- `test/ClaudeMeter.Desktop.Tests/Navigation/DummyScreenA.razor` /
  `DummyScreenB.razor` — **Nuevos.** Pantallas Razor mínimas
  (`@implements IWidgetScreen`, `[Parameter] WidgetUsageState State`, un
  `<span data-testid="dummy-screen">` con su `ScreenId` y el
  `PercentageUsed` de sesión recibido) para poder probar el ciclado de
  `ScreenNavigator` de forma aislada, sin arrastrar las dependencias
  completas de `UsagePage`/`MascotPage`.
- `test/ClaudeMeter.Desktop.Tests/Navigation/ScreenNavigatorTests.cs` —
  **Nuevo** (migración obligatoria exigida por el paso 12 del
  Implementation Plan de diseño). 25 tests bUnit: recibe literalmente los
  tests de polling/chime/tema/botón de cierre/pausa/desmontaje que antes
  vivían en `UsagePageTests.cs` (F2/F3 Ciclos A/B), ahora montando
  `ScreenNavigator` en vez de `UsagePage` — primer fetch al montar; dos
  ciclos consecutivos de éxito sin mezclar valores; fallo transitorio tras
  éxito marca "(desactualizado)" conservando el último valor; las 6
  variantes del chime (transición a Crítico, no-repetición, deshabilitado,
  primer snapshot ya en Crítico, transición solo por la ventana semanal,
  fallo transitorio no resetea la detección, salida y reentrada a Crítico
  suena de nuevo); tema claro/oscuro sobre `.claudemeter-root` (incluido
  con `ReauthNotice`); botón de cierre siempre presente (con éxito y con
  `Unauthorized`); pausar desde `PollingControlService` marca
  "(desactualizado)"; desmontaje detiene el polling y desadjunta el
  coordinador de `PollingControlService`. Añade además el test explícito
  exigido por el documento de diseño para la corrección de este ciclo
  (`ScreenNavigator_ConRamaUnauthorized_ReseteaSessionYWeeklyAUnavailableEnVezDeConservarElUltimoValorParseado`,
  verificado indirectamente montando `ScreenNavigator` con
  `ScreenTypes = [MascotPage]`: un ciclo Critical→Unauthorized debe
  resolver `NoData`, no conservar `NearLimit`) y los 4 tests de ciclado
  (paso 13): la primera pantalla registrada se muestra al montar; un clic
  en `.claudemeter-cycle` alterna entre las dos pantallas simuladas (ida y
  vuelta); con una única pantalla registrada el botón no se renderiza; y
  el estado ya calculado se conserva al cambiar de pantalla sin esperar a
  un nuevo ciclo de poll.
- `test/ClaudeMeter.Desktop.Tests/Pages/UsagePageTests.cs` —
  **Reescrito por completo** (mucho más simple, tal como exige el paso 12
  del Implementation Plan): monta `UsagePage` directamente pasando un
  `WidgetUsageState` ya construido vía
  `Render<UsagePage>(p => p.Add(x => x.State, ...))`, sin ningún servicio
  inyectado. 11 tests bUnit: colores/valores de las dos `UsageBar`
  (incluyendo Theory de los tres estados de fallo con ambas ventanas
  `Unavailable`); mismo umbral → misma clase en ambas barras; percentage
  nulo en una ventana → "No disponible" en esa barra; `IsStale`
  true/false → con/sin sufijo "(desactualizado)"; `Status == Unauthorized`
  → `ReauthNotice`, oculta las dos barras; implementa `IWidgetScreen` con
  `ScreenId == "usage"`; `WidgetUsageState.Initial` no lanza y muestra
  ambas barras como "No disponible". Los tests de tema/botón de
  cierre/chime/polling desaparecen de este fichero (ya no aplican a un
  componente puramente presentacional) — quedan en
  `ScreenNavigatorTests.cs`.
- `test/ClaudeMeter.Desktop.Tests/Pages/MascotPageTests.cs` — **Nuevo.** 8
  tests bUnit: los 4 estados de `MascotState` (`Calm`/`Alert`/`NearLimit`/
  `NoData`), el caso simétrico "semana en Critical, sesión Normal" →
  también `NearLimit` (AC de "peor caso" independiente de cuál ventana),
  `Status == Unauthorized` con `Session`/`Weekly` en `Unavailable` (tal
  como los deja `ScreenNavigator.Apply()` corregido) → `NoData`; icono y
  etiqueta se renderizan con contenido no vacío; implementa
  `IWidgetScreen` con `ScreenId == "mascot"`.

## Coverage Result

- Target (`testingCoverage` en `.claude/sdlc.config.yaml`): **70%**
- Comandos:
  `dotnet test test/ClaudeMeter.Domain.Tests/ClaudeMeter.Domain.Tests.csproj --collect:"XPlat Code Coverage"`
  y
  `dotnet test test/ClaudeMeter.Desktop.Tests/ClaudeMeter.Desktop.Tests.csproj --collect:"XPlat Code Coverage"`
  (Coverlet, ya presente en ambos `.csproj`, sin herramienta nueva).
- Resultado de ejecución: **91/91 correctos** en `ClaudeMeter.Domain.Tests`
  (77 preexistentes + 14 nuevos de `MascotStateClassifierTests`) y
  **182/182 correctos** en `ClaudeMeter.Desktop.Tests` (159 preexistentes
  de F3/Ciclo B − 27 del `UsagePageTests.cs` anterior + 11 del
  `UsagePageTests.cs` reescrito + 25 de `ScreenNavigatorTests.cs` + 8 de
  `MascotPageTests.cs` + 6 de `ScreenCycleTests.cs`).
- Cobertura por clase, leída de `coverage.cobertura.xml`, para el código
  nuevo/modificado de F3 Ciclo C:
  - `ClaudeMeter.Domain.Usage.MascotStateClassifier`: **line-rate 100%,
    branch-rate 100%.**
  - `ClaudeMeter.Desktop.Navigation.WidgetUsageState`: **line-rate 100%,
    branch-rate 100%.**
  - `ClaudeMeter.Desktop.Pages.UsagePage`: **line-rate 100%, branch-rate
    100%.**
  - `ClaudeMeter.Desktop.Pages.MascotPage`: **line-rate 100%, branch-rate
    100%.**
  - `ClaudeMeter.Desktop.Navigation.ScreenCycle`: **line-rate 95.65%,
    branch-rate 83.33%** — la única línea sin cubrir (`return -1;` en
    `IndexOfCurrent()`) es un *fallback* defensivo inalcanzable en la
    práctica: `Current` siempre es un elemento de `_screens` por
    construcción (el constructor lo fija a `_screens[0]`, `Next()` siempre
    lo reasigna a otro elemento de la misma lista) — gap aceptado, ver
    abajo.
  - `ClaudeMeter.Desktop.Navigation.ScreenNavigator`: **line-rate 97.16%,
    branch-rate 84.61%** (más un estado de máquina async interno de
    `OnAfterRenderAsync` al 78.57%/100%, ver Gaps) — muy por encima del
    70% objetivo.
- **Achieved: 95.65%-100% en las seis clases nuevas/modificadas de este
  ciclo, todas por encima del 70% objetivo — sin ningún gap significativo
  ni test artificial añadido para forzar el número.**
- Confirmación final, solución completa: `dotnet test ClaudeMeter.sln` —
  **335/335 correctos, 0 fallos** (91 `ClaudeMeter.Domain.Tests`, 20
  `ClaudeMeter.Console.Tests`, 42 `ClaudeMeter.Infrastructure.Tests`, 182
  `ClaudeMeter.Desktop.Tests`; `ClaudeMeter.Application.Tests` sigue sin
  tests, scaffolding vacío preexistente sin relación con esta fase) — los
  298 tests que ya estaban en verde antes de este ciclo (menos los 27 del
  `UsagePageTests.cs` migrado/dividido) siguen en verde, sin ninguna
  regresión introducida por el código nuevo ni por sus tests.
  `dotnet build ClaudeMeter.sln -c Debug` y `-c Release` (con
  `TreatWarningsAsErrors` + analizadores en Release) compilan **sin
  advertencias ni errores**, incluyendo los cinco ficheros de test nuevos,
  los dos ficheros `.razor` de pantallas simuladas, y el fichero
  reescrito.

## Gaps / Not Covered

- **`ScreenCycle.IndexOfCurrent()`, línea `return -1;` — gap aceptado, no
  un descuido.** Es un *fallback* defensivo para el caso "`Current` no
  está en `_screens`", que por construcción del tipo nunca ocurre: el
  constructor fija `Current = _screens[0]` y `Next()` siempre reasigna
  `Current` a otro elemento de la misma lista `_screens` — no hay ningún
  camino de código, público o interno, que deje `Current` con un valor
  ajeno a la lista. Forzar este camino requeriría reflexión para escribir
  el campo privado, lo que probaría la mecánica de reflexión, no el
  comportamiento real de la clase (ver `unit-testing-strategy.md`: "no
  escribir tests que no fallan cuando el comportamiento real se rompe").
- **`ScreenNavigator.OnSnapshotReceived`, rama `catch (Exception ex) when
  (ex is ObjectDisposedException or InvalidOperationException)` — mismo
  gap ya aceptado en ciclos anteriores (heredado de `UsagePage`, F1-F3).**
  Es la carrera benigna "`MainWindow` se cerró mientras la respuesta HTTP
  estaba en vuelo" — no reproducible de forma determinista en bUnit sin
  simular esa carrera exacta con temporización real. Documentado como
  gap aceptado también en `f3-ux-mascota-ciclo-b.md`.
- **`ScreenNavigator.IsPollingActiveForTests`/`Dispose()`, ramas del
  operador `?.` cuando `_coordinator`/`_dragServiceRef`/
  `_resizeServiceRef`/`_closeServiceRef` son `null` — gap aceptado.**
  En todos los tests de este fichero, `OnInitialized()`/
  `OnAfterRenderAsync()` ya se han ejecutado antes de leer estas
  propiedades o desmontar el componente (bUnit no deja observar un
  `ScreenNavigator` a medio inicializar), así que la rama `null` de estos
  guards defensivos no es alcanzable con el patrón de montaje síncrono de
  bUnit. Mismo criterio que el resto de guards de "sin instancia adjunta"
  ya aceptados en el proyecto (`WindowDragServiceTests`,
  `WindowCloseServiceTests`, etc., ver `f3-ux-mascota-ciclo-b.md`).
- No se ha escrito ningún test de integración (Testcontainers/DB/HTTP): no
  aplica — F3 Ciclo C no toca Domain (salvo `MascotState`, cubierto al
  100%) ni Application/Infrastructure; toda la superficie nueva es
  Desktop puro (Blazor/Razor) y una pieza pura de Domain, cubierta con
  xUnit/bUnit consistente con la política de testing de `CLAUDE.md`.
- `MainWindow.xaml` (cambio de `ComponentType`)/`_Imports.razor`
  (`@using` nuevo)/`wwwroot/css/app.css` (`.claudemeter-cycle`/`.mascot*`)
  — sin test dedicado, mismo criterio que ciclos anteriores: composition
  root/XAML y CSS puro sin lógica propia verificable de forma
  significativa con xUnit/bUnit; el efecto visual real (colores, opacidad
  en hover, posición) queda en la validación manual de la Definition of
  Done.

## Acceptance Criteria Coverage

| Acceptance Criterion | Covered by |
|---|---|
| US-1 GIVEN el contrato `IWidgetScreen` WHEN una página Razor lo implementa THEN expone el mínimo necesario sin que `ScreenNavigator` conozca detalles internos | `UsagePageTests.UsagePage_ImplementaIWidgetScreenConScreenIdUsage` + `MascotPageTests.MascotPage_ImplementaIWidgetScreenConScreenIdMascot` + `ScreenNavigatorTests` (ciclando `DummyScreenA`/`DummyScreenB`, tipos que `ScreenNavigator` nunca conoce más allá de `IWidgetScreen`) |
| US-1 GIVEN dos o más `IWidgetScreen` registradas WHEN gesto de cambio de pantalla THEN muestra la siguiente en orden cíclico sin recargar ni perder estado | `ScreenCycleTests.Next_ConDosPantallas_...`/`Next_ConTresPantallas_...` (lógica pura) + `ScreenNavigatorTests.ScreenNavigator_ConClicEnBotonDeCambioDePantalla_AlternaEntreLasDosPantallasSimuladas` + `ScreenNavigator_AlCambiarDePantallaTrasUnSnapshotYaRecibido_ConservaElMismoEstadoSinEsperarUnNuevoPoll` |
| US-1 GIVEN una única `IWidgetScreen` registrada WHEN gesto de cambio de pantalla THEN no hay error, ciclo trivial válido | `ScreenCycleTests.Next_ConUnaUnicaPantallaRegistrada_NoLanzaYSiempreDevuelveLaMisma` + `ScreenNavigatorTests.ScreenNavigator_ConUnaUnicaPantallaRegistrada_NoRenderizaElBotonDeCambioDePantalla` |
| US-1 GIVEN el estado actual (UsagePage único RootComponent) WHEN se completa la user story THEN `UsagePage`/`MascotPage` implementan `IWidgetScreen` y `ScreenNavigator` alterna entre ambas sin que ninguna conozca a la otra | `ScreenNavigatorTests.ScreenNavigator_ConDosPantallasSimuladas_RenderizaLaPrimeraAlMontarse` (con `ScreenTypes` de producción por defecto en el resto de tests del fichero, que renderiza `UsagePage` primero) + `UsagePageTests`/`MascotPageTests` (cada una probada de forma aislada, sin referenciar la otra) |
| US-2 GIVEN snapshot con éxito y ambas ventanas en `Normal` WHEN deriva `MascotState` THEN estado "tranquilo" visualmente distinguible | `MascotStateClassifierTests.Classify_ConAmbasVentanasNormal_DevuelveCalm` + `MascotPageTests.MascotPage_ConAmbasVentanasNormal_RenderizaElEstadoCalm` |
| US-2 GIVEN al menos una ventana en `Warning` (ninguna en `Critical`) WHEN deriva `MascotState` THEN estado de alerta intermedio | `MascotStateClassifierTests.Classify_ConAlMenosUnaVentanaEnWarningYNingunaEnCritical_DevuelveAlert` + `MascotPageTests.MascotPage_ConAlMenosUnaVentanaEnWarningYNingunaEnCritical_RenderizaElEstadoAlert` |
| US-2 GIVEN al menos una ventana en `Critical` WHEN deriva `MascotState` THEN "cerca del límite" con independencia de la otra ventana ("peor caso") | `MascotStateClassifierTests.Classify_ConAlMenosUnaVentanaEnCritical_DevuelveNearLimitConIndependenciaDeLaOtra` (Theory, 5 combinaciones simétricas) + `MascotPageTests.MascotPage_ConSesionEnCriticalYSemanaNormal_...` + `MascotPage_ConSemanaEnCriticalYSesionNormal_...` |
| US-2 GIVEN no hay datos disponibles o snapshot `Unauthorized` WHEN renderiza `MascotPage` THEN cuarto estado explícito "sin datos" | `MascotStateClassifierTests.Classify_ConAmbasVentanasSinDatoInterpretable_DevuelveNoData` + `MascotPageTests.MascotPage_SinDatosDisponiblesAunNoHuboExito_...` + `MascotPage_ConSnapshotUnauthorized_RenderizaElEstadoNoDataNoElUmbralAnterior` + `ScreenNavigatorTests.ScreenNavigator_ConRamaUnauthorized_ReseteaSessionYWeeklyAUnavailableEnVezDeConservarElUltimoValorParseado` (cubre la corrección de origen del dato, no solo el clasificador puro) |
| US-2 GIVEN `MascotState` ya resuelto WHEN llega un nuevo snapshot THEN se recalcula exclusivamente del nuevo, nunca de estado anterior ni histórico | `MascotStateClassifier.Classify` es una función pura sin estado (verificado por los 14 tests de `MascotStateClassifierTests`, cada uno invoca `Classify` de forma aislada) + `MascotPage.CurrentState` es una propiedad calculada en cada render (sin campo persistido) + `ScreenNavigatorTests.ScreenNavigator_TrasDosCiclosConsecutivosConExito_MuestraSiempreElUltimoSnapshotSinMezclar` (confirma que `ScreenNavigator` tampoco mezcla ciclos antes de pasar el `WidgetUsageState`) |
| US-2 GIVEN `MascotPage` implementando `IWidgetScreen` WHEN `ScreenNavigator` la muestra tras un cambio de pantalla THEN refleja el `MascotState` del último snapshot conocido sin esperar un nuevo poll | `ScreenNavigatorTests.ScreenNavigator_AlCambiarDePantallaTrasUnSnapshotYaRecibido_ConservaElMismoEstadoSinEsperarUnNuevoPoll` (mecanismo genérico, probado con pantallas simuladas) + `MascotPageTests` (prueba que `MascotPage` renderiza correctamente cualquier `WidgetUsageState` recibido, con independencia de cuándo llegó) — combinados verifican el mismo mecanismo que aplicaría a `MascotPage` real |

## Definition of Done

(Copiada literalmente de `docs/sdlc/requirements/f3-ux-mascota-ciclo-c.md`
y `docs/sdlc/design/f3-ux-mascota-ciclo-c.md`, ambas idénticas en este
punto.)

- [ ] Todas las Acceptance Criteria de US-1 y US-2 anteriores se cumplen —
      **las 9 ACs verificables por tests automáticos se cumplen** (ver
      tabla de trazabilidad de arriba: cobertura automática completa en
      las 9, combinando xUnit puro para la lógica de `MascotStateClassifier`/
      `ScreenCycle` con bUnit para el comportamiento observable de
      `MascotPage`/`UsagePage`/`ScreenNavigator`) — no queda marcada como
      100% cerrada solo con esta fase porque el propio ítem de validación
      manual de abajo sigue PENDIENTE.
- [x] Cobertura de tests automatizados según el mínimo configurado en
      `.claude/sdlc.config.yaml` (`testingCoverage: 70`): xUnit puro para
      `MascotStateClassifier` (100%/100%) y para `ScreenCycle`
      (95.65%/83.33%, gap aceptado y documentado); bUnit para
      `MascotPage.razor` (100%/100%, los 4 estados de consumo + "sin
      datos") y para el comportamiento de `ScreenNavigator` alternando
      entre `IWidgetScreen` simuladas (97.16%/84.61%, gaps aceptados y
      documentados) — **conseguido: 95.65%-100% en las seis clases
      nuevas/modificadas de este ciclo, todas por encima del umbral** —
      335/335 tests en verde en la solución completa
      (`dotnet test ClaudeMeter.sln`).
- [x] Ninguna prueba automatizada depende de un `UsageSnapshot` real de la
      API ni de interacción real de ratón/WebView2 — todos los tests
      nuevos/modificados de esta fase usan `FakeUsageDataSource` con
      snapshots simulados y, para `ScreenNavigator`, invocan directamente
      `ApplyForTests`/`cut.Find("button.claudemeter-cycle").Click()`
      (una interacción DOM simulada por bUnit, no un gesto de ratón real)
      — verificado por revisión de cada test nuevo de esta fase.
- [ ] **PENDIENTE — Validación manual:** se confirma, ejecutando la
      aplicación real en Windows, que (a) se puede alternar entre
      `UsagePage` y `MascotPage` con el botón de cambio de pantalla, (b)
      `MascotPage` refleja visualmente el estado correcto
      (tranquilo/alerta/cerca del límite/sin datos) en un escenario real
      de consumo, incluyendo al perder la credencial (401/403), y (c)
      ninguna funcionalidad ya entregada en F3/Ciclos A y B (tema
      claro/oscuro, countdown animado, arrastre, auto-ajuste de altura,
      click-through, icono de bandeja, cierre directo) sufre una
      regresión al introducir `ScreenNavigator` como nuevo
      `RootComponent` — ningún agente de este pipeline puede verificar
      renderizado real de WPF/WebView2 ni interacción real de ratón;
      `ScreenNavigatorTests`/`MascotPageTests`/`UsagePageTests` son la
      mitigación automatizable más cercana (la misma lógica de
      estado/orquestación y de clasificación de umbral, verificada de
      forma determinista), pero no sustituyen esta validación. Con este
      ítem PENDIENTE se cierra el milestone F3 completo salvo esta
      confirmación manual final.

---

Nada se ha confirmado (`commit`) ni enviado (`push`) al repositorio: los
ficheros de test nuevos
(`test/ClaudeMeter.Domain.Tests/Usage/MascotStateClassifierTests.cs`,
`test/ClaudeMeter.Desktop.Tests/Navigation/ScreenCycleTests.cs`,
`test/ClaudeMeter.Desktop.Tests/Navigation/ScreenNavigatorTests.cs`,
`test/ClaudeMeter.Desktop.Tests/Navigation/DummyScreenA.razor`,
`test/ClaudeMeter.Desktop.Tests/Navigation/DummyScreenB.razor`,
`test/ClaudeMeter.Desktop.Tests/Pages/MascotPageTests.cs`) y el
reescrito (`test/ClaudeMeter.Desktop.Tests/Pages/UsagePageTests.cs`)
quedan en el árbol de trabajo, listos para revisión, junto con el resto de
cambios de F3 Ciclo C ya presentes sin confirmar (dejados por
`sdlc-development`). Ningún fichero fuera de `test/` se ha tocado en esta
fase, salvo este propio informe.
