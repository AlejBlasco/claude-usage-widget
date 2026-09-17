# Requirements: F2 — Robustez, Ciclo A

## Source

Milestone **F2 — Robustez**, GitHub issues #10, #11 y #12 —
https://github.com/AlejBlasco/claude-usage-widget/issues/10,
/issues/11, /issues/12.

El usuario ha decidido tratar estas 3 issues como **un único ciclo SDLC**
("Ciclo A" de F2), siguiendo el mismo enfoque ya usado en F1 (ver
`docs/sdlc/requirements/f1-widget-visual-base.md`): se cubren como
US-1..US-3 con una cadena de dependencias interna explícita **#10 → #11 →
#12**, tal como decidió el usuario (primero se define el estado explícito
de 401/403, luego el retry se apoya en distinguir ese estado de un fallo
transitorio real, luego el logging instrumenta ambos).

La issue **#13** (`config.json` + arrastre del widget) **queda
explícitamente fuera de alcance de este ciclo**: no comparte superficie de
código con #10-#12 (no toca `AnthropicApiUsageDataSource`, la política de
reintentos ni el logging) y se tratará como un ciclo SDLC aparte.

## Punto de partida verificado en el repositorio

- **`src/ClaudeMeter.Domain/Usage/UsageSnapshot.cs`** — el enum
  `UsageSnapshotStatus` **ya distingue** `Unauthorized` (401/403) como
  estado propio, separado de `TokenUnavailable` y de `RequestFailed` (red,
  timeout, 5xx, u otro 2xx sin las cabeceras `anthropic-ratelimit-unified-*`
  mínimas). El XMLDoc del propio estado ya documenta explícitamente: *"La
  API respondió 401 o 403: el token fue rechazado. Nunca se intenta
  refrescar el token por cuenta propia (regla de `CLAUDE.md`)"*. **No hay
  distinción entre 401 y 403** — ambos colapsan en el mismo `Unauthorized`,
  y no hay ninguna señal en las 3 issues de que deban tratarse distinto.
- **`src/ClaudeMeter.Infrastructure/Usage/AnthropicApiUsageDataSource.cs`**
  — ya mapea la respuesta HTTP a `UsageSnapshot` correctamente respecto a
  los dos primeros AC de la issue #10: detecta `HttpStatusCode.Unauthorized`
  y `HttpStatusCode.Forbidden` y devuelve `UsageSnapshot.Unauthorized()`
  (nunca lanza excepción para este caso), y **nunca** intenta refrescar el
  token (no existe ninguna lógica de refresh en el fichero; el propio
  XMLDoc de la clase lo declara explícitamente fuera de alcance). Es decir:
  **dos de los tres AC de la issue #10 ya están satisfechos por el código
  existente de F0/F1** — el hueco real de US-1 en este ciclo es únicamente
  el tercer AC ("mensaje claro en la UI, distinto del estado 'sin datos'").
  El XMLDoc de la clase también confirma explícitamente que **no reintenta
  ante fallos transitorios** todavía ("ni reintenta ante fallos
  transitorios (alcance de F2)") — un único intento por llamada, sin
  ninguna política de reintento/backoff: este es el hueco real de US-2.
- **`src/ClaudeMeter.Desktop/Pages/UsagePage.razor`** y
  **`UsageBar.razor`** (F1) — hoy **no distinguen** `Unauthorized` de
  `RequestFailed`/`TokenUnavailable` en la UI: el método `Apply()` de
  `UsagePage.razor` solo diferencia dos casos, "nunca hubo éxito" (se
  muestra "No disponible", el mismo texto que usa `UsageBar.razor` para
  cualquier `RateLimitWindow.Unavailable`, sea cual sea la causa) y "hubo
  éxito antes" (se marca `_isStale`, con sufijo "(desactualizado)"). No
  existe ningún camino que muestre un mensaje distinto para "hay que volver
  a autenticarse" frente a "no hay datos" genérico — confirma que este es
  el trabajo real pendiente de US-1.
- **`src/ClaudeMeter.Desktop/Polling/UsagePollingCoordinator.cs`** — el
  ciclo de poll de 60s (`System.Timers.Timer`) hace **una única llamada**
  por tick a `IUsageDataSource.GetUsageAsync()`, sin ninguna política de
  reintento; ya incluye una guarda `_isPolling` que evita solapar dos polls
  si una llamada tarda más que el intervalo — relevante para US-2: si el
  backoff se implementa aquí, debe convivir con esa guarda sin bloquear
  ciclos futuros indefinidamente.
- **`src/ClaudeMeter.Desktop/App.xaml.cs`** — composition root actual
  (construido a mano en F1: `HttpClient`, `ITokenProvider`,
  `IUsageDataSource` como singletons vía `IServiceCollection`, sin
  contenedor de terceros ni MediatR todavía, con `_httpClient?.Dispose()`
  en `OnExit`). No hay ninguna referencia a Serilog ni a ningún sink de
  logging todavía — es el punto de entrada natural para US-3, tal como
  señala el propio issue #12 ("Serilog configurado en el composition root
  de Desktop").
- **`CLAUDE.md`** — invariante dura reconfirmada por el código ya
  existente: *"Never implement the API's own OAuth token refresh; on
  401/403 surface a clear error to the user instead"*, y regla de diseño
  *"Reading the token, calling the API, computing the countdown, and
  rendering are separate classes — don't collapse them"* (relevante para
  dónde debería vivir el backoff de US-2, ver Technical Notes).

