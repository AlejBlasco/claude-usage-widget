# Testing Summary: [F0] Parsear headers anthropic-ratelimit-* a minutos/porcentaje

## Scope

Cobertura de tests unitarios para el código nuevo, aún sin confirmar (`commit`),
del Issue #4: `src/ClaudeMeter.Domain/Usage/RateLimitWindow.cs`, que contiene el
record `RateLimitWindow` (`PercentageUsed: double?`, `MinutesRemaining: int?`,
más el miembro estático `Unavailable`) y la clase estática
`RateLimitWindowParser` (`Parse(RawRateLimitHeaders?, DateTimeOffset now)` y
`ParseSnapshot(UsageSnapshot, DateTimeOffset now)`).

Se ha trabajado a partir de tres documentos ya existentes en el repositorio:

- `docs/sdlc/requirements/issue-04-parsear-ratelimit-headers.md` — los ocho
  GIVEN-WHEN-THEN funcionales.
- `docs/sdlc/design/issue-04-parsear-ratelimit-headers.md` — los 10 casos de
  la sección "Estrategia de Testing" (usados como base 1:1, con nombres de
  test casi idénticos a los propuestos allí).
- `docs/sdlc/development/issue-04-parsear-ratelimit-headers.md` — confirma
  que el desarrollador dejó explícitamente la suite de tests fuera de
  alcance de su fase, pendiente de esta fase de QA.

Al ser lógica 100% pura (sin HTTP, sin E/S, sin reloj real), no se ha
necesitado ningún test double/fake ni ninguna prueba de integración — todos
los tests son `[Fact]`/`[Theory]` de xUnit directos contra
`RateLimitWindowParser`, construyendo `RawRateLimitHeaders`/`UsageSnapshot` a
mano y usando siempre un `DateTimeOffset` literal fijo
(`2026-09-16T10:00:00Z`) como "ahora".

## Tests Added/Modified

- `test/ClaudeMeter.Domain.Tests/Usage/RateLimitWindowParserTests.cs` —
  **Nuevo.** 15 métodos de test (22 casos ejecutados en total, contando las
  variantes `[Theory]`), namespace `ClaudeMeter.Domain.Tests.Usage`, siguiendo
  la misma convención de nombres/estilo ya usada en
  `UsageSnapshotTests.cs`/`TokenResultTests.cs` del mismo proyecto:
  - `Parse_ConUtilizationYResetValidos_CalculaPercentageUsedYMinutesRemaining` —
    caso feliz (`Utilization="10%"`, reset a +120 min exactos).
  - `Parse_ConUtilizationYRemainingNoComplementarios_IgnoraRemainingPorCompleto` —
    `Utilization=10%`/`Remaining=80%` no complementarios; `Remaining` se
    ignora sin validación cruzada ni error.
  - `Parse_ConUtilizationCero_DevuelvePercentageUsedCeroSinNegativos` — `0%`.
  - `Parse_ConUtilizacionCienYResetYaPasado_DevuelveMinutesRemainingCero` —
    `100%` con reset ya pasado → `MinutesRemaining=0`, nunca negativo.
  - `Parse_ConResetExactamenteIgualAAhora_DevuelveMinutesRemainingCero` —
    caso límite explícito del AC ("igual o anterior").
  - `Parse_ConMinutosFraccionarios_RedondeaHaciaArribaConMathCeiling` — reset a
    +4 min 10 s → `MinutesRemaining=5` (verifica `Math.Ceiling`, no truncado).
  - `Parse_ConResetAusente_DevuelveMinutesRemainingNullSinAfectarPercentageUsed`.
  - `Parse_ConUtilizationAusente_DevuelvePercentageUsedNullSinAfectarMinutesRemaining`.
  - `Parse_ConHeadersNull_DevuelveRateLimitWindowUnavailable` — `headers=null`
    → `RateLimitWindow.Unavailable` sin excepción.
  - `Parse_ConUtilizationEnFormatoInesperado_DevuelvePercentageUsedNull`
    (`[Theory]`: `"0.1"`, `"10,5%"`, `" 10% "`, `"abc%"`, `"150%"`, `""`) —
    fracción sin `%`, separador decimal distinto, espacios extra, texto no
    numérico, fuera de rango `[0,100]` y cadena vacía; verifica además que
    `MinutesRemaining` no se ve afectado.
  - `Parse_ConResetEnFormatoInesperado_DevuelveMinutesRemainingNullSinAfectarPercentageUsed`
    (`[Theory]`: `"not-a-date"`, `""`) — caso simétrico para `Reset`, no
    exigido explícitamente por el AC pero cubierto por completitud (`Reset`
    también usa una ruta `TryParse` que nunca lanza).
  - `ParseSnapshot_ConUsageSnapshotEnEstadoDeFallo_DevuelveAmbasVentanasUnavailable`
    (`[Theory]` sobre `TokenUnavailable`/`Unauthorized`/`RequestFailed`).
  - `ParseSnapshot_ConSessionYWeeklyCompletos_CalculaAmbasVentanasCorrectamente` —
    `ParseSnapshot` contra un `UsageSnapshot.Success` real, verificando
    `Session` y `Weekly` de forma explícita (120 min / 5760 min).
  - `ParseSnapshot_ConSessionCompletaYWeeklyConCamposAusentes_CalculaCadaVentanaDeFormaIndependiente` —
    confirma que un fallo de parseo en `Weekly` no contamina `Session`.

