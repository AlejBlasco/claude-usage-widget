# Requirements: [F0] Leer token desde .credentials.json

## Source
GitHub Issue #2 — "[F0] Leer token desde .credentials.json" (milestone "F0 — Núcleo de validación", label `enhancement`), repo `claude-usage-widget`.

## US-1: Proveer el token OAuth a la aplicación sin exponer el formato del fichero de credenciales

**As a** desarrollador del proyecto,
**I want** un `CredentialsFileTokenProvider` (Infrastructure) que implemente el puerto `ITokenProvider` (Application) leyendo el token en texto plano de `%USERPROFILE%\.claude\.credentials.json` en Windows,
**so that** el resto de la aplicación pueda obtener el token OAuth sin conocer el formato ni la ubicación del fichero.

### Acceptance Criteria

- **GIVEN** el proyecto Application, **WHEN** se inspecciona sus dependencias, **THEN** existe una interfaz `ITokenProvider` definida en Application que no referencia ningún tipo del proyecto Infrastructure ni de librerías externas de acceso a ficheros, y su método de lectura devuelve un `TokenResult` (tipo de dominio, ver Technical Notes) en lugar de un `string?` o un `Result<string>` genérico.

- **GIVEN** un fichero `%USERPROFILE%\.claude\.credentials.json` válido presente en esa ruta fija, cuyo JSON contiene un objeto anidado `claudeAiOauth` con un campo `accessToken` relleno con un valor no vacío, **WHEN** `CredentialsFileTokenProvider` intenta obtener el token, **THEN** devuelve un `TokenResult` en estado de éxito que contiene exactamente el valor de `claudeAiOauth.accessToken` leído del fichero, sin transformarlo.

- **GIVEN** que el fichero `%USERPROFILE%\.claude\.credentials.json` no existe en esa ruta, **WHEN** `CredentialsFileTokenProvider` intenta obtener el token, **THEN** devuelve un `TokenResult` en un estado explícito de "fichero no encontrado" (nunca lanza una excepción no controlada), distinguible del resto de estados de fallo.

- **GIVEN** que el fichero `%USERPROFILE%\.claude\.credentials.json` existe pero su contenido no es un JSON válido (JSON mal formado / corrupto), **WHEN** `CredentialsFileTokenProvider` intenta obtener el token, **THEN** devuelve un `TokenResult` en un estado explícito de "JSON inválido" (nunca lanza una excepción no controlada), y este caso queda registrado internamente de forma que sea diagnosticable (p. ej. mediante logging, cuando exista en fases posteriores) sin filtrar el contenido del fichero.

- **GIVEN** que el fichero `%USERPROFILE%\.claude\.credentials.json` existe y es JSON válido, pero no contiene el objeto `claudeAiOauth` o este no contiene el campo `accessToken`, **WHEN** `CredentialsFileTokenProvider` intenta obtener el token, **THEN** devuelve un `TokenResult` en un estado explícito de "token ausente o vacío".

- **GIVEN** que el fichero `%USERPROFILE%\.claude\.credentials.json` existe, es JSON válido y `claudeAiOauth.accessToken` existe pero su valor es una cadena vacía o solo espacios en blanco, **WHEN** `CredentialsFileTokenProvider` intenta obtener el token, **THEN** devuelve un `TokenResult` en el mismo estado explícito de "token ausente o vacío" que el caso de campo ausente (un token vacío no se considera un token válido).

- **GIVEN** cualquiera de los tres estados de fallo de `TokenResult` (fichero no encontrado, JSON inválido, token ausente o vacío), **WHEN** el consumidor de `ITokenProvider` solo necesita saber si obtuvo un token utilizable, **THEN** puede comprobarlo de manera uniforme (p. ej. una propiedad `IsSuccess`) sin lanzar ni capturar excepciones en ningún caso, aunque el estado concreto del fallo sigue disponible en el `TokenResult` para diagnóstico (consistencia de contrato, alineado con la regla de sustituibilidad tipo Liskov aplicada a las fuentes de datos del proyecto).

- **GIVEN** el `CredentialsFileTokenProvider` implementado, **WHEN** se revisa su responsabilidad, **THEN** la clase únicamente lee y parsea el fichero de credenciales — no realiza llamadas HTTP a la API de Anthropic ni calcula countdowns/porcentajes de rate limit (esas responsabilidades permanecen en clases separadas).

- **GIVEN** el conjunto de pruebas del proyecto, **WHEN** se ejecuta el test unitario de `CredentialsFileTokenProvider`, **THEN** dicho test utiliza un fichero de credenciales simulado/fixture (con un valor de `claudeAiOauth.accessToken` ficticio) y en ningún caso una credencial real, cubriendo como mínimo los casos y estados de `TokenResult`: éxito, fichero ausente, JSON mal formado, y token ausente/vacío (campo `accessToken` faltante y campo presente pero en blanco).