## US-1: Manejo de 401/403 con aviso claro (issue #10)

**As a** usuario del widget,
**I want** que un 401/403 de la API muestre un aviso claro en el widget,
**so that** sepa que tengo que volver a autenticarme en Claude Code, en
lugar de ver datos rancios o el widget colgado.

### Acceptance Criteria

- **GIVEN** la API de Anthropic responde con código 401 o 403 a una llamada
  de `AnthropicApiUsageDataSource`
  **WHEN** se traduce la respuesta a `UsageSnapshot`
  **THEN** el resultado es `UsageSnapshot.Unauthorized()` (estado explícito
  del dominio, nunca una excepción sin capturar) — *ya satisfecho por el
  código actual, este AC queda como verificación de regresión para este
  ciclo*

- **GIVEN** un `UsageSnapshot` en estado `Unauthorized` recibido por
  `UsagePage.razor`
  **WHEN** el componente renderiza el estado tras ese snapshot
  **THEN** se muestra un mensaje explícito de "hay que volver a
  autenticarse" (o equivalente), **visualmente y textualmente distinto**
  del mensaje "No disponible" usado hoy para `TokenUnavailable`/
  `RequestFailed`

- **GIVEN** un `UsageSnapshot` en estado `Unauthorized` recibido **después**
  de que el widget ya mostrara datos válidos (`_hasEverSucceeded == true`)
  **WHEN** el componente procesa ese snapshot
  **THEN** se muestra el mismo mensaje explícito de reautenticación (no el
  sufijo genérico "(desactualizado)" usado para un `RequestFailed`
  transitorio tras un éxito previo), ya que un 401/403 no es un dato
  "desactualizado" sino una credencial inválida

- **GIVEN** cualquier ciclo de poll en el que `GetUsageAsync` devuelve
  `UsageSnapshot.Unauthorized()`
  **WHEN** se procesa la respuesta, en cualquier capa (Infrastructure,
  `UsagePollingCoordinator`, `UsagePage.razor`)
  **THEN** en ningún punto del flujo se invoca ninguna lógica de
  refresco/renovación del token OAuth — invariante dura de `CLAUDE.md`,
  verificable por inspección de código y por un test que confirme que no
  se dispara ninguna segunda llamada HTTP tras un 401/403

## US-2: Reintento con backoff ante fallos transitorios (issue #11)

**As a** usuario del widget,
**I want** que una caída de red puntual se reintente con backoff en lugar
de mostrar error inmediatamente,
**so that** el widget no "parpadee" en rojo por un fallo de un segundo de
conectividad.

Modelo de 3 categorías (decisión confirmada por el usuario, cierra las
"Open question" que este documento dejaba pendientes en su versión
anterior):

1. **Reintentable** — error de red, timeout, código 5xx → backoff; tras
   `N` intentos fallidos, error visible.