No se ha modificado ningún test existente (`UsageSnapshotTests.cs`,
`TokenResultTests.cs` y los de `Infrastructure`/`Application` quedan
intactos).

## Coverage Result

- Target (`testingCoverage` en `.claude/sdlc.config.yaml`): **70%**
- Comando ejecutado: `dotnet test test/ClaudeMeter.Domain.Tests/ClaudeMeter.Domain.Tests.csproj --collect:"XPlat Code Coverage"`
  (Coverlet, ya presente como `PackageReference` en el proyecto de test — no
  se ha introducido ninguna herramienta nueva).
- Resultado de la ejecución: **37/37 tests correctos** (22 nuevos de
  `RateLimitWindowParserTests` + 15 preexistentes de
  `UsageSnapshotTests`/`TokenResultTests`), 0 fallos.
- Cobertura del fichero nuevo (`Usage/RateLimitWindow.cs`), leída del
  `coverage.cobertura.xml` generado:
  - `ClaudeMeter.Domain.Usage.RateLimitWindow` (el record): **line-rate 100%,
    branch-rate 100%**.
  - `ClaudeMeter.Domain.Usage.RateLimitWindowParser` (la lógica de parseo):
    **line-rate 93.1%, branch-rate 94.4%**.
- **Achieved: ~93-100% en el código tocado por este issue — muy por encima
  del 70% objetivo.**

## Gaps / Not Covered

- Dos líneas de `RateLimitWindowParser.TryParsePercentageUsed` quedan sin
  cubrir: la rama `if (!double.TryParse(...)) { return null; }` posterior a
  que la regex `UtilizationPattern` ya haya validado el string. En la
  práctica esta rama es código defensivo prácticamente inalcanzable: la
  regex (`^(0|[1-9]\d*)(\.\d+)?%$`) sólo deja pasar cadenas que son, por
  construcción, representaciones válidas de `double` en formato invariante;
  no se ha encontrado (ni se considera razonable fabricar) una cadena que
  matchee la regex y a la vez haga fallar `double.TryParse` con
  `NumberStyles.AllowDecimalPoint` — en .NET 8 un número que desborda el
  rango de `double` se parsea igualmente como `Infinity` en vez de fallar.
  Forzar un test para esta rama exigiría un `InlineData` artificioso sin
  ningún escenario real detrás, lo que iría contra la regla de "nunca tests
  para inflar cobertura" — se deja documentado como código defensivo, no
  como gap funcional.
- No se ha escrito ningún test de integración: no aplica, `RateLimitWindowParser`
  no tiene ninguna dependencia externa (HTTP, E/S, reloj del sistema) que
  justifique una prueba de ese tipo, tal y como confirma el propio diseño.
- Fuera de alcance de este issue/fase (documentado ya en el resumen de
  desarrollo, no es un gap de testing): el cableado real de
  `RateLimitWindowParser` desde un `IRequestHandler` de Application (F1) no
  existe todavía, por lo que no hay ningún test de ese nivel que escribir
  aquí.

---

Nada se ha confirmado (`commit`) ni enviado (`push`) al repositorio: el
fichero de test nuevo queda en el árbol de trabajo, listo para revisión.
