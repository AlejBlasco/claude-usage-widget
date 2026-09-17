# Requirements: F1 — Widget visual base

## Source
Milestone **F1 — Widget visual base**, GitHub issues #6, #7, #8 y #9 —
https://github.com/AlejBlasco/claude-usage-widget/issues/6,
/issues/7, /issues/8, /issues/9.

El usuario ha decidido ejecutar analysis → design → development → testing →
documentation como **un único ciclo SDLC** para todo el milestone F1 (las 4
issues son pequeñas, secuenciales y forman una sola funcionalidad coherente:
"primer widget visual"), en vez de un ciclo por issue. Este documento cubre
las 4 issues como US-1..US-4, con una cadena de dependencias interna
explícita: **#6 → #7 → #8 → #9**.

## Punto de partida verificado en el repositorio

- **Core de F0 (ya mergeado, no requiere cambios):**
  - `src/ClaudeMeter.Domain/Usage/UsageSnapshot.cs` — estados explícitos
    (`Success`/`TokenUnavailable`/`Unauthorized`/`RequestFailed`), nunca lanza
    para los casos "sin datos".
  - `src/ClaudeMeter.Domain/Usage/RateLimitWindow.cs` —
    `RateLimitWindowParser.Parse`/`ParseSnapshot`, lógica pura y determinista
    (recibe `now` explícito, sin leer el reloj del sistema).
  - `src/ClaudeMeter.Application/Abstractions/IUsageDataSource.cs` (puerto
    `GetUsageAsync`) y `ITokenProvider.cs`.
  - `src/ClaudeMeter.Infrastructure/CredentialsFileTokenProvider.cs` y
    `AnthropicApiUsageDataSource.cs`.
  - Commits confirmados en `git log`: `44b07da` (auth), `5436eec` (llamada
    API), `10c345d` (parseo de headers), `d1fb170` (bucle de consola, #32).
  - El bucle de polling de F0 vive en un proyecto de consola desechable
    (`ClaudeMeter.Console`, ver `docs/sdlc/requirements/issue-05-bucle-polling-consola.md`).
    Esa "decisión confirmada" ya deja dicho que **F1 no reutiliza ese
    proyecto de consola**: reutiliza directamente los mismos puertos de
    Application/Infrastructure (`ITokenProvider`, `IUsageDataSource`,
    `RateLimitWindowParser`) desde `ClaudeMeter.Desktop`.
- **Estado actual de `src/ClaudeMeter.Desktop` (verificado, no asumido):**
  es scaffolding puro. `MainWindow.xaml` es la ventana por defecto de la
  plantilla WPF (con borde, `Title="MainWindow"`, `Height="450" Width="800"`,
  sin `Topmost`/`AllowsTransparency`/`Background` configurados).
  `App.xaml.cs` está vacío (sin composition root). El `.csproj` usa
  `Sdk="Microsoft.NET.Sdk"` (no `Microsoft.NET.Sdk.Razor`) y **no** referencia
  el paquete de BlazorWebView — no hay ningún `BlazorWebView` en el proyecto
  todavía. No existe ningún `.razor` en el repositorio.

## US-1: MainWindow WPF sin bordes, topmost, transparente (issue #6)

**As a** usuario del widget,
**I want** una ventana WPF sin bordes, siempre encima (topmost) y con fondo transparente,
**so that** el widget se sienta como un overlay de escritorio y no como una ventana de aplicación normal.

### Acceptance Criteria

- **GIVEN** la aplicación `ClaudeMeter.Desktop` arrancando
  **WHEN** se crea `MainWindow`
  **THEN** la ventana se muestra sin barra de título ni bordes (equivalente a `WindowStyle="None"`)

- **GIVEN** `MainWindow` visible en pantalla
  **WHEN** el usuario da foco a otra ventana o aplicación
  **THEN** `MainWindow` permanece por encima de las demás ventanas (`Topmost="true"`)

- **GIVEN** `MainWindow` visible sobre el escritorio o sobre otra ventana
  **WHEN** no hay contenido Razor pintado sobre una región de la ventana
  **THEN** esa región deja ver lo que hay detrás (fondo transparente fuera del contenido Razor, `AllowsTransparency="true"` + `Background="Transparent"`)

- **GIVEN** `MainWindow` inicializada
  **WHEN** la ventana termina de cargar
  **THEN** aloja un `BlazorWebView` operativo (renderiza al menos una página Razor placeholder sin errores)

- **GIVEN** que WPF exige `WindowStyle="None"` siempre que `AllowsTransparency="true"` (restricción de la plataforma, no una elección de diseño)
  **WHEN** se configuran ambas propiedades en `MainWindow.xaml`
  **THEN** la combinación es válida y no lanza una excepción en tiempo de ejecución (`System.Windows.Markup.XamlParseException` si se hiciera mal, p. ej. `AllowsTransparency="true"` con `WindowStyle` distinto de `None`)

### Fuentes verificadas
[Borderless WPF Window (blachniet.com)](https://blachniet.com/posts/borderless-wpf-window/) —
combinación `WindowStyle="None"` + `AllowsTransparency="True"` + `Background="Transparent"` (+ `Topmost="True"` opcional); WPF exige `WindowStyle="None"` cuando `AllowsTransparency="True"`.
[Build a WPF Blazor app (Microsoft Learn)](https://learn.microsoft.com/en-us/aspnet/core/blazor/hybrid/tutorials/wpf) —
paquete `Microsoft.AspNetCore.Components.WebView.Wpf`, SDK del proyecto `Microsoft.NET.Sdk.Razor`, namespace/control `BlazorWebView` en XAML.

## US-2: UsagePage.razor con barras de sesión/semana (issue #7)

**As a** usuario del widget,
**I want** una página Razor que muestre barras de progreso para el consumo de sesión y de semana,
**so that** pueda ver de un vistazo cuánta cuota me queda.

### Acceptance Criteria

- **GIVEN** un `UsageSnapshot` en estado `Success` con `RateLimitWindow` de sesión y semana con `PercentageUsed` no nulo
  **WHEN** `UsagePage.razor` renderiza
  **THEN** se muestran dos barras de progreso, una para sesión y otra para semana, cada una reflejando su `PercentageUsed`

- **GIVEN** un `RateLimitWindow` con `PercentageUsed` a `null` (dato no parseable, p. ej. header ausente)
  **WHEN** `UsagePage.razor` renderiza esa ventana
  **THEN** la barra correspondiente se muestra en un estado explícito de "sin datos" (p. ej. vacía o con una etiqueta "no disponible"), sin lanzar una excepción ni dejar la página en blanco

- **GIVEN** un `UsageSnapshot` en estado distinto de `Success` (`TokenUnavailable`, `Unauthorized` o `RequestFailed`)
  **WHEN** `UsagePage.razor` renderiza
  **THEN** ambas barras (sesión y semana) se muestran en el mismo estado explícito de "sin datos" que en el caso anterior, ya que `RateLimitWindowParser.ParseSnapshot` ya devuelve `RateLimitWindow.Unavailable` para ambas ventanas en este caso

- **GIVEN** `UsagePage.razor` como componente aislado
  **WHEN** se ejecuta la suite de tests
  **THEN** existe al menos un test bUnit que renderiza el componente con un `UsageSnapshot` simulado y verifica el marcado/valores mostrados (sin necesidad de una `MainWindow` real ni de red)

### Fuentes verificadas
[Test Razor components in ASP.NET Core Blazor (Microsoft Learn)](https://learn.microsoft.com/en-us/aspnet/core/blazor/test) y
[Writing tests for Blazor components (bUnit.dev)](https://bunit.dev/docs/getting-started/writing-tests.html) —
proyecto de test con SDK `Microsoft.NET.Sdk.Razor`, `TestContext` de bUnit para renderizar el componente bajo prueba (CUT) e inspeccionar su marcado.

## US-3: Colores por umbral verde/ámbar/rojo (issue #8)

**As a** usuario del widget,
**I want** que las barras de UsagePage cambien de color según el porcentaje consumido (verde/ámbar/rojo),
**so that** identifique de un vistazo si estoy cerca del límite sin leer números.

### Acceptance Criteria

- **GIVEN** un `PercentageUsed` menor al 70%
  **WHEN** se calcula el color de la barra
  **THEN** el resultado es el umbral "verde" (normal)

- **GIVEN** un `PercentageUsed` entre 70% (inclusive) y 90% (exclusive)
  **WHEN** se calcula el color de la barra
  **THEN** el resultado es el umbral "ámbar" (advertencia)

- **GIVEN** un `PercentageUsed` de 90% o superior
  **WHEN** se calcula el color de la barra
  **THEN** el resultado es el umbral "rojo" (crítico)

- **GIVEN** los valores límite exactos 70 y 90
  **WHEN** se calcula el color de la barra
  **THEN** el comportamiento en cada frontera es el documentado arriba (70 → ámbar, 90 → rojo), sin zonas ambiguas entre umbrales

- **GIVEN** un `PercentageUsed` a `null` (ventana sin datos)
  **WHEN** se calcula el color de la barra
  **THEN** no se asigna ninguno de los tres umbrales de color; se usa el mismo estado neutro de "sin datos" ya definido en US-2, para no inducir a error mostrando un color de umbral sin un porcentaje real detrás

- **GIVEN** la lógica de umbral implementada como función pura (sin dependencias de UI)
  **WHEN** se ejecuta la suite de tests
  **THEN** existen tests unitarios que cubren cada umbral y ambas fronteras (69.9/70/89.9/90) de forma aislada, sin necesidad de renderizar ningún componente Razor

- **GIVEN** la misma lógica de umbral
  **WHEN** se aplica tanto a la barra de sesión como a la de semana en `UsagePage.razor`
  **THEN** ambas barras usan la misma función/regla de umbral (sin una implementación distinta o duplicada por ventana)

## US-4: Conectar el poll de 60s con la UI (issue #9)

**As a** usuario del widget,
**I want** que UsagePage se refresque automáticamente cada 60s usando el core de F0,
**so that** no tenga que recargar nada manualmente para ver datos actuales.

### Acceptance Criteria

- **GIVEN** `UsagePage.razor` montado y visible
  **WHEN** transcurren 60 segundos desde la última actualización
  **THEN** el componente vuelve a invocar `IUsageDataSource.GetUsageAsync` y refresca las barras (valores y colores) con el nuevo `UsageSnapshot`, sin que el usuario recargue nada manualmente

- **GIVEN** una iteración de refresco en la que `GetUsageAsync` devuelve un `UsageSnapshot` en estado `TokenUnavailable`, `Unauthorized` o `RequestFailed`
  **WHEN** el componente procesa esa respuesta
  **THEN** las barras pasan al estado "sin datos" ya definido en US-2/US-3 (o, si ya había un valor previo válido, se mantiene visualmente distinguible que el dato pudo quedar desactualizado), y el ciclo de 60s continúa sin detenerse ni lanzar una excepción no controlada

- **GIVEN** el timer de refresco activo mientras el componente está vivo
  **WHEN** el usuario cierra `MainWindow`
  **THEN** el componente se desmonta (`IDisposable`/`IAsyncDisposable`) y el timer/temporizador subyacente se detiene y libera correctamente, sin dejar el proceso corriendo en segundo plano ni programando más llamadas HTTP

- **GIVEN** el mismo core de F0 (`ITokenProvider` → `IUsageDataSource` → `RateLimitWindowParser`) ya usado por el proyecto de consola de F0
  **WHEN** se implementa el poll de 60s en `ClaudeMeter.Desktop`
  **THEN** se reutilizan directamente esos mismos puertos de Application/Infrastructure (sin reimplementar el parseo de headers ni la llamada HTTP, y sin depender del proyecto `ClaudeMeter.Console`)

- **GIVEN** dos ciclos de refresco consecutivos con éxito
  **WHEN** el segundo ciclo completa
  **THEN** el estado mostrado corresponde siempre al `UsageSnapshot` más reciente (no queda mezclado con datos de la iteración anterior, ni se disparan dos timers en paralelo)

## Technical Notes

- **US-1 / composition root:** el composition root de Desktop vive en
  `MainWindow`/`App.xaml.cs` (issue #6), tal como indica el issue original.
- **US-1 / paquete BlazorWebView:** el `.csproj` de `ClaudeMeter.Desktop`
  deberá referenciar `Microsoft.AspNetCore.Components.WebView.Wpf` y (según
  la guía oficial) cambiar el SDK del proyecto a `Microsoft.NET.Sdk.Razor` —
  ninguno de los dos está presente hoy. Se deja como sugerencia para la fase
  de Design, no como decisión tomada aquí.
- **US-1 / riesgo de "airspace":** `BlazorWebView` aloja un control nativo
  (WebView2), que es una ventana con su propio "airspace"; con
  `AllowsTransparency="true"` en la ventana host, solo el área **fuera** del
  `BlazorWebView` puede ser realmente transparente — el propio
  `BlazorWebView` seguirá siendo opaco/rectangular. El AC de la issue ya lo
  redacta así ("fondo transparente **fuera** del contenido Razor"), por lo
  que no hay conflicto, pero es importante que Design no intente hacer
  transparente el contenido dentro del `BlazorWebView` mismo (requeriría
  técnicas adicionales fuera de alcance de F1).
- **US-2/US-3:** ambas issues no especifican una tecnología de "barra de
  progreso" — se deja a criterio de Design (podría ser un `<progress>` HTML,
  una barra CSS con `div` + `width` dinámico, o un componente de terceros);
  el requisito funcional es únicamente el valor mostrado y el color
  aplicado.
- **US-3 / umbrales por defecto:** se adoptan literalmente los umbrales de
  ejemplo del issue original: **verde &lt;70% · ámbar 70-90% · rojo ≥90%**.
  El issue los presenta como ejemplo ("p. ej.") pero no hay ninguna señal de
  que deban ser otros; se documenta como valor por defecto asumido, revisable
  por el usuario si lo desea (ver Dependencies).
- **US-4 / mecanismo de timer en Blazor:** Design deberá decidir entre
  `System.Threading.Timer`/`PeriodicTimer` gestionado en el propio componente
  Razor (con `IDisposable`) o un servicio de aplicación inyectado; en
  cualquier caso el ciclo de vida debe atarse a `OnInitialized(Async)` /
  `Dispose(Async)` del componente, no a la ventana WPF directamente, para
  que el AC de "sin fugas de memoria/timers" sea verificable con bUnit
  (montar/desmontar el componente y comprobar que el timer se detiene).
- **Tamaño/posición por defecto de `MainWindow`:** ninguna de las 4 issues
  especifica tamaño o posición inicial de la ventana. Se asume un tamaño fijo
  pequeño acorde a un widget (p. ej. similar al de una barra de tareas
  extendida) sin lógica de posicionamiento configurable — la posición/tamaño
  configurables son explícitamente F2 (`config.json` de intervalo/posición/
  chime, según `CLAUDE.md`), fuera de alcance de F1.

## Dependencies

- **Cadena de dependencias interna del milestone (secuencial):**
  US-1 (#6) → US-2 (#7) → US-3 (#8) → US-4 (#9). Cada user story asume
  completada la anterior; no se espera desarrollo en paralelo entre ellas.
- **Depends on (externo, ya satisfecho):** core de F0 — `ITokenProvider`,
  `IUsageDataSource`, `RateLimitWindow`/`RateLimitWindowParser` — mergeado y
  verificado en el repositorio (ver commits arriba). No requiere cambios
  para F1.
- **Related to:** Icono de bandeja y click-through de la ventana (ambos F3,
  fuera de alcance de F1, según issue #6); tema claro/oscuro y countdown
  animado (F3); histórico/peak-off-peak (F4, depende de
  `SqliteUsageHistoryStore`, no relevante aquí).
- **Open question:** ninguna de scope o de producto genuina detectada — las
  4 issues están suficientemente acotadas y el usuario ya decidió tratarlas
  como un único ciclo. Los puntos que podrían parecer ambiguos (tamaño de
  ventana, tecnología de la barra de progreso, mecanismo exacto del timer)
  son decisiones de implementación que se documentan como asunciones arriba
  y quedan delegadas a la fase de Design, no al usuario.

## Risks

- **Performance (US-1):** la transparencia de ventana (`AllowsTransparency`)
  puede afectar al rendimiento de composición en equipos modestos, y
  combinada con un `BlazorWebView` (proceso WebView2 embebido) aumenta el
  consumo de memoria de un widget que se pretende ligero. Impact: Low —
  aceptado explícitamente en el issue original.
- **Compatibilidad (US-1):** riesgo de "airspace" entre la transparencia de
  la ventana WPF y el control nativo `BlazorWebView`/WebView2 (ver Technical
  Notes). Impact: Low — mitigado por el propio wording del AC ("fuera del
  contenido Razor").
- **Calidad de datos (US-2/US-3):** ninguno adicional — ambas consumen
  `RateLimitWindow` ya validado por F0; el caso "sin datos" ya está
  modelado explícitamente en Domain y no requiere lógica nueva de
  validación.
- **Performance/recursos (US-4):** un timer no liberado correctamente al
  cerrar `MainWindow` (o al navegar fuera de `UsagePage`) podría seguir
  disparando llamadas HTTP en segundo plano indefinidamente, acumulando
  tráfico contra la API de Anthropic y agotando recursos del proceso.
  Impact: Medium — es el riesgo más alto de todo el milestone (así lo marca
  también el issue #9 original); el AC de "sin fugas de memoria/timers" y el
  test bUnit de montar/desmontar el componente son la mitigación principal.
- **Regresión entre user stories:** al tratarse de un único ciclo SDLC para
  las 4 issues, un defecto introducido en US-1 (p. ej. una `MainWindow` mal
  configurada) puede enmascarar o complicar el diagnóstico de fallos en
  US-2/US-3/US-4. Impact: Low — mitigado por seguir la cadena de
  dependencias en orden y no paralelizar el desarrollo.

## Definition of Done

- [ ] Todas las Acceptance Criteria de US-1, US-2, US-3 y US-4 anteriores se cumplen
- [ ] Cobertura de tests automatizados (bUnit para US-2/US-4, xUnit puro para
      la lógica de umbral de US-3) según el mínimo configurado en
      `.claude/sdlc.config.yaml` (`testingCoverage: 70`)
- [ ] **Validación manual (US-1):** se confirma visualmente, ejecutando la
      aplicación en un equipo Windows real, que `MainWindow` aparece sin
      bordes/barra de título, permanece topmost sobre otras ventanas, y que
      el fondo es realmente transparente fuera del contenido Razor —
      ningún agente de este pipeline puede verificar renderizado real de
      WPF/WebView2 de forma automática
- [ ] **Validación manual (US-4):** se confirma, dejando la aplicación
      corriendo un periodo extendido (p. ej. 30-60 minutos) contra la API
      real o un mock de larga duración, que no se acumulan timers ni
      llamadas HTTP crecientes y que el proceso libera el timer al cerrar
      la ventana (uso de memoria/handles estable) — es una validación de
      un proceso de larga duración que ningún agente de este pipeline puede
      ejecutar de forma automática
