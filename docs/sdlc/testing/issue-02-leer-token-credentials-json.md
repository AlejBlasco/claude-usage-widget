# Testing Summary: [F0] Leer token desde .credentials.json

## Scope

Tests unitarios para la implementación del Issue #2, descrita en
`docs/sdlc/development/issue-02-leer-token-credentials-json.md` y diseñada en
`docs/sdlc/design/issue-02-leer-token-credentials-json.md`. Cubren los tres
tipos nuevos de esa fase:

- `ClaudeMeter.Domain.Authentication.TokenResult` / `TokenResultStatus`.
- `ClaudeMeter.Application.Abstractions.ITokenProvider` (puerto; no se testea
  directamente por ser solo una interfaz — se ejercita indirectamente a
  través de su implementación).
- `ClaudeMeter.Infrastructure.Authentication.CredentialsFileTokenProvider`
  (adaptador con toda la lógica de negocio de esta fase: lectura y parseo
  del fichero de credenciales).

Todos los casos GIVEN-WHEN-THEN del documento de requisitos están cubiertos,
más dos casos límite adicionales identificados en el propio documento de
diseño (JSON válido pero no es el objeto esperado, y condición de carrera de
fichero bloqueado entre el `File.Exists` y la lectura).

**Ningún test usa un token real ni la ruta de producción**
(`%USERPROFILE%\.claude\.credentials.json`): cada test de
`CredentialsFileTokenProvider` genera un fichero temporal único bajo
`Path.GetTempPath()` con un `accessToken` ficticio
(`"fake-access-token-for-tests"`), usando el constructor `internal` habilitado
vía `InternalsVisibleTo`, y lo borra en `Dispose()`.

## Tests Added/Modified

- `test/ClaudeMeter.Infrastructure.Tests/Authentication/CredentialsFileTokenProviderTests.cs`
  — **Nuevo.** 8 métodos de test (11 casos ejecutados, contando `[Theory]`):
  - `GetTokenAsync_ConFicheroValido_DevuelveSuccessConElTokenExacto` — éxito,
    verifica que el `accessToken` devuelto es exactamente el leído del JSON.
  - `GetTokenAsync_SinFichero_DevuelveFileNotFound` — fichero ausente en la
    ruta.
  - `GetTokenAsync_ConFicheroBloqueadoPorOtroProceso_DevuelveFileNotFound` —
    condición de carrera: el fichero existe pero se bloquea con
    `FileShare.None` justo antes de leerlo, forzando la `IOException` que
    captura la clase y la mapea también a `FileNotFound`.
  - `GetTokenAsync_ConJsonMalFormado_DevuelveInvalidJson` — JSON corrupto.
  - `GetTokenAsync_SinCampoAccessToken_DevuelveTokenMissing` — JSON válido
    sin el objeto `claudeAiOauth`.
  - `GetTokenAsync_ConAccessTokenVacioOEnBlanco_DevuelveTokenMissing`
    (`[Theory]`: `""` y `"   "`) — token presente pero vacío/en blanco.
  - `GetTokenAsync_ParaCualquierEstadoDeFallo_IsSuccessEsFalse` (`[Theory]`
    sobre los tres fallos: fichero ausente, JSON corrupto, `TokenMissing`) —
    comprobación uniforme de `IsSuccess` sin excepciones.
  - `GetTokenAsync_ConJsonValidoNulo_DevuelveTokenMissing` — caso límite
    documentado en el diseño: el literal JSON `null` deserializa a `dto ==
    null` vía null-conditional en vez de lanzar.

- `test/ClaudeMeter.Domain.Tests/Authentication/TokenResultTests.cs` —
  **Nuevo** (test opcional sugerido por el diseño, no bloqueante pero
  añadido por ser barato y de valor real). 5 métodos de test (7 casos
  ejecutados): cada fábrica estática (`Success`/`FileNotFound`/
  `InvalidJson`/`TokenMissing`) produce el `Status`/`AccessToken`/
  `IsSuccess` esperado, más una `[Theory]` que confirma `IsSuccess == false`
  de forma uniforme para los tres estados de fallo.

