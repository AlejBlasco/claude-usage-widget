# Requirements: [F0] Bucle de polling con salida por consola cada 60s

## Source
GitHub issue #5 — https://github.com/AlejBlasco/claude-usage-widget/issues/5

## US-1: Bucle de polling con salida por consola cada 60s

**As a** desarrollador del proyecto,
**I want** un bucle que use el core anterior (token → API → parseo) y saque el resultado por consola cada 60 segundos,
**so that** pueda validar de extremo a extremo que el pipeline de datos funciona antes de construir la UI.

### Acceptance Criteria

**Camino feliz — impresión periódica de sesión/semana y countdown**

- **GIVEN** un `.credentials.json` válido con un token OAuth vigente y la API de Anthropic respondiendo correctamente con los headers `anthropic-ratelimit-*`
  **WHEN** el bucle de polling ejecuta una iteración
  **THEN** la consola imprime una línea con marca de tiempo, el porcentaje de uso y los minutos restantes de la ventana de sesión, y el porcentaje de uso y los minutos restantes de la ventana semanal

- **GIVEN** el bucle de polling en ejecución con el core devolviendo datos válidos en cada ciclo
  **WHEN** transcurren 60 segundos desde la última impresión
  **THEN** el bucle ejecuta una nueva iteración y imprime una nueva línea de estado, sin que el proceso finalice

- **GIVEN** una respuesta de la API en la que el header de sesión o el semanal está ausente o no es parseable (p. ej. `RateLimitWindow` con `PercentageUsed` o `MinutesRemaining` a `null`)
  **WHEN** el bucle formatea la línea de salida para esa iteración
  **THEN** la consola muestra claramente el dato disponible y marca el dato ausente como "no disponible" (o equivalente), sin lanzar una excepción ni interrumpir el bucle

**Errores — token no disponible / archivo de credenciales ausente**

- **GIVEN** que `.credentials.json` no existe o no puede leerse (`ITokenProvider` devuelve `FileNotFound` o `InvalidJson` o `TokenMissing`)
  **WHEN** el bucle ejecuta una iteración
  **THEN** la consola imprime un mensaje de error claro y distinguible indicando que no se pudo obtener el token, sin que el proceso se cierre

**Errores — token inválido o expirado (401/403)**

- **GIVEN** que la API responde 401 o 403 (`AnthropicApiUsageDataSource` devuelve `UsageSnapshotStatus.Unauthorized`)
  **WHEN** el bucle ejecuta una iteración
  **THEN** la consola imprime un mensaje de error claro y distinguible (diferente del de fallo de red) indicando que el token es inválido/expirado y que no se intentará refrescarlo automáticamente, sin que el proceso se cierre

**Errores — fallo de red / petición fallida**

- **GIVEN** que la llamada a la API falla por un problema de red, timeout, error 5xx, o una respuesta 200 sin los headers esperados (`UsageSnapshotStatus.RequestFailed`)
  **WHEN** el bucle ejecuta una iteración
  **THEN** la consola imprime un mensaje de error claro y distinguible indicando el fallo de la petición, sin que el proceso se cierre

**Continuidad — el bucle sigue funcionando tras un error**

- **GIVEN** que una iteración del bucle produjo cualquiera de los estados de error anteriores (`TokenUnavailable`, `Unauthorized` o `RequestFailed`) o incluso una excepción inesperada no controlada por las capas inferiores
  **WHEN** transcurren los siguientes 60 segundos
  **THEN** el bucle ejecuta una nueva iteración con normalidad (incluyendo la posibilidad de que esa iteración sí tenga éxito), sin que el error previo detenga o finalice el proceso

## Technical Notes

