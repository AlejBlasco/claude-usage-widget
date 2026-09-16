# Testing Summary: [F0] Llamada HTTP con headers OAuth correctos

## Scope

Tests unitarios para la implementación del Issue #3, descrita en
`docs/sdlc/development/issue-03-llamada-http-headers-oauth.md` y diseñada en
`docs/sdlc/design/issue-03-llamada-http-headers-oauth.md`. El propio
documento de implementación deja explícitamente pendiente la suite de tests
para esta fase (`sdlc-testing`) — este documento la cubre. Los tres tipos
nuevos de esa fase quedan cubiertos:

- `ClaudeMeter.Domain.Usage.UsageSnapshot` / `UsageSnapshotStatus` /
  `RawRateLimitHeaders`.
- `ClaudeMeter.Application.Abstractions.IUsageDataSource` (puerto; no se
  testea directamente por ser solo una interfaz — se ejercita indirectamente
  a través de su única implementación).
- `ClaudeMeter.Infrastructure.Usage.AnthropicApiUsageDataSource` (adaptador
  con toda la lógica de negocio de esta fase: construcción de la petición
  HTTP, envío, traducción de la respuesta a `UsageSnapshot`).

Todos los casos GIVEN-WHEN-THEN del documento de requisitos y los 9 casos de
la sección "Estrategia de Testing" del documento de diseño están cubiertos,
más un caso límite adicional (200 con solo la cabecera `-5h-status` presente
pero no `-7d-status`) y un caso de timeout interno de `HttpClient`
(`TaskCanceledException` sin cancelación pedida por el consumidor)
identificados durante esta fase para cerrar una rama del código no cubierta
por los 9 casos originales.

**Ningún test usa un token real ni realiza una llamada de red real**: cada
test de `AnthropicApiUsageDataSource` inyecta un `FakeTokenProvider`
(devuelve un `TokenResult` pre-configurado, nunca un token real) y un
`HttpClient` construido sobre un `HttpMessageHandler` test double
(`StubHttpMessageHandler`, `ThrowingHttpMessageHandler`,
`TimeoutHttpMessageHandler`) que nunca abre una conexión de red real,
conforme a la regla de QA de `CLAUDE.md` y al Acceptance Criteria del issue
original ("Tests con HttpMessageHandler fake, sin token real").

## Tests Added/Modified

- `test/ClaudeMeter.Domain.Tests/Usage/UsageSnapshotTests.cs` — **Nuevo.**
  6 métodos de test (9 casos ejecutados, contando `[Theory]`):
  - `Success_ConAmbasVentanas_DevuelveStatusSuccessConSessionYWeeklyExactos`
    — éxito, verifica que `Session`/`Weekly` son exactamente las instancias
    pasadas.
  - `TokenUnavailable_DevuelveStatusTokenUnavailableSinSessionNiWeekly`,
    `Unauthorized_DevuelveStatusUnauthorizedSinSessionNiWeekly`,
    `RequestFailed_DevuelveStatusRequestFailedSinSessionNiWeekly` — cada
    fábrica de fallo, `Session`/`Weekly` nulos, `IsSuccess == false`.
  - `IsSuccess_ParaCualquierEstadoDeFallo_EsSiempreFalse` (`[Theory]` sobre
    los tres estados de fallo) — comprobación uniforme.
  - `RawRateLimitHeaders_ConTodosLosCamposNulos_SeConstruyeSinLanzar` — caso
    límite documentado en el diseño: cualquier sufijo (`Utilization`/
    `Remaining`/`Reset`) puede ser `null`.

- `test/ClaudeMeter.Infrastructure.Tests/Usage/StubHttpMessageHandler.cs` —
  **Nuevo.** Test double de `HttpMessageHandler` (tal y como lo especifica el
  diseño) que devuelve una respuesta pre-configurada y captura la última
  petición enviada (`LastRequest`, `CallCount`) para poder hacer aserciones
  sobre headers/URL/método. Se añadió `LastRequestBody` (no estaba en el
  snippet del diseño) porque `AnthropicApiUsageDataSource.GetUsageAsync` hace
  `using var request = ...`, y por tanto el `StringContent` del
  `HttpRequestMessage` original queda `dispose`d nada más volver el método —
  leer el cuerpo *dentro* del propio `SendAsync` simulado (antes de que
  ocurra ese `dispose`) es la única forma de poder aserirlo después sin un
  `ObjectDisposedException`.

- `test/ClaudeMeter.Infrastructure.Tests/Usage/ThrowingHttpMessageHandler.cs`
  — **Nuevo.** Dos test doubles de `HttpMessageHandler`: uno que lanza
  siempre `HttpRequestException` (fallo de red) y otro
  (`TimeoutHttpMessageHandler`) que lanza siempre `TaskCanceledException`
  sin que el `cancellationToken` esté marcado como cancelado (simula el
  timeout interno de `HttpClient.Timeout`, distinto de una cancelación
  genuina pedida por el consumidor).