2. **No reintentable, accionable por el usuario** — `TokenUnavailable` y
   `Unauthorized` (401/403) → aviso claro inmediato de US-1, nunca pasan
   por la política de reintento.
3. **No reintentable, tipo bug/contrato roto** — 2xx sin las cabeceras
   `anthropic-ratelimit-unified-*` esperadas → error inmediato, sin
   reintentar (no es un fallo de red: reintentar no lo arregla, solo
   retrasa mostrar el problema).

### Acceptance Criteria

- **GIVEN** una llamada a la API que falla por error de red, timeout, o
  código 5xx (causas que hoy colapsan en `UsageSnapshot.RequestFailed()`
  junto con otras, ver categoría 1 arriba)
  **WHEN** se ejecuta la política de reintento
  **THEN** se reintenta automáticamente con un backoff creciente (p. ej.
  exponencial) hasta un número máximo `N` de reintentos configurado, antes
  de propagar el fallo como estado visible

- **GIVEN** una secuencia de fallos transitorios seguida de una respuesta
  exitosa dentro del número máximo de reintentos
  **WHEN** se completa la política de reintento
  **THEN** el resultado final es `UsageSnapshot.Success(...)` con los datos
  de esa respuesta, y el usuario **no** ve en ningún momento un estado de
  error visible en la UI para esa iteración

- **GIVEN** `N` reintentos consecutivos fallidos (se agota la política de
  backoff)
  **WHEN** se completa el último reintento
  **THEN** se propaga `UsageSnapshot.RequestFailed()` como estado de error
  visible (mismo tratamiento de "sin datos"/"desactualizado" ya definido en
  F1), sin quedar reintentando indefinidamente ni bloquear el siguiente
  ciclo de poll de 60s

- **GIVEN** una respuesta 2xx que no trae las cabeceras
  `anthropic-ratelimit-unified-*` mínimas esperadas (categoría 3 arriba)
  **WHEN** se traduce esa respuesta
  **THEN** se propaga como error inmediato (`UsageSnapshot.RequestFailed()`
  visible en la UI), **sin pasar por la política de reintento** — un
  contrato de API roto/persistente no se arregla reintentando, y reintentar
  solo retrasaría mostrar el problema al usuario

- **GIVEN** una respuesta con código 401/403 (`UsageSnapshot.Unauthorized`)
  o la imposibilidad de obtener un token utilizable
  (`UsageSnapshot.TokenUnavailable`) — categoría 2 arriba
  **WHEN** se evalúa si aplica la política de reintento
  **THEN** **ninguno de los dos se reintenta nunca**: ambos significan "el
  token está mal", no "la red falló" — solo un login manual del usuario lo
  arregla — y van directos al aviso claro inmediato de US-1, consistente
  con la invariante dura de `CLAUDE.md` de nunca auto-refrescar el token

- **GIVEN** una suite de tests para la política de reintento
  **WHEN** se ejecuta
  **THEN** existe al menos un test que simula una secuencia de fallos
  transitorios seguidos de éxito (usando un `HttpMessageHandler` falso o
  equivalente, nunca un token real, según la convención de testing de
  `CLAUDE.md`) y verifica tanto el número de intentos como el resultado
  final, además de tests que verifiquen que las categorías 2 y 3 nunca
  disparan un reintento

## US-3: Logging estructurado con Serilog (issue #12)

**As a** desarrollador del proyecto,
**I want** logging estructurado (Serilog) escrito en `%LOCALAPPDATA%`,
**so that** pueda diagnosticar fallos del widget en la máquina del usuario
sin depender de una consola visible.

### Acceptance Criteria

- **GIVEN** `App.xaml.cs` (composition root de Desktop) arrancando
  **WHEN** se inicializa la aplicación
  **THEN** Serilog queda configurado como sink de logging estructurado,
  con un logger accesible desde las clases que ya orquestan el poll
  (`UsagePollingCoordinator`) y la llamada a la API
  (`AnthropicApiUsageDataSource`)

