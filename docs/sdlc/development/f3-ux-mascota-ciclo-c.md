# Implementation Summary: F3 — UX + página Mascota, Ciclo C

## Design Reference

`docs/sdlc/design/f3-ux-mascota-ciclo-c.md` (US-1 `ScreenNavigator` +
`IWidgetScreen`, issue #18; US-2 `MascotPage.razor`, issue #19). Se ha
seguido su Implementation Plan paso a paso (pasos 1, 3, 4, 6, 7, 8, 9, 10,
11 y 15; los pasos 2, 5, 12, 13 y 14 son de tests xUnit/bUnit y quedan
fuera de esta fase — ver Deviations y Follow-ups). Con este ciclo se cierra
el milestone F3 completo.

## Files Changed

- `src/ClaudeMeter.Domain/Usage/MascotState.cs` (nuevo) — US-2. Enum
  `MascotState` (`Calm`/`Alert`/`NearLimit`/`NoData`) +
  `MascotStateClassifier.Classify(RateLimitWindow session, RateLimitWindow
  weekly)`, función pura que reutiliza `UsageThresholdClassifier` y aplica
  la regla de "peor caso" (severidad máxima de las dos ventanas; `NoData`
  solo si ninguna de las dos tiene dato interpretable).
- `src/ClaudeMeter.Desktop/Navigation/IWidgetScreen.cs` (nuevo) — US-1.
  Contrato mínimo (`ScreenId`) tal cual Data Model.
- `src/ClaudeMeter.Desktop/Navigation/ScreenCycle.cs` (nuevo) — US-1.
  Ciclado por índice sobre `IReadOnlyList<Type>`, sin Blazor/WPF; incluye
  `ScreenCount` (omitida del snippet de diseño por brevedad, añadida aquí).
- `src/ClaudeMeter.Desktop/Navigation/WidgetUsageState.cs` (nuevo) — DTO
  compartido (`Session`, `Weekly`, `Status`, `IsStale`) + `Initial`, tal
  cual Data Model.
- `src/ClaudeMeter.Desktop/Navigation/ScreenNavigator.razor` (nuevo) —
  nuevo `RootComponent` real. Absorbe íntegramente el `@code` de
  polling/chime/tema/JS-interop que tenía `UsagePage` (coordinador,
  `PollingControlService`, los tres `DotNetObjectReference` de
  drag/resize/close, botón de cierre, `.claudemeter-root` con clase de
  tema), añade el botón `.claudemeter-cycle` y el `<DynamicComponent>` que
  renderiza la pantalla activa (`UsagePage`/`MascotPage` por defecto).
  Incluye la corrección de la rama `Unauthorized` de `Apply()`: ahora
  resetea `_session`/`_weekly` a `RateLimitWindow.Unavailable` (antes,
  heredado de F2, los dejaba con el último valor parseado con éxito).
- `src/ClaudeMeter.Desktop/Pages/UsagePage.razor` (modificado en
  profundidad) — pasa de "dueño de su ciclo de vida" a componente
  puramente presentacional: `[Parameter, EditorRequired] WidgetUsageState
  State`, `@implements IWidgetScreen`, renderiza `UsageBar`x2 o
  `ReauthNotice` según `State.Status`. Pierde toda inyección de servicios
  de polling/ventana.
- `src/ClaudeMeter.Desktop/Pages/MascotPage.razor` (nuevo) — US-2.
  `[Parameter, EditorRequired] WidgetUsageState State`, `@implements
  IWidgetScreen`, deriva `MascotState` vía
  `MascotStateClassifier.Classify(State.Session, State.Weekly)` en cada
  render y renderiza una de 4 representaciones (`.mascot--calm/alert/
  near-limit/no-data`).
- `src/ClaudeMeter.Desktop/MainWindow.xaml` — `RootComponent.ComponentType`
  pasa de `{x:Type pages:UsagePage}` a `{x:Type navigation:ScreenNavigator}`;
  `xmlns:pages` sustituido por `xmlns:navigation` (ya no queda ninguna
  referencia a `pages:` en el fichero). Sin cambios en
  `MainWindow.xaml.cs`/`App.xaml.cs` (los cuatro servicios de ciclo de vida
  ya estaban registrados como singleton desde Ciclos A/B; este ciclo solo
  mueve quién los inyecta).
- `src/ClaudeMeter.Desktop/_Imports.razor` — añade `@using
  ClaudeMeter.Desktop.Navigation` (ver Deviations).
- `src/ClaudeMeter.Desktop/wwwroot/css/app.css` — añade `.claudemeter-cycle`
  (mismo tratamiento visual que `.claudemeter-close`, esquina opuesta) y
  `.mascot`/`.mascot__icon`/`.mascot__label`/`.mascot--{calm,alert,
  near-limit,no-data}` (reutilizan las custom properties de umbral ya
  existentes: `--color-normal/warning/critical/neutral`).

## Deviations from the Design

- **`ScreenCycle.Next()` no compilaba tal cual el Data Model:**
  `IReadOnlyList<Type>.IndexOf(...)` no existe (solo `IList<T>` lo expone;
  el compilador intentaba resolverlo como `MemoryExtensions.IndexOf` sobre
  `ReadOnlySpan<Type>`, incompatible). Sustituido por una búsqueda lineal
  propia (`IndexOfCurrent()`, método privado) en vez de ampliar el tipo del
  campo a `IList<Type>` — mantiene el contrato del constructor tal cual lo
  fija el documento ("cualquier lista de solo lectura de tipos").
- **`ScreenNavigator._screenParameters` no compilaba tal cual el Data
  Model:** `DynamicComponent.Parameters` exige `IDictionary<string,
  object>`, no `IReadOnlyDictionary<string, object>` (el tipo usado en el
  snippet de diseño). Cambiado el tipo del campo a `IDictionary<string,
  object>`; sin ningún otro cambio de comportamiento.
- **`_Imports.razor`: se añadió `@using ClaudeMeter.Desktop.Navigation`
  global**, no listado explícitamente como fichero a modificar en el
  Implementation Plan (aunque sí implícito, ya que `MainWindow.xaml`
  necesita el namespace vía `xmlns:navigation` y los ficheros `.razor`
  nuevos ya traían su propio `@using ClaudeMeter.Desktop.Navigation`
  local). Se añadió por consistencia con el resto de namespaces de Desktop
  ya importados globalmente (`Windowing`, `Polling`, `Pages`); es
  redundante con los `@using` locales que ya trae cada fichero (tal cual el
  Data Model), pero no genera advertencia de duplicado — build verificado
  sin warnings.
- Ambas desviaciones de compilación son mecánicas (ajuste de tipo/algoritmo
  para que el snippet del documento compile), sin cambiar ningún
  comportamiento observable descrito por el diseño.
- Los pasos 2, 5, 12, 13 y 14 del Implementation Plan (tests xUnit de
  `MascotStateClassifier`/`ScreenCycle`, migración de
  `UsagePageTests.cs`, tests bUnit de `ScreenNavigator` alternando
  pantallas simuladas y de `MascotPage`) quedan **fuera de esta fase** por
  restricción explícita de este agente (Development nunca escribe ni
  ejecuta tests) — responsabilidad de `sdlc-testing`, ver Follow-ups.

## How to Verify

1. `dotnet build src/ClaudeMeter.Desktop/ClaudeMeter.Desktop.csproj -c
   Debug` y `-c Release` — ambos compilan sin advertencias ni errores
   (verificado en esta sesión; Release incluye analizadores +
   `TreatWarningsAsErrors`). `dotnet build src/ClaudeMeter.Domain/...` en
   Debug también verificado, limpio.
2. `dotnet build ClaudeMeter.sln -c Debug` — falla **solo** en
   `test/ClaudeMeter.Desktop.Tests/Pages/UsagePageTests.cs` (68 errores
   `CS1061`, todos por referenciar miembros que ya no existen en
   `UsagePage` — `ApplyForTests`/`IsPollingActiveForTests`/
   `StatusForTests`, ahora en `ScreenNavigator`). Ningún otro proyecto de
   `src/` ni de `test/` se ve afectado — confirmado revisando la lista
   completa de ficheros con error en la salida del build. Este es
   exactamente el efecto anticipado por el paso 8 del Implementation Plan
   del documento de diseño.
3. Ejecutar `ClaudeMeter.Desktop.exe` en Windows real y confirmar
   manualmente (Definition of Done del documento de diseño, fuera del
   alcance de este agente):
   - Al arrancar se muestra `UsagePage` (dos `UsageBar`, Sesión/Semana).
   - El botón `⇄` (visible solo en hover, esquina superior izquierda)
     alterna a `MascotPage` y de vuelta, sin perder el snapshot ya
     calculado ni reiniciar el polling.
   - `MascotPage` refleja el estado correcto (tranquilo/alerta/cerca del
     límite/sin datos) según el consumo real, y al perder la credencial
     (401/403) muestra el cuarto estado "sin datos" (no el umbral del
     último snapshot exitoso).
   - Ninguna funcionalidad de Ciclos A/B sufre regresión: tema claro/oscuro,
     countdown animado, arrastre, auto-ajuste de altura, click-through,
     icono de bandeja (Pausar/Reanudar/Recargar/Click-through/Salir) y
     cierre directo (botón `✕`) siguen funcionando igual, con
     independencia de qué pantalla esté visible.

## Follow-ups / Known Limitations

- Cobertura de tests automatizados pendiente de `sdlc-testing`, según el
  Implementation Plan del documento de diseño:
  - xUnit puro: `MascotStateClassifierTests.cs` (Domain, paso 2) y
    `ScreenCycleTests.cs` (Desktop, paso 5).
  - Migración obligatoria de `test/ClaudeMeter.Desktop.Tests/Pages/
    UsagePageTests.cs` (paso 12): dividir en un nuevo
    `Navigation/ScreenNavigatorTests.cs` (recibe los tests de
    polling/chime/tema/botón de cierre/pausa/desmontaje, montando ahora
    `ScreenNavigator`, más un test nuevo de que la rama `Unauthorized`
    deja `_session`/`_weekly` en `Unavailable`) y un `UsagePageTests.cs`
    reescrito y mucho más simple (monta `UsagePage` directamente pasando
    `WidgetUsageState`, sin `RegisterCoreServices`).
  - bUnit: `ScreenNavigatorCyclingTests.cs` (paso 13, pantallas simuladas
    `DummyScreenA`/`DummyScreenB`) y `Pages/MascotPageTests.cs` (paso 14,
    los 4 estados + los casos simétricos de "peor caso").
- Sin cambios en `App.xaml.cs`/`MainWindow.xaml.cs` (confirmado, ver Files
  Changed) — ningún riesgo de doble registro de servicios.
- Emoji/etiquetas textuales de `MascotPage` son un placeholder razonable
  (el propio documento de diseño lo deja así, Requirements no fija la
  redacción final) — sustituibles sin cambio estructural si se pide otra
  ilustración.