- El "core" (token → API → parseo) ya está completamente implementado y no requiere cambios: `CredentialsFileTokenProvider` (Infrastructure), `AnthropicApiUsageDataSource` (Infrastructure) y `RateLimitWindowParser` (Domain, puro) ya devuelven estados explícitos (`TokenResult`, `UsageSnapshotStatus`) en vez de lanzar excepciones para los casos esperados. Este issue únicamente añade la orquestación (bucle + formateo + impresión), sin tocar Domain/Application/Infrastructure.
- Salida por consola: se recomienda un formato legible de una línea por iteración, por ejemplo `[HH:mm:ss] Sesión: 42% (23 min) | Semana: 10% (620 min)` para el caso de éxito, y una línea igualmente prefijada con la marca de tiempo pero con una etiqueta de error explícita y distinta por cada estado (p. ej. `[HH:mm:ss] ERROR - Token no disponible: ...`, `[HH:mm:ss] ERROR - Token inválido/expirado (401/403): ...`, `[HH:mm:ss] ERROR - Fallo de conexión con la API: ...`).
- Se confirma `Console.WriteLine` (sin logger) como el mecanismo correcto para F0, según el roadmap — Serilog no se introduce hasta F2.
- Mecánica del bucle: `while(true)` con `Task.Delay(TimeSpan.FromSeconds(60))` entre iteraciones; cada iteración debe envolverse en su propio manejo de errores (try/catch para cualquier excepción no prevista, además de gestionar explícitamente los estados de error de `TokenResult`/`UsageSnapshotStatus`) de forma que ninguna excepción escape y termine el proceso. F0 no requiere apagado controlado (no hay UI todavía); el proceso corre hasta que se mate manualmente.
- La reutilización por parte de F1 ("Related to") queda satisfecha por construcción: el bucle solo orquesta los puertos de Application/Infrastructure ya existentes, por lo que F1 puede reutilizar la misma lógica sin duplicarla.
- Nuevo proyecto `src/ClaudeMeter.Console`: proyecto ejecutable de consola (.NET, `OutputType=Exe`) añadido a `ClaudeMeter.sln`, que referencia `ClaudeMeter.Application` y `ClaudeMeter.Infrastructure`. Es el punto de entrada de F0 (`Program.cs` con `Main`) y compone: `CredentialsFileTokenProvider`, un `HttpClient` compartido, `AnthropicApiUsageDataSource`, `RateLimitWindowParser` y el propio bucle de polling (formateo + impresión por `Console.WriteLine` cada 60s). Es un arnés de validación desechable, exclusivo de F0.

## Dependencies

- **Depends on:** Parsear headers anthropic-ratelimit-* (issue #4) — COMPLETADO Y MERGEADO. `RateLimitWindow`/`RateLimitWindowParser` están implementados en `src/ClaudeMeter.Domain/Usage/RateLimitWindow.cs`, commiteados en `10c345d feat(domain): parsear headers anthropic-ratelimit-* a RateLimitWindow (#31)`, presente en la rama actual con working tree limpio.
- **Related to:** F1 reutilizará este mismo core (token → API → parseo) para alimentar la UI del widget; la lógica del bucle debe quedar en un lugar reutilizable, no duplicado.
- **Decisión confirmada:** el bucle de polling se aloja en un proyecto nuevo `src/ClaudeMeter.Console` (`Program.cs` con `Main`), que referencia `ClaudeMeter.Application` y `ClaudeMeter.Infrastructure`. Es un arnés de validación desechable de F0 — F1 reutilizará el mismo core (los mismos puertos de Application/Infrastructure) directamente desde `ClaudeMeter.Desktop`, no desde este proyecto de consola.

## Risks

- **Data quality:** N/A — los datos de uso provienen directamente de los headers de la API ya parseados por `RateLimitWindowParser`; no se introduce transformación adicional en este issue.
- **Performance:** Polling cada 60 segundos; no se espera problema de rate limit con esta cadencia.
- **Seguridad:** Se respeta la regla de CLAUDE.md de no implementar refresco automático del token OAuth; ante 401/403 se debe mostrar un aviso claro y distinguible en vez de reintentar la autenticación.
- **Impact:** Low — es un issue de cierre de F0 (validación de consola sin componente visual), acotado y sin impacto en el resto del sistema. El único punto que requería una decisión (dónde alojar el punto de entrada) ya está resuelto: nuevo proyecto `src/ClaudeMeter.Console`.
