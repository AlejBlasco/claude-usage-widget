# Requirements: F3 — UX + página Mascota, Ciclo C

## Source

Milestone **F3 — UX + página Mascota**
(https://github.com/AlejBlasco/claude-usage-widget/milestone/4), dos issues
agrupadas porque la #19 depende explícitamente del navegador de pantallas
que define la #18:

- **#18** — [F3] ScreenNavigator + interfaz IWidgetScreen —
  https://github.com/AlejBlasco/claude-usage-widget/issues/18
- **#19** — [F3] MascotPage.razor — estado derivado del snapshot —
  https://github.com/AlejBlasco/claude-usage-widget/issues/19

Ambas leídas tal como están en GitHub en el momento de redactar este
documento (ambas en estado Open, milestone F3, label "enhancement", sin
asignar, sin PR asociada).

Con este ciclo se cierra el milestone F3 completo: Ciclo A (#14 tema
claro/oscuro + #15 animación del countdown) y Ciclo B (#16 click-through +
#17 icono de bandeja) ya están commiteados en la rama actual
(`features/f3-UX-mascota`, commits `97206e7` y `acb13b6`). Este documento
cubre el trabajo restante del milestone.

Se estructura en dos user stories (una por issue) porque son capacidades
observables distinguibles — "existe un mecanismo genérico para navegar
entre páginas del widget" (#18) vs. "existe una página Mascota concreta que
usa ese mecanismo y deriva su estado del snapshot" (#19) — pero con una
dependencia secuencial real y no artificial: la propia issue #19 declara
"Depends on: ScreenNavigator + IWidgetScreen interface", y su Acceptance
Criteria exige que `MascotPage` implemente `IWidgetScreen`, un tipo que no
existe hasta que se resuelve US-1.

## Punto de partida verificado en el repositorio

- **`UsagePage` es hoy el único `RootComponent` de `BlazorWebView`.**
  Confirmado en `src/ClaudeMeter.Desktop/MainWindow.xaml` (líneas 34-39):
  `BlazorWebView.RootComponents` registra un único `RootComponent` con
  `Selector="#app"` y `ComponentType="{x:Type pages:UsagePage}"`. No existe
  hoy ningún mecanismo para que WPF/`BlazorWebView` alterne entre más de un
  componente raíz.
- **Qué cambia exactamente al introducir `ScreenNavigator`/`IWidgetScreen`
  — hecho verificado, decisión explícitamente para Design.** Dos formas
  serían consistentes con el AC de la issue #18 ("`UsagePage` y
  `MascotPage` implementan `IWidgetScreen`"): (a) `ScreenNavigator` pasa a
  ser el nuevo valor de `ComponentType` en `MainWindow.xaml`, y `UsagePage`
  se convierte en una de las pantallas que `ScreenNavigator` renderiza
  internamente según el `IWidgetScreen` activo; o (b) alguna variante donde
  `MainWindow.xaml` sigue apuntando a un componente distinto y
  `ScreenNavigator` vive dentro de él. Ninguna de las dos issues especifica
  cuál, y no es una ambigüedad de producto (el comportamiento observable
  para el usuario — poder alternar entre `UsagePage` y `MascotPage` — es
  idéntico en ambos casos) sino una decisión de composición de componentes
  Razor/WPF que corresponde al Software Architect. Este documento se limita
  a dejar constancia del punto de partida real.
- **Servicios que hoy `UsagePage.razor` inyecta y gestiona directamente en
  su propio ciclo de vida — hecho verificado con precisión, línea a línea:**
  revisado `src/ClaudeMeter.Desktop/Pages/UsagePage.razor`. `UsagePage`
  declara `@inject` para `WindowDragService`, `WindowResizeService`,
  `PollingControlService` y `WindowCloseService`, y los gestiona así:
  - `OnInitialized()`: crea su propio `UsagePollingCoordinator`, lo arranca
    y lo adjunta a `PollingControlService` vía `AttachCoordinator` (más la
    suscripción a `PollingControl.PauseStateChanged`).
  - `OnAfterRenderAsync(firstRender)`: solo en el primer render, crea un
    `DotNetObjectReference` para `DragService`, `ResizeService` y
    `CloseService` cada uno, y registra tres listeners JS independientes
    (`claudeMeterDrag.init`, `claudeMeterResize.init`,
    `claudeMeterClose.init`), cada uno en su propio `try/catch` con log de
    error si el interop falla.
  - `Dispose()`: desuscribe `PauseStateChanged`, libera las tres
    `DotNetObjectReference`, desadjunta y libera el `UsagePollingCoordinator`
    de `PollingControlService`.
  Ninguna de las dos issues (#18, #19) dice si estos cuatro servicios deben
  pasar a vivir en `ScreenNavigator` (si éste se convierte en el
  `RootComponent` real, sería el único componente Razor garantizado montado
  mientras la aplicación viva, con independencia de qué `IWidgetScreen` esté
  visible) o si cada `IWidgetScreen` (incluida una futura `MascotPage`) debe
  seguir registrando/liberando su propia copia igual que hace `UsagePage`
  hoy. Esto importa de forma muy concreta: el arrastre (`WindowDragService`)
  y el cierre directo (`WindowCloseService`) son gestos sobre la ventana
  completa, no sobre el contenido de una pantalla concreta, y
  `PollingControlService` expone un único coordinador adjuntado a la vez
  (`AttachCoordinator`/`DetachCoordinator`) — si `MascotPage` no consume el
  mismo `UsageSnapshot` que hoy produce el `UsagePollingCoordinator` de
  `UsagePage`, o si ambas pantallas intentan adjuntar su propio coordinador
  simultáneamente, el comportamiento de pausar/reanudar desde la bandeja
  (F3/Ciclo B) podría romperse. Se documenta el hecho tal cual está hoy sin
  proponer una solución — es trabajo de `sdlc-design`.
- **`MascotState` no existe todavía en ningún sitio del código.** Búsqueda
  `grep -r "MascotState" src/` sin resultados; las únicas menciones del
  repositorio están en `CLAUDE.md`, `README.md` y en la documentación ya
  generada de F1/F2 (`docs/sdlc/design/f1-widget-visual-base.md`,
  `docs/sdlc/technical/f1-widget-visual-base.md`,
  `docs/sdlc/design/f2-robustez-ciclo-b.md`,
  `docs/sdlc/design/issue-02-leer-token-credentials-json.md`), todas como
  mención de roadmap futuro, no como tipo ya declarado. Confirmado también
  que `ScreenNavigator`/`IWidgetScreen` no existen en ningún fichero bajo
  `src/`. Este ciclo es, por tanto, trabajo completamente nuevo tanto en
  Domain (`MascotState`) como en Desktop (`ScreenNavigator`,
  `IWidgetScreen`, `MascotPage.razor`) — no una extensión de algo existente.
- **Restricción explícita de `CLAUDE.md` (citada tal cual):**
  *"`MascotPage.razor` driven only by the current snapshot (no history
  dependency)"*. El estado de la mascota debe derivarse únicamente del
  `UsageSnapshot`/`RateLimitWindow` actual — el mismo dato que ya consume
  `UsagePage` vía `RateLimitWindowParser.ParseSnapshot` y
  `UsageThresholdClassifier` — sin leer ni depender en ningún momento de
  `IUsageHistoryStore` ni de ningún otro dato histórico. Eso es
  explícitamente F4 (`PeakOffPeakAnalyzer`), fuera de alcance de este
  ciclo. Coincide con el propio AC de la issue #19 ("`MascotState` deriva
  de `UsageSnapshot` sin requerir datos históricos").
- **Piezas ya disponibles de los Ciclos A/B de F3 que este ciclo puede
  reutilizar — verificadas, no asumidas:**
  - `AppConfig.Theme` / `AppThemeExtensions.ToCssClass()`
    (`src/ClaudeMeter.Desktop/Configuration/AppConfig.cs`, F3/Ciclo A):
    el propio XMLDoc de `AppThemeExtensions` documenta explícitamente que es
    "Única fuente de verdad del mapeo para que cualquier página Razor
    futura (p. ej. `MascotPage`, Ciclo C) no lo duplique" — confirma que el
    diseño de Ciclo A ya se estructuró pensando en esta reutilización.
    `UsagePage.razor` lo aplica hoy en un único `<div class="claudemeter-root
    @Config.Theme.ToCssClass()">` que envuelve todo su contenido; el mismo
    patrón está disponible para `MascotPage`.
  - `CountdownFormatter`
    (`src/ClaudeMeter.Desktop/Rendering/CountdownFormatter.cs`, F3/Ciclo A):
    función pura que formatea `RateLimitWindow.MinutesRemaining` (`int`) de
    forma compacta ("Xd Yh" / "Xh Ym" / "Xm", con "0m" para el caso cero).
    Reutilizable si `MascotPage` decide mostrar también el countdown.
  - `WindowResizeService`
    (`src/ClaudeMeter.Desktop/Windowing/WindowResizeService.cs`, F3/Ciclo
    A): ajusta `Window.Height` a la altura real del contenido Razor vía
    `ResizeObserver`/interop JS (`SetContentHeight`, `[JSInvokable]`),
    ancla el borde inferior. Relevante si `MascotPage` tiene un layout de
    altura distinta a `UsagePage` (280x140px hoy) — el servicio ya existe y
    es agnóstico de qué página lo invoca, no haría falta un mecanismo
    nuevo.
  - `UsageThresholdClassifier`
    (`src/ClaudeMeter.Domain/Usage/UsageThreshold.cs`, Domain, desde
    F1/F2): función pura `Classify(double? percentageUsed) ->
    UsageThreshold?` con tres buckets (`Normal` &lt;70, `Warning` [70,90),
    `Critical` &gt;=90) y `null` explícito para "sin datos". Es la señal de
    entrada más directa y ya probada para derivar `MascotState` sin
    depender de historial, reutilizando exactamente el mismo umbral que ya
    usa `UsageBar` para los colores verde/ámbar/rojo — evita que la mascota
    y las barras "cuenten historias distintas" sobre el mismo porcentaje.

## US-1: ScreenNavigator + interfaz IWidgetScreen (issue #18)

**As a** desarrollador del proyecto,
**I want** un `ScreenNavigator` y una interfaz `IWidgetScreen` para ciclar
entre las páginas del widget,
**so that** añadir una página nueva (p. ej. `MascotPage`, y en el futuro
`PeakOffPeakPage` de F4) no requiera modificar el resto de la navegación.

### Acceptance Criteria

- **GIVEN** el contrato `IWidgetScreen` tal como lo exige la issue #18
  **WHEN** una página Razor concreta lo implementa
  **THEN** expone el mínimo necesario para que `ScreenNavigator` pueda
  mostrarla y ciclar hacia/desde ella sin conocer ningún detalle interno de
  esa página (p. ej. un tipo de componente Razor renderizable y, si aplica,
  un identificador/etiqueta de pantalla) — ninguna página necesita exponer
  más superficie que esa para integrarse

- **GIVEN** `ScreenNavigator` con dos o más `IWidgetScreen` registradas
  **WHEN** el usuario realiza el gesto de cambio de pantalla (clic o el
  gesto que Design determine, ver Technical Notes)
  **THEN** `ScreenNavigator` muestra la siguiente pantalla registrada en
  orden cíclico (tras la última, vuelve a la primera), sin recargar ni
  perder el estado de las pantallas que no cambian de estado (p. ej. el
  polling de `UsagePage` sigue el criterio que Design determine para los
  servicios compartidos — ver Punto de partida verificado)

- **GIVEN** `ScreenNavigator` con una única `IWidgetScreen` registrada (caso
  límite, p. ej. durante una migración incremental)
  **WHEN** el usuario realiza el gesto de cambio de pantalla
  **THEN** no ocurre ningún error ni excepción — el ciclo trivial "vuelve a
  la misma pantalla" es un comportamiento válido, no un caso a rechazar

- **GIVEN** el estado actual del código, donde `UsagePage` es hoy el único
  `RootComponent` de `BlazorWebView` (ver Punto de partida verificado)
  **WHEN** se completa esta user story
  **THEN** tanto `UsagePage` como `MascotPage` (US-2 de este documento)
  implementan `IWidgetScreen`, y `ScreenNavigator` es el mecanismo mediante
  el cual el widget alterna entre ambas sin que ninguna de las dos conozca
  la existencia de la otra directamente

## US-2: MascotPage.razor — estado derivado del snapshot (issue #19)

**As a** usuario del widget,
**I want** una página Mascota ("Clawd") que reaccione al estado actual de
consumo,
**so that** tenga una señal visual de mi consumo sin depender de datos
históricos.

### Acceptance Criteria

- **GIVEN** un `UsageSnapshot` con éxito y ambas ventanas (`Session`,
  `Weekly`) disponibles, con `UsageThresholdClassifier.Classify` devolviendo
  `Normal` para ambas
  **WHEN** `MascotPage` deriva su `MascotState` a partir de ese snapshot
  **THEN** se resuelve un estado "tranquilo" (consumo bajo, sin ninguna
  ventana en `Warning`/`Critical`), y `MascotPage.razor` lo renderiza con
  una representación visual distinguible de los otros estados

- **GIVEN** el mismo snapshot pero con al menos una de las dos ventanas en
  `Warning` (y ninguna en `Critical`)
  **WHEN** se deriva `MascotState`
  **THEN** se resuelve un estado intermedio de alerta, visualmente distinto
  tanto del estado "tranquilo" como del estado "cerca del límite"

- **GIVEN** el mismo snapshot pero con al menos una de las dos ventanas
  (`Session` o `Weekly`) en `Critical`
  **WHEN** se deriva `MascotState`
  **THEN** se resuelve el estado "cerca del límite", con independencia de
  en qué estado esté la otra ventana — **decisión por defecto documentada
  en Technical Notes:** cuando `Session` y `Weekly` clasifican en umbrales
  distintos, `MascotState` refleja siempre el más severo de los dos (el
  "peor caso"), nunca un promedio ni solo una de las dos ventanas fija

- **GIVEN** no hay datos disponibles todavía (nunca hubo un snapshot con
  éxito) o el snapshot está en estado `Unauthorized`
  (`UsageThresholdClassifier.Classify` devuelve `null` para ambas ventanas,
  igual que hoy hace `RateLimitWindow.Unavailable`)
  **WHEN** se renderiza `MascotPage`
  **THEN** se muestra un cuarto estado explícito de "sin datos" (no se
  fuerza a "tranquilo" ni a ningún otro de los tres estados de consumo, que
  presupondrían un dato real), sin excepción ni estado visual inconsistente

- **GIVEN** `MascotState` ya resuelto para un ciclo de poll
  **WHEN** llega un nuevo `UsageSnapshot` en el siguiente ciclo (con o sin
  cambio de umbral respecto al anterior)
  **THEN** `MascotState` se recalcula exclusivamente a partir de ese nuevo
  snapshot — nunca a partir de ningún estado anterior de la mascota ni de
  ningún dato histórico, cumpliendo la restricción explícita de `CLAUDE.md`
  citada en Punto de partida verificado

- **GIVEN** `MascotPage` implementando `IWidgetScreen` (US-1)
  **WHEN** `ScreenNavigator` la muestra tras un ciclo de cambio de pantalla
  **THEN** refleja el `MascotState` correspondiente al último snapshot
  conocido en ese momento, sin esperar a un nuevo ciclo de poll para
  mostrar un estado coherente (no aparece en blanco ni en un estado por
  defecto distinto del real)

## Technical Notes

- **Gesto exacto de cambio de pantalla en `ScreenNavigator` — sugerido, no
  decisión cerrada:** la propia issue #18 dice "clic o gesto", sin
  concretar cuál. Un clic sobre una zona no ocupada por contenido
  interactivo (similar al patrón ya usado para el botón de cierre directo
  de F3/Ciclo B, que reenvía el evento vía interop JS a un método
  `[JSInvokable]`) es consistente con el resto del widget y evita el mismo
  problema de "airspace" de WebView2 ya documentado en ciclos anteriores.
  Se deja como sugerencia para Design.
- **Ubicación del `RootComponent` y de los cuatro servicios de ciclo de
  vida (`WindowDragService`, `WindowResizeService`, `WindowCloseService`,
  `PollingControlService`) — decisión explícitamente para Design, no
  cerrada aquí:** ver el hecho verificado en detalle en "Punto de partida
  verificado en el repositorio". Cualquiera de las dos alternativas
  (servicios centralizados en `ScreenNavigator` si pasa a ser el
  `RootComponent` real, o cada `IWidgetScreen` gestionando su propia copia
  como hace `UsagePage` hoy) es compatible con las Acceptance Criteria de
  este documento; ninguna de las dos issues fuerza una u otra.
- **Combinación de `Session`/`Weekly` en un único `MascotState` — resuelto
  como valor por defecto, no como pregunta abierta:** ni la issue #19 ni
  `CLAUDE.md` especifican qué ocurre cuando las dos ventanas caen en
  umbrales distintos. Se adopta "gana el más severo" (peor caso) como
  comportamiento por defecto: el propósito declarado de la mascota es dar
  una señal visual de consumo, y una mascota "tranquila" mientras una de
  las dos ventanas está en `Critical` transmitiría una falsa sensación de
  margen — el mismo razonamiento que ya aplica `UsagePage` al mostrar dos
  `UsageBar` independientes en vez de una sola combinada, salvo que aquí
  `MascotState` es un único valor y por tanto necesita explícitamente esta
  regla de combinación.
- **Etiquetas/copys exactos de los estados ("idle", "relaxed", "near
  limit" según el texto de la issue) — no son una decisión funcional de
  este documento:** el requisito funcional verificable es que existan al
  menos tres estados de consumo distinguibles, derivados 1:1 de los tres
  buckets de `UsageThresholdClassifier` (`Normal`/`Warning`/`Critical`),
  más un cuarto estado explícito de "sin datos" (ver AC de US-2). La
  redacción textual/visual final de cada estado (nombre, ilustración,
  color) es una decisión de Design/implementación, no de negocio.
- **`MascotState` en Domain — forma sugerida, no decisión cerrada:** un
  enum análogo a `UsageThreshold` (p. ej. `Idle`/`Calm`, `Warning`,
  `NearLimit`, más un valor explícito para "sin datos", o bien reutilizar
  `UsageThreshold?` envuelto en un tipo propio) más una función pura
  `MascotStateClassifier`/`MascotState.From(UsageSnapshot, DateTimeOffset)`
  que internamente llame a `RateLimitWindowParser.ParseSnapshot` y
  `UsageThresholdClassifier.Classify` sobre ambas ventanas y aplique la
  regla de "peor caso" de más arriba — manteniendo el mismo estándar de
  "función pura, sin E/S, sin reloj propio" que ya siguen
  `RateLimitWindowParser` y `UsageThresholdClassifier`. Se deja a Design
  decidir la forma exacta del tipo.

## Dependencies

- **Depends on:** `UsagePage.razor` (F1, ya mergeado) — satisfecho, es la
  pantalla existente que debe pasar a implementar `IWidgetScreen` junto a
  `MascotPage` (`Depends on` original de la issue #18).
- **Depends on (interno a este documento):** US-2 (`MascotPage.razor`)
  depende de que US-1 (`ScreenNavigator`/`IWidgetScreen`) esté resuelta
  primero — dependencia secuencial real, no artificial, declarada
  explícitamente por la propia issue #19 ("Depends on: ScreenNavigator +
  IWidgetScreen interface") y reflejada en su AC ("`MascotPage` implementa
  `IWidgetScreen`").
- **Related to:** `MascotPage.razor` (issue #19, este mismo documento) —
  la propia issue #18 la menciona como "Related to" en su redacción
  original; resuelto al agrupar ambas issues en este ciclo.
- **Related to:** `UsageThresholdClassifier` (Domain, F1/F2, ya mergeado) —
  señal de entrada reutilizada para derivar `MascotState` sin depender de
  historial (ver Punto de partida verificado).
- **Related to:** `AppConfig.Theme`/`AppThemeExtensions.ToCssClass()`,
  `CountdownFormatter`, `WindowResizeService` (F3/Ciclo A, ya en el árbol
  de trabajo) — piezas reutilizables por `MascotPage` sin duplicar reglas
  de color/formato/ajuste de altura (ver Punto de partida verificado).
- **Open question:** ninguna — las ambigüedades detectadas (gesto exacto de
  cambio de pantalla, ubicación del `RootComponent`/servicios de ciclo de
  vida, combinación de umbrales `Session`/`Weekly`, etiquetas textuales
  exactas de los estados de la mascota) se han resuelto con valores por
  defecto documentados en Technical Notes o dejado explícitamente como
  decisión de Design cuando el comportamiento observable para el usuario no
  cambia según la alternativa elegida.

## Risks

- **Regresión de UX (arrastre/redimensionado/cierre/click-through):**
  introducir `ScreenNavigator` como posible nuevo `RootComponent` obliga a
  reubicar (o no) los cuatro servicios que hoy gestiona `UsagePage`
  directamente (`WindowDragService`, `WindowResizeService`,
  `WindowCloseService`, `PollingControlService`). Un reparto incorrecto
  podría romper gestos ya entregados y verificados manualmente en F3/Ciclos
  A y B (arrastre, autoajuste de altura, cierre directo, pausar/reanudar
  desde bandeja) al cambiar de pantalla. Impact: Medium.
- **Consistencia de datos entre pantallas:** si `MascotPage` termina
  creando su propio `UsagePollingCoordinator` en vez de compartir el mismo
  snapshot que `UsagePage`, ambas pantallas podrían mostrar información
  ligeramente desincronizada (dos fetches independientes) o duplicar
  llamadas a la API — cubierto como advertencia en Punto de partida
  verificado, decisión pendiente de Design. Impact: Medium.
- **Alcance del nuevo tipo Domain (`MascotState`):** riesgo de que el
  nuevo tipo acabe acoplado a conceptos de presentación (colores, texto)
  si no se mantiene con la misma disciplina que `UsageThreshold`
  ("sin ninguna noción de color ni de UI"). Mitigado por Technical Notes
  sugiriendo replicar ese mismo patrón. Impact: Low.
- **Cierre de milestone:** este es el último ciclo de F3; cualquier
  regresión introducida aquí sobre Ciclos A/B ya commiteados (tema,
  countdown, click-through, bandeja) queda expuesta solo al ejecutar la
  aplicación real, no solo con tests automatizados de este ciclo. Impact:
  Medium — mitigado por la Definition of Done exigiendo validación manual
  explícita de que nada de lo ya entregado se rompe.
- **Performance/Data quality:** N/A — ninguna de las dos user stories
  introduce llamadas de red nuevas ni datos nuevos; ambas derivan de datos
  que ya se calculan hoy (`UsageSnapshot`, `RateLimitWindow`,
  `UsageThreshold`).

## Definition of Done

- [ ] Todas las Acceptance Criteria de US-1 y US-2 anteriores se cumplen
- [ ] Cobertura de tests automatizados según el mínimo configurado en
      `.claude/sdlc.config.yaml` (`testingCoverage: 70`): xUnit puro para
      `MascotState`/su clasificador (Domain, incluyendo el caso de
      combinación `Session`/`Weekly` en umbrales distintos y el caso "sin
      datos") y para cualquier lógica de ciclo de `ScreenNavigator` que no
      dependa de WPF real; bUnit para `MascotPage.razor` (AC explícita de
      la propia issue #19: "Coverage by bUnit tests") cubriendo al menos
      los tres estados de consumo más el estado "sin datos", y para el
      comportamiento de `ScreenNavigator` alternando entre `IWidgetScreen`
      simuladas
- [ ] Ninguna prueba automatizada depende de un `UsageSnapshot` real de la
      API ni de interacción real de ratón/WebView2 — los tests usan
      snapshots simulados y, para `ScreenNavigator`, invocan directamente
      el método de cambio de pantalla en vez de simular el gesto de UI real
- [ ] **Validación manual:** se confirma, ejecutando la aplicación real en
      Windows, que (a) se puede alternar entre `UsagePage` y `MascotPage`
      con el gesto definido por Design, (b) `MascotPage` refleja
      visualmente el estado correcto (tranquilo/alerta/cerca del
      límite/sin datos) en un escenario real de consumo, y (c) ninguna
      funcionalidad ya entregada en F3/Ciclos A y B (tema claro/oscuro,
      countdown animado, click-through, icono de bandeja con
      pausar/reanudar/recargar/salir, cierre directo) sufre una regresión
      al introducir `ScreenNavigator` — ningún agente de este pipeline
      puede verificar renderizado real de WPF/WebView2 ni interacción real
      de ratón
