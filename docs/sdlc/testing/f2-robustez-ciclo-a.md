# Testing Summary: F2 — Robustez, Ciclo A

## Scope

Cobertura de tests para el código nuevo/modificado, aún sin confirmar
(`commit`), del milestone F2 Ciclo A (issues #10, #11, #12 — US-1, US-2,
US-3), tratado como un único ciclo SDLC según decidió el usuario en
Requirements/Design/Development:

- `src/ClaudeMeter.Domain/Usage/UsageSnapshot.cs` — nuevo
  `UsageSnapshotStatus.MalformedResponse` + fábrica
  `UsageSnapshot.MalformedResponse()` (categoría 3 del modelo de reintento).
- `src/ClaudeMeter.Infrastructure/Usage/AnthropicApiUsageDataSource.cs` —
  nuevo parámetro `ILogger<AnthropicApiUsageDataSource>`; el caso "2xx sin
  cabeceras" ahora devuelve `MalformedResponse()` en vez de
  `RequestFailed()`; logging por intento HTTP (US-3).
- `src/ClaudeMeter.Infrastructure/Usage/RetryPolicyOptions.cs` y
  `RetryingUsageDataSource.cs` (nuevos) — decorator de `IUsageDataSource`
  con el modelo de 3 categorías de reintento de US-2.
- `src/ClaudeMeter.Desktop/Logging/SerilogConfigurator.cs` (nuevo) —
  fábrica testeable con directorio inyectable (US-3).
- `src/ClaudeMeter.Desktop/Pages/ReauthNotice.razor` (nuevo) y los cambios
  en `src/ClaudeMeter.Desktop/Pages/UsagePage.razor` (US-1: rastrea el
  `UsageSnapshotStatus` completo, sustituye las `UsageBar` por
  `ReauthNotice` cuando es `Unauthorized`).
- `src/ClaudeMeter.Desktop/Polling/UsagePollingCoordinator.cs` — nuevo
  parámetro `ILogger<UsagePollingCoordinator>`, heartbeat por tick y log
  de la red de seguridad final (US-3, logging puro sobre un flujo ya
  cubierto en F1 — ver "Gaps" para el razonamiento de por qué no se
  duplican tests ya existentes solo por esto).
- `src/ClaudeMeter.Console/Polling/UsagePollingLoop.cs` — nuevo
  `case UsageSnapshotStatus.MalformedResponse` en el switch de `Render`.

Se ha trabajado a partir de `docs/sdlc/development/f2-robustez-ciclo-a.md`
(que detalla exactamente qué se implementó, sus desviaciones respecto al
diseño, y los 19 errores `CS7036` que dejaba el build), de
`docs/sdlc/requirements/f2-robustez-ciclo-a.md` (para la tabla de
trazabilidad AC → test) y se ha leído directamente el código de producción
y de los tests ya existentes (nunca se asumieron firmas/nombres) antes de
escribir ningún test nuevo.

## Arreglo mecánico de compilación (paso previo obligatorio)

Antes de escribir ningún test nuevo, se corrigieron los 19 errores
`CS7036` ya anticipados por Development, confirmados de nuevo con
`dotnet build ClaudeMeter.sln -c Debug` al empezar esta fase — arreglo
puramente mecánico (constructor con un parámetro `ILogger<T>` adicional),
sin cambio de comportamiento:

- `test/ClaudeMeter.Infrastructure.Tests/Usage/AnthropicApiUsageDataSourceTests.cs`
  (11 sitios) — se añadió `NullLogger<AnthropicApiUsageDataSource>.Instance`
  (`Microsoft.Extensions.Logging.Abstractions`, mismo patrón ya usado por
  Development en `ClaudeMeter.Console/Program.cs`) a cada instanciación.
- `test/ClaudeMeter.Desktop.Tests/Polling/UsagePollingCoordinatorTests.cs`
  (8 sitios) — mismo arreglo con
  `NullLogger<UsagePollingCoordinator>.Instance`.

