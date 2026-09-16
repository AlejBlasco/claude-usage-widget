# Testing Summary: [F0] Bucle de polling con salida por consola cada 60s

## Scope

Cobertura de tests unitarios para el código nuevo, aún sin confirmar (`commit`),
del Issue #5: el proyecto `src/ClaudeMeter.Console` (namespace de código
`ClaudeMeter.ConsoleApp`), con dos piezas testeables en el sentido en que el
propio diseño lo define:

- `Rendering/UsagePollingLineFormatter.cs` — formateo puro de las cinco líneas
  de consola (una de éxito, cuatro de error).
- `Polling/UsagePollingLoop.cs` — método `internal Task ExecuteIterationAsync(CancellationToken)`,
  expuesto a tests vía `InternalsVisibleTo` (ya configurado en el `.csproj`
  por la fase de desarrollo), que despacha por `UsageSnapshot.Status` hacia
  `IUsagePollingRenderer` y nunca deja escapar una excepción no controlada.

Se ha trabajado a partir de tres documentos ya existentes en el repositorio:

- `docs/sdlc/requirements/issue-05-bucle-polling-consola.md` — los seis
  GIVEN-WHEN-THEN funcionales (camino feliz con ambas ventanas, camino feliz
  con campos ausentes, token no disponible, 401/403, fallo de red/petición,
  y continuidad del bucle tras cualquiera de los errores anteriores).
- `docs/sdlc/design/issue-05-bucle-polling-consola.md` — especifica
  literalmente el proyecto `ClaudeMeter.Console.Tests`, los dos ficheros de
  test (`UsagePollingLineFormatterTests`, `UsagePollingLoopTests`) y los dos
  dobles de test (`FakeUsageDataSource`, `SpyUsagePollingRenderer`), además
  de excluir explícitamente de la cobertura automática `RunAsync` (bucle
  infinito real) y `Program.cs` (composition root trivial).
- `docs/sdlc/development/issue-05-bucle-polling-consola.md` — confirma que el
  desarrollador dejó explícitamente el proyecto de tests fuera de alcance de
  su fase (ni `test/ClaudeMeter.Console.Tests` ni `dotnet test` se
  ejecutaron), pendiente en su totalidad de esta fase de QA.

El código de producción se leyó directamente (no se asumieron firmas del
diseño) para confirmar nombres exactos de tipos/métodos/namespaces antes de
escribir ningún test.

## Tests Added/Modified

- `test/ClaudeMeter.Console.Tests/ClaudeMeter.Console.Tests.csproj` —
  **Nuevo**, generado con `dotnet new xunit` y ajustado para replicar
  exactamente el patrón ya usado por `ClaudeMeter.Domain.Tests`/
  `ClaudeMeter.Infrastructure.Tests` (sin `TargetFramework`/`ImplicitUsings`/
  `Nullable` explícitos, heredados de `Directory.Build.props`; mismas
  versiones de paquete `xunit 2.9.2`/`Microsoft.NET.Test.Sdk 17.12.0`/
  `coverlet.collector 6.0.2`). Se eliminó el `UnitTest1.cs` de plantilla.
- `ClaudeMeter.sln` — proyecto añadido bajo la carpeta de solución `test`
  mediante `dotnet sln add` (mismo mecanismo por CLI ya usado para el resto
  de proyectos, sin edición manual del `.sln`).
- `test/ClaudeMeter.Console.Tests/Polling/FakeUsageDataSource.cs` — **Nuevo.**
  Doble de test de `IUsageDataSource` con dos factorías: `Returning(UsageSnapshot)`
  (devuelve un snapshot pre-configurado) y `Throwing(Exception)` (lanza una
  excepción pre-configurada), necesaria para simular tanto los cuatro
  `UsageSnapshotStatus` como el caso de excepción no controlada.
- `test/ClaudeMeter.Console.Tests/Polling/SpyUsagePollingRenderer.cs` —
  **Nuevo.** Doble de test de `IUsagePollingRenderer` que registra el número
  de invocaciones de cada método (`RenderSuccessCallCount`, etc.), el
  `TotalCallCount` agregado (para verificar exclusividad del despacho) y los
  últimos argumentos recibidos (`LastTimestamp`, `LastSession`, `LastWeekly`,
  `LastException`).
