# Testing Summary: F3 — UX + página Mascota, Ciclo A

## Scope

Cobertura de tests para el código nuevo/modificado, aún sin confirmar
(`commit`), de F3 Ciclo A (issues **#14** US-1 tema claro/oscuro y **#15**
US-2 animación del countdown), a partir de
`docs/sdlc/development/f3-ux-mascota-ciclo-a.md` y, siguiendo su cadena, de
`docs/sdlc/design/f3-ux-mascota-ciclo-a.md` y
`docs/sdlc/requirements/f3-ux-mascota-ciclo-a.md` (Acceptance Criteria y
Definition of Done usadas como fuente de verdad, no solo la nota "How to
Verify" del resumen de implementación). Se ha leído el código real de
producción y de los tests ya existentes antes de escribir ningún test
nuevo, sin asumir firmas/formato desde la descripción de la tarea — en
particular, los umbrales exactos de `CountdownFormatter` y el
comportamiento real de `ResolveTheme` ante un tipo JSON incorrecto se
verificaron leyendo el código, no se dieron por supuestos.

Piezas nuevas/modificadas cubiertas:

- `src/ClaudeMeter.Desktop/Rendering/CountdownFormatter.cs` (nuevo) —
  función pura `Format(int)`.
- `src/ClaudeMeter.Desktop/Configuration/AppConfig.cs` — `enum AppTheme`,
  `AppThemeExtensions.ToCssClass()`, campo `AppConfig.Theme`.
- `src/ClaudeMeter.Desktop/Configuration/AppConfigStore.cs` —
  `ResolveTheme(string?)` integrado en `Load()`/`Save()`.
- `src/ClaudeMeter.Desktop/Pages/UsagePage.razor` — clase CSS de tema en
  `.claudemeter-root`.
- `src/ClaudeMeter.Desktop/Pages/UsageBar.razor` — countdown renderizado
  con `@key="minutes"`.

**Ampliación de esta fase (misma `sdlc-testing`, ejecutada después de lo
anterior):** `src/ClaudeMeter.Desktop/Windowing/WindowResizeService.cs`
(nuevo), añadido **después** de que corriera la primera pasada de esta
fase, como fix de un bug real encontrado en validación manual: con el
"Tamaño de texto" de Accesibilidad de Windows subido, WebView2 aplica ese
factor como zoom sobre el contenido y una altura de ventana fija (la de
F1) recortaba el contenido con `overflow: hidden` en vez de mostrarlo
completo. `WindowResizeService.SetContentHeight(double)` es un método
`[JSInvokable]` invocado desde el nuevo `wwwroot/js/resize.js` (un
`ResizeObserver` sobre `.claudemeter-root`) que ajusta `Window.Height` y
ancla el borde inferior (`Window.Top -= delta`) para que el widget crezca
hacia arriba en vez de desplazar la posición donde el usuario lo dejó
(por defecto o arrastrada, F2 Ciclo B). No hay una Acceptance Criterion
formal en `docs/sdlc/requirements/f3-ux-mascota-ciclo-a.md` para este fix
(es un bug de validación manual posterior al propio documento de
requisitos, no una AC pre-existente) — no se ha inventado ninguna para la
tabla de trazabilidad de abajo.

## Tests Added/Modified

- `test/ClaudeMeter.Desktop.Tests/Rendering/CountdownFormatterTests.cs` —
  **Nuevo.** 16 casos (`[Fact]`/`[Theory]`) xUnit puro cubriendo
  `CountdownFormatter.Format`: `0` y valores negativos → `"0m"` (guarda
  defensiva `<= 0`); minutos simples (`1`, `30`, `59`, frontera exacta de
  59 antes de completar una hora); horas+minutos (`60` frontera exacta de
  1h, `61`, `90`, `119`, `1439` frontera exacta antes de completar un día);
  días+horas sin minutos sueltos (`1440` frontera exacta de 1 día, `1441`
  un minuto después sin sumar hora, `1500`, `2880` frontera exacta de 2
  días, `4319` un minuto antes de 3 días).
- `test/ClaudeMeter.Desktop.Tests/Configuration/AppConfigStoreTests.cs` —
  **Modificado.** 8 tests nuevos de `ResolveTheme`/tema: campo `theme`
  ausente → `Dark` sin `Warning` en el log (usando el `CapturingLogger<T>`
  ya existente de F2 Ciclo B); `"dark"`/`"light"` y sus variantes de
  mayúsculas (`Dark`, `DARK`, `Light`, `LIGHT`) → tema respetado
  case-insensitive, sin `Warning`; cadena no reconocida (`"blue"`, cadena
  vacía, `"oscuro"`) → `Dark` + `Warning`, sin lanzar; **tipo JSON
  incorrecto** (`"theme": 5`, un número) → confirmado por lectura del
  código y por este mismo test que cae al bloque `catch (JsonException)`
  ya existente de `Load()` (no a la rama de `ResolveTheme`), por lo que
  devuelve el `AppConfig.Default` **completo**, con `Warning` y sin
  lanzar — comportamiento ya anticipado como tal por el resumen de
  implementación, verificado aquí en vez de darlo por supuesto; round-trip
  `Save`→`Load` con tema claro y con tema oscuro por separado. También se
  actualizaron los dos `new AppConfig(...)` posicionales preexistentes
  (round-trip de `Save` y `SavePosition`) para pasar `Theme` de forma
  explícita, tal como el propio resumen de implementación invitaba a
  `sdlc-testing` a hacer — el valor por defecto de la firma del
  constructor (`Deviations` de Development) sigue siendo válido de todas
  formas y no se ha tocado.
- `test/ClaudeMeter.Desktop.Tests/Pages/UsageBarTests.cs` — **Modificado.**
  4 tests bUnit nuevos: countdown formateado renderizado
  (`"Restablece en {texto}"`) para varios `MinutesRemaining` (0, 5, 90,
  1500), consistentes con los casos de `CountdownFormatterTests`; el
  `<span>` del countdown se recrea (no se parchea) al cambiar
  `MinutesRemaining` entre dos renders del mismo componente — confirma el
  mecanismo (`@key`) que dispara la animación CSS, no la animación visual
  en sí (ver Gaps); ausencia total del bloque `.usage-bar__countdown`
  cuando `Window` es `RateLimitWindow.Unavailable`.
- `test/ClaudeMeter.Desktop.Tests/Pages/UsagePageTests.cs` — **Modificado.**
  2 tests bUnit nuevos: la clase CSS de `.claudemeter-root` corresponde
  exactamente a `Config.Theme.ToCssClass()` para `Dark`→`theme-dark` y
  `Light`→`theme-light`; con tema claro y un snapshot `Unauthorized`, la
  clase `theme-light` se sigue aplicando cuando lo que se renderiza dentro
  es `ReauthNotice` en vez de las dos `UsageBar` (AC de "ambas superficies
  reflejan el tema de forma consistente", con un único punto de aplicación
  en el contenedor raíz).
- `test/ClaudeMeter.Desktop.Tests/Windowing/WindowResizeServiceTests.cs` —
  **Nuevo (ampliación).** 4 tests xUnit puro del guard de "sin ventana
  adjunta" (`_window is null` → `LogWarning` + `return`) de
  `WindowResizeService.SetContentHeight`, mismo espíritu y mismo
  `CapturingLogger<T>` ya usados en `WindowDragServiceTests.cs` (leído como
  referencia de estilo y alcance antes de escribir nada): no lanza con un
  valor típico (`480`); registra `Warning` y se ignora la llamada; es
  idempotente ante varias llamadas repetidas sin `AttachWindow()` previo
  (simula el `ResizeObserver` disparando antes de que `MainWindow` termine
  de adjuntarse); no lanza con un valor negativo (el guard corta antes de
  cualquier cálculo sobre el valor recibido, y `SetContentHeight` es
  `[JSInvokable]` — no controla lo que envía el JS de origen). La
  conversión de píxeles de dispositivo a DIPs
  (`PresentationSource.CompositionTarget.TransformFromDevice`) y la
  escritura real sobre `Window.Top`/`Window.Height` no se han cubierto:
  requieren una `System.Windows.Window` real en un hilo STA con sesión de
  escritorio real, exactamente el mismo gap ya aceptado y documentado para
  `WindowDragService.DragDelta`/`EndDrag` en ese mismo fichero de
  referencia (ver Gaps).
- `test/ClaudeMeter.Desktop.Tests/Pages/UsagePageTests.cs` — **Revisado,
  sin cambios adicionales en esta ampliación.** Su helper
  `RegisterCoreServices` ya registraba un `WindowResizeService` real
  (`new WindowResizeService(NullLogger<WindowResizeService>.Instance)`,
  con el comentario ya presente en el código explicando que
  `SetContentHeight()` nunca se invoca en estos tests bUnit porque no hay
  un `ResizeObserver` real de WebView2) desde que se añadió la clase al
  árbol de trabajo — necesario únicamente para que `UsagePage` siga
  montando (inyecta `WindowResizeService` vía `@inject`), no para verificar
  ningún comportamiento de resize en sí. Confirmado por lectura de todo el
  fichero: no hace falta ningún test nuevo aquí, la cobertura de
  comportamiento de `WindowResizeService` vive íntegramente en
  `WindowResizeServiceTests.cs`.

No se ha modificado `ReauthNoticeTests.cs` (el componente en sí no aplica
ningún tema — lo hereda de `.claudemeter-root` en `UsagePage`, ya cubierto
arriba) ni ningún fichero de `ClaudeMeter.Domain.Tests`/
`ClaudeMeter.Application.Tests`/`ClaudeMeter.Infrastructure.Tests`/
`ClaudeMeter.Console.Tests` — F3 Ciclo A no toca ninguna de esas capas
(confirmado leyendo `docs/sdlc/development/f3-ux-mascota-ciclo-a.md` y el
propio `Files Changed`, todos bajo `ClaudeMeter.Desktop`).

## Coverage Result

- Target (`testingCoverage` en `.claude/sdlc.config.yaml`): **70%**
- Comando: `dotnet test test/ClaudeMeter.Desktop.Tests/ClaudeMeter.Desktop.Tests.csproj --collect:"XPlat Code Coverage"`
  (Coverlet, ya presente en el `.csproj`, sin herramienta nueva).
- Resultado de ejecución: **115/115 correctos** en `ClaudeMeter.Desktop.Tests`
  (91 preexistentes + 16 de `CountdownFormatterTests` + 8 de tema en
  `AppConfigStoreTests` + 4 de countdown en `UsageBarTests` + 2 de tema en
  `UsagePageTests`; las cuentas preexistentes ya incluían los 8 tests de
  chime añadidos en F2 Ciclo B).

**Ampliación de esta fase — `WindowResizeService`:**

- Comando: el mismo de arriba, re-ejecutado tras añadir
  `WindowResizeServiceTests.cs`.
- Resultado de ejecución: **119/119 correctos** en `ClaudeMeter.Desktop.Tests`
  (115 anteriores + 4 de `WindowResizeServiceTests`).
- Cobertura de `ClaudeMeter.Desktop.Windowing.WindowResizeService`, leída de
  `coverage.cobertura.xml`: **line-rate 29.16%, branch-rate 8.33%** — por
  debajo del 70% objetivo, **gap aceptado y documentado explícitamente
  abajo, no un descuido**, con el mismo criterio ya usado y ya aceptado
  para `WindowDragService` (line-rate 47.8%, branch-rate 25%, F2 Ciclo B):
  solo el guard de "sin ventana adjunta" (líneas 36-39, `.ctor`) es
  alcanzable de forma determinista sin una `System.Windows.Window` real en
  un hilo STA con sesión de escritorio real; `AttachWindow` (línea 25),
  el resto de `SetContentHeight` (guard de `Dispatcher.CheckAccess()`,
  cálculo del `delta` y escritura sobre `Window.Top`/`Window.Height`) y la
  totalidad de `ToDeviceIndependentPixels`
  (`PresentationSource.CompositionTarget.TransformFromDevice`) quedan sin
  cubrir por la misma razón. No se ha añadido ningún test artificial para
  forzar el número.
- Confirmación final, solución completa: `dotnet test ClaudeMeter.sln` —
  **258/258 correctos, 0 fallos** (77 `ClaudeMeter.Domain.Tests`, 20
  `ClaudeMeter.Console.Tests`, 42 `ClaudeMeter.Infrastructure.Tests`, 119
  `ClaudeMeter.Desktop.Tests`; `ClaudeMeter.Application.Tests` sigue sin
  tests, scaffolding vacío preexistente sin relación con esta fase) — los
  254 tests que ya estaban en verde antes de esta ampliación (todo el
  resto de la solución más los 115 de `ClaudeMeter.Desktop.Tests`) siguen
  en verde, sin ninguna regresión introducida por `WindowResizeService` ni
  por sus tests. `dotnet build ClaudeMeter.sln -c Release` (con
  `TreatWarningsAsErrors`) compila **sin advertencias ni errores**,
  incluyendo `WindowResizeServiceTests.cs`.
- Cobertura por clase, leída de `coverage.cobertura.xml`, para el código
  nuevo/modificado de F3 Ciclo A:
  - `ClaudeMeter.Desktop.Rendering.CountdownFormatter`: **line-rate 100%,
    branch-rate 100%.**
  - `ClaudeMeter.Desktop.Configuration.AppConfig`: **100%/100%.**
  - `ClaudeMeter.Desktop.Configuration.AppThemeExtensions`: **100%/100%.**
  - `ClaudeMeter.Desktop.Configuration.AppConfigStore`: **line-rate 98.85%,
    branch-rate 95.23%** (sube desde el 98.6%/94.4% de F2 Ciclo B al
    incorporar `ResolveTheme`).
  - `ClaudeMeter.Desktop.Pages.UsageBar`: **line-rate 100%, branch-rate
    100%.**
  - `ClaudeMeter.Desktop.Pages.UsagePage`: **line-rate 95.94%, branch-rate
    90%** — mismo ~4-5% de gap ya aceptado en F1/F2 (ver Gaps), sin cambio
    de alcance por este ciclo.
- **Achieved: 95.94%-100% en las seis clases nuevas/modificadas de este
  ciclo — muy por encima del 70% objetivo.** No ha hecho falta ningún test
  adicional de relleno para alcanzar el umbral; el único gap real
  (`UsagePage`, ~4-5% de branch-rate) es el mismo ya documentado y
  aceptado en ciclos anteriores, no uno nuevo introducido por F3 Ciclo A.
- Confirmación final: `dotnet build ClaudeMeter.sln -c Release` (con
  `TreatWarningsAsErrors`) compila **sin advertencias ni errores**,
  incluyendo los ficheros de test nuevos/modificados. No se ha vuelto a
  ejecutar la suite completa de `ClaudeMeter.Domain.Tests`/
  `ClaudeMeter.Application.Tests`/`ClaudeMeter.Infrastructure.Tests`/
  `ClaudeMeter.Console.Tests` porque F3 Ciclo A no toca ninguna de esas
  capas (confirmado por grep: `AppConfig`/`AppTheme` no se referencian
  fuera de `ClaudeMeter.Desktop`) y el build Release en solución completa
  ya confirma que nada dejó de compilar en ningún proyecto.

## Gaps / Not Covered

- **`UsagePage`, mismo ~4-5% de branch-rate ya aceptado por F1/F2 Ciclo A y
  reiterado por F2 Ciclo B** (carrera benigna
  `ObjectDisposedException`/`InvalidOperationException` en
  `OnSnapshotReceived`, guarda `_coordinator is null` de
  `IsPollingActiveForTests`) — sin cambios de alcance introducidos por este
  ciclo.
- **Contraste visual real de la paleta clara (umbral verde/ámbar/rojo
  legible sobre fondo claro) — no verificable por bUnit.** bUnit renderiza
  el árbol de componentes y sus atributos/clases CSS, pero no ejecuta un
  motor CSS real que resuelva `var(--color-normal)` etc. sobre un lienzo;
  `UsageBarTests` ya confirma que la clase de color (`usage-bar--green`/
  `--amber`/`--red`) sigue aplicándose de forma idéntica bajo ambos temas
  (la regla de umbral es independiente del tema, por diseño), pero el
  valor de color resultante y su legibilidad real son responsabilidad de
  `app.css` y solo verificables visualmente — ya señalado como tal en
  Risks del documento de requisitos.
- **La animación CSS del countdown en sí (fundido/`transform`) — no
  verificable por bUnit.** Se confirma el mecanismo que la dispara
  (`@key="minutes"` fuerza la recreación del nodo `<span>` en vez de un
  parche in-place, verificado con `UsageBar_ConMinutesRemainingDisponible_AplicaElKeyIgualAlValorDeMinutos`),
  pero no la animación `@keyframes` en sí ni su coste real de CPU/GPU — un
  navegador/motor de renderizado real es necesario, fuera del alcance de
  bUnit (mismo criterio de "no testear el motor de renderizado de Blazor"
  de `blazor-testing-bunit.md`).
- No se ha escrito ningún test de integración (Testcontainers/DB/HTTP): no
  aplica — F3 Ciclo A no introduce ninguna dependencia nueva de base de
  datos ni de red; toda la superficie nueva es lógica pura
  (`CountdownFormatter`), E/S de fichero local ya cubierta con ficheros
  temporales reales (`AppConfigStore`) o componentes Razor cubiertos con
  bUnit, consistente con la política de testing de `CLAUDE.md`.
- **`WindowResizeService`, branch-rate 8.33% — gap aceptado (ampliación de
  esta fase), mismo criterio ya autorizado para `WindowDragService` en F2
  Ciclo B.** Solo la rama de "sin ventana adjunta" (`_window is null` →
  `Warning` + `return`) es alcanzable de forma determinista sin una
  `System.Windows.Window` real en un hilo STA con sesión de escritorio
  real. El resto — el guard de `Dispatcher.CheckAccess()` (con su
  reentrada vía `Dispatcher.Invoke`), el cálculo de `delta` y la escritura
  final sobre `Window.Top`/`Window.Height`, y la totalidad de
  `ToDeviceIndependentPixels` (que depende de
  `PresentationSource.FromVisual(_window).CompositionTarget.TransformFromDevice`,
  solo disponible cuando la ventana está realmente compuesta en un
  `HwndSource`) — requiere esa ventana real; no hay forma determinista de
  simular un `ResizeObserver` real de WebView2 disparando
  `SetContentHeight` con una ventana WPF de verdad detrás. Mismo criterio
  ya aceptado por Development/Design/Testing para `MainWindow`/
  `Win32ScreenInfo`/`WindowDragService.DragDelta`/`EndDrag` en ciclos
  anteriores. No hay Acceptance Criterion formal asociada (bug de
  validación manual posterior al documento de requisitos de este ciclo,
  ver Scope) — la mitigación más cercana es la validación manual descrita
  en el propio hallazgo del bug: ejecutar la app real con el "Tamaño de
  texto" de Accesibilidad de Windows subido y confirmar que el contenido
  ya no se recorta.

## Acceptance Criteria Coverage

| Acceptance Criterion | Covered by |
|---|---|
| US-1 GIVEN sin `config.json`/sin campo `theme` WHEN arranca THEN tema oscuro, sin regresión | `AppConfigStoreTests.Load_ConThemeAusente_UsaDarkPorDefectoSinLoguearWarning` + `UsagePageTests.UsagePage_ConTemaEnAppConfig_AplicaLaClaseCssCorrespondienteAlContenedorRaiz(Dark)` |
| US-1 GIVEN `config.json` válido con tema claro WHEN arranca THEN paleta clara propia | `AppConfigStoreTests.Load_ConThemeValidoCaseInsensitive_LoRespeta("light", Light)` + `UsagePageTests.UsagePage_ConTemaEnAppConfig_AplicaLaClaseCssCorrespondienteAlContenedorRaiz(Light)` (clase CSS aplicada; los valores de color concretos viven en `app.css`, ver Gaps) |
| US-1 GIVEN tema no reconocido o de tipo incorrecto WHEN `Load` lo resuelve THEN cae a oscuro + `Warning`, sin excepción | `AppConfigStoreTests.Load_ConThemeDeCadenaNoReconocida_CaeADarkYRegistraWarningSinLanzar` (cadena inválida) + `Load_ConThemeDeTipoJsonIncorrecto_CaeADefaultCompletoConWarningSinLanzar` (número) |
| US-1 GIVEN tema claro activo WHEN se renderizan `UsagePage`/`UsageBar` y `ReauthNotice` THEN ambas reflejan el tema de forma consistente | `UsagePageTests.UsagePage_ConTemaClaroYUnauthorized_AplicaLaClaseDeTemaTambienCuandoSeRenderizaReauthNotice` |
| US-1 GIVEN tema claro activo WHEN `UsageBar` en Normal/Warning/Crítico THEN colores distintos y legibles sobre fondo claro | Manual validation — see Definition of Done (contraste visual real; la clase de color en sí es independiente del tema y ya está cubierta por `UsageBarTests` existente de F1) |
| US-1 GIVEN usuario edita `theme` en `config.json` en caliente WHEN la app se reinicia THEN arranca con el nuevo tema | `AppConfigStoreTests.Save_ConTemaClaro_...`/`Save_ConTemaOscuro_...` (round-trip) + `Load_ConThemeValidoCaseInsensitive_LoRespeta` |
| US-2 GIVEN snapshot con éxito y `MinutesRemaining` disponible WHEN se renderiza `UsageBar` THEN se muestra el tiempo restante como texto | `UsageBarTests.UsageBar_ConMinutesRemainingDisponible_RenderizaElCountdownFormateado` |
| US-2 GIVEN countdown mostrando un valor WHEN un nuevo ciclo lo actualiza a otro distinto THEN transición animada | `UsageBarTests.UsageBar_ConMinutesRemainingDisponible_AplicaElKeyIgualAlValorDeMinutos` (mecanismo `@key` que dispara la animación; la animación visual en sí no es verificable por bUnit, ver Gaps) |
| US-2 GIVEN `MinutesRemaining` llega a 0 WHEN se renderiza THEN estado de cero definido, sin animación a medias | `CountdownFormatterTests.Format_ConCero_DevuelveCeroM` (formato `"0m"`) + `UsageBarTests.UsageBar_ConMinutesRemainingDisponible_RenderizaElCountdownFormateado(0, "0m")` |
| US-2 GIVEN sin datos o `RateLimitWindow.Unavailable` WHEN se renderiza `UsageBar` THEN no se anima ningún valor ausente, sin excepción | `UsageBarTests.UsageBar_ConWindowUnavailable_NoRenderizaNingunCountdown` + `UsageBar_ConWindowUnavailable_MuestraNoDisponibleEnEstadoNeutroSinLanzar` (ya existente) |
| US-2 GIVEN la animación se dispara en cada ciclo de poll WHEN se ejecuta repetidamente THEN sin impacto perceptible de CPU/GPU | Manual validation — see Definition of Done |

## Definition of Done

(Copiada literalmente de `docs/sdlc/requirements/f3-ux-mascota-ciclo-a.md`.)

- [ ] Todas las Acceptance Criteria de US-1 y US-2 anteriores se cumplen —
      **las verificables por tests automáticos se cumplen** (ver tabla de
      trazabilidad: 9 de las 11 ACs tienen test dedicado, verde); las 2
      restantes (contraste visual real de la paleta clara, fluidez/coste
      real de CPU-GPU de la animación) dependen de un renderizado
      WPF/WebView2 real que ningún agente de este pipeline puede producir
      — ver los ítems PENDIENTE de abajo, que impiden marcar esto como
      100% cerrado solo con esta fase.
- [x] Cobertura de tests automatizados (bUnit para el renderizado de
      `UsageBar`/`UsagePage`/`ReauthNotice` bajo ambos temas y para la
      presencia/transición de clases CSS del countdown; xUnit puro para la
      resolución del nuevo campo `theme` en `AppConfigStore`, incluyendo el
      caso de valor inválido con fallback) según el mínimo configurado en
      `.claude/sdlc.config.yaml` (`testingCoverage: 70`) — **conseguido:
      95.94%-100% en las seis clases nuevas/modificadas de este ciclo**
      (`CountdownFormatter`, `AppConfig`, `AppThemeExtensions`,
      `AppConfigStore`, `UsageBar`, `UsagePage`), con 115/115 tests en
      verde (`dotnet test test/ClaudeMeter.Desktop.Tests/ClaudeMeter.Desktop.Tests.csproj --collect:"XPlat Code Coverage"`).
- [x] Ninguna prueba automatizada depende de un tema real de Windows ni de
      un `config.json` real del usuario — los 8 tests nuevos de tema en
      `AppConfigStoreTests` reutilizan el mismo directorio temporal propio
      por test (`IDisposable`, nunca `%LOCALAPPDATA%`) ya establecido en F2
      Ciclo B, y los tests bUnit de `UsageBarTests`/`UsagePageTests`
      inyectan instancias de `AppConfig`/`RateLimitWindow` simuladas
      directamente vía `Services.AddSingleton`/parámetros de componente,
      nunca leyendo ningún fichero real ni el tema real del sistema
      operativo — verificado por revisión de cada test nuevo de esta fase.
- [ ] **PENDIENTE — Validación manual (US-1):** se confirma, ejecutando la
      aplicación real en Windows con `config.json` en tema claro y en tema
      oscuro, que ambas paletas se ven correctamente y son legibles sobre
      el escritorio real. Ningún agente de este pipeline puede verificar
      renderizado real de WPF/WebView2 ni contraste de color real;
      `AppConfigStoreTests`/`UsagePageTests` son la mitigación
      automatizable más cercana (la misma lógica de decisión de tema,
      verificada de forma determinista), pero no sustituyen esta
      validación.
- [ ] **PENDIENTE — Validación manual (US-2):** se confirma, ejecutando la
      aplicación real durante varios ciclos de poll, que la animación del
      countdown se percibe fluida y sin impacto notable de CPU/GPU (AC
      explícita de la issue #15). `UsageBarTests` verifica el mecanismo
      (`@key`) que dispara la animación y el formato del texto, pero la
      percepción de fluidez y el coste real de rendimiento en un equipo
      real no son medibles de forma automática por este pipeline.

---

Nada se ha confirmado (`commit`) ni enviado (`push`) al repositorio: los
ficheros de test nuevos/modificados
(`test/ClaudeMeter.Desktop.Tests/Rendering/CountdownFormatterTests.cs`,
`test/ClaudeMeter.Desktop.Tests/Configuration/AppConfigStoreTests.cs`,
`test/ClaudeMeter.Desktop.Tests/Pages/UsageBarTests.cs`,
`test/ClaudeMeter.Desktop.Tests/Pages/UsagePageTests.cs`,
`test/ClaudeMeter.Desktop.Tests/Windowing/WindowResizeServiceTests.cs`
— este último añadido en la ampliación de esta fase) quedan en el árbol de
trabajo, listos para revisión, junto con el resto de cambios de F3 Ciclo A
ya presentes sin confirmar. Ningún fichero fuera de `test/` se ha tocado
en esta fase (ni en la pasada original ni en esta ampliación), salvo este
propio informe.