Además de la compilación, dos tests de
`AnthropicApiUsageDataSourceTests.cs` cambiaban de comportamiento (ya
anticipado por Development): `GetUsageAsync_Con200SinCabecerasUnified_...`
y `GetUsageAsync_Con200ConSoloCabeceraDe5hStatus_...` pasaron de esperar
`RequestFailed` a esperar `MalformedResponse` — se renombraron y se
reforzaron sus asserts (`Session`/`Weekly` nulos) para dejar constancia
explícita del cambio de contrato, no solo cambiar el valor esperado.

## Tests Added/Modified

- `test/ClaudeMeter.Infrastructure.Tests/Usage/AnthropicApiUsageDataSourceTests.cs`
  — **Modificado.** Arreglo de compilación (arriba) + renombrado/reforzado
  de los 2 tests de `MalformedResponse` + 4 tests nuevos de nivel de log
  (US-3): `TokenUnavailable` → `Warning`, `401` → `Error`, `500`
  (`RequestFailed`, fallo transitorio) → `Warning` sin `Error`,
  `MalformedResponse` → `Error` sin `Warning`.
- `test/ClaudeMeter.Infrastructure.Tests/Usage/CapturingLogger.cs` —
  **Nuevo.** Test double genérico de `ILogger<T>` que solo registra el
  `LogLevel` de cada llamada (nunca escribe a consola/fichero real);
  compartido por los tests de log de `AnthropicApiUsageDataSource` y
  `RetryingUsageDataSource`.
- `test/ClaudeMeter.Infrastructure.Tests/Usage/FakeUsageDataSource.cs` —
  **Nuevo.** Test double de `IUsageDataSource` con secuencia de resultados
  configurable (uno por llamada), necesario porque
  `RetryingUsageDataSource` invoca la fuente envuelta más de una vez por
  intento de reintento (a diferencia del `FakeUsageDataSource` de un solo
  uso ya existente en `ClaudeMeter.Console.Tests`).
- `test/ClaudeMeter.Infrastructure.Tests/Usage/RetryingUsageDataSourceTests.cs`
  — **Nuevo.** 13 tests xUnit puro con función de espera inyectable (sin
  segundos reales): éxito al primer intento sin backoff ni espera; éxito
  tras 1 reintento (2s) y tras 2 reintentos (2s→4s, verifica la secuencia
  exacta de backoff); agotamiento de los 3 intentos de
  `RetryPolicyOptions.Default` → se propaga el último `RequestFailed` sin
  lanzar, con solo 2 esperas (nunca tras el último intento); `[Theory]`
  que confirma que `TokenUnavailable`/`Unauthorized`/`MalformedResponse`
  nunca disparan un segundo intento (usa un segundo resultado en cola que,
  de reintentarse por error, aparecería como resultado final — no una
  aserción indirecta); mismo caso para `Success` por separado (no cabe en
  el switch por nombre); constructor público (`Task.Delay` real) cubierto
  solo en el camino sin reintento, para no introducir esperas reales de
  segundos en la suite; y 3 tests de nivel de log (US-3): `Warning` en
  cada reintento, `Error` al agotar, y ningún log propio del decorator
  para los 3 estados que nunca reintenta (el log de esa categoría ya lo
  hizo la fuente envuelta).
- `test/ClaudeMeter.Domain.Tests/Usage/UsageSnapshotTests.cs` —
  **Modificado.** 2 tests nuevos (`MalformedResponse()` produce el
  `Status`/`Session`/`Weekly`/`IsSuccess` esperados; `MalformedResponse` y
  `RequestFailed` son estados explícitamente distintos, la distinción real
  que introduce F2) + `MalformedResponse` añadido al `[Theory]` existente
  de `IsSuccess == false`.