- `test/ClaudeMeter.Console.Tests/Polling/UsagePollingLoopTests.cs` —
  **Nuevo.** 6 métodos de test, invocando `ExecuteIterationAsync` directamente
  vía `InternalsVisibleTo`:
  - `ExecuteIterationAsync_ConSnapshotDeExito_LlamaARenderSuccessConLasVentanasParseadas` —
    `UsageSnapshot.Success` con ambas ventanas; verifica que se llama
    exactamente a `RenderSuccess` (y a ningún otro método), con
    `RateLimitWindowParser.ParseSnapshot` ya aplicado (`PercentageUsed`
    correcto; `Reset` ausente a propósito para que `MinutesRemaining` sea
    `null` de forma determinista sin depender de la hora real del test).
  - `ExecuteIterationAsync_ConTokenUnavailable_LlamaSoloARenderTokenUnavailable`.
  - `ExecuteIterationAsync_ConUnauthorized_LlamaSoloARenderUnauthorized`.
  - `ExecuteIterationAsync_ConRequestFailed_LlamaSoloARenderRequestFailed`.
  - `ExecuteIterationAsync_AnteExcepcionInesperadaDelDataSource_LaCapturaYLlamaARenderUnexpectedErrorSinPropagar` —
    **el test que prueba directamente el criterio de aceptación "el bucle
    sigue funcionando tras un error"**: `FakeUsageDataSource.Throwing` lanza
    una `InvalidOperationException`; si `ExecuteIterationAsync` la dejase
    escapar, el `await` la relanzaría y el test fallaría en el `Act` en vez
    de llegar a los `Assert` — llegar a ellos ya demuestra que el proceso no
    se cae. Verifica además que `RenderUnexpectedError` recibe exactamente
    esa misma instancia de excepción.
  - `ExecuteIterationAsync_AnteOperationCanceledExceptionGenuina_LaPropagaSinRenderizarNada` —
    confirma el criterio explícito del diseño ("mismo criterio que
    `AnthropicApiUsageDataSource`"): una `OperationCanceledException` genuina
    no se trata como fallo a renderizar, se propaga tal cual.
- `test/ClaudeMeter.Console.Tests/Rendering/UsagePollingLineFormatterTests.cs` —
  **Nuevo.** 13 métodos de test (14 casos contando la variante `[Theory]`):
  - Línea de éxito con ambas ventanas completas (caso feliz del AC).
  - Línea de éxito con `PercentageUsed` con decimales (`42.5` → `"42.5%"`,
    formato `"0.##"` con cultura invariante, sin coma decimal ni ceros
    sobrantes).
  - Línea de éxito con `MinutesRemaining` nulo en una sola ventana (la otra
    ventana no se ve afectada) — AC de "dato ausente, marcado como 'no
    disponible', sin lanzar excepción".
  - Línea de éxito con `PercentageUsed` nulo en una sola ventana (caso
    simétrico).
  - Línea de éxito con una ventana completa `RateLimitWindow.Unavailable`
    (ambos campos `null` a la vez).
  - Línea de éxito con **ambas** ventanas `RateLimitWindow.Unavailable`.
  - Las cuatro líneas de error (`FormatTokenUnavailableLine`,
    `FormatUnauthorizedLine`, `FormatRequestFailedLine`), cada una verificando
    el texto exacto carácter a carácter, incluyendo la mención explícita de
    "no se intentará refrescarlo automáticamente" en el caso 401/403.
  - `FormatUnexpectedErrorLine` con dos tipos de excepción distintos
    (`InvalidOperationException`, `TimeoutException`) para confirmar que el
    nombre de tipo mostrado refleja el tipo real recibido y no está
    hardcodeado.
  - `[Theory]` con `PercentageUsed` en los extremos `0.0`/`100.0` → `"0%"`/`"100%"`
    (sin `.00` sobrante).

  Todas las aserciones de timestamp comparan contra
  `timestamp.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture)`
  calculado en el propio test (misma conversión que usa el `FormatTimestamp`
  privado de la clase bajo test), para que los tests sean deterministas
  independientemente de la zona horaria de la máquina que los ejecute, sin
  dejar de ejercitar la ruta real de formateo del `DateTimeOffset` recibido
  como parámetro.

No se ha modificado ningún test existente de `ClaudeMeter.Domain.Tests`/
`ClaudeMeter.Infrastructure.Tests`.

## Coverage Result

- Target (`testingCoverage` en `.claude/sdlc.config.yaml`): **70%**
- Comando ejecutado: `dotnet test test/ClaudeMeter.Console.Tests/ClaudeMeter.Console.Tests.csproj --collect:"XPlat Code Coverage"`
  (Coverlet, ya presente como `PackageReference` — no se ha introducido
  ninguna herramienta nueva).
- Resultado de la ejecución: **19/19 tests correctos, 0 fallos.**
- Cobertura leída de `coverage.cobertura.xml`, para las clases que el propio
  diseño identifica como el alcance testeable de este issue (ver "Gaps" para
  el resto):
  - `ClaudeMeter.ConsoleApp.Rendering.UsagePollingLineFormatter`: **9/9
    líneas, line-rate 100%, branch-rate 100%** (incluye las dos ramas
    `?:`/`is null` de `FormatPercentage`/`FormatMinutes`, cada una con
    `condition-coverage 100% (2/2)`).
  - `ClaudeMeter.ConsoleApp.Polling.UsagePollingLoop` (constructor + método
    privado `Render`, el switch de despacho por `UsageSnapshotStatus`):
    **18/18 líneas, line-rate 100%**, incluyendo el `switch` con
    `condition-coverage 100% (5/5)`.
  - `ClaudeMeter.ConsoleApp.Polling.UsagePollingLoop.ExecuteIterationAsync`
    (el método `internal` objetivo principal de este issue): **11/11 líneas,
    line-rate 100%**, incluyendo la rama del `catch` filtrado
    (`when (ex is not OperationCanceledException)`).
  - **Total del alcance testeable (Formatter + Loop.Render + Loop.ExecuteIterationAsync): 38/38 líneas → 100%.**
- **Achieved: 100% sobre el código que el diseño define como cubierto por
  tests automáticos — muy por encima del 70% objetivo.** No hizo falta
  añadir tests adicionales solo para "rellenar" cobertura: los 19 tests
  escritos ya cubren el 100% de las líneas y ramas alcanzables de este
  alcance sin ningún caso artificioso.
- Confirmación final: `dotnet test ClaudeMeter.sln` sobre la solución
  completa — **80 tests correctos, 0 fallos** en total (37 de
  `ClaudeMeter.Domain.Tests`, 24 de `ClaudeMeter.Infrastructure.Tests`, 19
  nuevos de `ClaudeMeter.Console.Tests`; `ClaudeMeter.Application.Tests` y
  `ClaudeMeter.Desktop.Tests` siguen sin ningún test — scaffolding F0 vacío,
  preexistente, sin relación con este issue). Ningún test existente se ha
  visto afectado.

## Gaps / Not Covered

- **`UsagePollingLoop.RunAsync` (0% de cobertura, línea a línea) — exclusión
  deliberada, no un gap.** El propio diseño (sección "Risks & Open
  Decisions", punto 4) fija explícitamente que el bucle infinito real
  (`while(true)` + `Task.Delay(60s)`) queda fuera de los tests automáticos:
  testearlo exigiría o bien esperar 60s reales por test (coste de tiempo sin
  señal adicional, ya que la única lógica no trivial vive en
  `ExecuteIterationAsync`, ya cubierto al 100%), o bien introducir un
  abstracción de tiempo (`IDelayProvider`/reloj virtual) no contemplada por
  el diseño y que constituiría un cambio de producción no solicitado. Queda
  cubierto, según el propio diseño (paso 12 del "Implementation Plan"), por
  la validación manual de extremo a extremo (`dotnet run --project
  src/ClaudeMeter.Console` con un `.credentials.json` real durante 2-3
  iteraciones) — validación que sigue pendiente de ejecutarse y no es
  responsabilidad de esta fase de QA (requiere un token OAuth real y consume
  cuota real de la cuenta, riesgo ya documentado en el diseño).
- **`ConsoleUsagePollingRenderer` (0% de cobertura) — exclusión deliberada,
  no un gap.** El diseño no incluye ningún fichero de test para esta clase
  en su tabla de "Ubicación exacta de cada pieza nueva" ni en el
  "Implementation Plan": es una capa de I/O trivial (cinco métodos de una
  línea, cada uno delegando el texto en `UsagePollingLineFormatter` —ya
  cubierto al 100%— y añadiendo únicamente `Console.WriteLine`/
  `Console.Error.WriteLine`). Testearla exigiría capturar `Console.Out`/
  `Console.Error` reales (`Console.SetOut`), justo el enfoque que el propio
  diseño rechaza explícitamente en favor de la interfaz
  `IUsagePollingRenderer` como costura de test — escribir ese test habría
  sido replicar en Console lo que `UsagePollingLineFormatterTests` ya
  verifica, sin añadir señal real.
- **`Program.cs` (0% de cobertura) — exclusión deliberada, no un gap.**
  Composition root trivial (`new` directo de cuatro colaboradores + una
  llamada a `RunAsync`), sin lógica propia; el propio diseño y el resumen de
  desarrollo lo señalan como no testeable de forma significativa, cubierto
  únicamente por la misma validación manual de extremo a extremo del punto
  anterior.
- No se ha escrito ningún test de integración: no aplica — este issue no
  introduce ninguna dependencia nueva de HTTP/E/S/base de datos (esas ya
  están cubiertas por los tests existentes de `AnthropicApiUsageDataSource`/
  `CredentialsFileTokenProvider` en `ClaudeMeter.Infrastructure.Tests`, sin
  cambios); el propio diseño confirma que Console solo orquesta puertos ya
  existentes sin añadir I/O propia más allá de la ya excluida arriba.

---

Nada se ha confirmado (`commit`) ni enviado (`push`) al repositorio: el
proyecto de test nuevo (`test/ClaudeMeter.Console.Tests`, cuatro ficheros
`.cs` + `.csproj`) y la entrada añadida a `ClaudeMeter.sln` quedan en el
árbol de trabajo, listos para revisión.