- **GIVEN** el logger configurado
  **WHEN** la aplicación escribe cualquier entrada de log
  **THEN** el fichero de log resultante se persiste bajo
  `%LOCALAPPDATA%\ClaudeMeter\logs` (o una ruta equivalente documentada, si
  Design decide un nombre de subcarpeta distinto), sin depender de ninguna
  consola visible

- **GIVEN** el ciclo normal de poll (sin fallos)
  **WHEN** se completa un ciclo con éxito
  **THEN** se registra como mínimo una entrada de nivel `Information`
  (arranque, resultado del poll), sin ruido excesivo por cada tick de 60s

- **GIVEN** un fallo de red/token (`TokenUnavailable`, `Unauthorized` o
  `RequestFailed`, incluidos los reintentos de US-2)
  **WHEN** ese fallo se produce
  **THEN** se registra como mínimo una entrada de nivel `Warning` (fallo
  transitorio/reintento) o `Error` (fallo definitivo tras agotar
  reintentos, o 401/403), de forma que cada intento/reintento de US-2 quede
  también trazado en el log — enlaza directamente con el "Related to" que
  el propio issue #12 declara hacia #11

- **GIVEN** la suite de tests del proyecto
  **WHEN** se ejecuta
  **THEN** la configuración de Serilog es verificable sin escribir logs
  reales en el `%LOCALAPPDATA%` de la máquina de CI (p. ej. mediante un
  sink en memoria o un directorio temporal inyectado), para no contaminar
  ni depender del sistema de ficheros real del agente de CI

## Technical Notes

- **US-1 / alcance real reducido:** como se documenta en "Punto de partida
  verificado", dos de los tres AC originales de la issue #10 (detección de
  401/403 sin excepción, y "nunca refresco propio del token") ya están
  satisfechos por `UsageSnapshot`/`AnthropicApiUsageDataSource` desde F0.
  El trabajo real de este ciclo para US-1 es exclusivamente de UI:
  diferenciar `Unauthorized` en `UsagePage.razor`/`UsageBar.razor` del
  resto de estados "sin datos". Se deja a Design la decisión de mecanismo
  exacto (nuevo parámetro/estado en `UsageBar`, un componente nuevo, un
  texto/icono distinto) — el requisito funcional es únicamente que el
  mensaje sea distinguible.