- `test/ClaudeMeter.Console.Tests/Polling/UsagePollingLoopTests.cs` —
  **Modificado.** 1 test nuevo: `MalformedResponse` reutiliza
  `RenderRequestFailed` (nunca `RenderUnexpectedError`), tal como fija el
  diseño para la consola de F0.
- `test/ClaudeMeter.Desktop.Tests/Logging/SerilogConfiguratorTests.cs` —
  **Nuevo.** 3 tests: `CreateLogger(directorioInyectado)` crea ese
  directorio y escribe ahí un log legible (rotación/plantilla de salida
  correcta, verificado leyendo el fichero tras forzar el flush vía
  `IDisposable`), es idempotente si el directorio ya existe, y
  `DefaultLogDirectory()` calcula `%LOCALAPPDATA%\ClaudeMeter\logs` sin
  escribir ningún fichero (no se invoca `CreateLogger` con ese valor en
  ningún test, para no depender del `%LOCALAPPDATA%` real de la máquina de
  CI, tal como exige el AC de US-3).
- `test/ClaudeMeter.Desktop.Tests/Pages/ReauthNoticeTests.cs` — **Nuevo.**
  3 tests bUnit: muestra el mensaje de reautenticación; nunca lleva el
  sufijo "(desactualizado)"; no contiene ninguna `UsageBar` (el componente
  las sustituye por completo).
