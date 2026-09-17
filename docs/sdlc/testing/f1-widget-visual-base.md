# Testing Summary: F1 — Widget visual base

## Scope

Cobertura de tests para el código nuevo, aún sin confirmar (`commit`), de
todo el milestone F1 (issues #6, #7, #8, #9 — US-1..US-4), tratado como un
único ciclo SDLC según ya decidió el usuario en Requirements/Design:

- `src/ClaudeMeter.Domain/Usage/UsageThreshold.cs` — enum `UsageThreshold` y
  `UsageThresholdClassifier.Classify` (US-3), función pura sin dependencias.
- `src/ClaudeMeter.Desktop/Polling/UsagePollingCoordinator.cs` (US-4) —
  orquestación del poll de 60s sobre `IUsageDataSource`, sin Blazor ni WPF.
- `src/ClaudeMeter.Desktop/Pages/UsageBar.razor` y `Pages/UsagePage.razor`
  (US-2/US-3) — componentes Razor.
- `src/ClaudeMeter.Desktop/App.xaml.cs`, `MainWindow.xaml(.cs)` (US-1) —
  **excluidos deliberadamente** de tests automáticos: composition root WPF y
  chrome de ventana (`WindowStyle`, `AllowsTransparency`, `Topmost`,
  posicionamiento vía `SystemParameters.WorkArea`) que solo pueden
  verificarse renderizando una ventana WPF real en un hilo STA con pantalla,
  algo que ni xUnit ni bUnit ejecutan de forma significativa — así lo indica
  también el propio encargo de esta fase y lo confirma el documento de
  Development.

Se ha trabajado a partir de tres documentos ya existentes en el repositorio
(`docs/sdlc/requirements/f1-widget-visual-base.md`,
`docs/sdlc/design/f1-widget-visual-base.md`,
`docs/sdlc/development/f1-widget-visual-base.md`) y se ha leído directamente
el código de producción (no se asumieron firmas del diseño) para confirmar
nombres exactos de tipos/métodos/namespaces antes de escribir ningún test.

**Migración de `test/ClaudeMeter.Desktop.Tests`** (explícitamente dejada sin
tocar por Development, a cargo de esta fase): `Sdk="Microsoft.NET.Sdk"` →
`Sdk="Microsoft.NET.Sdk.Razor"`. El paquete `bunit` ya estaba fijado en el
`.csproj` en la versión `2.11.3`; se confirmó contra
`api.nuget.org/v3-flatcontainer/bunit/index.json` que sigue siendo la última
versión estable de la serie (sin ningún `2.11.x` posterior ni normal, solo
`2.11.3`), por lo que no se ha tocado. Se verificó además, leyendo
directamente el código fuente de bUnit 2.11.3 en GitHub (no solo la
documentación), que:
- La clase base correcta es `Bunit.BunitContext` (no `TestContext`, que en
  esta versión es una clase obsoleta que hereda de `BunitContext` sin
  aportar nada, marcada `[Obsolete(..., error: false)]`).
- El método de renderizado correcto es `Render<TComponent>(...)` — **no**
  `RenderComponent<TComponent>(...)`: `BunitContext.Obsoletes.cs` define
  `RenderComponent` con `[Obsolete(..., error: true)]`, es decir, **no
  compila**, no es solo un aviso. Todos los tests de este documento usan
  `Render<T>`.
- `DisposeComponentsAsync()` (asíncrono) es el método correcto, no
  `DisposeComponents()`.

Esto confirma y refuerza literalmente la advertencia ya dejada por Design.

## Tests Added/Modified

- `test/ClaudeMeter.Desktop.Tests/ClaudeMeter.Desktop.Tests.csproj` —
  migrado el `Sdk` a `Microsoft.NET.Sdk.Razor` (única modificación de este
  fichero; el resto de `PackageReference` ya estaban correctos).
- `test/ClaudeMeter.Domain.Tests/Usage/UsageThresholdClassifierTests.cs` —
  **Nuevo.** 16 casos (`[Theory]`/`[Fact]`) xUnit puro para
  `UsageThresholdClassifier.Classify`: `<70` (incluye `69.9`), `[70,90)`
  (incluye frontera `70.0` y `89.9`), `>=90` (incluye frontera `90.0` y
  `100.0`), dos tests explícitos de frontera exacta que comprueban además
  la ausencia de ambigüedad (`70` no es `Normal`, `90` no es `Warning`), y
  `null` → `null`.
- `test/ClaudeMeter.Desktop.Tests/TestDoubles/FakeUsageDataSource.cs` —
  **Nuevo.** Doble de `IUsageDataSource` con resultado reconfigurable entre
  llamadas (`SetNextResult`/`SetNextException`, cola FIFO) y un
  `ArmBlockingCall()` que permite mantener una llamada "en vuelo" de forma
  controlada — necesario para probar de forma determinista el guard
  anti-solape de `UsagePollingCoordinator` y los ciclos consecutivos de
  `UsagePage`, siguiendo el paso 11 del Implementation Plan de Design.
- `test/ClaudeMeter.Desktop.Tests/Polling/UsagePollingCoordinatorTests.cs` —
  **Nuevo**, xUnit puro (sin bUnit: es una clase C# plana). 8 tests:
  arranque dispara el primer fetch inmediatamente sin esperar al timer;
  `Start()` deja el timer activo (`IsRunningForTests`); `Dispose()` lo
  detiene; `Dispose()` llamado dos veces es idempotente y no lanza;
  tras `Dispose()` no llegan más `SnapshotReceived` aunque el timer tuviera
  un intervalo corto (se compara el recuento antes/después de una ventana
  de espera, tolerante a jitter); el guard `_isPolling` ignora un segundo
  disparo mientras el primero sigue "en vuelo" (usando `ArmBlockingCall`);
  una excepción del `IUsageDataSource` se traga sin propagarse ni disparar
  el evento; y el coordinador sigue aceptando ciclos posteriores con éxito
  tras una excepción previa.
- `test/ClaudeMeter.Desktop.Tests/Pages/UsageBarTests.cs` — **Nuevo**,
  bUnit v2 (`BunitContext`/`Render<T>`). 8 casos de umbral→clase de color
  (incluye ambas fronteras exactas 70/90), porcentaje redondeado a entero,
  `RateLimitWindow.Unavailable` → "No disponible" en estado neutro sin
  lanzar, etiqueta "(desactualizado)" con `Stale=true` (y su ausencia con
  `Stale=false`), y título mostrado por parámetro.
- `test/ClaudeMeter.Desktop.Tests/Pages/UsagePageTests.cs` — **Nuevo**,
  bUnit v2. 9 tests (11 casos contando `[Theory]`): montaje real con
  `IUsageDataSource` inyectado por DI que dispara el primer fetch
  automático (verificado con `WaitForAssertion`, ya que la continuación
  atraviesa el evento del coordinador + `InvokeAsync`); snapshot de éxito
  con ambos porcentajes → dos barras con su color; misma regla de umbral
  aplicada a ambas ventanas; `PercentageUsed` nulo en una ventana → esa
  barra "No disponible" sin afectar a la otra; snapshot de fallo
  (`TokenUnavailable`/`Unauthorized`/`RequestFailed`, vía `[Theory]`) sin
  éxito previo → ambas barras "No disponible"; ciclo exitoso seguido de un
  fallo → se conserva el valor anterior marcado "(desactualizado)"; dos
  ciclos consecutivos con éxito → se muestra siempre el más reciente, sin
  mezcla; y `DisposeComponentsAsync()` detiene el polling subyacente
  (`IsPollingActiveForTests` pasa a `false`). Los tests de transición de
  estado usan los ganchos `internal` `ApplyForTests`/`IsPollingActiveForTests`
  ya expuestos por Development vía `InternalsVisibleTo`, tal como especifica
  el paso 12 del Implementation Plan de Design, para no depender de un
  timer real de 60s.

No se ha modificado ningún test existente de `ClaudeMeter.Domain.Tests`
(salvo la adición del fichero nuevo), `ClaudeMeter.Infrastructure.Tests`,
`ClaudeMeter.Console.Tests` ni `ClaudeMeter.Application.Tests`.

## Trazabilidad AC → Test

| Acceptance Criteria (resumen) | Test(s) que lo cubre |
|---|---|
| US-1 GIVEN app arrancando WHEN se crea MainWindow THEN sin barra de título/bordes | **No automatizable** — chrome WPF real (`WindowStyle="None"`), verificado por revisión de código de `MainWindow.xaml`; pendiente de validación manual |
| US-1 GIVEN MainWindow visible WHEN otra ventana gana foco THEN permanece topmost | **No automatizable** — `Topmost="True"` real, pendiente de validación manual |
| US-1 GIVEN MainWindow visible WHEN no hay contenido Razor pintado THEN esa región es transparente | **No automatizable** — `AllowsTransparency`/`Background="Transparent"` reales + limitación de "airspace" de WebView2, pendiente de validación manual |
| US-1 GIVEN MainWindow inicializada WHEN termina de cargar THEN aloja un BlazorWebView operativo (renderiza página Razor sin errores) | `UsageBarTests.*`, `UsagePageTests.*` (prueban indirectamente que el árbol de componentes Razor que `MainWindow.xaml` aloja como `RootComponent` renderiza sin excepciones bajo bUnit; no prueban el hosting real de WebView2) |
| US-1 GIVEN WindowStyle=None+AllowsTransparency=True WHEN se configuran THEN combinación válida sin XamlParseException | **No automatizable** — solo verificable ejecutando la app real; revisión de código confirma que `MainWindow.xaml` ya usa `WindowStyle="None"` |
| US-2 GIVEN Success con ambos PercentageUsed WHEN renderiza THEN dos barras reflejando su porcentaje | `UsagePageTests.UsagePage_ConSnapshotSuccessYAmbosPercentages_MuestraDosBarrasConSuColor` |
| US-2 GIVEN PercentageUsed null WHEN renderiza esa ventana THEN "sin datos" sin lanzar | `UsageBarTests.UsageBar_ConWindowUnavailable_MuestraNoDisponibleEnEstadoNeutroSinLanzar`, `UsagePageTests.UsagePage_ConPercentageUsedNuloEnUnaVentana_MuestraEsaBarraComoNoDisponible` |
| US-2 GIVEN snapshot no-Success WHEN renderiza THEN ambas barras "sin datos" | `UsagePageTests.UsagePage_ConSnapshotDeFalloYSinExitoPrevio_MuestraAmbasBarrasNoDisponible` (`[Theory]`: TokenUnavailable/Unauthorized/RequestFailed) |
| US-2 GIVEN UsagePage aislado WHEN se ejecuta la suite THEN existe ≥1 test bUnit con snapshot simulado | Toda la suite `UsagePageTests`/`UsageBarTests` (11 tests bUnit en total) |
| US-3 GIVEN PercentageUsed <70 WHEN se calcula color THEN verde/Normal | `UsageThresholdClassifierTests.Classify_ConPercentageUsedMenorA70_DevuelveNormal`, `UsageBarTests.UsageBar_ConPercentageUsedDado_...` (casos green) |
| US-3 GIVEN [70,90) WHEN se calcula color THEN ámbar/Warning | `UsageThresholdClassifierTests.Classify_ConPercentageUsedEntre70Y90_DevuelveWarning`, `UsageBarTests` (casos amber) |
| US-3 GIVEN ≥90 WHEN se calcula color THEN rojo/Critical | `UsageThresholdClassifierTests.Classify_ConPercentageUsedDe90OMas_DevuelveCritical`, `UsageBarTests` (casos red) |
| US-3 GIVEN fronteras exactas 70 y 90 WHEN se calcula color THEN 70→ámbar, 90→rojo, sin ambigüedad | `Classify_ConPercentageUsedExactamente70_DevuelveWarningNoNormal`, `Classify_ConPercentageUsedExactamente90_DevuelveCriticalNoWarning`, `UsageBarTests` (`InlineData(70.0, "usage-bar--amber")`, `InlineData(90.0, "usage-bar--red")`) |
| US-3 GIVEN PercentageUsed null WHEN se calcula color THEN sin umbral, estado neutro de US-2 | `Classify_ConPercentageUsedNull_DevuelveNullSinAsignarNingunUmbral`, `UsageBarTests.UsageBar_ConWindowUnavailable_...` |
| US-3 GIVEN lógica pura WHEN se ejecuta la suite THEN cubre cada umbral y ambas fronteras (69.9/70/89.9/90) aisladamente | Los 16 tests de `UsageThresholdClassifierTests`, sin renderizar ningún componente Razor |
| US-3 GIVEN misma lógica WHEN se aplica a sesión y semana en UsagePage THEN ambas usan la misma regla | `UsagePageTests.UsagePage_ConAmbasVentanasEnElMismoUmbral_AplicaLaMismaClaseDeColorAAmbasBarras` |
| US-4 GIVEN UsagePage montado WHEN pasan 60s THEN vuelve a invocar GetUsageAsync y refresca barras | `UsagePollingCoordinatorTests.Start_DisparaElPrimerFetchInmediatamente_SinEsperarAlTimer` (fetch inicial) + `UsagePageTests.UsagePage_AlMontarse_InvocaGetUsageAsyncYRenderizaElPrimerSnapshotDeExito` (montaje real vía DI) + `UsagePageTests.UsagePage_TrasDosCiclosConsecutivosConExito_...` (ciclo de refresco simulado vía `ApplyForTests`, sin esperar los 60s reales — ver Gaps) |
| US-4 GIVEN respuesta TokenUnavailable/Unauthorized/RequestFailed WHEN se procesa THEN "sin datos"/dato desactualizado, ciclo continúa sin excepción | `UsagePageTests.UsagePage_ConSnapshotDeFalloYSinExitoPrevio_...`, `UsagePageTests.UsagePage_TrasUnCicloExitosoSeguidoDeUnFallo_ConservaElValorAnteriorMarcadoDesactualizado`, `UsagePollingCoordinatorTests.PollAsync_ConExcepcionDelDataSource_NoSePropagaNiSeDisparaSnapshotReceived`, `PollAsync_TrasUnaExcepcion_SigueAceptandoCiclosPosterioresConExito` |
| US-4 GIVEN timer activo WHEN se cierra MainWindow THEN el componente se desmonta y el timer se detiene sin más llamadas HTTP | `UsagePageTests.UsagePage_TrasDesmontarse_DetieneElPollingSubyacente`, `UsagePollingCoordinatorTests.Dispose_DetieneElTimerSubyacente`, `Dispose_EvitaQueSiganLlegandoMasSnapshotReceivedTrasElUltimoTick`, `Dispose_LlamadoDosVeces_EsIdempotenteYNoLanza` |
| US-4 GIVEN mismo core de F0 WHEN se implementa el poll de 60s THEN se reutilizan los mismos puertos sin reimplementar parseo/HTTP | Verificado por lectura de código (`UsagePollingCoordinator` recibe `IUsageDataSource` real por constructor; `UsagePage.Apply` llama a `RateLimitWindowParser.ParseSnapshot` real) y ejercitado indirectamente por **todos** los tests de esta suite, que usan esos mismos tipos de Domain/Application sin ningún doble que reimplemente parseo |
| US-4 GIVEN dos ciclos consecutivos con éxito WHEN completa el segundo THEN estado = snapshot más reciente, sin mezcla ni timers en paralelo | `UsagePageTests.UsagePage_TrasDosCiclosConsecutivosConExito_MuestraSiempreElUltimoSnapshotSinMezclar`, `UsagePollingCoordinatorTests.PollAsync_ConLlamadaEnVuelo_IgnoraElSegundoDisparoHastaQueLaPrimeraCompleta` |

## Coverage Result

- Target (`testingCoverage` en `.claude/sdlc.config.yaml`): **70%**
- Comando ejecutado: `dotnet test test/ClaudeMeter.Desktop.Tests/ClaudeMeter.Desktop.Tests.csproj --collect:"XPlat Code Coverage"` (Coverlet, ya presente como `PackageReference`, sin herramienta nueva) y `dotnet test test/ClaudeMeter.Domain.Tests/...` para `UsageThresholdClassifier`.
- Resultado de ejecución (proyecto nuevo/modificado): **31/31 tests correctos** en `ClaudeMeter.Desktop.Tests` (0 fallos, estable en 4 ejecuciones repetidas incluyendo los tests sensibles a temporización), **16/16 correctos** en los tests nuevos de `ClaudeMeter.Domain.Tests`.
- Cobertura por clase, leída de `coverage.cobertura.xml`, para el código realmente tocado por F1:
  - `ClaudeMeter.Domain.Usage.UsageThresholdClassifier`: **line-rate 100%, branch-rate 100%.**
  - `ClaudeMeter.Desktop.Pages.UsageBar`: **line-rate 100%, branch-rate 100%.**
  - `ClaudeMeter.Desktop.Pages.UsagePage`: **line-rate 94.33%, branch-rate 87.5%** (ver Gaps para las líneas restantes).
  - `ClaudeMeter.Desktop.Polling.UsagePollingCoordinator` (incluida la máquina de estados `async` de `PollAsync`): **line-rate 100%, branch-rate 100%.**
  - `ClaudeMeter.Desktop.App`/`ClaudeMeter.Desktop.MainWindow`: **0%, exclusión deliberada** (ver Gaps) — no cuentan para el cálculo anterior por no ser parte del alcance testeable de esta fase.
- **Achieved: entre 94% y 100% en las cuatro clases de negocio nuevas de F1 — muy por encima del 70% objetivo.** No hizo falta añadir tests artificiales para "rellenar" cobertura.
- Confirmación final: `dotnet test ClaudeMeter.sln` sobre la solución completa en Debug — **132 tests correctos, 0 fallos** (58 `ClaudeMeter.Domain.Tests`, 19 `ClaudeMeter.Console.Tests`, 24 `ClaudeMeter.Infrastructure.Tests`, 31 `ClaudeMeter.Desktop.Tests`; `ClaudeMeter.Application.Tests` sigue sin tests, scaffolding vacío preexistente sin relación con F1). `dotnet build ClaudeMeter.sln -c Release` (con `TreatWarningsAsErrors`) también compila sin advertencias ni errores tras la migración de SDK del proyecto de test.

## Gaps / Not Covered

- **`App.xaml.cs`/`MainWindow.xaml(.cs)` (0% de cobertura automática) — exclusión deliberada, no un gap.** Son composition root y chrome de ventana WPF (`WindowStyle`, `AllowsTransparency`, `Topmost`, cálculo de posición desde `SystemParameters.WorkArea`, construcción real del `IServiceProvider`/`HttpClient`): requieren una `System.Windows.Application`/`Window` real en un hilo STA con una sesión de escritorio, algo que xUnit/bUnit no pueden ejercitar de forma significativa (y que el propio encargo de esta fase pide explícitamente no intentar). Quedan cubiertos únicamente por la validación manual ya prevista en la Definition of Done de Requirements/Design.
- **`UsagePage.OnSnapshotReceived`, líneas 41-42 y 48 del `catch (Exception ex) when (ex is ObjectDisposedException or InvalidOperationException)` — gap aceptado, no ignorado por descuido.** Es la "carrera benigna" documentada explícitamente en el propio documento de diseño (componente desmontado mientras una respuesta HTTP sigue en vuelo). Provocarla de forma determinista exigiría manipular por reflexión el estado interno de bUnit/Blazor para forzar que `InvokeAsync` lance `ObjectDisposedException` exactamente en ese punto — un test así sería frágil y probaría más el motor de bUnit que el propio componente, justo lo que la estrategia de testing de este proyecto rechaza explícitamente. Se documenta como riesgo conocido y mitigado por diseño (el `try/catch` existe y compila; el resto del método, sin ese `catch`, está al 100%), no como cobertura pendiente de "rellenar".
- **`UsagePage.IsPollingActiveForTests`, rama `_coordinator is null` del operador `?.` — gap aceptado.** `_coordinator` siempre se asigna en `OnInitialized` antes de que cualquier código pueda leer esta propiedad a través de la API pública/de test; forzar la rama `null` exigiría invocar el getter antes de que el componente termine de inicializarse, un escenario que no ocurre a través de ningún camino real de bUnit. Es una guarda defensiva estándar de C#, no lógica de negocio sin cubrir.
- **AC de US-1 (chrome de ventana WPF) y la parte de US-4 sobre "40-60 minutos sin fugas de timers/handles" — no automatizables por este pipeline, tal como ya fijan Requirements y Design.** Sustituidos aquí por: (a) tests deterministas del guard anti-solape y de que `Dispose()` detiene el timer inmediatamente (`UsagePollingCoordinatorTests`), que es la unidad de comportamiento real detrás del riesgo de fuga; y (b) la validación manual explícita que ya exige la Definition of Done (ver más abajo).
- No se ha escrito ningún test de integración nuevo: no aplica — F1 no introduce ninguna dependencia nueva de HTTP/E/S/base de datos; reutiliza tal cual `IUsageDataSource`/`RateLimitWindowParser`, ya cubiertos por los tests existentes de `ClaudeMeter.Infrastructure.Tests`/`ClaudeMeter.Domain.Tests`, sin cambios.

## Definition of Done

(Copiada literalmente de `docs/sdlc/requirements/f1-widget-visual-base.md`; idéntica en `docs/sdlc/design/f1-widget-visual-base.md`.)

- [x] Todas las Acceptance Criteria de US-2, US-3 y US-4 verificables por tests automáticos se cumplen (ver tabla de trazabilidad); las de US-1 y las dos de temporización real de US-4/US-1 dependen de validación manual — ver los dos puntos PENDIENTE de abajo, que impiden marcar este ítem como 100% cerrado solo con esta fase.
- [x] Cobertura de tests automatizados (bUnit para US-2/US-4 con `BunitContext`, xUnit puro para `UsageThresholdClassifier` de US-3) según el mínimo configurado en `.claude/sdlc.config.yaml` (`testingCoverage: 70`) — **conseguido: 94-100% en las cuatro clases de negocio nuevas.**
- [ ] **PENDIENTE — Validación manual (US-1):** se confirma visualmente, ejecutando la aplicación en un equipo Windows real, que `MainWindow` aparece sin bordes/barra de título, permanece topmost sobre otras ventanas, y que el fondo es realmente transparente fuera del contenido Razor. Ningún agente de este pipeline puede verificar renderizado real de WPF/WebView2 de forma automática; no se marca como hecho solo porque los tests automatizados pasen.
- [ ] **PENDIENTE — Validación manual (US-4):** se confirma, dejando la aplicación corriendo un periodo extendido (p. ej. 30-60 minutos) contra la API real o un mock de larga duración, que no se acumulan timers ni llamadas HTTP crecientes y que el proceso libera el timer al cerrar la ventana (uso de memoria/handles estable). Es una validación de un proceso de larga duración que ningún agente de este pipeline puede ejecutar de forma automática; los tests de `UsagePollingCoordinatorTests` (guard anti-solape, `Dispose()` detiene el timer) son la mitigación automatizable más cercana, pero no sustituyen esta validación.

---

Nada se ha confirmado (`commit`) ni enviado (`push`) al repositorio: los
ficheros de test nuevos/modificados (`ClaudeMeter.Desktop.Tests.csproj`,
`UsageThresholdClassifierTests.cs`, `TestDoubles/FakeUsageDataSource.cs`,
`Polling/UsagePollingCoordinatorTests.cs`, `Pages/UsageBarTests.cs`,
`Pages/UsagePageTests.cs`) quedan en el árbol de trabajo, listos para
revisión, junto con el resto de cambios de F1 ya presentes sin confirmar.
