# Requirements: F2 — Robustez, Ciclo B

## Source

Milestone **F2 — Robustez**, GitHub issue única **#13** —
https://github.com/AlejBlasco/claude-usage-widget/issues/13
("[F2] config.json — intervalo, posición, chime").

A diferencia del Ciclo A de F2 (`docs/sdlc/requirements/f2-robustez-ciclo-a.md`,
que agrupó 3 issues encadenadas #10→#11→#12), este ciclo cubre **una única
issue**, ampliada por el usuario durante esta conversación respecto a su
redacción original: los 4 Acceptance Criteria originales de `config.json`
(lectura al arrancar con valores por defecto, intervalo de polling
configurable, posición inicial configurable, chime activable/desactivable)
se mantienen, y el usuario añadió **2 Acceptance Criteria nuevos** sobre
arrastrar el widget con el ratón para reposicionarlo en caliente y persistir
esa nueva posición en `config.json` para el siguiente arranque. Este
documento refleja la issue **tal como está en GitHub en el momento de
redactar este documento** (`updated_at: 2026-09-17T09:15:55Z`), no su
versión original.

Se estructura en dos user stories relacionadas pero distinguibles — US-1
("configuración vía `config.json`") y US-2 ("arrastrar y persistir
posición") — porque, aunque ambas tocan la misma issue y comparten el mismo
fichero `config.json` como superficie de persistencia, son dos capacidades
observables por separado: US-1 es "leer configuración al arrancar", US-2 es
"escribir configuración en caliente a partir de un gesto de usuario".

## Punto de partida verificado en el repositorio

- **No existe ningún lector/modelo de configuración todavía.** Se ha
  buscado en `src/` cualquier clase tipo `AppConfig`/`ConfigReader`/
  `ConfigLoader`/referencia a un `config.json`: las únicas coincidencias de
  "config" en `src/` son `RetryPolicyOptions.cs` (parámetros de la política
  de reintento de F2/Ciclo A, un objeto en memoria sin persistencia a
  disco) y un comentario en `CredentialsFileTokenProvider.cs` sobre el
  fichero de credenciales de Claude Code (`.credentials.json`, no
  relacionado). **Este ciclo es trabajo completamente nuevo**, no una
  extensión de código existente.
- **`src/ClaudeMeter.Desktop/App.xaml.cs`** — composition root actual
  (F1 + F2/Ciclo A): construye `HttpClient`, `ITokenProvider`,
  `IUsageDataSource` (hoy `RetryingUsageDataSource` envolviendo
  `AnthropicApiUsageDataSource`, ver Ciclo A), inicializa Serilog vía
  `SerilogConfigurator.CreateLogger()` y registra todo a mano en un
  `IServiceCollection` sin contenedor de terceros (`OnStartup`/`OnExit`).
  Es el punto natural donde leer `config.json` al arrancar, antes de
  construir cualquier servicio que dependa de sus valores.
- **`src/ClaudeMeter.Desktop/MainWindow.xaml`** — tamaño fijo `Width="280"
  Height="140"`, `WindowStyle="None"` + `AllowsTransparency="True"` +
  `Background="Transparent"` + `Topmost="True"` + `ResizeMode="NoResize"` +
  `ShowInTaskbar="False"` + `WindowStartupLocation="Manual"`. Aloja un
  único `BlazorWebView` que ocupa toda el área del `Grid` raíz.
- **`src/ClaudeMeter.Desktop/MainWindow.xaml.cs`** — el constructor
  posiciona la ventana en runtime en la esquina inferior derecha del área
  de trabajo (`SystemParameters.WorkArea`) con un margen fijo codificado
  (`ScreenMargin = 16`, líneas 29-31: `Left = workArea.Right - Width -
  ScreenMargin; Top = workArea.Bottom - Height - ScreenMargin;`). No hay
  ninguna lógica de arrastre (`MouseLeftButtonDown`/`DragMove`) todavía; el
  propio XMLDoc de la clase documenta explícitamente que "la posición
  configurable queda fuera de alcance de F1 (ver F2 en `CLAUDE.md`)".
- **`src/ClaudeMeter.Desktop/Pages/UsagePage.razor`** — el intervalo de
  poll está hardcodeado como constante en el bloque `@code`, línea 24:
  `private static readonly TimeSpan RefreshInterval =
  TimeSpan.FromSeconds(60);`, usada en `OnInitialized()` (línea 35) para
  construir `new UsagePollingCoordinator(UsageDataSource, RefreshInterval,
  Logger)`. Este es el punto exacto que la US del intervalo configurable
  necesita sustituir por un valor leído de `config.json` (con 60s como
  valor por defecto si no hay override).
- **`src/ClaudeMeter.Desktop/Polling/UsagePollingCoordinator.cs`** — ya
  recibe el intervalo como parámetro (`TimeSpan interval`) en su
  constructor; no necesita cambios de contrato, solo que quien lo
  construye (`UsagePage.razor`) le pase un valor proveniente de
  configuración en lugar de la constante actual.
- **Issue #16 (click-through, F3) — confirmado sin implementar.**
  `MainWindow` hoy es completamente interactiva: no existe ningún modo
  click-through activo (`WS_EX_TRANSPARENT` o equivalente) en el código
  actual. El arrastre de US-2 de este ciclo por tanto **no tiene ningún
  bloqueo real hoy** — se documenta como "Depends on: ninguno bloqueante"
  en Dependencies, con una nota de riesgo explícita sobre la interacción
  futura con #16 cuando F3 la implemente.
- **`CLAUDE.md`** — el roadmap ya fija F2 con "`config.json` para
  interval/position/chime" como parte explícita del alcance de la fase,
  confirmando que esta issue es la pieza que cierra ese punto del roadmap.

## US-1: Configuración vía `config.json` (intervalo, posición, chime)

**As a** usuario del widget,
**I want** un fichero `config.json` donde pueda ajustar el intervalo de
polling, la posición inicial del widget y si suena un chime,
**so that** pueda adaptar el widget a mi flujo de trabajo sin recompilar
nada.

### Acceptance Criteria

- **GIVEN** no existe ningún `config.json` en la ruta esperada al arrancar
  la aplicación
  **WHEN** `App.xaml.cs` inicializa el composition root
  **THEN** se usan los valores por defecto actuales (intervalo de 60s,
  posición en la esquina inferior derecha del área de trabajo tal como hoy,
  chime desactivado) y la aplicación arranca con normalidad, sin excepción
  ni fichero creado automáticamente como efecto secundario de solo leer

- **GIVEN** existe un `config.json` válido con un valor de intervalo de
  polling distinto de 60 segundos
  **WHEN** se construye el `UsagePollingCoordinator` al iniciar
  **THEN** el poll usa el intervalo configurado en vez de la constante
  `RefreshInterval` hardcodeada hoy en `UsagePage.razor`

- **GIVEN** existe un `config.json` válido con una posición inicial
  (coordenadas X, Y) dentro del área de trabajo de algún monitor conectado
  **WHEN** se crea `MainWindow`
  **THEN** la ventana aparece en esa posición configurada en lugar del
  cálculo por defecto de esquina inferior derecha

- **GIVEN** existe un `config.json` válido con el chime activado, y una
  `RateLimitWindow` (sesión o semana) transiciona de un estado distinto de
  `UsageThreshold.Critical` a `UsageThreshold.Critical` (según
  `UsageThresholdClassifier.Classify`, `PercentageUsed` ≥90%, el mismo
  umbral rojo ya definido en F1)
  **WHEN** se procesa esa transición
  **THEN** se reproduce un sonido audible (el chime) para esa transición; y
  si el chime está desactivado en la configuración, esa misma transición
  **no** produce ningún sonido

- **GIVEN** el chime ya sonó por una transición a Crítico (sesión o
  semana), y el estado de esa misma ventana sigue siendo Crítico en el
  siguiente ciclo de poll, sin que se haya producido ninguna transición
  nueva (es decir, sin haber bajado antes a Warning/Normal)
  **WHEN** se procesa ese siguiente ciclo
  **THEN** el chime **no** vuelve a sonar — solo sonará de nuevo si se
  produce una nueva transición hacia Crítico tras haber bajado del umbral
  rojo, nunca de forma repetida en cada ciclo de 60s mientras el estado se
  mantenga en Crítico

- **GIVEN** existe un `config.json` pero está corrupto o mal formado (JSON
  inválido, o valores fuera de rango como un intervalo negativo o cero)
  **WHEN** se lee al arrancar
  **THEN** la aplicación cae a los valores por defecto para cada campo
  afectado (no aborta el arranque), y se registra una entrada de nivel
  `Warning` en el log (Serilog, ya disponible desde F2/Ciclo A) indicando
  que la configuración no pudo aplicarse

- **GIVEN** una posición configurada que queda fuera del área visible de
  todos los monitores actualmente conectados (p. ej. quedó guardada cuando
  había un segundo monitor que ahora está desconectado)
  **WHEN** se crea `MainWindow`
  **THEN** se aplica el fallback a la posición por defecto (esquina
  inferior derecha del monitor principal) en lugar de dejar la ventana
  inaccesible fuera de pantalla

## US-2: Arrastrar el widget y persistir la nueva posición

**As a** usuario del widget,
**I want** poder arrastrar el widget con el ratón a cualquier punto de la
pantalla y que esa posición se recuerde,
**so that** pueda reposicionarlo en caliente sin editar `config.json` a
mano, y no tenga que volver a moverlo cada vez que abro la aplicación.

### Acceptance Criteria

- **GIVEN** `MainWindow` visible en su posición actual
  **WHEN** el usuario pulsa el botón izquierdo del ratón sobre el widget y
  lo arrastra a otro punto de la pantalla
  **THEN** la ventana sigue el puntero y se reposiciona en caliente, sin
  necesidad de editar `config.json` a mano

- **GIVEN** el usuario suelta el botón izquierdo tras arrastrar el widget a
  una nueva posición
  **WHEN** se completa el gesto de arrastre
  **THEN** la nueva posición (X, Y) se persiste en `config.json`

- **GIVEN** una posición persistida por un arrastre de una sesión anterior
  **WHEN** la aplicación se reinicia
  **THEN** `MainWindow` aparece en esa misma posición (consistente con el
  AC de posición inicial de US-1), no en la posición por defecto ni en
  ninguna posición intermedia de un arrastre anterior sin soltar

- **GIVEN** el usuario realiza dos arrastres consecutivos en la misma
  sesión (mueve el widget, suelta, y vuelve a moverlo a otro punto)
  **WHEN** se completa cada arrastre
  **THEN** `config.json` refleja siempre la posición del último arrastre
  completado; no queda una escritura a medias ni una mezcla de coordenadas
  de arrastres distintos

- **GIVEN** un fallo al escribir en disco al persistir `config.json` tras
  soltar el arrastre (p. ej. permisos insuficientes o disco lleno)
  **WHEN** se intenta guardar la nueva posición
  **THEN** el widget permanece en la nueva posición en memoria para la
  sesión actual (el reposicionamiento en caliente del primer AC no se
  revierte), se registra una entrada de nivel `Warning` o `Error` en el
  log, y la aplicación no se cae

## Technical Notes

- **Relación con #16 (click-through, F3, no implementada todavía):** la
  propia issue #13 señala esta relación en su Technical Note original — "el
  arrastre requiere que la ventana esté en modo interactivo […] si
  `MainWindow` es click-through, el gesto de arrastre debe funcionar en el
  mismo modo de interacción temporal que ya prevé el AC de esa issue". Hoy
  no es un bloqueo real (`MainWindow` es completamente interactiva, sin
  ningún modo click-through activo), así que este ciclo puede implementarse
  sin esperar a #16. Queda documentado como riesgo futuro (ver Risks) para
  que, cuando F3 implemente click-through, Design revise cómo coexiste con
  el arrastre aquí implementado.
- **Mecanismo de arrastre sugerido (verificado, no decidido):** el patrón
  estándar documentado por Microsoft para una ventana `WindowStyle="None"`
  es manejar `MouseLeftButtonDown` en la ventana y llamar a
  `this.DragMove()` — confirmado como técnica oficial soportada
  ([`Window.DragMove` — Microsoft Learn](https://learn.microsoft.com/dotnet/api/system.windows.window.dragmove)).
  Se deja como sugerencia por defecto para Design, no como decisión
  cerrada.
- **Advertencia técnica real para Design sobre ese mecanismo:** la propia
  documentación de `DragMove()` exige que el botón izquierdo esté pulsado
  "over an exposed area of the window's client area". En este proyecto, el
  `Grid` raíz de `MainWindow.xaml` está completamente cubierto por el
  `BlazorWebView` (que aloja un control nativo WebView2 con su propio
  `HWND`/"airspace", el mismo problema ya documentado en
  `f1-widget-visual-base.md` para la transparencia). Es plausible que un
  `MouseLeftButtonDown` capturado a nivel de `Window` **no** se dispare
  cuando el puntero está sobre el contenido Razor visible, porque WebView2
  captura el evento de ratón primero. Design deberá decidir el mecanismo
  concreto (p. ej. una franja/zona de arrastre fuera o superpuesta al
  `BlazorWebView`, o reenviar el `mousedown` desde JavaScript/interop de
  Blazor hacia el lado WPF para invocar `DragMove()` igualmente) — el
  requisito funcional de este documento es únicamente que el arrastre
  funcione desde algún punto del widget visible, no el mecanismo exacto.
- **Ubicación y formato de `config.json`:** ni la issue ni `CLAUDE.md`
  especifican la ruta exacta. Por consistencia con la ruta de logging ya
  adoptada en F2/Ciclo A (`%LOCALAPPDATA%\ClaudeMeter\logs`), una ruta
  razonable por defecto sería `%LOCALAPPDATA%\ClaudeMeter\config.json`,
  pero se deja como sugerencia para Design, no como decisión cerrada. La
  tecnología de (de)serialización (p. ej. `System.Text.Json`, ya parte del
  BCL de .NET 8 y sin dependencias nuevas) también se deja a Design.
- **Valores por defecto:** se asumen los ya existentes en el código actual
  — intervalo 60s, posición esquina inferior derecha con margen de 16px,
  chime desactivado (un sonido inesperado en el primer arranque sería una
  mala primera experiencia) — como los valores por defecto documentados en
  los AC de US-1. No hay señal en la issue de que deban ser otros.

## Dependencies

- **Depends on:** Scaffolding inicial de la solución (F0, ya mergeado) —
  satisfecho, es el `IServiceCollection`/composition root de
  `App.xaml.cs` ya existente donde debe leerse `config.json`.
- **Related to:** Conectar el poll de 60s con la UI (issue #9, F1, ya
  mergeada) — es el código exacto (`UsagePage.razor`, constante
  `RefreshInterval`) que el AC de intervalo configurable de US-1 necesita
  modificar.
- **Related to:** Click-through de la ventana (issue #16, F3, **no
  implementada todavía**) — sin bloqueo real hoy (ver Punto de partida
  verificado y Technical Notes); relación documentada para cuando F3 la
  implemente.
- **Depends on:** ninguno bloqueante respecto a #16 — confirmado por
  inspección del código actual de `MainWindow.xaml.cs` (sin ningún modo
  click-through activo hoy).
- **Decisión confirmada por el usuario (disparador del chime):** el chime
  suena al cruzar al umbral rojo/Crítico (`UsageThreshold.Critical`,
  `PercentageUsed` ≥90%, el mismo umbral ya definido en F1 por
  `UsageThresholdClassifier`), tanto para la ventana de sesión como para la
  de semana. Suena **una sola vez por transición** hacia Crítico — no se
  repite en cada ciclo de poll de 60s mientras el estado se mantenga en
  Crítico, ni vuelve a sonar hasta que se produzca una nueva transición
  (p. ej. baja del umbral a Warning/Normal y vuelve a subir a Crítico).
  Cierra la "Open question" que este documento dejaba pendiente en su
  versión anterior.

## Risks

- **Data quality:** `config.json` corrupto o mal formado debe caer a
  valores por defecto, nunca provocar un crash — riesgo ya marcado
  explícitamente por la propia issue #13. Impact: Low.
- **UX / multi-monitor:** una posición persistida que deja de ser válida
  (monitor desconectado, cambio de resolución) podría dejar la ventana
  inaccesible fuera de pantalla si no hay un fallback robusto (cubierto por
  el último AC de US-1). Impact: Medium.
- **Compatibilidad (arrastre vs. `BlazorWebView`/WebView2):** el mismo
  problema de "airspace" ya documentado en F1 para la transparencia podría
  impedir que el gesto de arrastre se detecte desde el contenido
  Razor visible si se implementa el patrón `MouseLeftButtonDown` +
  `DragMove()` de forma ingenua a nivel de `Window` (ver Technical Notes).
  Impact: Medium.
- **Interacción futura con click-through (#16, F3):** cuando se implemente
  el modo click-through, el arrastre implementado en este ciclo tendrá que
  coexistir con él (la propia issue #13 ya lo señala) — un diseño de
  arrastre que no anticipe ese modo futuro podría requerir rediseño en F3.
  Impact: Low para este ciclo (no bloquea nada hoy), documentado para no
  perder el contexto de cara a F3.
- **Persistencia (US-2):** un fallo de escritura a disco al persistir la
  posición tras un arrastre (permisos, disco lleno) no debe crashear la
  aplicación ni revertir el reposicionamiento en memoria de la sesión
  actual (cubierto por el AC correspondiente). Impact: Low.
- **Performance:** N/A — ninguna de las dos user stories añade llamadas de
  red ni trabajo por ciclo de poll adicional.

## Definition of Done

- [ ] Todas las Acceptance Criteria de US-1 y US-2 anteriores se cumplen
- [ ] Cobertura de tests automatizados (xUnit puro para el lector/parser de
      `config.json` — valores por defecto, fallback ante JSON corrupto o
      fuera de rango, fallback de posición fuera de pantalla — y para la
      lógica de persistencia de posición tras arrastre) según el mínimo
      configurado en `.claude/sdlc.config.yaml` (`testingCoverage: 70`)
- [ ] Ninguna prueba automatizada depende de un `config.json` real del
      usuario ni de un monitor/resolución real: los tests usan ficheros
      temporales o valores simulados de área de trabajo
- [x] Disparador del chime (transición a umbral Crítico ≥90%, una sola vez
      por transición) — confirmado por el usuario (ver Dependencies): no
      quedan "Open question" pendientes en este documento
- [ ] **Validación manual (US-1):** se confirma, ejecutando la aplicación
      real en Windows, que un `config.json` con intervalo, posición y
      chime personalizados efectivamente cambia el comportamiento del
      widget (poll al ritmo configurado, ventana en la posición indicada,
      chime sonando/silencioso según el interruptor) — ningún agente de
      este pipeline puede verificar renderizado ni audio real de
      WPF/WebView2 de forma automática
- [ ] **Validación manual (US-2):** se confirma, arrastrando el widget con
      el ratón en un equipo Windows real y reiniciando la aplicación
      después, que la ventana reaparece exactamente en el punto donde se
      soltó el arrastre — validación de interacción de ratón en tiempo
      real que ningún agente de este pipeline puede ejecutar de forma
      automática