- `test/ClaudeMeter.Desktop.Tests/Pages/UsagePageTests.cs` —
  **Modificado.** Se corrigió una Theory existente que se había vuelto
  vacuamente verdadera tras el cambio de comportamiento (ver "Gaps
  corregidos" abajo) y se añadieron 2 tests nuevos para el estado
  `Unauthorized`.

No se ha modificado ningún test de
`test/ClaudeMeter.Desktop.Tests/Pages/UsageBarTests.cs` (no le afecta este
ciclo) ni de `ClaudeMeter.Application.Tests` (scaffolding vacío
preexistente, sin relación con F2).

## Regresión corregida en un test ya existente (no solo compilación)

Al leer `UsagePageTests.cs` se detectaron dos problemas de calidad
introducidos indirectamente por el cambio de comportamiento de F2, que
Development no podía tocar (regla dura de esa fase) y que la compilación
por sí sola no habría revelado:

1. **`UsagePage_ConSnapshotDeFalloYSinExitoPrevio_MuestraAmbasBarrasNoDisponible`**
   incluía `Unauthorized` en su `[Theory]`. Tras el cambio de F2,
   `Unauthorized` ya no renderiza ninguna `UsageBar` — pero
   `Assert.All(bars, ...)` sobre una colección vacía **pasa trivialmente**,
   así que el test seguía "en verde" sin verificar nada real para ese
   caso. Se quitó `Unauthorized` de la Theory (se cubre aparte, ver abajo),
   se añadió `MalformedResponse` (estado nuevo de F2 que sí pertenece a
   este grupo) y se añadió `Assert.Equal(2, bars.Count)` para que la
   aserción ya no pueda volver a pasar vacía por accidente.
2. **`UsagePage_TrasUnCicloExitosoSeguidoDeUnFallo_ConservaElValorAnteriorMarcadoDesactualizado`**
   usaba `UsageSnapshot.Unauthorized()` como el "fallo transitorio" tras un
   éxito previo. Con el comportamiento nuevo de F2 (AC 3 de US-1:
   `Unauthorized` nunca marca "(desactualizado)", sustituye las barras por
   completo) este test habría fallado de verdad al recompilar — se cambió
   a `RequestFailed()` para conservar la intención original de F1 (fallo
   transitorio → dato conservado y marcado "(desactualizado)"), y el caso
   `Unauthorized` se cubrió con un test dedicado y correcto
   (`UsagePage_ConUnauthorizedTrasUnCicloExitoso_...`).

Ambos se documentan explícitamente aquí en vez de corregirse en
silencio, siguiendo la regla de este agente de nunca dejar una aserción
que pase sin verificar comportamiento real.

## Trazabilidad AC → Test

| Acceptance Criteria (resumen) | Test(s) que lo cubre |
|---|---|
| US-1 GIVEN 401/403 WHEN se traduce a `UsageSnapshot` THEN `Unauthorized()` explícito, nunca excepción | `AnthropicApiUsageDataSourceTests.GetUsageAsync_Con401_DevuelveUnauthorized`, `_Con403_...` (regresión F0/F1, ya satisfecho, verificado de nuevo tras el arreglo de compilación) |
| US-1 GIVEN `Unauthorized` recibido por `UsagePage` WHEN renderiza THEN mensaje distinto de "No disponible" | `ReauthNoticeTests.ReauthNotice_AlRenderizar_MuestraElMensajeDeReautenticacion`, `UsagePageTests.UsagePage_ConUnauthorizedYSinExitoPrevio_RenderizaReauthNoticeYOcultaLasBarras` |
| US-1 GIVEN `Unauthorized` tras éxito previo WHEN se procesa THEN mismo mensaje de reautenticación, nunca "(desactualizado)" | `UsagePageTests.UsagePage_ConUnauthorizedTrasUnCicloExitoso_SustituyeLasBarrasPorReauthNoticeSinMarcarDesactualizado`, `ReauthNoticeTests.ReauthNotice_AlRenderizar_NuncaMuestraElSufijoDesactualizado` |
| US-1 GIVEN `Unauthorized` en cualquier capa WHEN se procesa THEN nunca se invoca refresco de token / nunca una segunda llamada HTTP | Verificado por inspección de código (ninguna clase de Infrastructure/Desktop contiene lógica de refresh) + `RetryingUsageDataSourceTests.GetUsageAsync_ConUnEstadoQueNuncaSeReintenta_...` (`Unauthorized` incluido: `inner.CallCount == 1`, nunca un segundo intento) |
| US-2 GIVEN fallo transitorio (red/timeout/5xx) WHEN se ejecuta la política THEN reintenta con backoff creciente hasta N intentos | `RetryingUsageDataSourceTests.GetUsageAsync_ConUnRequestFailedSeguidoDeExito_...`, `_ConDosRequestFailedSeguidosDeExito_ReintentaDosVecesConBackoffExponencialYDevuelveSuccess` (verifica la secuencia exacta 2s→4s) |
| US-2 GIVEN fallos transitorios seguidos de éxito dentro de N WHEN completa THEN `Success` final, sin error visible en esa iteración | Los mismos 2 tests de arriba: `result.Status == Success` |
| US-2 GIVEN N reintentos agotados WHEN completa el último THEN error visible, sin bloquear el siguiente poll | `RetryingUsageDataSourceTests.GetUsageAsync_ConRequestFailedEnLosTresIntentos_AgotaLosReintentosYDevuelveElUltimoRequestFailed` (3 intentos, 2 esperas, retorna sin lanzar) |
| US-2 GIVEN 2xx sin cabeceras (categoría 3) WHEN se traduce THEN error inmediato sin pasar por retry | `AnthropicApiUsageDataSourceTests.GetUsageAsync_Con200SinCabecerasUnified_DevuelveMalformedResponse`, `_Con200ConSoloCabeceraDe5hStatus_...` + `RetryingUsageDataSourceTests.GetUsageAsync_ConUnEstadoQueNuncaSeReintenta_...` (`MalformedResponse` incluido) — nota: el diseño refinó "error inmediato" a `MalformedResponse()` en vez del `RequestFailed()` literal del documento de requisitos, documentado ya por Development |
| US-2 GIVEN `Unauthorized`/`TokenUnavailable` (categoría 2) WHEN se evalúa retry THEN nunca se reintentan | `RetryingUsageDataSourceTests.GetUsageAsync_ConUnEstadoQueNuncaSeReintenta_DevuelveDeInmediatoSinSegundoIntentoNiEspera` (`[Theory]`: TokenUnavailable/Unauthorized/MalformedResponse) |
| US-2 GIVEN suite de tests de retry WHEN se ejecuta THEN existe test con secuencia fallo→éxito + tests de categorías 2/3 sin retry | Toda la suite `RetryingUsageDataSourceTests` (13 tests, `HttpMessageHandler` fake vía `AnthropicApiUsageDataSourceTests`/`FakeUsageDataSource`, nunca un token real) |
| US-3 GIVEN `App.xaml.cs` arrancando WHEN se inicializa THEN Serilog configurado, logger accesible desde `UsagePollingCoordinator`/`AnthropicApiUsageDataSource` | Verificado por inspección de código (ambos constructores exigen `ILogger<T>`, `App.xaml.cs` los conecta vía `AddLogging(b => b.AddSerilog(...))`) — `App.xaml.cs` es composition root WPF, **no automatizable** por este pipeline (mismo criterio que F1 con `MainWindow`) |
| US-3 GIVEN logger configurado WHEN se escribe THEN el fichero se persiste bajo `%LOCALAPPDATA%\ClaudeMeter\logs` (o equivalente) | `SerilogConfiguratorTests.CreateLogger_ConDirectorioInyectado_CreaEseDirectorioYEscribeElLogAhiSinTocarLocalAppData`, `DefaultLogDirectory_DevuelveLocalAppDataClaudeMeterLogs_SinEscribirNingunFichero` |
| US-3 GIVEN ciclo normal de poll WHEN completa con éxito THEN ≥1 entrada `Information`, sin ruido excesivo por tick | Verificado por inspección de código (`UsagePollingCoordinator.Start()`/`PollAsync()` registran `Information` una vez por invocación, sin bucles de log adicionales) — no se duplicó ningún test de `UsagePollingCoordinatorTests` solo para "tocar" el logger: es logging puro sobre un flujo (timer start/stop, guard anti-solape, manejo de excepción) ya cubierto al 100% por los 8 tests de F1, ver "Gaps" |
| US-3 GIVEN fallo de red/token/reintento WHEN se produce THEN ≥1 entrada `Warning` o `Error` según corresponda | `AnthropicApiUsageDataSourceTests.GetUsageAsync_ConTokenUnavailable_RegistraWarning`, `_Con401_RegistraError`, `_Con500_RegistraWarningNoError`, `_Con200SinCabecerasUnified_RegistraErrorParaMalformedResponse` + `RetryingUsageDataSourceTests.GetUsageAsync_ConUnRequestFailedSeguidoDeExito_RegistraWarningPorElReintentoSinError`, `_ConRequestFailedEnLosTresIntentos_RegistraErrorAlAgotarLosReintentos` |
| US-3 GIVEN suite de tests WHEN se ejecuta THEN Serilog verificable sin escribir logs reales en el `%LOCALAPPDATA%` de CI | `SerilogConfiguratorTests` completo (directorio temporal inyectado + `DefaultLogDirectory()` nunca invocado junto a `CreateLogger` en el mismo test) |
| (F2, no AC explícito pero comportamiento nuevo real) `UsagePollingLoop` (consola) reutiliza `RenderRequestFailed` para `MalformedResponse` | `UsagePollingLoopTests.ExecuteIterationAsync_ConMalformedResponse_ReutilizaRenderRequestFailed` |
| (F2, Domain) `UsageSnapshot.MalformedResponse()` produce el contrato "sin datos" esperado y es distinto de `RequestFailed` | `UsageSnapshotTests.MalformedResponse_DevuelveStatusMalformedResponseSinSessionNiWeekly`, `_EsUnEstadoDistintoDeRequestFailed`, `IsSuccess_ParaCualquierEstadoDeFallo_EsSiempreFalse` (con `MalformedResponse` añadido) |

## Coverage Result

- Target (`testingCoverage` en `.claude/sdlc.config.yaml`): **70%**
- Comando ejecutado por proyecto: `dotnet test <proyecto> --collect:"XPlat Code Coverage"`
  (Coverlet, ya presente como `PackageReference` en los 4 proyectos de
  test, sin herramienta nueva).
- Resultado de ejecución (proyectos tocados por este ciclo): **42/42
  correctos** en `ClaudeMeter.Infrastructure.Tests`, **61/61** en
  `ClaudeMeter.Domain.Tests`, **20/20** en `ClaudeMeter.Console.Tests`,
  **39/39** en `ClaudeMeter.Desktop.Tests`.
- Cobertura por clase, leída de `coverage.cobertura.xml`, para el código
  realmente nuevo/modificado por F2:
  - `ClaudeMeter.Infrastructure.Usage.RetryingUsageDataSource`: **line-rate
    100%, branch-rate 100%.**
  - `ClaudeMeter.Infrastructure.Usage.RetryPolicyOptions`: **100%/100%**
    (record de configuración, sin lógica más allá de la construcción).
  - `ClaudeMeter.Infrastructure.Usage.AnthropicApiUsageDataSource`:
    **100%/100%** (incluye la rama `MalformedResponse` y los 3 niveles de
    log por categoría).
  - `ClaudeMeter.Domain.Usage.UsageSnapshot`: **100%/100%** (incluye la
    fábrica `MalformedResponse()`).
  - `ClaudeMeter.ConsoleApp.Polling.UsagePollingLoop`: **100%/100%**
    (incluye el nuevo `case MalformedResponse`); `RunAsync` (el bucle
    infinito, sin relación con este ciclo) sigue en 0% — exclusión ya
    documentada por F1/F0, no forma parte de F2.
  - `ClaudeMeter.Desktop.Pages.UsagePage`: **line-rate 95.3%, branch-rate
    91.7%** (subió respecto al 94.33%/87.5% de F1 gracias a la cobertura
    nueva de la rama `Unauthorized`; el resto del gap es el mismo de F1,
    ver Gaps).
  - `ClaudeMeter.Desktop.Polling.UsagePollingCoordinator`: **100%/100%**
    (sin cambios de flujo respecto a F1, solo logging puro).
  - `ClaudeMeter.Desktop.Logging.SerilogConfigurator`: **line-rate 100%,
    branch-rate 50%** (ver Gaps — la rama del operador `??` que usa
    `DefaultLogDirectory()` real no se ejercita deliberadamente).
  - `ClaudeMeter.Desktop.Pages.ReauthNotice`: Coverlet **no genera ninguna
    entrada de clase** para este componente en `coverage.cobertura.xml`
    (a diferencia de `UsageBar`/`UsagePage`, que sí aparecen) — parece una
    limitación de instrumentación para un componente Razor sin `@code`
    propio (solo marcado estático), no una ausencia de tests: está cubierto
    directa (`ReauthNoticeTests`, 3 tests) e indirectamente
    (`UsagePageTests`, 2 tests) por esta suite.
- **Achieved: 91.7%-100% en las ocho clases de negocio nuevas/modificadas
  de F2 — muy por encima del 70% objetivo.** No hizo falta añadir tests
  artificiales para "rellenar" cobertura; los dos gaps de branch-rate
  (`UsagePage` línea 52/53/59, `SerilogConfigurator` línea del `??`) son
  exclusiones deliberadas y documentadas, no lagunas de esfuerzo.
- Confirmación final: `dotnet test ClaudeMeter.sln` sobre la solución
  completa en Debug — **162 tests correctos, 0 fallos** (61
  `ClaudeMeter.Domain.Tests`, 20 `ClaudeMeter.Console.Tests`, 42
  `ClaudeMeter.Infrastructure.Tests`, 39 `ClaudeMeter.Desktop.Tests`;
  `ClaudeMeter.Application.Tests` sigue sin tests, scaffolding vacío
  preexistente sin relación con F2). `dotnet build ClaudeMeter.sln -c
  Release` (con `TreatWarningsAsErrors`) compila sin advertencias ni
  errores. Los 19 `CS7036` documentados por Development ya no aparecen.

## Gaps / Not Covered

- **`SerilogConfigurator`, rama `logDirectory ?? DefaultLogDirectory()`
  cuando `logDirectory` es `null` (branch-rate 50%) — gap aceptado, no un
  descuido.** El propio AC de US-3 exige explícitamente que la suite de
  tests "no escriba logs reales en el `%LOCALAPPDATA%` de la máquina de
  CI"; ejercitar esa rama con su valor real invocaría
  `Directory.CreateDirectory` y escribiría un fichero de log fuera del
  directorio temporal de test en cualquier máquina que ejecute la suite
  (incluida CI). `DefaultLogDirectory()` en sí (el cálculo de la ruta, sin
  escribir nada) sí está cubierto y verificado al 100% por separado.
- **`App.xaml.cs`/`MainWindow.xaml(.cs)` — exclusión deliberada, ya
  documentada por F1, sin cambios de alcance en F2.** Composition root WPF
  real (arranque de Serilog, `AddLogging`, registro de
  `RetryingUsageDataSource` como el único `IUsageDataSource` expuesto):
  requiere una `System.Windows.Application` real en un hilo STA, algo que
  xUnit/bUnit no pueden ejercitar de forma significativa. Cubierto
  únicamente por revisión de código (ver tabla de trazabilidad) y por la
  validación manual ya prevista en la Definition of Done.
- **`UsagePage.OnSnapshotReceived`, líneas 52-53/59 — mismo gap aceptado ya
  documentado por F1, sin relación con el código nuevo de F2.** Es la
  "carrera benigna" (`ObjectDisposedException`/`InvalidOperationException`
  cuando el componente se desmonta con una respuesta HTTP en vuelo) — se
  reitera aquí solo para dejar constancia de que sigue siendo el mismo gap
  ya evaluado y aceptado en F1, no uno nuevo introducido por este ciclo.
- **`UsagePage.IsPollingActiveForTests`, rama `_coordinator is null` —
  mismo gap aceptado ya documentado por F1** (guarda defensiva que ningún
  camino real de bUnit puede forzar a `null`).
- **`ReauthNotice` sin entrada propia en `coverage.cobertura.xml`** — ver
  nota en Coverage Result; se considera una limitación de la herramienta
  para este tipo de componente (markup puro sin `@code`), no un gap de
  tests: 5 tests (3 directos + 2 vía `UsagePageTests`) ejercitan su
  renderizado real bajo bUnit.
- **Las 3 validaciones manuales de la Definition of Done (US-1/US-2/US-3)
  siguen pendientes de un equipo Windows real** — no automatizables por
  este pipeline (WPF/WebView2 real, corte de conectividad real,
  inspección del fichero de log real en `%LOCALAPPDATA%`), ya marcadas
  como tales en Requirements/Design/Development.
- No se ha escrito ningún test de integración nuevo (Testcontainers/DB):
  no aplica — F2 no introduce ninguna dependencia nueva de base de datos;
  toda la superficie nueva (retry decorator, logging, estado de dominio,
  componente Razor) es lógica pura o usa dobles de test ya establecidos
  (`HttpMessageHandler` fake, `IUsageDataSource` fake), consistente con
  la política de testing de `CLAUDE.md`.

## Definition of Done

(Copiada literalmente de `docs/sdlc/requirements/f2-robustez-ciclo-a.md`;
misma redacción en el documento de diseño.)

- [x] Todas las Acceptance Criteria de US-1, US-2 y US-3 verificables por
      tests automáticos se cumplen (ver tabla de trazabilidad); los 3
      puntos de validación manual real (WPF/WebView2, corte de red,
      fichero de log en disco) dependen de un equipo Windows real — ver
      los 3 ítems PENDIENTE de abajo, que impiden marcar este ítem como
      100% cerrado solo con esta fase.
- [x] Cobertura de tests automatizados (xUnit puro para la política de
      reintento/backoff de US-2 y para la configuración de Serilog de
      US-3 con directorio inyectado; bUnit para el mensaje distinto de
      `Unauthorized` en `UsagePage.razor`/`ReauthNotice.razor` de US-1)
      según el mínimo configurado en `.claude/sdlc.config.yaml`
      (`testingCoverage: 70`) — **conseguido: 91.7%-100% en las ocho
      clases de negocio nuevas/modificadas de F2.**
- [x] Ninguna prueba automatizada usa un token OAuth real ni realiza
      llamadas reales a la API de Anthropic — todos los fallos/reintentos
      se simulan con `StubHttpMessageHandler`/`ThrowingHttpMessageHandler`/
      `TimeoutHttpMessageHandler` (ya existentes) o con
      `FakeUsageDataSource`/`FakeTokenProvider`, nunca con un token real,
      verificado por revisión de cada test nuevo de esta fase.
- [x] Alcance exacto de "fallo transitorio" en US-2 y tratamiento de
      `TokenUnavailable`/`Unauthorized` frente al retry — confirmado por
      el usuario en Requirements/Design, y verificado aquí con tests
      explícitos de las 4 categorías no-reintentables
      (`RetryingUsageDataSourceTests`).
- [ ] **PENDIENTE — Validación manual (US-1):** se confirma, invalidando
      deliberadamente el token local (o simulando un 401/403 contra un
      mock de larga duración), que el mensaje de reautenticación aparece
      en la UI real (WPF/WebView2) y es visualmente distinguible del
      estado "sin datos". Ningún agente de este pipeline puede verificar
      renderizado real de WPF/WebView2 de forma automática; los tests de
      `ReauthNoticeTests`/`UsagePageTests` son la mitigación automatizable
      más cercana (confirman el markup/texto renderizado bajo bUnit), pero
      no sustituyen esta validación.
- [ ] **PENDIENTE — Validación manual (US-2):** se confirma, cortando la
      conectividad de red brevemente (&lt;~6s) mientras la aplicación
      real está corriendo, que el widget no muestra error inmediato y se
      recupera solo tras el reintento, sin parpadeo visible; y que
      cortarla más de ~6s sí muestra el fallo tras 3 intentos. Los 13
      tests de `RetryingUsageDataSourceTests` (con función de espera
      falsa) verifican la lógica de reintento con determinismo total, pero
      no el comportamiento en tiempo real con red real.
- [ ] **PENDIENTE — Validación manual (US-3):** se confirma que el fichero
      de log aparece realmente en `%LOCALAPPDATA%\ClaudeMeter\logs` tras
      ejecutar `ClaudeMeter.Desktop` en Windows real, y que su contenido es
      legible/estructurado con los niveles esperados. `SerilogConfiguratorTests`
      verifica la misma configuración exacta (plantilla de salida,
      rotación, retención) pero contra un directorio temporal inyectado,
      nunca contra el `%LOCALAPPDATA%` real de ninguna máquina.

---

Nada se ha confirmado (`commit`) ni enviado (`push`) al repositorio: los
ficheros de test nuevos/modificados (`AnthropicApiUsageDataSourceTests.cs`,
`CapturingLogger.cs`, `FakeUsageDataSource.cs` (Infrastructure),
`RetryingUsageDataSourceTests.cs`, `UsageSnapshotTests.cs`,
`UsagePollingLoopTests.cs`, `Logging/SerilogConfiguratorTests.cs`,
`Pages/ReauthNoticeTests.cs`, `Pages/UsagePageTests.cs`,
`Polling/UsagePollingCoordinatorTests.cs`) quedan en el árbol de trabajo,
listos para revisión, junto con el resto de cambios de F2 ya presentes sin
confirmar. Ningún fichero fuera de `test/` se ha tocado en esta fase, salvo
este propio informe.