No se modificó ningún fichero de producción ni ningún `.csproj` — el
`InternalsVisibleTo` necesario ya lo añadió la fase de desarrollo.

## Resultado de la ejecución

- `dotnet test test/ClaudeMeter.Infrastructure.Tests/...`: **11/11 correctos**.
- `dotnet test test/ClaudeMeter.Domain.Tests/...`: **7/7 correctos**.
- `dotnet test ClaudeMeter.sln` (pasada completa, ejecutada una sola vez como
  confirmación final, no como bucle de iteración): **18/18 correctos** en
  total (`ClaudeMeter.Application.Tests` y `ClaudeMeter.Desktop.Tests` no
  aportan pruebas todavía — scaffolding vacío de F0, fuera del alcance de
  este issue).
- `dotnet build ClaudeMeter.sln -c Release`: **0 advertencias, 0 errores**
  (Roslyn analyzers + warnings-as-errors en Release, verificado tras añadir
  los tests).

## Coverage Result

- **Herramienta detectada**: `coverlet.collector` (paquete NuGet ya presente
  en `ClaudeMeter.Infrastructure.Tests.csproj` y `ClaudeMeter.Domain.Tests.csproj`
  desde el scaffolding F0). Se ejecutó con
  `dotnet test --collect:"XPlat Code Coverage"` (formato Cobertura), tal y
  como recomienda `dotnet-testing.md`.
- **Target**: 70% (`testingCoverage` en `.claude/sdlc.config.yaml`).
- **Achieved** (medido, no estimado, sobre el código nuevo de este issue):
  - `ClaudeMeter.Domain.Authentication.TokenResult` (`TokenResult.cs`):
    **100% de líneas** (12/12) y **100% de ramas** — cubierto íntegramente
    por `TokenResultTests`.
  - `ClaudeMeter.Infrastructure.Authentication.CredentialsFileTokenProvider`
    (`CredentialsFileTokenProvider.cs`, único fichero de producción del
    proyecto `ClaudeMeter.Infrastructure`): **85.4% de líneas** (41/48) y
    **100% de ramas** (8/8) a nivel de ensamblado.
    - El método con la lógica de negocio real, `GetTokenAsync` (compilado
      como máquina de estados async), queda al **100% de líneas y ramas**:
      los dos `try/catch`, la comprobación `File.Exists`, el
      `string.IsNullOrWhiteSpace` y las cuatro ramas de `TokenResultStatus`
      están todos ejercitados.

Ambos ficheros superan holgadamente el objetivo del 70%.

## Gaps / Not Covered

- El **constructor público sin parámetros** de `CredentialsFileTokenProvider`
  y su método privado `GetDefaultCredentialsFilePath()` (líneas 26-28 y
  43-46 del fichero) quedan **deliberadamente sin cubrir**: ejercitarlos
  implicaría resolver y tocar la ruta real de producción
  (`%USERPROFILE%\.claude\.credentials.json`), justo lo que las reglas de
  `CLAUDE.md` y el propio documento de diseño prohíben explícitamente
  ("nunca un token real en tests", "nunca la ruta real de producción"). Es
  el único motivo por el que la clase no llega al 100% de líneas — es un
  hueco aceptado y documentado, no una omisión.
- `OperationCanceledException` por cancelación del `CancellationToken` no
  tiene un test dedicado: el diseño decide explícitamente dejarla propagar
  sin capturar (comportamiento estándar de .NET para cancelación
  cooperativa, no un estado de `TokenResult`), por lo que no hay
  comportamiento de negocio nuevo que verificar ahí.
- No se han añadido tests de integración: el diseño descarta expresamente
  `IFileSystem`/`System.IO.Abstractions` y cualquier infraestructura externa
  (DB, HTTP) para este issue — toda la superficie testeable es E/S de
  fichero local determinista, ya cubierta con ficheros temporales reales en
  los tests unitarios. No aplica una "pasada de confirmación de integración"
  adicional aquí.

## Confirmación

No se ha ejecutado ningún `git commit` ni `git push` en ningún momento de
esta fase. Todos los cambios (los dos ficheros de test nuevos y este
resumen) quedan en el árbol de trabajo, listos para revisión manual del
usuario.
