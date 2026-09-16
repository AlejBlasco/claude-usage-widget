# Implementation Summary: [F0] Llamada HTTP con headers OAuth correctos

## Design Reference
`docs/sdlc/design/issue-03-llamada-http-headers-oauth.md` (Issue #3, milestone "F0 — Núcleo de validación"). Documento cerrado, sin decisiones de diseño abiertas; implementado literalmente para la parte de producción (Domain/Application/Infrastructure). La parte de testing descrita en el diseño (`AnthropicApiUsageDataSourceTests`, `StubHttpMessageHandler`, `FakeTokenProvider`) **no** se ha implementado en esta fase — ver "Deviations from the Design".

## Files Changed

- `src/ClaudeMeter.Domain/Usage/UsageSnapshot.cs` — **Nuevo.** `UsageSnapshotStatus` (enum: `Success`, `TokenUnavailable`, `Unauthorized`, `RequestFailed`), `RawRateLimitHeaders` (record posicional con 4 `string?`: `Status`, `Utilization`, `Remaining`, `Reset`) y `UsageSnapshot` (record sellado con constructor privado, propiedades `Status`/`Session`/`Weekly`/`IsSuccess`, y cuatro fábricas estáticas `Success(session, weekly)`/`TokenUnavailable()`/`Unauthorized()`/`RequestFailed()`). Sin dependencias externas ni de E/S, siguiendo exactamente el mismo patrón que `TokenResult` (Issue #2).

- `src/ClaudeMeter.Application/Abstractions/IUsageDataSource.cs` — **Nuevo.** Puerto `IUsageDataSource` con el único método `Task<UsageSnapshot> GetUsageAsync(CancellationToken cancellationToken = default)`. Solo referencia `ClaudeMeter.Domain.Usage.UsageSnapshot`; no referencia `System.Net.Http`, `System.Text.Json` ni ningún tipo de `ClaudeMeter.Infrastructure` — cumple literalmente el primer Acceptance Criteria del documento de requisitos. No se introduce ningún `IUsageService` monolítico.

- `src/ClaudeMeter.Infrastructure/Usage/AnthropicApiUsageDataSource.cs` — **Nuevo.** Implementación de `IUsageDataSource`:
  - Constructor público `(ITokenProvider tokenProvider, HttpClient httpClient)` — inyección por constructor, sin `IHttpClientFactory` (decisión de diseño ya justificada en el documento de diseño: no hay contenedor DI en Desktop todavía y un único `HttpClient` singleton reutilizado no sufre el agotamiento de sockets que resuelve `IHttpClientFactory`).
  - `GetUsageAsync`: si `ITokenProvider.GetTokenAsync` no devuelve éxito → `UsageSnapshot.TokenUnavailable()` **sin construir ni enviar ninguna petición HTTP**. Si hay token, construye la petición (`BuildRequest`) y la envía con `HttpClient.SendAsync`, envuelta en `try/catch` de `HttpRequestException` y `TaskCanceledException` (filtrada `when (!cancellationToken.IsCancellationRequested)`) → ambas mapean a `RequestFailed()`. `OperationCanceledException` genuina se propaga sin capturar.
  - Respuesta 401/403 → `Unauthorized()` (nunca se intenta refrescar el token). Cualquier otro código no exitoso → `RequestFailed()`. 200 sin `anthropic-ratelimit-unified-5h-status` **y** `anthropic-ratelimit-unified-7d-status` presentes → `RequestFailed()`. 200 con ambos `-status` presentes → `Success(session, weekly)` con las cabeceras crudas exactas leídas (el resto de sufijos `-utilization`/`-remaining`/`-reset` quedan `null` si no vienen, sin afectar al éxito).
  - `BuildRequest`: `POST https://api.anthropic.com/v1/messages`, header `Authorization: Bearer <token>` vía `AuthenticationHeaderValue`, `anthropic-version: 2023-06-01`, `anthropic-beta: oauth-2025-04-20`, `User-Agent: claude-code/0.1.0`, cuerpo JSON mínimo (`model: claude-haiku-4-5-20251001`, `max_tokens: 1`, un único mensaje `{"role":"user","content":"ping"}`) vía DTOs privados `CreateMessageRequestDto`/`MessageDto` con `System.Text.Json`/`JsonPropertyName`.
  - `ReadWindow`/`GetHeaderValue`: leen las 4 cabeceras por ventana (`5h`/`7d`) desde `HttpResponseHeaders` sin lanzar si faltan.
  - No calcula countdowns/porcentajes, no lee el token de ningún fichero directamente (delega en `ITokenProvider`), no reintenta ante fallos transitorios (F2) y no intenta refrescar el token OAuth.

## Deviations from the Design

- **Suite de tests no implementada en esta fase.** El diseño incluye en su "Implementation Plan" (pasos 4-7) la creación de `StubHttpMessageHandler`, `FakeTokenProvider` y `AnthropicApiUsageDataSourceTests` bajo `test/ClaudeMeter.Infrastructure.Tests/Usage/`, con 9 casos de test cubriendo los cuatro estados de `UsageSnapshotStatus` y la forma exacta de la petición HTTP. **No se ha creado ni ejecutado ningún fichero bajo `test/` en esta fase**: por reglas explícitas de este agente (Software Developer), escribir y ejecutar tests — incluidos los descritos en un documento de diseño — está fuera de alcance y es responsabilidad exclusiva de la fase `sdlc-testing` (QA Engineer). Esto es una restricción del proceso de esta sesión, no una decisión técnica sobre el diseño en sí; el diseño permanece válido y estos tests deben implementarse literalmente tal y como están especificados cuando se ejecute `sdlc-testing`.
- **Paso 8 del Implementation Plan (validación manual contra la API real) no ejecutado en esta fase.** El diseño marca esa validación como obligatoria antes de cerrar el issue, pero requiere un token OAuth real (`%USERPROFILE%\.claude\.credentials.json`) que no está disponible/no corresponde usar en este entorno de desarrollo automatizado. Se documenta en "How to Verify" el procedimiento exacto para que el usuario la ejecute manualmente y confirme el nombre/formato real de las cabeceras (`utilization` vs `remaining`, formato de `reset`).
- El resto del código (Domain, Application, Infrastructure de producción) sigue el diseño literalmente: mismos namespaces, rutas de fichero, firmas, catálogo de excepciones capturadas, DTOs, valores literales (modelo, versión de API, beta header, user-agent) y comentarios XMLDoc en español.

## How to Verify

1. **Compilación** (ya ejecutada como parte de esta fase): `dotnet build ClaudeMeter.sln` y `dotnet build ClaudeMeter.sln -c Release` — ambas terminan en "Compilación correcta", 0 advertencias, 0 errores.
2. **Verificación manual exploratoria contra la API real** (paso 8 del plan de diseño, obligatorio antes de cerrar el issue original — no ejecutado en esta fase, pendiente para el usuario): en una máquina con `%USERPROFILE%\.claude\.credentials.json` real (generado por el CLI de Claude Code), se puede validar sin escribir ningún fichero de test, por ejemplo desde un pequeño programa de consola temporal (no incluido en el repositorio) que haga:

   ```csharp
   var tokenProvider = new CredentialsFileTokenProvider();
   var httpClient = new HttpClient(new SocketsHttpHandler
   {
       PooledConnectionLifetime = TimeSpan.FromMinutes(15)
   })
   {
       Timeout = TimeSpan.FromSeconds(30)
   };
   var dataSource = new AnthropicApiUsageDataSource(tokenProvider, httpClient);

   var snapshot = await dataSource.GetUsageAsync();
   Console.WriteLine(snapshot.Status);
   Console.WriteLine(snapshot.Session);
   Console.WriteLine(snapshot.Weekly);
   ```

   Comprobar que `Status == Success` y que `Session`/`Weekly` traen valores no nulos en `Status` (y anotar si `Utilization`/`Remaining`/`Reset` vienen poblados y con qué nombre/formato exacto, por si difiere de lo documentado en el diseño). **No comprometer ningún token real ni el resultado de esta prueba en el repositorio.**
3. Para verificar los estados de fallo sin depender de la API real: instanciar `AnthropicApiUsageDataSource` con un `FakeTokenProvider`/`HttpClient` con handler personalizado equivalente a los descritos en el diseño (sección "Estrategia de Testing") en un scratch/consola de prueba manual, sin añadirlo al proyecto de tests.

## Follow-ups / Known Limitations

- **Tests unitarios pendientes (bloqueante para cerrar el issue según el propio diseño):** los 9 casos de `AnthropicApiUsageDataSourceTests` (más los test doubles `StubHttpMessageHandler`/`FakeTokenProvider`) especificados en la sección "Estrategia de Testing" del documento de diseño, y opcionalmente el test puro de `UsageSnapshot`/`RawRateLimitHeaders` en `ClaudeMeter.Domain.Tests`, quedan pendientes para la fase `sdlc-testing` (QA Engineer).
- **Validación manual contra la API real (paso 8 del Implementation Plan) pendiente:** ver "How to Verify" — el usuario debe ejecutarla con su propio token antes de dar por cerrado el issue original, y ajustar el diseño/código si el nombre real de las cabeceras (`utilization` vs `remaining`) o el `model` id difieren de lo asumido.
- **Composition root / DI:** el cableado de `IUsageDataSource` → `AnthropicApiUsageDataSource` (con el `HttpClient` singleton documentado en el snippet del diseño) en `ClaudeMeter.Desktop` sigue sin existir — está fuera de alcance de este issue (llega en F1).
- **Riesgos heredados del documento de diseño/requisitos** (no bloquean esta entrega, pero conviene tenerlos presentes): las cabeceras `anthropic-ratelimit-unified-*` no son un contrato oficial documentado por Anthropic y pueden diferir de lo aquí asumido; Anthropic podría cambiar sin aviso el comportamiento no documentado del que depende esta llamada (incluido `User-Agent`); cada llamada (y cada ciclo de polling en F1) consume una cantidad mínima de cuota real.

Nada se ha confirmado (`commit`) ni enviado (`push`) al repositorio: todos los cambios quedan en el árbol de trabajo, listos para revisión. No se ha escrito ni ejecutado ningún test (`dotnet test` no se ha invocado) — la validación realizada en esta fase se limita exclusivamente a `dotnet build` (Debug y Release), conforme a las reglas de este agente. Próximos pasos sugeridos del pipeline: `sdlc-testing` (escribir y ejecutar los tests descritos en el diseño, incluida la validación manual contra la API real) y `sdlc-documentation` (documentación técnica/funcional de este componente).
