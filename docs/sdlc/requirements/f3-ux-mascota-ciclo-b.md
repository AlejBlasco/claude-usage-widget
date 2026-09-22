# Requirements: F3 — UX + página Mascota, Ciclo B

## Source

Milestone **F3 — UX + página Mascota**
(https://github.com/AlejBlasco/claude-usage-widget/milestone/4), dos issues
agrupadas porque la #17 depende explícitamente del "modo de interacción
temporal" que define la #16:

- **#16** — [F3] Click-through de la ventana —
  https://github.com/AlejBlasco/claude-usage-widget/issues/16
- **#17** — [F3] Icono de bandeja con menú (pausar/recargar/salir) —
  https://github.com/AlejBlasco/claude-usage-widget/issues/17

Ambas leídas tal como están en GitHub en el momento de redactar este
documento (#16 `updated_at: 2026-09-16T16:00:05Z`, sin cambios desde su
creación; #17 `updated_at: 2026-09-17T09:15:59Z`, ya ampliada respecto a su
redacción original con los dos AC de cierre directo desde `MainWindow`).

Quedan **fuera de alcance** de este documento el resto de issues del
milestone F3: **#18** (`ScreenNavigator`/`IWidgetScreen`) y **#19**
(`MascotPage.razor`, depende de #18). Se agruparán en un Ciclo C posterior.
Ninguna de las dos issues de este ciclo (#16, #17) menciona #18/#19 en su
propio texto, así que no hay ninguna dependencia bloqueante real hacia
ellas.

Se estructura en dos user stories (una por issue) porque son capacidades
observables distinguibles — "la ventana puede dejar de interceptar el
ratón" (#16) vs. "hay un icono de bandeja con control básico del ciclo de
vida" (#17) — aunque, como señalan ambas issues, comparten el mismo "modo
de interacción temporal" como mecanismo transversal.

## Punto de partida verificado en el repositorio

- **No existe ningún modo click-through activo hoy.** Se ha revisado
  `MainWindow.xaml`: usa `AllowsTransparency="True"` + `Background="Transparent"`,
  y ya documenta un problema conocido de WPF+WebView2 (`dotnet/maui#9024`,
  `MicrosoftEdge/WebView2Feedback#997`) por el cual una ventana con
  transparencia real (alpha 0) compuesta con `UpdateLayeredWindow` hace que
  el SO trate los clics como click-through **de forma involuntaria** antes
  de que WPF llegue a hacer su propio hit-test. El workaround ya aplicado
  (`<Rectangle Fill="#01000000" />`, alpha 1/255, imperceptible) fuerza a
  que el SO considere opaco todo el rectángulo para que el `BlazorWebView`
  reciba el ratón con normalidad. El propio comentario en el XAML es
  explícito: *"click-through real queda para el modo configurable de F3,
  issue #16 — hoy no debe existir"*. Confirmado: la ventana es completamente
  interactiva hoy, sin ningún `WS_EX_TRANSPARENT` ni equivalente aplicado
  deliberadamente.
- **No existe ningún mecanismo de cierre propio ni icono de bandeja hoy.**
  Se ha revisado `App.xaml.cs` (`OnStartup`/`OnExit`) y `MainWindow.xaml.cs`:
  ninguno de los dos contiene `NotifyIcon`, ninguna referencia a
  `System.Windows.Forms`, ni ninguna llamada a
  `Application.Current.Shutdown()` o equivalente (búsqueda `grep` sobre
  `src/` sin resultados para `NotifyIcon|System.Windows.Forms|Shutdown`).
  `MainWindow` no tiene ningún botón/gesto de cierre; el único cierre
  disponible hoy es el comportamiento estándar de WPF (p. ej. Alt+F4, que
  tampoco tiene ningún manejador explícito), consistente con lo ya
  confirmado en sesiones previas: el usuario ha tenido que matar el proceso
  desde el Administrador de tareas al no haber icono de bandeja ni barra de
  título con la que cerrar la ventana (`ShowInTaskbar="False"`,
  `WindowStyle="None"`).
- **`AppConfig`/`AppConfigStore` (F2/Ciclo B + F3/Ciclo A, ya en el árbol de
  trabajo) fijan un patrón exacto y ya extendido dos veces** (`ChimeEnabled`
  en F2/Ciclo B, `Theme` en F3/Ciclo A, este último todavía sin commitear):
  `AppConfig` es un record inmutable con un valor por defecto en
  `AppConfig.Default`; `AppConfigDto` (privado a `AppConfigStore`) declara
  cada campo nuevo como anulable; `Load()` resuelve cada campo con un método
  `ResolveX` dedicado que distingue "campo ausente" (valor por defecto en
  silencio) de "campo presente pero fuera de rango/tipo incorrecto" (valor
  por defecto + `LogWarning`); `Save()` escribe todo el DTO de forma atómica
  (`.tmp` + `File.Move` con reemplazo) y nunca lanza. Cualquier campo nuevo
  de configuración de este ciclo (ver Technical Notes sobre persistencia de
  pausa/click-through) debe seguir este mismo patrón si se decide
  persistirlo, no inventar un mecanismo propio.
- **`ClaudeMeter.Desktop.csproj` no tiene `<UseWindowsForms>true</UseWindowsForms>`
  hoy.** El proyecto es `Sdk="Microsoft.NET.Sdk.Razor"`, `TargetFramework`
  `net8.0-windows`, `<UseWPF>true</UseWPF>`, sin ninguna referencia a
  `System.Windows.Forms` ni a ningún paquete de tray icon. Ver Technical
  Notes para las dos vías verificadas para el icono de bandeja de #17 y lo
  que implicaría cada una sobre el `.csproj`.
- **`UsagePollingCoordinator` (Desktop/Polling) no tiene ningún concepto de
  pausa hoy.** Expone `Start()` (dispara un fetch inmediato y arranca el
  timer), `Dispose()` (detiene y libera el timer) y, solo para tests,
  `PollOnceForTestsAsync()`/`IsRunningForTests`. No hay ningún `Stop()`/`Pause()`
  público. `UsagePage.razor` crea una instancia nueva por componente (no un
  singleton de DI) y la libera en su propio `Dispose()`. "Pausar" (AC de
  #17) necesita, por tanto, una forma de detener el timer sin destruir el
  coordinador (para poder reanudar después) — hoy solo existe la opción
  binaria "corriendo" u "destruido".
- **`docs/sdlc/requirements/f2-robustez-ciclo-b.md` y
  `f3-ux-mascota-ciclo-a.md`** confirman el mismo patrón de "Punto de
  partida verificado en el repositorio" y de resolver ambigüedades con
  valores por defecto documentados en vez de preguntas abiertas cuando es
  razonable — replicado en este documento.

## US-1: Click-through de la ventana (issue #16)

**As a** usuario del widget,
**I want** que los clics atraviesen el widget cuando no estoy interactuando
con él directamente,
**so that** no me estorbe al trabajar con las ventanas que tiene debajo.

### Acceptance Criteria

- **GIVEN** el widget arranca sin ninguna interacción previa del usuario
  **WHEN** se crea `MainWindow`
  **THEN** la ventana arranca en modo interactivo (click-through
  **desactivado**), idéntico al comportamiento actual — sin cambio de
  comportamiento por defecto respecto a hoy (ver Technical Notes sobre por
  qué éste es el valor por defecto elegido, no un valor persistido)

- **GIVEN** el widget en modo interactivo (click-through desactivado)
  **WHEN** el usuario activa el click-through desde el menú de la bandeja
  (issue #17)
  **THEN** los clics del ratón sobre el área del widget dejan de ser
  interceptados por `MainWindow`/`BlazorWebView` y llegan a la ventana que
  esté debajo en el escritorio, sin que el widget deje de renderizarse ni
  de actualizar su contenido (el polling sigue activo salvo que,
  independientemente, esté pausado — ver US-2)

- **GIVEN** el widget en modo click-through
  **WHEN** el usuario desactiva el click-through desde el mismo menú de la
  bandeja (mismo elemento de menú actuando como interruptor)
  **THEN** la ventana vuelve a interceptar el ratón con normalidad —
  arrastre (F2/Ciclo B, `WindowDragService`) y el gesto de cierre directo
  (US-2 de este documento) vuelven a responder de inmediato

- **GIVEN** el widget en modo click-through
  **WHEN** el usuario intenta arrastrarlo con el ratón (gesto de F2/Ciclo B)
  o cerrarlo directamente (AC de US-2 de este documento)
  **THEN** ninguno de los dos gestos tiene efecto — el ratón no llega en
  absoluto a `MainWindow` mientras el click-through está activo, así que
  ambos gestos quedan implícitamente inhibidos sin necesidad de una guarda
  explícita adicional en `WindowDragService` (ver Technical Notes: el
  click-through ya impide que el SO entregue el evento de ratón a la
  ventana, no hace falta comprobar el estado en el propio servicio)

- **GIVEN** el estado de click-through en un momento dado (activado o
  desactivado)
  **WHEN** el usuario abre el menú de la bandeja (issue #17)
  **THEN** el elemento de menú correspondiente refleja visualmente el
  estado actual (p. ej. marcado/desmarcado), de forma que el usuario sepa
  si el widget está o no interceptando el ratón antes de intentar
  interactuar con él

## US-2: Icono de bandeja con menú — pausar/recargar/salir/cerrar (issue #17)

**As a** usuario del widget,
**I want** un icono en la bandeja del sistema con opciones para pausar,
recargar y salir, además de poder cerrar el widget directamente desde sí
mismo,
**so that** tenga control básico del widget sin necesidad de un Task
Manager.

### Acceptance Criteria

- **GIVEN** la aplicación arranca con normalidad
  **WHEN** se completa `App.OnStartup`
  **THEN** aparece un icono en la bandeja del sistema de Windows, visible
  mientras el proceso siga vivo

- **GIVEN** el icono de bandeja visible
  **WHEN** el usuario hace clic derecho (o el gesto estándar de Windows
  para abrir el menú contextual de un icono de bandeja) sobre él
  **THEN** se despliega un menú contextual con, como mínimo, las opciones
  "Pausar"/"Reanudar" (interruptor, ver AC siguiente), "Recargar", el
  interruptor de click-through de US-1 y "Salir"

- **GIVEN** el widget con el polling activo (comportamiento actual: fetch
  inmediato + timer cada `Config.PollingInterval`)
  **WHEN** el usuario selecciona "Pausar" en el menú de bandeja
  **THEN** el timer de `UsagePollingCoordinator` se detiene (sin llamar a
  `Dispose()` — la instancia sigue viva para poder reanudar), el widget
  conserva en pantalla el último `UsageSnapshot` recibido (mismo criterio
  visual que hoy usa `_isStale` para "datos potencialmente desactualizados"),
  y la aplicación **no** se cierra; el mismo elemento de menú pasa a mostrar
  "Reanudar"

- **GIVEN** el widget con el polling pausado
  **WHEN** el usuario selecciona "Reanudar" en el menú de bandeja (mismo
  elemento que "Pausar", ahora en su otro estado)
  **THEN** el timer se reinicia con el mismo intervalo configurado, sin
  esperar a que se cumpla un ciclo completo — dispara un fetch inmediato
  (mismo comportamiento que el `Start()` inicial de
  `UsagePollingCoordinator`) y continúa con el intervalo normal a partir de
  ahí

- **GIVEN** el widget en cualquier estado (con o sin pausa activa)
  **WHEN** el usuario selecciona "Recargar" en el menú de bandeja
  **THEN** se dispara un ciclo de poll inmediato adicional (fuera de la
  cadencia normal del timer, sin reiniciar el propio timer ni su cuenta
  atrás para el siguiente ciclo automático), y el resultado se aplica al
  widget igual que cualquier otro `UsageSnapshot` recibido

- **GIVEN** el widget visible, en modo interactivo (click-through
  desactivado, ver US-1)
  **WHEN** el usuario realiza el gesto de cierre directo definido para
  `MainWindow` (ver Technical Notes: el mecanismo concreto del gesto se
  deja a Design, el requisito funcional es que exista un gesto de cierre
  accionable directamente sobre la ventana visible, sin depender solo del
  menú de bandeja)
  **THEN** la aplicación se cierra por completo: el mismo efecto observable
  que "Salir" desde la bandeja (ver AC siguiente) — ningún proceso queda
  corriendo en segundo plano

- **GIVEN** el widget en modo click-through (ver US-1)
  **WHEN** el usuario intenta el mismo gesto de cierre directo sobre
  `MainWindow`
  **THEN** el gesto no tiene efecto — el ratón no llega a la ventana
  mientras el click-through está activo (mismo razonamiento que el AC
  equivalente de US-1 sobre el arrastre); el usuario debe primero
  desactivar el click-through desde la bandeja (US-1) o usar directamente
  "Salir" desde el propio menú de bandeja, que no depende de que la ventana
  intercepte el ratón

- **GIVEN** el widget en cualquier estado (pausado o no, click-through
  activado o no)
  **WHEN** el usuario selecciona "Salir" en el menú de bandeja
  **THEN** la aplicación se cierra por completo — se detiene el timer de
  polling si estuviera activo, se libera el icono de bandeja (evita que un
  icono huérfano quede visible hasta que el usuario pase el ratón por
  encima, comportamiento conocido de `NotifyIcon` si no se hace
  `Dispose()`/`Visible = false` antes de salir), se llama a
  `Log.CloseAndFlush()` (ya presente en `App.OnExit`) y no queda ningún
  proceso `ClaudeMeter.Desktop.exe` corriendo en segundo plano

- **GIVEN** la aplicación se cierra por cualquiera de las tres vías del
  ciclo de vida (cierre directo desde `MainWindow`, "Salir" desde la
  bandeja, o el cierre estándar de WPF si existiera algún otro camino no
  cubierto explícitamente arriba)
  **WHEN** se completa el cierre
  **THEN** el efecto observable final es idéntico en los tres casos:
  proceso terminado limpiamente, sin ninguna ventana ni icono de bandeja
  residual (AC explícita de la propia issue #17: "Cerrar desde el widget
  produce el mismo efecto que 'Salir' desde la bandeja")

## Technical Notes

- **Convivencia entre click-through y arrastre — resuelto, no es pregunta
  abierta:** mientras el click-through está activo, el sistema operativo no
  entrega ningún evento de ratón a `MainWindow` (esa es la definición misma
  de click-through, vía `WS_EX_TRANSPARENT` o equivalente) — ni
  `pointerdown`/`pointermove` (arrastre, `drag.js`/`WindowDragService`) ni
  el futuro gesto de cierre directo llegan a la ventana en absoluto. No
  hace falta ninguna guarda explícita adicional en `WindowDragService` ni en
  el nuevo servicio de cierre para "no arrastrar/cerrar en modo
  click-through": el propio modo ya lo garantiza a nivel de SO, antes de
  que WPF/WebView2 lleguen a procesar nada. El único punto que sí requiere
  decisión de Design es el gesto que **saca** a la ventana del modo
  click-through — resuelto aquí como "el interruptor de click-through del
  menú de bandeja" (US-1), tal como la propia issue #16 ya sugiere
  literalmente en su AC ("se puede desactivar temporalmente [...] p. ej.
  desde el icono de bandeja"). No existe ningún gesto alternativo (p. ej.
  un atajo de teclado global) porque ninguna de las dos issues lo menciona
  y añadirlo sería inventar alcance no pedido.
- **Persistencia de pausa/click-through — decisión por defecto, documentada
  aquí en vez de como pregunta abierta:** a diferencia de `PollingInterval`/
  `Position`/`ChimeEnabled`/`Theme` (los cuatro campos de `AppConfig` hoy,
  todos con AC explícita de "se guarda en `config.json`" en sus propios
  documentos de requisitos), **ni la issue #16 ni la #17 mencionan
  persistir el estado de pausa ni el de click-through entre reinicios**.
  Se adopta como valor por defecto que **ambos son estado en memoria,
  exclusivamente de la sesión actual** — la aplicación arranca siempre con
  el polling activo y el click-through desactivado (modo totalmente
  interactivo, igual que el comportamiento de hoy), y el usuario debe
  volver a pausar/activar el click-through tras cada reinicio si lo desea.
  Razones del valor por defecto: (1) arrancar en click-through por defecto
  dejaría al usuario sin forma evidente de recuperar el control del widget
  en su primer arranque tras esta actualización, un riesgo de UX peor que
  el beneficio de persistir el ajuste; (2) es el valor más fácil de
  corregir después (añadir dos campos más a `AppConfigDto` siguiendo el
  patrón ya establecido) si el usuario decide lo contrario al usar la
  función; (3) mantiene el "modo interactivo" como lo que ambas issues lo
  describen — algo *temporal* ("se puede desactivar **temporalmente**"),
  no un estado persistente de configuración. Si en el futuro se decide
  persistir alguno de los dos, el mecanismo a reutilizar es exactamente el
  mismo patrón de `AppConfigDto`/`ResolveX` ya usado por `ChimeEnabled`/
  `Theme` — no uno nuevo.
- **Menú de bandeja — un elemento más de lo que enumera literalmente el AC
  de #17:** el AC original de #17 lista "Pausar, Recargar, Salir" como los
  tres elementos del menú. Este documento añade un cuarto elemento
  (interruptor de click-through) porque es el mecanismo que la propia issue
  #16 exige para desactivar temporalmente el click-through, y #17 depende
  explícitamente de #16 según indica su propia Technical Note ("El cierre
  directo desde el widget requiere el mismo modo de interacción temporal
  que el arrastre [...] ver #16"). No es alcance añadido por iniciativa
  propia: es la unión necesaria de los AC de las dos issues agrupadas en
  este ciclo, tal como el propio "Dependencies"/"Related to" de ambas ya
  apuntaba.
- **Mecanismo del icono de bandeja — dos vías verificadas, decisión para
  Design, no cerrada aquí:**
  1. `System.Windows.Forms.NotifyIcon` — la vía "clásica" en WPF, requiere
     añadir `<UseWindowsForms>true</UseWindowsForms>` al `.csproj` (hoy
     ausente, confirmado). Es compatible con el TFM ya fijado
     (`net8.0-windows`) sin cambiarlo. No añade ningún paquete NuGet nuevo
     (forma parte del SDK de .NET), pero introduce una dependencia de
     ensamblado a Windows Forms dentro de un proyecto que hasta ahora es
     WPF puro.
  2. `Hardcodet.NotifyIcon.Wpf` (última versión estable **2.0.1** en
     NuGet.org, verificado en
     [nuget.org/packages/Hardcodet.NotifyIcon.Wpf](https://www.nuget.org/packages/Hardcodet.NotifyIcon.Wpf/))
     — implementación nativa de WPF (no envuelve `System.Windows.Forms`),
     pensada específicamente para exponer el icono/menú contextual como
     control WPF/XAML. Evita `UseWindowsForms`, a cambio de una dependencia
     NuGet de terceros nueva.
  Ambas opciones son técnicamente viables hoy; la elección concreta
  (mantener el proyecto 100% WPF con una dependencia de terceros vs. sumar
  `UseWindowsForms` sin dependencias nuevas) es una decisión de Design, no
  de este documento — el requisito funcional de este ciclo es únicamente
  que exista un icono de bandeja funcional con el menú descrito.
- **Gesto exacto de cierre directo desde `MainWindow` — sugerido, no
  decisión cerrada:** ni la issue #17 ni `CLAUDE.md` especifican el gesto
  concreto ("un botón/gesto", literalmente, según el propio AC de la
  issue). Dado que `MainWindow` no tiene bordes ni barra de título
  (`WindowStyle="None"`, `ShowInTaskbar="False"`), un botón de cierre
  discreto superpuesto al contenido Razor (visible solo en hover, similar
  al patrón ya usado para otras superficies de UI del widget) es una
  sugerencia razonable para Design, evitando reintroducir el mismo
  problema de "airspace" de WebView2 ya documentado en F1/F2/Ciclo B para
  el arrastre (un `MouseLeftButtonDown` a nivel de `Window` no se dispara
  de forma fiable sobre el contenido cubierto por `BlazorWebView`) — lo
  más consistente con el patrón ya usado para el arrastre es reenviar el
  evento de clic desde JavaScript/interop de Blazor hacia un nuevo método
  `[JSInvokable]` en un servicio análogo a `WindowDragService` que invoque
  `Application.Current.Shutdown()`. Se deja como sugerencia, no como
  decisión cerrada.
- **Liberación del icono de bandeja al salir:** tanto `NotifyIcon`
  (WinForms) como `Hardcodet.NotifyIcon.Wpf` documentan que el icono debe
  liberarse explícitamente (`Dispose()` o `Visible = false`) antes de que
  el proceso termine, o puede quedar visualmente "fantasma" en la bandeja
  hasta que el usuario pase el ratón por encima (comportamiento conocido de
  la Shell de Windows, no un bug del control). Esto debe ocurrir en las
  tres vías de cierre (cierre directo, "Salir" de bandeja, y `App.OnExit`)
  — cubierto como AC explícita más arriba.

## Dependencies

- **Depends on:** `MainWindow` WPF (F1, ya mergeado) — satisfecho, es la
  ventana sobre la que se aplica el click-through de US-1 y el gesto de
  cierre de US-2 (`Depends on` original de ambas issues).
- **Related to:** `WindowDragService`/gesto de arrastre (F2/Ciclo B, ya
  mergeado) — resuelto en este documento (ver Punto de partida verificado y
  Technical Notes): el click-through no requiere ningún cambio en
  `WindowDragService`, el SO ya bloquea la entrega del evento.
- **Related to:** `config.json`/`AppConfig`/`AppConfigStore` (F2/Ciclo B +
  F3/Ciclo A, ya en el árbol de trabajo) — patrón a reutilizar **si** en el
  futuro se decide persistir pausa/click-through (ver Technical Notes); no
  se usa en este ciclo porque ambos quedan como estado de sesión, no
  persistido (decisión con valor por defecto documentada arriba).
- **Related to:** `config.json` — intervalo, posición, chime (issue #13,
  F2/Ciclo B, ya mergeada) — la propia issue #17 la menciona como
  "Related to" en su redacción original por compartir la necesidad de un
  modo interactivo temporal; sin acoplamiento de código real más allá del
  patrón de configuración ya descrito.
- **Open question:** ninguna — las ambigüedades detectadas (persistencia de
  pausa/click-through, gesto exacto de cierre, mecanismo concreto del icono
  de bandeja) se han resuelto con valores por defecto documentados en
  Technical Notes, sin afectar el comportamiento observable descrito en las
  Acceptance Criteria.

## Risks

- **UX (click-through sin salida evidente):** si el usuario activa
  click-through y no recuerda que la bandeja es la única vía para
  desactivarlo, el widget queda "atrapado" en modo no interactivo. Mitigado
  por el AC de US-1 que exige reflejar el estado actual en el propio menú
  (marcado/desmarcado) y por el valor por defecto de arranque siempre
  interactivo (nunca se persiste click-through activado). Impact: Low.
- **Icono de bandeja huérfano al salir:** un `Dispose()`/`Visible = false`
  omitido en alguna de las tres vías de cierre dejaría un icono visual
  residual hasta el siguiente refresco de la bandeja por parte de Windows
  — cubierto explícitamente como AC de US-2, pero requiere disciplina en
  las tres rutas de salida (cierre directo, "Salir", cualquier cierre
  inesperado). Impact: Low.
- **Dependencia nueva de framework (`UseWindowsForms` o paquete NuGet de
  terceros):** cualquiera de las dos vías verificadas en Technical Notes
  introduce algo nuevo en el `.csproj` de Desktop que no existe hoy —
  `System.Windows.Forms` como dependencia de ensamblado, o
  `Hardcodet.NotifyIcon.Wpf` como paquete de terceros. Ninguna de las dos
  es gratuita en términos de superficie de mantenimiento; la decisión final
  la toma Design. Impact: Low.
- **Coexistencia pausa + stale data:** al pausar el polling, la UI debe
  seguir mostrando el último snapshot sin que el usuario lo confunda con
  datos en vivo — cubierto por el AC de US-2 que reutiliza el criterio
  visual de "stale" ya existente, pero requiere que Design decida si vale
  la pena distinguir visualmente "pausado" de "stale por fallo transitorio"
  (dos causas distintas del mismo estado visual hoy). Impact: Low.
- **Performance:** N/A — ninguna de las dos user stories añade llamadas de
  red ni trabajo por ciclo de poll adicional; "Recargar" dispara como mucho
  un fetch adicional bajo demanda explícita del usuario, no en bucle.

## Definition of Done

- [ ] Todas las Acceptance Criteria de US-1 y US-2 anteriores se cumplen
- [ ] Cobertura de tests automatizados (xUnit puro para el nuevo estado de
      pausa/reanudación de `UsagePollingCoordinator` y para la lógica de
      activación/desactivación de click-through como estado en memoria;
      bUnit o equivalente para que el menú de bandeja refleje el estado
      correcto de pausa/click-through) según el mínimo configurado en
      `.claude/sdlc.config.yaml` (`testingCoverage: 70`)
- [ ] Ninguna prueba automatizada depende de una bandeja de sistema real ni
      de interacción de ratón real de Windows — los tests simulan el estado
      (pausado/no pausado, click-through activado/desactivado) e invocan
      los métodos correspondientes directamente
- [ ] **Validación manual:** se confirma, ejecutando la aplicación real en
      Windows, que (a) el icono aparece en la bandeja y su menú responde a
      Pausar/Reanudar/Recargar/click-through/Salir con el efecto descrito,
      (b) el click-through activado deja pasar los clics a la ventana de
      debajo del escritorio real, y (c) el cierre directo desde `MainWindow`
      y "Salir" desde la bandeja terminan el proceso sin dejarlo corriendo
      en segundo plano (verificable en el Administrador de tareas) — ningún
      agente de este pipeline puede verificar interacción de ratón real,
      comportamiento real de la bandeja de Windows, ni procesos del sistema
      operativo de forma automática