- **US-2 / dónde vive el backoff (duda real para Design):** el issue no
  especifica si la política de reintento debe implementarse a nivel de
  llamada HTTP individual (dentro de `AnthropicApiUsageDataSource`, antes
  de traducir a `UsageSnapshot`) o a nivel de ciclo de poll (dentro de
  `UsagePollingCoordinator`, reintentando `GetUsageAsync()` completo). La
  regla de diseño de `CLAUDE.md` ("Reading the token, calling the API,
  computing the countdown, and rendering are separate classes — don't
  collapse them") sugiere que el backoff, al ser parte de "llamar a la
  API", encajaría mejor dentro de `AnthropicApiUsageDataSource` o en un
  decorator/wrapper alrededor de `IUsageDataSource`, en vez de mezclarse
  con la responsabilidad de temporización que ya tiene
  `UsagePollingCoordinator` (que además ya incluye la guarda `_isPolling`
  para evitar solapes, con la que el tiempo total de una secuencia de
  reintentos con backoff tendría que convivir sin bloquear el ciclo de
  60s). Esta decisión se deja explícitamente a la fase de Design, no se
  toma aquí.
- **US-2 / alcance de "fallo transitorio" (decisión confirmada por el
  usuario):** `RequestFailed()` hoy agrupa varias causas distintas en
  `AnthropicApiUsageDataSource` (error de red, timeout, código 5xx, y
  también un 2xx sin las cabeceras `anthropic-ratelimit-unified-*` mínimas
  esperadas), pero la política de reintento **no** trata a todas por igual:
  solo red/timeout/5xx son reintentables; el caso "2xx sin cabeceras" se
  trata como error inmediato sin reintentar, por no ser un fallo de
  conectividad real sino un contrato de API roto/persistente. Esto implica
  que, a nivel de implementación, Design probablemente necesite refinar
  `RequestFailed()` (o la capa que decide si reintentar) para distinguir
  esta subcausa específica en vez de tratar el enum como una única bolsa
  homogénea — ver también el modelo de 3 categorías en los AC de US-2.
- **US-2 / parámetros de la política:** ni número de reintentos `N` ni la
  base/multiplicador del backoff exponencial están especificados en el
  issue (a diferencia, por ejemplo, de los umbrales 70/90% de F1, que sí
  venían con valores de ejemplo). Se deja como decisión de Design/config,
  sin asumir un valor concreto aquí.
- **US-3 / correlación con reintentos:** dado que el propio issue #12
  declara "Related to: Reintento con backoff", el nivel de detalle exacto
  de qué se loguea por cada intento de US-2 (URL, código de estado, número
  de intento, tiempo de espera aplicado) se deja a Design, pero el
  requisito funcional de que "cada intento/reintento debe quedar
  trazable en el log" sí es parte del AC de este documento.
- **US-3 / ruta exacta del log:** el issue da `%LOCALAPPDATA%\ClaudeMeter\logs`
  como ejemplo ("o ruta equivalente documentada") — se respeta como
  sugerencia por defecto, no como decisión cerrada; Design puede ajustar el
  nombre exacto de subcarpeta/fichero (rolling por día, retención, etc.)
  siempre que quede documentado.

## Dependencies

- **Cadena de dependencias interna del ciclo (secuencial, decidida por el
  usuario):** US-1 (#10) → US-2 (#11) → US-3 (#12). Coincide con las
  dependencias que las propias issues ya declaran en GitHub: la issue #11
  declara `Depends on: Manejo de 401/403 con aviso claro` (#10), y la
  issue #12 declara `Related to: Reintento con backoff` (#11). No se
  espera desarrollo en paralelo entre las tres.
- **Depends on (externo, ya satisfecho):** `AnthropicApiUsageDataSource`
  (F0, ya mergeado) para US-1/US-2 — verificado en el repositorio, no
  requiere cambios de contrato para este ciclo, solo la política de
  reintento envolviéndolo (US-2) y el uso ya existente del estado
  `Unauthorized` (US-1). Scaffolding inicial de la solución (F0, ya
  mergeado) para US-3 — `App.xaml.cs` ya existe como composition root
  funcional desde F1.
- **Related to:** issue #13 (`config.json` — intervalo/posición/chime +
  arrastre del widget) — **explícitamente fuera de alcance de este ciclo**,
  se tratará como un ciclo SDLC aparte porque no comparte superficie de
  código con #10-#12. Se menciona aquí solo como contexto: cuando exista
  `config.json`, el número de reintentos/backoff de US-2 o el nivel de log
  de US-3 podrían eventualmente volverse configurables, pero eso no forma
  parte de los AC de este documento.
- **Decisión confirmada por el usuario (alcance de "fallo transitorio",
  US-2):** el retry con backoff aplica **solo** a fallos realmente
  transitorios — error de red, timeout, código 5xx. El caso "2xx sin las
  cabeceras `anthropic-ratelimit-unified-*` esperadas" queda **excluido
  explícitamente**: no se reintenta, se trata como error inmediato, porque
  no es un fallo de red sino un contrato de API roto/persistente y
  reintentar no lo arreglaría, solo retrasaría mostrar el problema.
- **Decisión confirmada por el usuario (`TokenUnavailable`/`Unauthorized`,
  US-2):** ninguno de los dos se reintenta nunca. Ambos significan "el
  token está mal", no "la red falló": solo un login manual del usuario los
  arregla, consistente con la invariante dura de `CLAUDE.md` de nunca
  auto-refrescar el token. Ambos van directos al aviso claro inmediato de
  US-1, sin pasar por la política de reintento de US-2.

Con esto el modelo de reintento queda cerrado en 3 categorías (ver también
los Acceptance Criteria de US-2): **reintentable** (red/timeout/5xx) →
backoff, tras `N` intentos → error visible; **no reintentable, accionable
por el usuario** (`Unauthorized`/`TokenUnavailable`) → aviso claro
inmediato (US-1); **no reintentable, tipo bug** (2xx sin cabeceras) → error
inmediato, sin reintentar. No quedan "Open question" pendientes en este
ciclo.

## Risks

- **Costo/cuota de API (US-2):** cada llamada de `AnthropicApiUsageDataSource`
  consume cuota real (aunque mínima, vía `PingModel`/`MaxTokens: 1`); una
  política de reintento mal calibrada (backoff demasiado agresivo o `N`
  demasiado alto) multiplicaría esas llamadas precisamente durante una
  degradación de servicio, cuando la API podría estar más sobrecargada.
  Impact: Medium — es el riesgo que el propio issue #11 ya marca
  explícitamente ("reintentos mal calibrados podrían saturar la API").
- **Interacción con el ciclo de poll de 60s (US-2):** si una secuencia de
  reintentos con backoff tarda más que el intervalo de poll, podría
  solaparse conceptualmente con el siguiente tick (mitigado hoy por la
  guarda `_isPolling` de `UsagePollingCoordinator`, pero el tiempo total de
  la política de reintento debe quedar acotado por diseño para no "comerse"
  varios ciclos de poll seguidos). Impact: Medium.
- **I/O de logging (US-3):** logging excesivo (p. ej. una entrada por cada
  intento de un backoff con muchos reintentos, o nivel `Information` en
  cada tick de 60s) podría generar escritura a disco innecesaria en un
  widget pensado para ser ligero. Impact: Low — es el riesgo que el propio
  issue #12 ya marca explícitamente.
- **UX / confusión de estados (US-1):** si el nuevo mensaje de
  "reautenticación requerida" no queda suficientemente distinguido
  visualmente del resto de estados "sin datos", el usuario podría seguir
  sin entender que necesita reautenticarse en Claude Code (el problema de
  producto que la propia issue #10 busca resolver). Impact: Medium — según
  la propia issue original.
- **Regresión entre user stories:** al tratarse de un único ciclo SDLC para
  las 3 issues, un defecto introducido en US-1 (p. ej. una clasificación
  incorrecta de qué estados son "reintentables") podría propagarse
  directamente a la lógica de US-2. Impact: Low — mitigado por seguir la
  cadena de dependencias en orden y no paralelizar el desarrollo, igual que
  en F1.

## Definition of Done

- [ ] Todas las Acceptance Criteria de US-1, US-2 y US-3 anteriores se
      cumplen
- [ ] Cobertura de tests automatizados (xUnit puro para la política de
      reintento/backoff de US-2 y para la configuración de Serilog de
      US-3 con sink en memoria; bUnit para el mensaje distinto de
      `Unauthorized` en `UsagePage.razor`/`UsageBar.razor` de US-1) según
      el mínimo configurado en `.claude/sdlc.config.yaml`
      (`testingCoverage: 70`)
- [ ] Ninguna prueba automatizada usa un token OAuth real ni realiza
      llamadas reales a la API de Anthropic (fallos/reintentos simulados
      con `HttpMessageHandler` falso o equivalente)
- [x] Alcance exacto de "fallo transitorio" en US-2 y tratamiento de
      `TokenUnavailable`/`Unauthorized` frente al retry — confirmado por el
      usuario (ver Dependencies): no quedan "Open question" pendientes en
      este documento
- [ ] **Validación manual (US-1):** se confirma, invalidando
      deliberadamente el token local (o simulando un 401/403 contra un
      mock de larga duración), que el mensaje de reautenticación aparece
      en la UI real y es visualmente distinguible del estado "sin datos" —
      ningún agente de este pipeline puede verificar renderizado real de
      WPF/WebView2 de forma automática
- [ ] **Validación manual (US-2):** se confirma, cortando la conectividad
      de red brevemente mientras la aplicación está corriendo, que el
      widget no muestra error inmediato y se recupera solo tras el
      reintento, sin parpadeo visible — validación de comportamiento en
      tiempo real que ningún agente de este pipeline puede ejecutar de
      forma automática
- [ ] **Validación manual (US-3):** se confirma que el fichero de log
      aparece realmente en `%LOCALAPPDATA%\ClaudeMeter\logs` (o la ruta
      que Design documente) tras ejecutar la aplicación real en Windows,
      y que su contenido es legible/estructurado — validación del sistema
      de ficheros real que ningún agente de este pipeline puede ejecutar
      de forma automática