- `test/ClaudeMeter.Infrastructure.Tests/Usage/FakeTokenProvider.cs` —
  **Nuevo.** Test double mínimo de `ITokenProvider` (tal y como lo
  especifica el diseño) que devuelve siempre un `TokenResult`
  pre-configurado.

- `test/ClaudeMeter.Infrastructure.Tests/Usage/AnthropicApiUsageDataSourceTests.cs`
  — **Nuevo.** 11 métodos de test (13 casos ejecutados, contando `[Theory]`):
  - `GetUsageAsync_ConTokenDeExitoY200ConCabecerasCompletas_DevuelveSuccessConLosValoresExactos`
    — token OK + 200 con las 8 cabeceras `unified-5h-*`/`unified-7d-*` →
    `Success`, verificando los valores exactos leídos en `Session`/`Weekly`.
  - `GetUsageAsync_ConTokenEnEstadoDeFallo_DevuelveTokenUnavailableSinLlamarAlHandler`
    (`[Theory]`: `FileNotFound`/`InvalidJson`/`TokenMissing`) — cualquier
    fallo de `ITokenProvider` → `TokenUnavailable`, verificando
    `handler.CallCount == 0` y `handler.LastRequest is null` (el handler
    lanza si se le llega a invocar, como aserción adicional de "nunca se
    llama").
  - `GetUsageAsync_Con401_DevuelveUnauthorized`,
    `GetUsageAsync_Con403_DevuelveUnauthorized` — 401/403 → `Unauthorized`.
  - `GetUsageAsync_Con500_DevuelveRequestFailed` — 5xx → `RequestFailed`.
  - `GetUsageAsync_Con200SinCabecerasUnified_DevuelveRequestFailed` — 200 sin
    ninguna cabecera `anthropic-ratelimit-unified-*`.
  - `GetUsageAsync_Con200ConSoloCabeceraDe5hStatus_DevuelveRequestFailed` —
    caso límite adicional (no listado explícitamente entre los 9 del
    diseño, pero cubierto por su tabla de decisiones): solo llega la
    ventana de 5h, falta la de 7d → sigue siendo `RequestFailed`.
  - `GetUsageAsync_AnteHttpRequestException_DevuelveRequestFailedSinLanzar`
    — error de red → `RequestFailed`, sin excepción no controlada.
  - `GetUsageAsync_AnteTimeoutInternoSinCancelacionPedida_DevuelveRequestFailedSinLanzar`
    — caso adicional identificado durante esta fase (no listado
    explícitamente en el diseño): `TaskCanceledException` sin cancelación
    pedida (timeout de `HttpClient`) → `RequestFailed`, cerrando la única
    rama que quedaba sin ejercitar en `GetUsageAsync`.
  - `GetUsageAsync_ConTokenDeExito_EnviaLosHeadersEsperados` — inspecciona
    `StubHttpMessageHandler.LastRequest` tras una llamada exitosa:
    `Authorization` (`Bearer <token>`), `anthropic-version`,
    `anthropic-beta`, `User-Agent`, `Content-Type`.
  - `GetUsageAsync_ConTokenDeExito_EnviaPostALaUrlYPayloadEsperados` —
    verifica método `POST`, URL `https://api.anthropic.com/v1/messages` y
    que el body deserializado tiene `max_tokens == 1`, un único mensaje con
    `role == "user"` y un `model` no vacío.

No se modificó ningún fichero de producción ni ningún `.csproj` — ambos
proyectos de test ya referenciaban lo necesario (`ClaudeMeter.Infrastructure`
y, transitivamente, `ClaudeMeter.Application`/`ClaudeMeter.Domain`).

## Resultado de la ejecución

- `dotnet test test/ClaudeMeter.Domain.Tests/... --filter "FullyQualifiedName~UsageSnapshotTests"`:
  **8/8 correctos**.
- `dotnet test test/ClaudeMeter.Infrastructure.Tests/... --filter "FullyQualifiedName~AnthropicApiUsageDataSourceTests"`:
  **13/13 correctos**.
- `dotnet test ClaudeMeter.sln` (pasada completa, ejecutada una sola vez como
  confirmación final, no como bucle de iteración): **39/39 correctos** en
  total — `ClaudeMeter.Domain.Tests`: 15/15 (7 de Issue #2 + 8 nuevos de
  Issue #3); `ClaudeMeter.Infrastructure.Tests`: 24/24 (11 de Issue #2 + 13
  nuevos de Issue #3). `ClaudeMeter.Application.Tests` y
  `ClaudeMeter.Desktop.Tests` siguen sin aportar pruebas — scaffolding vacío
  de F0, fuera del alcance de este issue.
- `dotnet build ClaudeMeter.sln -c Release`: **0 advertencias, 0 errores**
  (Roslyn analyzers + warnings-as-errors en Release, verificado tras añadir
  los tests nuevos).
- No se han ejecutado tests de integración: ninguno de los tres tipos
  nuevos de este issue requiere una base de datos ni un pipeline HTTP real
  — toda la superficie testeable queda cubierta con test doubles
  deterministas (unit tests), conforme al diseño ("por qué no WireMock.Net")
  y a la regla de QA de no usar tests de integración para cubrir ramas de
  lógica de negocio.

## Coverage Result

- **Herramienta detectada**: `coverlet.collector` (paquete NuGet ya presente
  en `ClaudeMeter.Domain.Tests.csproj` y
  `ClaudeMeter.Infrastructure.Tests.csproj`). Se ejecutó con
  `dotnet test ClaudeMeter.sln --collect:"XPlat Code Coverage"` (formato
  Cobertura), tal y como recomienda `dotnet-testing.md`.
- **Target**: 70% (`testingCoverage` en `.claude/sdlc.config.yaml`).
- **Achieved** (medido, no estimado, sobre el código nuevo de este issue):
  - `ClaudeMeter.Domain.Usage.UsageSnapshot` y
    `ClaudeMeter.Domain.Usage.RawRateLimitHeaders`
    (`UsageSnapshot.cs`, único fichero de producción nuevo en
    `ClaudeMeter.Domain` de este issue): **100% de líneas y 100% de ramas**
    para ambas clases — cubierto íntegramente por `UsageSnapshotTests`.
  - `ClaudeMeter.Infrastructure.Usage.AnthropicApiUsageDataSource`
    (`AnthropicApiUsageDataSource.cs`, único fichero de producción nuevo en
    `ClaudeMeter.Infrastructure` de este issue), desglosado por el reporte
    de Coverlet en sus clases compiladas:
    - Clase contenedora (constructor, `BuildRequest`, `ReadWindow`,
      `GetHeaderValue`): **100% de líneas y ramas**.
    - Máquina de estados async de `GetUsageAsync`
      (`<GetUsageAsync>d__8.MoveNext`): **100% de líneas y ramas** — tras
      añadir el test del timeout interno (`TaskCanceledException` sin
      cancelación pedida), la única rama que quedaba en 0 hits se cerró.
    - DTOs privados `CreateMessageRequestDto` y `MessageDto`: **100% de
      líneas y ramas** para ambos (cubiertos indirectamente por cualquier
      test que llegue a `BuildRequest`, en particular
      `GetUsageAsync_ConTokenDeExito_EnviaPostALaUrlYPayloadEsperados`).

Los tres tipos de producción de este issue quedan al **100% de cobertura de
líneas y ramas**, muy por encima del objetivo del 70%.

## Gaps / Not Covered

- **Validación manual contra la API real (paso 8 del Implementation Plan del
  diseño) — pendiente, explícitamente fuera de alcance de esta fase.** El
  propio documento de requisitos y el de diseño marcan esa validación
  (confirmar con un token OAuth real que la API responde 200 con las
  cabeceras `anthropic-ratelimit-unified-5h-*`/`-7d-*` esperadas, y anotar
  si el sufijo real es `utilization` o `remaining`) como obligatoria antes
  de cerrar el issue original, pero **nunca** como parte de la suite
  automatizada — requiere un token real que las reglas de `CLAUDE.md` y de
  este agente prohíben usar en tests. Sigue pendiente para el usuario, tal y
  como ya documentaba el resumen de implementación en su sección "How to
  Verify".
- `IUsageDataSource` (la interfaz en sí) no tiene un test dedicado: es un
  puerto sin lógica propia, ejercitado indirectamente a través de
  `AnthropicApiUsageDataSourceTests` — coherente con el mismo criterio ya
  aplicado a `ITokenProvider` en el testing summary de Issue #2.
- `OperationCanceledException` por cancelación *genuina* del
  `CancellationToken` (pedida por el propio consumidor, no un timeout
  interno de `HttpClient`) no tiene un test dedicado: el diseño decide
  explícitamente dejarla propagar sin capturar (comportamiento estándar de
  .NET para cancelación cooperativa, no un estado de `UsageSnapshot`), por
  lo que no hay comportamiento de negocio nuevo que verificar ahí — mismo
  criterio ya aplicado a `CredentialsFileTokenProvider` en Issue #2.
- No se han añadido tests de integración (ver "Resultado de la ejecución").

## Confirmación

No se ha ejecutado ningún `git commit` ni `git push` en ningún momento de
esta fase. Todos los cambios (los cinco ficheros de test nuevos bajo
`test/ClaudeMeter.Domain.Tests/Usage/` y
`test/ClaudeMeter.Infrastructure.Tests/Usage/`, y este resumen) quedan en el
árbol de trabajo, listos para revisión manual del usuario.