## Technical Notes
- Sin integración con Windows Credential Manager en esta fase (según roadmap F0); el token se lee en texto plano directamente del fichero.
- `ITokenProvider` debe seguir el principio de puertos pequeños de Application (no un `IUsageService` monolítico) — su única responsabilidad es exponer el token.
- Respetar la separación de responsabilidades explícita del proyecto: leer el token, llamar a la API, calcular el countdown y renderizar deben seguir siendo clases distintas; este issue cubre exclusivamente la lectura/parseo del token.
- No implementar refresco de OAuth propio en esta clase ni en ninguna parte de esta fase; esa responsabilidad (aviso claro ante 401/403) corresponde a F2 y queda fuera del alcance de este issue.
- Cobertura de test objetivo del proyecto: 70% (`testingCoverage` en `.claude/sdlc.config.yaml`); el test unitario de esta clase debe contribuir a ese objetivo para Infrastructure/Application.
- **Estructura esperada del fichero de credenciales:** un objeto JSON raíz con una propiedad anidada `claudeAiOauth`, que a su vez contiene (entre otros) el campo `accessToken` (string, token OAuth en texto plano). Es probable que `claudeAiOauth` también incluya `refreshToken` y `expiresAt` junto a `accessToken`, pero ninguno de esos dos campos es responsabilidad de este issue — solo `accessToken` debe leerse aquí. Ejemplo orientativo:
  ```json
  { "claudeAiOauth": { "accessToken": "...", "refreshToken": "...", "expiresAt": "..." } }
  ```
- **Tipo del resultado (`TokenResult`):** el "resultado sin datos" debe modelarse como un tipo de dominio dedicado (p. ej. `TokenResult`), no como `string?` ni como un `Result<string>` genérico. Debe exponer un estado explícito con (al menos) cuatro casos: éxito (con el valor del token), fichero no encontrado, JSON mal formado, y token ausente/vacío. Al ser un value object del dominio del problema (no depende de infraestructura), debe vivir en **Domain**; `ITokenProvider` (Application) lo referencia como tipo de retorno, respetando que Application solo depende de Domain.
- **Ruta fija en F0:** la ruta `%USERPROFILE%\.claude\.credentials.json` debe quedar hardcodeada en esta fase (p. ej. como constante en `CredentialsFileTokenProvider` o en su ensamblado de Infrastructure). No introducir configurabilidad de ruta (variables de entorno, `config.json`, parámetros de constructor pensados para variarla en runtime) en F0; esa capacidad llega en F2 junto con el resto de `config.json` (intervalo/posición/chime), según el roadmap.

## Dependencies
- **Depends on:** Scaffolding inicial de la solución .NET (`ClaudeMeter.sln` + proyectos de las 4 capas) — ya completado, ver commit "F0: scaffolding de la solución .NET (ClaudeMeter) + plantilla de PR (#28)", mergeado en `master`.
- **Related to:** — (ninguna dependencia adicional identificada en el issue original)
- **Decidido:** Ruta del fichero de credenciales confirmada como `%USERPROFILE%\.claude\.credentials.json` (la misma que usa el CLI oficial de Claude Code en Windows).
- **Decidido:** Campo del token confirmado como `claudeAiOauth.accessToken` (objeto anidado `claudeAiOauth` en la raíz del JSON, con el campo `accessToken` en texto plano). Ver estructura detallada en Technical Notes.
- **Decidido:** El "resultado sin datos" se modela como el tipo de dominio `TokenResult`, con estado explícito (éxito / fichero no encontrado / JSON inválido / token ausente o vacío), viviendo en Domain. Ver detalle en Technical Notes.
- **Decidido:** La ruta del fichero de credenciales es fija/hardcodeada en F0; no se introduce configurabilidad hasta F2 (`config.json`).

## Risks
- **Data quality:** El fichero `%USERPROFILE%\.claude\.credentials.json` puede no existir en máquinas nuevas (usuario que nunca ha iniciado sesión con el CLI de Claude Code), o puede quedar en un estado corrupto/parcial si el proceso que lo escribe se interrumpe.
- **Security:** El token se maneja en texto plano en esta fase (sin Credential Manager); debe evitarse registrar el valor del token en logs o mensajes de error, incluso en los casos de fallo de parseo.
- **Performance:** N/A — la lectura de un fichero local pequeño no representa un riesgo de rendimiento relevante en esta fase.
- **Impact:** Medium/High — sin un token válido, ninguna otra funcionalidad de la aplicación (llamadas a la API, cálculo de rate limits, visualización) puede funcionar; este componente es una dependencia crítica de todo el resto del roadmap F0-F1.
