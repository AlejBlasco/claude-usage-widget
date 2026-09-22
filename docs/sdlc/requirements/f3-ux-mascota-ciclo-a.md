# Requirements: F3 — UX + página Mascota, Ciclo A

## Source

Milestone **F3 — UX + página Mascota**
(https://github.com/AlejBlasco/claude-usage-widget/milestone/4), dos issues
independientes entre sí y sin dependencias cruzadas con el resto del
milestone:

- **#14** — [F3] Tema claro/oscuro —
  https://github.com/AlejBlasco/claude-usage-widget/issues/14
- **#15** — [F3] Animación del countdown —
  https://github.com/AlejBlasco/claude-usage-widget/issues/15

Ambas leídas tal como están en GitHub en el momento de redactar este
documento (`updated_at` 2026-09-16 para las dos, sin cambios desde su
creación).

Quedan **fuera de alcance** de este documento el resto de issues del
milestone F3: **#16** (click-through), **#17** (icono de bandeja + cierre
directo, depende explícitamente de #16), **#18** (`ScreenNavigator`/
`IWidgetScreen`) y **#19** (`MascotPage.razor`, depende de #18). Se
agruparán en Ciclo B (#16+#17) y Ciclo C (#18+#19) en documentos de
requisitos posteriores.

Se estructura en dos user stories independientes (una por issue) porque son
capacidades observables completamente separadas — no comparten ningún
fichero de código ni superficie de UI entre sí — aunque ambas caigan bajo el
mismo epígrafe de "pulido visual" de F3.

## Punto de partida verificado en el repositorio

- **No existe ningún concepto de tema todavía.** Se ha revisado
  `src/ClaudeMeter.Desktop/wwwroot/css/app.css`: toda la paleta de color
  está hardcodeada como una única variante oscura (fondo
  `#20232aee`, texto `#e8e8e8`, colores de umbral `#3fb950`/`#d29922`/
  `#f85149`/`#5a6070` para verde/ámbar/rojo/neutral, más los estilos de
  `.usage-reauth`) — sin custom properties CSS, sin ninguna clase
  `theme-light`/`theme-dark`, sin ningún mecanismo de selección. **Este
  ciclo es trabajo completamente nuevo**, no una extensión de algo
  existente.
- **Páginas que existen hoy:** solo `UsagePage.razor` (raíz, F1), su hijo
  `UsageBar.razor` (F1) y `ReauthNotice.razor` (F2). El AC de la issue #14
  ("Se respeta en todas las páginas (UsagePage, MascotPage)") menciona
  `MascotPage`, que **no existe todavía** (issue #19, Ciclo C, depende a su
  vez de #18). Se documenta como límite de alcance más abajo — el tema debe
  cubrir las páginas que existen hoy y quedar estructurado de forma que
  `MascotPage` lo herede sin rediseño cuando aterrice en el Ciclo C, pero no
  puede haber una AC verificable contra una página que no existe aún.
- **No existe icono de bandeja todavía.** La issue #14 ofrece dos vías para
  el selector de tema: "desde el menú de bandeja o config.json". El menú de
  bandeja es la issue #17 (Ciclo B, no implementada — confirmado, no hay
  ningún `NotifyIcon`/tray en `src/`). La única vía disponible en este ciclo
  es, por tanto, `config.json`.
- **`config.json` ya existe y sigue un patrón establecido a reutilizar.**
  `src/ClaudeMeter.Desktop/Configuration/AppConfig.cs` (record `AppConfig`
  con `PollingInterval`, `Position`, `ChimeEnabled`, más `AppConfig.Default`)
  y `AppConfigStore.cs` (F2/Ciclo B, ya mergeado): `Load()` nunca lanza —
  fichero ausente o JSON inválido cae a `AppConfig.Default` completo; un
  campo concreto fuera de rango cae solo ese campo a su valor por defecto
  con un `Warning` en el log (Serilog, ya disponible desde F2/Ciclo A);
  `Save()`/`SavePosition()` escriben de forma atómica (fichero `.tmp` +
  `File.Move` con reemplazo) y nunca lanzan. Añadir un campo `theme` es
  extender este mismo patrón (DTO anulable en `AppConfigDto`, resolución con
  fallback y log de `Warning`), no crear uno nuevo.
- **`AppConfig` se lee una sola vez, en el arranque** (composition root de
  `App.xaml.cs`, según el propio comentario XMLDoc de `UsagePage.razor`:
  "Desde F2/Ciclo B lee el intervalo de poll de AppConfig"). No hay ningún
  mecanismo de recarga en caliente de `config.json` hoy (a diferencia de la
  posición, que sí se actualiza en caliente vía arrastre — un gesto de
  usuario explícito documentado en F2/Ciclo B, no aplicable aquí porque no
  hay ningún selector de tema en la UI todavía). El tema, igual que el
  intervalo de polling hoy, se aplicará por tanto al arrancar la aplicación
  a partir del valor leído de `config.json` — sin recarga en caliente en
  este ciclo, consistente con el comportamiento ya existente para los demás
  campos de configuración salvo la posición arrastrada.
- **El countdown (minutos hasta el reset) no se muestra en ningún sitio
  hoy.** `RateLimitWindow.MinutesRemaining` (Domain, calculado desde F0) ya
  existe como dato, pero `UsageBar.razor` solo renderiza el porcentaje
  (`pct%`) y la barra de relleno — nunca `MinutesRemaining`. `UsagePage.razor`
  tampoco lo renderiza. **La premisa de la issue #15** ("que el countdown...
  se anime en lugar de saltar de número en número") **presupone un
  countdown visible que hoy no existe en la UI** — solo existe el dato
  subyacente. Mostrar el valor como texto es, por tanto, un prerrequisito
  implícito de esta issue, no un comportamiento previo que haya que animar;
  se resuelve como AC explícita en US-2 en lugar de dejarse como pregunta
  abierta, porque es necesario para entregar la intención clara de la issue
  (animar un valor que se muestra).
- **Cadencia del poll confirmada:** `UsagePollingCoordinator` dispara el
  primer fetch inmediatamente y luego cada `Config.PollingInterval` (60s por
  defecto, configurable desde F2/Ciclo B) — el countdown cambiará, como
  mucho, una vez por ciclo de poll, no en tiempo real entre ciclos.
- **CSS sin frameworks de terceros** (decisión ya fijada en el documento de
  diseño de F1): el tema debe seguir el mismo patrón de CSS escrito a mano
  (p. ej. custom properties) en vez de introducir Bootstrap/Tailwind u otra
  librería nueva.

## US-1: Tema claro/oscuro (issue #14)

**As a** usuario del widget,
**I want** poder alternar entre tema claro y oscuro,
**so that** el widget encaje visualmente con el resto de mi escritorio.

### Acceptance Criteria

- **GIVEN** no existe ningún `config.json`, o existe pero sin el campo de
  tema
  **WHEN** la aplicación arranca y resuelve `AppConfig`
  **THEN** el widget se renderiza con el tema oscuro (paleta actual sin
  cambios: fondo `#20232aee`, texto `#e8e8e8`, colores de umbral ya
  existentes), preservando el aspecto visual de hoy sin ninguna regresión

- **GIVEN** un `config.json` válido con el tema puesto a claro
  **WHEN** la aplicación arranca
  **THEN** el widget se renderiza con una paleta clara definida (fondo,
  texto y colores de umbral verde/ámbar/rojo propios, no una simple
  reutilización sin ajuste de los colores oscuros) en lugar de la oscura

- **GIVEN** un `config.json` con un valor de tema no reconocido o de tipo
  incorrecto (p. ej. `"blue"`, cadena vacía, un número)
  **WHEN** `AppConfigStore.Load` lo resuelve
  **THEN** cae al tema por defecto (oscuro), se registra una entrada de
  nivel `Warning` en el log (mismo patrón que el resto de campos de
  `config.json`) y la aplicación arranca con normalidad, sin excepción

- **GIVEN** el tema está puesto a claro
  **WHEN** se renderizan `UsagePage`/`UsageBar` y, por separado,
  `ReauthNotice` (las dos superficies de UI que existen hoy)
  **THEN** ambas reflejan el tema claro de forma consistente — no solo una
  de las dos

- **GIVEN** el tema claro está activo
  **WHEN** una `UsageBar` se renderiza en estado Normal, Warning o Crítico
  (`UsageThresholdClassifier`)
  **THEN** los tres colores de umbral siguen siendo visualmente distintos y
  legibles sobre el fondo claro (no una copia sin ajustar de los colores
  pensados para fondo oscuro)

- **GIVEN** el usuario edita manualmente el campo de tema en `config.json`
  mientras la aplicación está en ejecución (no existe todavía ningún
  selector dentro del propio widget, ver Technical Notes sobre el menú de
  bandeja de la issue #17)
  **WHEN** la aplicación se reinicia
  **THEN** arranca con el nuevo tema aplicado

## US-2: Animación del countdown (issue #15)

**As a** usuario del widget,
**I want** que el countdown hasta el reset se anime en lugar de saltar de
número en número,
**so that** la actualización se sienta viva y no un refresco brusco de
pantalla.

### Acceptance Criteria

- **GIVEN** un snapshot con éxito y `RateLimitWindow.MinutesRemaining`
  disponible (sesión o semana)
  **WHEN** se renderiza `UsageBar`
  **THEN** se muestra el tiempo restante como texto, además del porcentaje
  ya existente (prerrequisito necesario para poder animarlo — ver Punto de
  partida verificado)

- **GIVEN** el countdown mostrando un valor de `MinutesRemaining`
  **WHEN** un nuevo ciclo de poll actualiza `MinutesRemaining` a un valor
  distinto
  **THEN** el cambio se muestra con una transición animada (p. ej.
  fundido/deslizamiento) en lugar de un salto brusco de un número a otro

- **GIVEN** `MinutesRemaining` llega a 0
  **WHEN** se renderiza el countdown
  **THEN** se muestra un estado de cero definido (p. ej. "0"), incluyendo su
  última transición animada, sin quedarse la animación a medias ni en un
  estado inconsistente

- **GIVEN** no hay datos disponibles todavía (nunca hubo un snapshot con
  éxito) o la ventana está en estado "sin datos" (`RateLimitWindow.Unavailable`,
  `MinutesRemaining == null`)
  **WHEN** se renderiza `UsageBar`
  **THEN** no se intenta animar ningún valor numérico ausente — se conserva
  el estado actual de "No disponible" sin excepción ni animación fantasma

- **GIVEN** la animación se dispara en cada ciclo de poll (cada 60s por
  defecto, o el intervalo configurado vía `config.json` desde F2/Ciclo B)
  **WHEN** se ejecuta repetidamente durante una sesión larga del widget
  **THEN** no introduce un impacto perceptible de CPU/GPU (AC explícita de
  la propia issue #15) — ver Definition of Done sobre la parte de esta AC
  que requiere validación manual

## Technical Notes

- **Mecanismo de tema sugerido (no decisión cerrada):** custom properties
  CSS (`:root { --bg-color: ...; }` redefinidas bajo una clase
  `theme-light`/`theme-dark` en `<body>` o `#app`) es el patrón más
  consistente con el CSS escrito a mano ya existente en `app.css` (sin
  frameworks de terceros, decisión ya fijada en F1) y el que mejor permite
  que `MascotPage` (Ciclo C, issue #19) reutilice el mismo tema sin
  duplicar reglas de color. Se deja como sugerencia para Design.
- **Selector de tema — alcance de este ciclo:** la issue #14 ofrece "menú de
  bandeja o config.json"; el menú de bandeja (issue #17) no existe todavía
  en este ciclo, así que la única vía implementable ahora es `config.json`
  (campo nuevo, siguiendo el patrón exacto ya usado para `chimeEnabled` en
  `AppConfigStore`). Cuando el Ciclo B implemente el tray icon, Design podrá
  añadir un selector en caliente que escriba al mismo campo, reutilizando
  `AppConfigStore.Save`.
- **`MascotPage` (issue #19, Ciclo C):** no existe todavía; no puede haber
  una AC verificable sobre ella en este documento. El requisito de este
  ciclo es que el mecanismo de tema quede estructurado (p. ej. custom
  properties a nivel de página raíz) para que cualquier página futura lo
  herede sin rediseño, no que `MascotPage` ya lo tenga.
- **Formato del texto del countdown:** ni la issue ni `CLAUDE.md`
  especifican el formato exacto (p. ej. "Xh Ym" vs. minutos totales vs.
  "HH:MM:SS"). Se deja como decisión de Design; el requisito funcional de
  este documento es únicamente que el valor se muestre y que su cambio se
  anime, no el formato textual concreto.
- **Mecanismo de animación sugerido (no decisión cerrada):** una transición
  CSS pura (similar a la ya usada en `.usage-bar__fill { transition: width
  0.3s ease-in-out; }`) es preferible a una librería JS de animación por
  coste de CPU, dado el AC explícito de la issue #15 sobre no impactar
  rendimiento. Se deja a Design decidir si el efecto deseado (p. ej. un
  "roll" dígito a dígito tipo odómetro) requiere JS/interop de Blazor en su
  lugar.

## Dependencies

- **Depends on:** `UsagePage.razor` (F1, ya mergeado) — satisfecho, es la
  superficie de UI que US-1 debe cubrir (`Depends on` original de la issue
  #14).
- **Related to:** `config.json` (F2/Ciclo B, ya mergeado —
  `AppConfig`/`AppConfigStore`) — satisfecho, es el mecanismo elegido para
  el selector de tema en este ciclo (`Related to` original de la issue #14).
- **Related to:** menú de bandeja (issue #17, Ciclo B, no implementada) —
  vía alternativa que la propia issue #14 menciona pero que no está
  disponible en este ciclo; no bloquea US-1, que se implementa solo vía
  `config.json`.
- **Related to:** `MascotPage.razor` (issue #19, Ciclo C, no implementada) —
  mencionada en el AC original de la issue #14 pero fuera de alcance de
  este documento (ver Punto de partida verificado y Technical Notes).
- **Depends on:** "Conectar el poll de 60s con la UI" (issue #9, F1, ya
  mergeada) — satisfecho, es el ciclo de poll (`UsagePollingCoordinator`)
  que produce los valores de `MinutesRemaining` que US-2 anima
  (`Depends on` original de la issue #15).
- **Open question:** ninguna — las ambigüedades detectadas (formato exacto
  del countdown, mecanismo CSS vs. JS de la animación, alcance del
  selector de tema sin tray) se han resuelto con valores por defecto
  documentados en Technical Notes, sin afectar el comportamiento
  observable descrito en las Acceptance Criteria.

## Risks

- **UX / Accesibilidad:** una paleta clara mal ajustada podría reducir el
  contraste de los colores de umbral (verde/ámbar/rojo) o del texto —
  cubierto explícitamente por una AC de US-1, pero requiere revisión visual
  real. Impact: Medium.
- **Performance:** la animación del countdown se dispara en cada ciclo de
  poll durante toda la sesión del widget — riesgo ya señalado por la propia
  issue #15 como AC explícita. Impact: Low si se implementa con transición
  CSS pura (ver Technical Notes); Medium si se optara por una librería JS
  de animación no evaluada aún.
- **Alcance / consistencia futura:** el AC original de la issue #14 nombra
  `MascotPage`, que no existe en este ciclo — si el mecanismo de tema no se
  estructura de forma reutilizable (ver Technical Notes), el Ciclo C podría
  requerir rehacer parte del trabajo de este ciclo. Impact: Low si Design
  sigue la sugerencia de custom properties a nivel de página raíz.
- **Data quality:** N/A — ninguna de las dos user stories introduce datos
  nuevos de red; ambas consumen datos que ya se calculan hoy (`AppConfig`,
  `RateLimitWindow.MinutesRemaining`).

## Definition of Done

- [ ] Todas las Acceptance Criteria de US-1 y US-2 anteriores se cumplen
- [ ] Cobertura de tests automatizados (bUnit para el renderizado de
      `UsageBar`/`UsagePage`/`ReauthNotice` bajo ambos temas y para la
      presencia/transición de clases CSS del countdown; xUnit puro para la
      resolución del nuevo campo `theme` en `AppConfigStore`, incluyendo el
      caso de valor inválido con fallback) según el mínimo configurado en
      `.claude/sdlc.config.yaml` (`testingCoverage: 70`)
- [ ] Ninguna prueba automatizada depende de un tema real de Windows ni de
      un `config.json` real del usuario — los tests usan ficheros
      temporales o valores simulados
- [ ] **Validación manual (US-1):** se confirma, ejecutando la aplicación
      real en Windows con `config.json` en tema claro y en tema oscuro, que
      ambas paletas se ven correctamente y son legibles sobre el
      escritorio real — ningún agente de este pipeline puede verificar
      renderizado real de WPF/WebView2
- [ ] **Validación manual (US-2):** se confirma, ejecutando la aplicación
      real durante varios ciclos de poll, que la animación del countdown se
      percibe fluida y sin impacto notable de CPU/GPU (AC explícita de la
      issue #15) — percepción de fluidez y coste real de rendimiento en
      un equipo real que ningún agente de este pipeline puede medir de
      forma automática
