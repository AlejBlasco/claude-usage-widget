# Design: [F0] Parsear headers anthropic-ratelimit-* a minutos/porcentaje

## Requirements Reference

`docs/sdlc/requirements/issue-04-parsear-ratelimit-headers.md` (Issue #4, milestone "F0 — Núcleo de validación"). Documento cerrado: todas las decisiones de forma que el propio documento delegaba explícitamente a esta fase de Diseño (forma exacta de `RateLimitWindow`, fuente autoritativa del porcentaje, mecanismo de inyección de "ahora") ya vienen resueltas en sus Technical Notes y se tratan aquí como restricciones, no como preguntas abiertas. Lo que añade este documento es la ubicación exacta del código, la firma completa de la función de parseo, el tratamiento preciso de cada caso límite y cómo encaja con `UsageSnapshot`/`AnthropicApiUsageDataSource` ya existentes.

## Resumen / Alcance

Este diseño cubre exclusivamente la conversión pura `RawRateLimitHeaders` (+ una fecha de referencia explícita) → `RateLimitWindow`, para las dos ventanas ya modeladas por `UsageSnapshot` (sesión de 5h y semanal de 7d). Introduce dos piezas nuevas, ambas en Domain:

- El tipo de valor `RateLimitWindow` (no existe todavía en el repo).
- La función de parseo (`RateLimitWindowParser`), pura y determinista, sin HTTP ni E/S.

Fuera de alcance (explícitamente, según el propio documento de requisitos y el roadmap de `CLAUDE.md`):
- Cualquier `Query`/`Command` de MediatR que invoque este parser desde Application — no existe todavía ningún caso de uso en `ClaudeMeter.Application` (hoy solo contiene `Abstractions/`); cablear un query real que combine `IUsageDataSource` + este parser es trabajo de F1.
- Countdown animado, colores por umbral, `UsagePage.razor` — F1/F3.
- Cualquier cambio en `AnthropicApiUsageDataSource` o en `UsageSnapshot`/`RawRateLimitHeaders` — ninguno de los dos necesita modificarse (ver "Arquitectura" para el rationale explícito de por qué `AnthropicApiUsageDataSource` no debe llamar a este parser).

No se necesita ninguna `ProjectReference` nueva (todo vive en `ClaudeMeter.Domain`, que ya no depende de ningún otro proyecto) ni ningún paquete NuGet nuevo: `System.Text.RegularExpressions` y `System.Globalization` ya están disponibles vía el BCL de .NET 8.

## Arquitectura

### Ubicación exacta de cada pieza

| Tipo | Proyecto | Namespace | Fichero propuesto |
|---|---|---|---|
| `RateLimitWindow` (record) + `RateLimitWindowParser` (clase estática) | `ClaudeMeter.Domain` | `ClaudeMeter.Domain.Usage` | `src/ClaudeMeter.Domain/Usage/RateLimitWindow.cs` |
| `RateLimitWindowParserTests` | `ClaudeMeter.Domain.Tests` | `ClaudeMeter.Domain.Tests.Usage` | `test/ClaudeMeter.Domain.Tests/Usage/RateLimitWindowParserTests.cs` |

**Rationale de namespace/fichero**: `ClaudeMeter.Domain.Usage` es el mismo sub-namespace por feature ya usado por `UsageSnapshot`/`RawRateLimitHeaders` (Issue #3) — `RateLimitWindow` es conceptualmente parte de la misma feature vertical ("uso/rate-limit"), no una feature nueva. Se bundlea el record `RateLimitWindow` y la clase estática `RateLimitWindowParser` en un único fichero `RateLimitWindow.cs`, replicando el precedente ya sentado por `UsageSnapshot.cs` (que agrupa un enum + dos records relacionados en un solo fichero) en vez de fragmentar en dos ficheros de pocas líneas cada uno. Alternativa considerada y rechazada: un fichero `RateLimitWindowParser.cs` separado — descartada por coherencia con el precedente del proyecto y porque ambos tipos cambian siempre juntos (el parser es la única forma de construir un `RateLimitWindow` con datos reales).

**Por qué Domain y no Application**: la función de parseo no necesita ningún puerto (`ITokenProvider`, `IUsageDataSource`) ni ningún caso de uso de MediatR — es una transformación pura `Domain → Domain` (`RawRateLimitHeaders` ya vive en Domain desde el Issue #3). Colocarla en Application obligaría a esa capa a exponer un tipo que en realidad no orquesta nada (no hay `IRequest`/`IRequestHandler` involucrado) y rompería la regla de `CLAUDE.md` de que Application son "casos de uso vía MediatR... más pequeños puertos" — esto no es ninguna de las dos cosas, es lógica de dominio. Alternativa considerada y rechazada: un `IRequestHandler<ParseRateLimitWindowsQuery, ...>` en Application que delegue en la lógica — rechazada por prematura: no hay ningún consumidor real todavía (F1) que necesite que esto pase por el pipeline de MediatR, y envolver una función pura y sin dependencias en un `IRequestHandler` solo añadiría ceremonia sin beneficio, contradiciendo el propio Technical Note del documento de requisitos ("Application... si RateLimitWindow no necesita ningún puerto").

### Por qué `AnthropicApiUsageDataSource` NO debe invocar este parser

El enunciado de esta tarea sugiere verificar "cómo se conecta... y es consumida posteriormente por `AnthropicApiUsageDataSource`" — es importante precisar esto con exactitud para no violar una regla ya fijada en `CLAUDE.md`: *"Reading the token, calling the API, computing the countdown, and rendering are separate classes — don't collapse them."* `AnthropicApiUsageDataSource` es la clase que **llama a la API**; `RateLimitWindowParser` es la clase que **calcula el countdown/porcentaje**. Si `AnthropicApiUsageDataSource.GetUsageAsync` invocara internamente a `RateLimitWindowParser`, colapsaría ambas responsabilidades en la misma clase, exactamente lo que la regla prohíbe — y además acoplaría el resultado devuelto (`UsageSnapshot`, pensado para representar fielmente "lo que dijo la API, sin transformar") a una fecha de referencia (`now`) que esa clase no tiene ninguna razón de negocio para conocer.

En consecuencia, este issue **no modifica** `AnthropicApiUsageDataSource` ni `UsageSnapshot`. El consumidor real de `RateLimitWindowParser` será, en F1, un componente nuevo de Application (p. ej. un `GetCurrentUsageQuery`/`GetCurrentUsageQueryHandler` de MediatR) que:
1. obtiene un `UsageSnapshot` vía `IUsageDataSource.GetUsageAsync()` (ya existente, sin cambios),
2. captura `now` como `DateTimeOffset.UtcNow` en el borde de la aplicación (Application/Desktop — nunca dentro de Domain),
3. llama a `RateLimitWindowParser.ParseSnapshot(snapshot, now)` para obtener `(RateLimitWindow Session, RateLimitWindow Weekly)`,
4. expone ambos a `UsagePage.razor`.

Ese cableado queda fuera de alcance de este issue (no existe hoy ningún `IRequestHandler` en el repo — `ClaudeMeter.Application` solo tiene `Abstractions/`); se documenta aquí únicamente como guía para cuando F1 lo necesite.

### Flujo de dependencias

```
ClaudeMeter.Domain.Tests
        │ (ProjectReference)
        ▼
ClaudeMeter.Domain
   RateLimitWindowParser.Parse(RawRateLimitHeaders?, DateTimeOffset now) ──► RateLimitWindow
   RateLimitWindowParser.ParseSnapshot(UsageSnapshot, DateTimeOffset now) ──► (RateLimitWindow, RateLimitWindow)
        │ usa (mismo proyecto, sin dependencia externa)
        ▼
   RawRateLimitHeaders / UsageSnapshot   (ya existentes, Issue #3 — sin cambios)

   (fuera de alcance) Application/F1 ── futuro query handler ── consumirá RateLimitWindowParser
   (fuera de alcance) Infrastructure/AnthropicApiUsageDataSource ── NUNCA llama a RateLimitWindowParser
```

- `ClaudeMeter.Domain` sigue sin ninguna `ProjectReference` (núcleo puro) — este issue no la introduce.
- No hay ningún nuevo puerto en `ClaudeMeter.Application.Abstractions`: no hace falta, es lógica de dominio sin I/O.

## Decisiones Técnicas Clave

| Decisión | Elección | Rationale | Alternativa(s) consideradas y rechazadas |
|---|---|---|---|
| **Forma de `RateLimitWindow`** | Record posicional sellado: `public sealed record RateLimitWindow(double? PercentageUsed, int? MinutesRemaining)`, con un miembro estático `RateLimitWindow.Unavailable` (`= new(null, null)`) | El propio documento de requisitos ya fija el contenido (solo estos dos campos, sin `Reset` crudo). Un record posicional simple (sin el patrón de fábricas estáticas + constructor privado de `TokenResult`/`UsageSnapshot`) es suficiente aquí porque no hay ningún estado inconsistente que prevenir: cualquier combinación de `null`/valor en los dos campos es válida y con significado propio (dato disponible vs. "no disponible"), a diferencia de `UsageSnapshot`, donde `Success` con `Session=null` sí sería un estado inconsistente que ese patrón evita. Se sigue así el mismo estilo posicional ya usado por `RawRateLimitHeaders` (el otro record "de solo datos" del mismo namespace). | (a) Reutilizar el patrón fábricas-estáticas + enum de estado (`RateLimitWindowStatus.Available`/`Unavailable`) — rechazada por sobre-ingeniería: no hay ninguna invariante que proteger más allá de "cada campo es su propio valor opcional", y el enum añadiría un tercer concepto (`Status`) que el Acceptance Criteria no pide y que duplicaría la información que ya llevan los dos `Nullable<T>`. (b) Exponer `PercentageUsed`/`MinutesRemaining` como `double`/`int` no-nulos con un valor centinela (p. ej. `-1`) para "no disponible" — rechazada explícitamente por el propio Acceptance Criteria ("sin inventar un valor por defecto... que pueda inducir a error"). |
| **Tipo de `PercentageUsed`** | `double?`, en escala 0-100 (no 0-1) | `Utilization="10%"` ya viene en escala porcentual; devolver `10.0` en vez de `0.10` evita una conversión mental extra en cada consumidor (UI, tests) y es la lectura literal del propio header. Nullable representa "no disponible" sin campo de estado adicional (ver decisión anterior) — además satisface directamente el Acceptance Criteria de "formato inesperado → distinguible como no interpretable" ya que `null` es, por construcción, distinguible de cualquier porcentaje real parseado. | `int?` — rechazada: aunque los ejemplos observados son enteros, el formato no está documentado oficialmente (Technical Notes) y no hay ninguna razón para descartar por diseño un futuro `"12.5%"`; `double?` lo admite sin más cambios. `decimal?` — rechazada: mayor precisión de la que aporta valor real para un porcentaje mostrado con como mucho un decimal en la UI; `double` es el tipo por defecto para valores de punto flotante en .NET y evita conversiones explícitas al comparar con umbrales (F1: verde/ámbar/rojo). |
| **Formato aceptado para `Utilization`** | Regex estricta `^(0\|[1-9]\d*)(\.\d+)?%$` sobre el string completo (sin recortar espacios antes de validar), parseada con `double.TryParse(..., NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, ...)`; fuera de este patrón ⇒ `PercentageUsed = null` | El Acceptance Criteria enumera explícitamente como "formato inesperado" tanto una fracción (`"0.1"`), como un separador decimal distinto, como espacios extra, como un valor no numérico — los cuatro deben mapear a "no disponible". Una regex estricta que exige exactamente dígitos + `.` opcional + `%`, sin espacios internos ni externos, rechaza los cuatro casos a la vez sin necesidad de cuatro ramas de validación distintas. Se usa `CultureInfo.InvariantCulture` (nunca la cultura del sistema) para que el resultado no dependa de la configuración regional de la máquina donde corra el widget — riesgo real en un runtime de escritorio, a diferencia de un backend con cultura fija. | Recortar (`Trim()`) el string antes de validar y aceptar espacios externos — rechazada porque el propio Acceptance Criteria pone "con espacios extra" en la misma lista que los demás formatos inesperados que deben tratarse como no disponibles; ser tolerante aquí iría contra el AC literal. Aceptar coma como separador decimal además de punto — rechazada por la misma razón (el AC lista "separador decimal distinto" como caso a rechazar, no a tolerar); además evita ambigüedad si el header cambiara a incluir separador de miles. |
| **Rango válido de `PercentageUsed`** | Un valor sintácticamente válido pero fuera de `[0, 100]` (p. ej. `"150%"`) también se trata como no disponible (`null`) | Un porcentaje "consumido" fuera de `[0, 100]` es semánticamente imposible dado lo que representa el header; aceptarlo sin validar propagaría un dato claramente corrupto hacia la UI (F1) sin ninguna señal, contradiciendo el espíritu del propio AC de formato inesperado (que ya pide tratar como no disponible cualquier valor no fiable). | Aceptar el valor tal cual sin acotar — rechazada: permitiría que un porcentaje corrupto llegara intacto hasta la UI. Recortar (`clamp`) a `[0, 100]` en vez de descartar — rechazada: enmascararía silenciosamente un dato erróneo con uno plausible pero inventado, exactamente el patrón que el AC pide evitar para los demás casos de formato inesperado; es preferible que quede explícitamente como "no disponible" y, por tanto, diagnosticable. |
| **Parseo de `Reset` y cálculo de `MinutesRemaining`** | `DateTimeOffset.TryParse(reset, CultureInfo.InvariantCulture, DateTimeStyles.None, out resetTime)`; si falla o `reset is null` ⇒ `MinutesRemaining = null`. Si `resetTime - now <= TimeSpan.Zero` ⇒ `0`; en otro caso, `(int)Math.Ceiling((resetTime - now).TotalMinutes)` | `TryParse` nunca lanza excepción, cumpliendo el AC de "no lanza ninguna excepción" para cualquier valor de `Reset`. El Acceptance Criteria original no incluye ningún caso de "formato inesperado" específico para `Reset` (solo lo exige para `Utilization`) — no se justifica una regex estricta adicional aquí; la tolerancia nativa de `DateTimeOffset.TryParse` a variantes ISO-8601/RFC 3339 (offsets explícitos, con o sin milisegundos) es una ventaja, no un riesgo, mientras siga sin lanzar. Se usa `Math.Ceiling` (redondeo hacia arriba) en vez de truncar/`Math.Floor`: para un contador de "minutos hasta que se libere el rate-limit", sobreestimar ligeramente el tiempo restante (p. ej. mostrar "5" cuando quedan 4 min 10 s) es más seguro que subestimarlo — subestimar podría hacer que la UI (F1) sugiera reintentar antes de que la ventana realmente se libere. | Exigir un formato exacto (`"yyyy-MM-ddTHH:mm:ssZ"` vía `DateTimeOffset.ParseExact`) para `Reset` — rechazada: el propio AC no lo exige (a diferencia de `Utilization`) y sería más frágil ante variantes válidas de ISO-8601 que Anthropic podría emitir sin que constituya un "cambio de formato" real. `Math.Floor`/truncar minutos — rechazada por el argumento de seguridad explicado (evitar subestimar el tiempo de espera real). |
| **Firma de la función de parseo** | `public static RateLimitWindow Parse(RawRateLimitHeaders? headers, DateTimeOffset now)`, más una sobrecarga de conveniencia `public static (RateLimitWindow Session, RateLimitWindow Weekly) ParseSnapshot(UsageSnapshot snapshot, DateTimeOffset now)` | `Parse` acepta `RawRateLimitHeaders?` (nullable) para poder tratar de forma uniforme tanto "la ventana venía con campos `null`" como "la ventana ni siquiera existía" (`UsageSnapshot.Session`/`Weekly` son `null` en cualquier estado que no sea `Success`) — ambos casos producen `RateLimitWindow.Unavailable` sin necesidad de que el llamante distinga previamente el `UsageSnapshotStatus`. `ParseSnapshot` es azúcar sintáctico que resuelve directamente el Acceptance Criteria de "un `UsageSnapshot` en cualquier estado distinto de `Success`... el consumidor recibe una indicación explícita de sin datos disponibles para ambas ventanas" sin que el futuro consumidor de F1 tenga que escribir `Parse(snapshot.Session, now)` / `Parse(snapshot.Weekly, now)` a mano cada vez. `now` como parámetro explícito (no `IClock`) — ya decidido por el documento de requisitos, aplicado literalmente. | Un único método que reciba directamente `UsageSnapshot` y devuelva una tupla, sin exponer el `Parse(RawRateLimitHeaders?, ...)` de una sola ventana — rechazada: los tests unitarios del Acceptance Criteria (0%, 100%, formato inesperado) se expresan de forma más directa y aislada contra una sola ventana (`RawRateLimitHeaders`) que contra un `UsageSnapshot` completo; exponer ambos niveles de granularidad no cuesta nada adicional y es estrictamente más flexible. |

## Modelo de Datos / Interfaces

### `RateLimitWindow` + `RateLimitWindowParser` (Domain)

```csharp
using System.Globalization;
using System.Text.RegularExpressions;

namespace ClaudeMeter.Domain.Usage;

/// <summary>
/// Datos de rate-limit ya calculados para una única ventana (sesión de 5h
/// o semanal de 7d): únicamente porcentaje consumido y minutos restantes
/// hasta el reset. No expone el <c>Reset</c> crudo ni ningún otro campo —
/// mantenido deliberadamente mínimo (Acceptance Criteria del Issue #4).
/// Cada propiedad es independientemente <c>null</c> cuando el dato
/// correspondiente no está disponible (cabecera ausente o con un formato
/// no interpretable), en vez de un valor por defecto que pudiera inducir
/// a error a quien lo consuma.
/// </summary>
/// <param name="PercentageUsed">
/// Porcentaje consumido en escala 0-100, calculado exclusivamente a
/// partir de <see cref="RawRateLimitHeaders.Utilization"/> (nunca de
/// <see cref="RawRateLimitHeaders.Remaining"/>, que se ignora por
/// completo); <c>null</c> si <c>Utilization</c> está ausente o no es
/// interpretable.
/// </param>
/// <param name="MinutesRemaining">
/// Minutos restantes hasta <see cref="RawRateLimitHeaders.Reset"/>,
/// calculados de forma determinista a partir de la fecha de referencia
/// pasada a <see cref="RateLimitWindowParser.Parse"/>; nunca negativo
/// (mínimo 0); <c>null</c> si <c>Reset</c> está ausente o no es
/// interpretable.
/// </param>
public sealed record RateLimitWindow(double? PercentageUsed, int? MinutesRemaining)
{
    /// <summary>
    /// Instancia compartida que representa "sin datos disponibles" para
    /// ambos campos — usada cuando no hay <see cref="RawRateLimitHeaders"/>
    /// de la que partir (p. ej. <see cref="UsageSnapshot"/> en un estado
    /// distinto de <see cref="UsageSnapshotStatus.Success"/>).
    /// </summary>
    public static readonly RateLimitWindow Unavailable = new(PercentageUsed: null, MinutesRemaining: null);
}

/// <summary>
/// Convierte <see cref="RawRateLimitHeaders"/> en <see cref="RateLimitWindow"/>.
/// Lógica pura y determinista: sin HTTP, sin E/S, sin leer el reloj del
/// sistema — la fecha de referencia ("ahora") se recibe siempre como
/// parámetro explícito.
/// </summary>
public static class RateLimitWindowParser
{
    // Exige exactamente dígitos + '.' opcional + dígitos + '%', sin
    // espacios internos ni externos — cualquier desviación (fracción sin
    // '%', separador decimal distinto, espacios extra, texto no numérico)
    // no matchea y se trata como "no disponible" (ver Decisiones Técnicas
    // Clave).
    private static readonly Regex UtilizationPattern =
        new(@"^(0|[1-9]\d*)(\.\d+)?%$", RegexOptions.Compiled);

    /// <summary>
    /// Parsea una única ventana. Si <paramref name="headers"/> es
    /// <c>null</c> (ventana no disponible en absoluto, p. ej.
    /// <see cref="UsageSnapshot"/> sin éxito), devuelve
    /// <see cref="RateLimitWindow.Unavailable"/> sin lanzar.
    /// </summary>
    public static RateLimitWindow Parse(RawRateLimitHeaders? headers, DateTimeOffset now)
    {
        if (headers is null)
        {
            return RateLimitWindow.Unavailable;
        }

        return new RateLimitWindow(
            PercentageUsed: TryParsePercentageUsed(headers.Utilization),
            MinutesRemaining: TryParseMinutesRemaining(headers.Reset, now));
    }

    /// <summary>
    /// Conveniencia: parsea ambas ventanas de un <see cref="UsageSnapshot"/>
    /// de una sola vez. Cuando <paramref name="snapshot"/> no está en
    /// estado <see cref="UsageSnapshotStatus.Success"/>, <c>Session</c> y
    /// <c>Weekly</c> ya son <c>null</c> por contrato de
    /// <see cref="UsageSnapshot"/>, por lo que ambas ventanas resultantes
    /// son <see cref="RateLimitWindow.Unavailable"/> sin ningún caso
    /// especial adicional aquí.
    /// </summary>
    public static (RateLimitWindow Session, RateLimitWindow Weekly) ParseSnapshot(
        UsageSnapshot snapshot, DateTimeOffset now) =>
        (Parse(snapshot.Session, now), Parse(snapshot.Weekly, now));

    private static double? TryParsePercentageUsed(string? utilization)
    {
        if (utilization is null || !UtilizationPattern.IsMatch(utilization))
        {
            return null;
        }

        var numericPart = utilization[..^1]; // quita el '%' final, ya validado por la regex
        if (!double.TryParse(numericPart, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
        {
            return null;
        }

        return value is >= 0 and <= 100 ? value : null;
    }

    private static int? TryParseMinutesRemaining(string? reset, DateTimeOffset now)
    {
        if (reset is null ||
            !DateTimeOffset.TryParse(reset, CultureInfo.InvariantCulture, DateTimeStyles.None, out var resetTime))
        {
            return null;
        }

        var remaining = resetTime - now;
        return remaining <= TimeSpan.Zero ? 0 : (int)Math.Ceiling(remaining.TotalMinutes);
    }
}
```

### Blindaje de pureza determinista: `BannedSymbols.txt` en `ClaudeMeter.Domain`

**Decisión confirmada por el usuario (dentro del alcance de este issue, no diferida a un issue separado)**: se añade el analizador `Microsoft.CodeAnalysis.BannedApiAnalyzers` al proyecto `ClaudeMeter.Domain`, con una lista de símbolos prohibidos que impide en tiempo de compilación cualquier lectura directa del reloj del sistema dentro de Domain — coherente con `CLAUDE.md` ("Roslyn analyzers enabled; warnings as errors in Release builds") y con la regla de este mismo issue de que la fecha de referencia solo puede llegar como parámetro explícito `DateTimeOffset now`.

**1. `src/ClaudeMeter.Domain/ClaudeMeter.Domain.csproj`** — añadir la referencia al analizador y registrar el fichero de símbolos prohibidos como `AdditionalFiles` (mecanismo estándar de `BannedApiAnalyzers`, que lee ese fichero en tiempo de análisis, no en tiempo de ejecución):

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <!-- Verificar en NuGet.org la última versión estable disponible en el
         momento de implementar este issue; 3.11.0 es la última conocida
         al escribir este diseño. -->
    <PackageReference Include="Microsoft.CodeAnalysis.BannedApiAnalyzers" Version="3.11.0">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <AdditionalFiles Include="BannedSymbols.txt" />
  </ItemGroup>

</Project>
```

**2. `src/ClaudeMeter.Domain/BannedSymbols.txt`** (fichero nuevo, formato estándar `<DocID>;<mensaje>` del analizador) — prohíbe `DateTime.Now`/`DateTime.UtcNow`/`DateTimeOffset.Now`/`DateTimeOffset.UtcNow` (los cuatro "equivalentes" de leer el reloj del sistema que la regla de este issue busca impedir dentro de Domain):

```text
P:System.DateTime.Now;Prohibido en ClaudeMeter.Domain: la hora de referencia debe recibirse siempre como parámetro explícito DateTimeOffset now (ver RateLimitWindowParser.Parse). Captúrala en el borde de la aplicación (Application/Desktop) y pásala como argumento.
P:System.DateTime.UtcNow;Prohibido en ClaudeMeter.Domain: la hora de referencia debe recibirse siempre como parámetro explícito DateTimeOffset now (ver RateLimitWindowParser.Parse). Captúrala en el borde de la aplicación (Application/Desktop) y pásala como argumento.
P:System.DateTimeOffset.Now;Prohibido en ClaudeMeter.Domain: la hora de referencia debe recibirse siempre como parámetro explícito DateTimeOffset now (ver RateLimitWindowParser.Parse). Captúrala en el borde de la aplicación (Application/Desktop) y pásala como argumento.
P:System.DateTimeOffset.UtcNow;Prohibido en ClaudeMeter.Domain: la hora de referencia debe recibirse siempre como parámetro explícito DateTimeOffset now (ver RateLimitWindowParser.Parse). Captúrala en el borde de la aplicación (Application/Desktop) y pásala como argumento.
```

**Efecto**: cualquier uso de estos cuatro símbolos dentro de `ClaudeMeter.Domain` produce el diagnóstico `RS0030` (banned API), que es un warning en `Debug` y un **error de compilación en `Release`**, gracias a `TreatWarningsAsErrors` ya activado condicionalmente en `Directory.Build.props`. El analizador solo se añade a `ClaudeMeter.Domain` — deliberadamente **no** a `ClaudeMeter.Application`/`Infrastructure`/`Desktop`, porque el futuro composition root de F1 sí necesita capturar `DateTimeOffset.UtcNow` legítimamente en el borde de la aplicación (ver "Por qué `AnthropicApiUsageDataSource` NO debe invocar este parser"); prohibirlo también ahí sería contraproducente.

**Alcance**: este fichero y el cambio de `.csproj` se implementan como parte de la misma tarea de desarrollo de este issue (`sdlc-development`), no como un issue/PR separado — así lo confirmó el usuario. Se añade como paso explícito en "Implementation Plan".

### Mapeo GIVEN-WHEN-THEN → comportamiento del parser

| Escenario del documento de requisitos | Entrada | Resultado |
|---|---|---|
| Sesión con `Utilization="10%"`, `Remaining="90%"`, `Reset` futuro | `Parse(new("allowed","10%","90%","2026-09-16T12:00:00Z"), now)` con `now` anterior al reset | `PercentageUsed = 10.0`, `MinutesRemaining` = minutos hasta el reset (`> 0`) |
| `Utilization="10%"` y `Remaining="80%"` (no complementarios) | igual que arriba, ignorando `Remaining` | `PercentageUsed = 10.0` (idéntico al caso anterior; `Remaining` nunca se lee) |
| `Utilization="0%"` | `Parse(new("allowed","0%",...,reset futuro), now)` | `PercentageUsed = 0.0`, `MinutesRemaining >= 0` |
| `Utilization="100%"`, `Reset` igual o anterior a `now` | `Parse(new("allowed","100%",...,reset pasado), now)` | `PercentageUsed = 100.0`, `MinutesRemaining = 0` (nunca negativo) |
| `Reset` ausente (`null`) | `Utilization` presente, `Reset: null` | `PercentageUsed` calculado normalmente, `MinutesRemaining = null` |
| `Utilization` ausente (`null`) | `Utilization: null`, `Reset` presente | `PercentageUsed = null`, `MinutesRemaining` calculado normalmente |
| `UsageSnapshot` en `TokenUnavailable`/`Unauthorized`/`RequestFailed` | `ParseSnapshot(UsageSnapshot.TokenUnavailable(), now)` (análogo para los otros dos) | `(RateLimitWindow.Unavailable, RateLimitWindow.Unavailable)` — sin excepción |
| `Utilization="0.1"` (fracción en vez de `%`) | `Parse(new(...,"0.1",...,...), now)` | `PercentageUsed = null` (no matchea la regex) |
| `Utilization="10,5%"` (separador decimal distinto) | ídem | `PercentageUsed = null` |
| `Utilization=" 10% "` (espacios extra) | ídem | `PercentageUsed = null` |
| `Utilization="abc%"` (no numérico) | ídem | `PercentageUsed = null` |
| Sesión completa + semanal con campos ausentes/no interpretables | `ParseSnapshot(UsageSnapshot.Success(sessionCompleta, weeklyIncompleta), now)` | `Session` con ambos campos calculados; `Weekly` con los campos afectados en `null` — sin que uno contamine al otro (cada `Parse` es una llamada independiente y sin estado compartido) |

## Estrategia de Testing

**Ubicación**: `test/ClaudeMeter.Domain.Tests/Usage/RateLimitWindowParserTests.cs` (el `.csproj` de `ClaudeMeter.Domain.Tests` ya referencia `ClaudeMeter.Domain`; no hace falta ningún cambio de referencias).

**Nada que mockear**: al ser lógica 100% pura, los tests son `[Fact]`/`[Theory]` directos sin fakes ni test doubles — a diferencia de `AnthropicApiUsageDataSourceTests` (que sí necesita `StubHttpMessageHandler`/`FakeTokenProvider`), aquí basta con construir `RawRateLimitHeaders`/`UsageSnapshot` a mano y un `DateTimeOffset` literal fijo para "ahora", cumpliendo directamente el último Acceptance Criteria del documento de requisitos ("xUnit puro, sin HTTP, sin E/S, sin reloj real").

**Casos cubiertos (mapeo 1:1 con los GIVEN-WHEN-THEN)**:

| Test | GIVEN-WHEN-THEN cubierto |
|---|---|
| `Parse_ConUtilizationYResetValidos_CalculaPercentageUsedYMinutesRemaining` | Caso feliz: `Utilization="10%"`, `Reset` futuro conocido, `now` fijo → `PercentageUsed=10.0` y `MinutesRemaining` exacto (valor determinista calculable a mano en el test) |
| `Parse_ConUtilizationYRemainingNoComplementarios_IgnoraRemainingPorCompleto` | `Utilization="10%"`, `Remaining="80%"` → `PercentageUsed=10.0`, sin ninguna referencia a `Remaining` en el resultado ni excepción por la discrepancia |
| `Parse_ConUtilizationCero_DevuelvePercentageUsedCeroSinNegativos` | `Utilization="0%"` → `PercentageUsed=0.0`, `MinutesRemaining>=0` |
| `Parse_ConUtilizacionCienYResetYaPasado_DevuelveMinutesRemainingCero` | `Utilization="100%"`, `Reset` anterior a `now` → `PercentageUsed=100.0`, `MinutesRemaining=0` (nunca negativo) |
| `Parse_ConResetAusente_DevuelveMinutesRemainingNullSinAfectarPercentageUsed` | `Reset: null`, `Utilization` presente → `MinutesRemaining=null`, `PercentageUsed` calculado con normalidad |
| `Parse_ConUtilizationAusente_DevuelvePercentageUsedNullSinAfectarMinutesRemaining` | `Utilization: null`, `Reset` presente → `PercentageUsed=null`, `MinutesRemaining` calculado con normalidad |
| `Parse_ConHeadersNull_DevuelveRateLimitWindowUnavailable` | `Parse(null, now)` → `RateLimitWindow.Unavailable`, sin excepción |
| `Parse_ConUtilizationEnFormatoInesperado_DevuelvePercentageUsedNull` (`[Theory]` con `InlineData`: `"0.1"`, `"10,5%"`, `" 10% "`, `"abc%"`, `"150%"`, `""`) | Los seis ejemplos de formato inesperado/fuera de rango del Acceptance Criteria → `PercentageUsed=null`, sin excepción no controlada |
| `ParseSnapshot_ConUsageSnapshotEnEstadoDeFallo_DevuelveAmbasVentanasUnavailable` (`[Theory]` sobre `TokenUnavailable`/`Unauthorized`/`RequestFailed`) | `UsageSnapshot` sin éxito → `(Unavailable, Unavailable)`, sin parsear nada |
| `ParseSnapshot_ConSessionCompletaYWeeklyConCamposAusentes_CalculaCadaVentanaDeFormaIndependiente` | `UsageSnapshot.Success(sessionCompleta, weeklyIncompleta)` → `Session` totalmente calculada, `Weekly` con los campos afectados en `null`, verificando explícitamente que `Session` no se ve alterada por el fallo de `Weekly` |

Estos 10 casos (con las variantes `[Theory]`) cubren íntegramente los ocho GIVEN-WHEN-THEN funcionales del documento de requisitos más el propio requisito de "test xUnit puro y determinista", y son suficientes por sí solos para que `ClaudeMeter.Domain` alcance con holgura el 70% de cobertura (`testingCoverage`) en este área — el código de producción a cubrir son ~35 líneas ejecutables sin ramas no alcanzables.

## Cross-Cutting Concerns

- **Seguridad**: no aplica ningún dato sensible — el parser no toca el token OAuth ni ningún secreto; solo transforma strings de cabeceras de rate-limit ya obtenidas (y ya cubiertas por la política de seguridad de Issue #3) en dos números.
- **Rendimiento**: coste despreciable — dos comprobaciones de regex/`TryParse` por ciclo de polling (60 s en F1); no hay asignación de memoria relevante ni estado compartido entre invocaciones.
- **Manejo de errores**: contrato "nunca excepción no controlada" garantizado estructuralmente — todas las conversiones usan las variantes `TryParse`/`IsMatch` (nunca `Parse`/lanzar-y-capturar), de forma que un formato inesperado se traduce en `null` por construcción, no por un `try/catch` alrededor de una excepción esperada. No hay ningún `catch` en este código porque no hay ninguna operación que pueda lanzar.
- **Retrocompatibilidad**: primer consumidor de `RawRateLimitHeaders`/`UsageSnapshot` desde que se introdujeron en Issue #3; no cambia la forma de ninguno de los dos tipos existentes, por lo que no hay riesgo de romper `AnthropicApiUsageDataSource` ni sus tests actuales.
- **Mantenibilidad / pureza determinista**: el riesgo "Maintainability" del documento de requisitos (tests intermitentes si `now` se filtra desde el reloj del sistema) se mitiga estructuralmente (`RateLimitWindowParser` no referencia `DateTime.Now`/`DateTime.UtcNow`/`DateTimeOffset.Now`/`DateTimeOffset.UtcNow` en ningún punto — `now` solo llega por parámetro) **y adicionalmente en tiempo de compilación**: este mismo issue añade `Microsoft.CodeAnalysis.BannedApiAnalyzers` + `BannedSymbols.txt` al proyecto `ClaudeMeter.Domain` (ver "Blindaje de pureza determinista" en "Modelo de Datos / Interfaces"), de forma que una futura modificación que reintroduzca sin querer una lectura directa del reloj falla la build en `Release` en vez de degradar silenciosamente en un test intermitente.
- **Observabilidad**: no aplica en F0 (Serilog llega en F2, igual que en Issues #2/#3); no se añade ninguna dependencia de logging en este issue. El propio tipo `RateLimitWindow` ya deja "no disponible" como un valor de primera clase (`null`), lo cual será suficiente para que F2 registre estos casos como una métrica/log si se decide entonces.

## Implementation Plan

1. Crear `src/ClaudeMeter.Domain/Usage/RateLimitWindow.cs` con el record `RateLimitWindow` (incluyendo `RateLimitWindow.Unavailable`) y la clase estática `RateLimitWindowParser` (`Parse`, `ParseSnapshot`, helpers privados `TryParsePercentageUsed`/`TryParseMinutesRemaining`), documentado con XMLDoc en español, según el diseño de la sección "Modelo de Datos / Interfaces".
2. Crear `test/ClaudeMeter.Domain.Tests/Usage/RateLimitWindowParserTests.cs` con los 10 casos (incluyendo las variantes `[Theory]`) descritos en "Estrategia de Testing".
3. Ejecutar `dotnet test` sobre la solución completa y comprobar que no se introducen warnings nuevos bajo `Release` (el `Directory.Build.props` ya trata warnings como errores en Release).
4. **(Dentro del alcance de este issue — confirmado por el usuario, no diferido)** Añadir `Microsoft.CodeAnalysis.BannedApiAnalyzers` como `PackageReference` en `src/ClaudeMeter.Domain/ClaudeMeter.Domain.csproj` y crear `src/ClaudeMeter.Domain/BannedSymbols.txt` prohibiendo `DateTime.Now`, `DateTime.UtcNow`, `DateTimeOffset.Now` y `DateTimeOffset.UtcNow`, exactamente según el snippet de la sección "Blindaje de pureza determinista: `BannedSymbols.txt` en `ClaudeMeter.Domain`". Verificar que `dotnet build -c Release` falla si se introduce deliberadamente una de estas llamadas dentro de `ClaudeMeter.Domain` (prueba de humo del propio analizador) y que, revertido ese cambio de prueba, la build vuelve a pasar limpia.
5. Actualizar, si se desea, el snippet de referencia del composition root documentado en `docs/sdlc/design/issue-03-llamada-http-headers-oauth.md` (o el que se cree en el diseño de F1) para reflejar que el futuro query handler de Application invoca `RateLimitWindowParser.ParseSnapshot(snapshot, DateTimeOffset.UtcNow)` — nota informativa, no una tarea de código de este issue.

## Risks & Open Decisions

**No quedan decisiones de diseño abiertas que requieran sign-off humano antes de pasar a desarrollo.** Las tres decisiones que esta fase había dejado planteadas para confirmación han sido revisadas y confirmadas explícitamente por el usuario:

1. **Convención de redondeo de `MinutesRemaining`** — **Confirmada: se mantiene `Math.Ceiling`, sin cambios de diseño.** El Acceptance Criteria no fija un valor exacto para minutos fraccionarios (solo fija los extremos 0%/100%), por lo que la elección original (sobreestimar el tiempo restante, más seguro ante el rate-limit que subestimarlo) queda como decisión definitiva de este documento — ver rationale completo en "Decisiones Técnicas Clave".

2. **Tratamiento de `Utilization` fuera de rango (p. ej. `"150%"`)** — **Confirmado: se trata como formato inválido.** El diseño ya descrito arriba implementa exactamente este comportamiento sin necesidad de ningún cambio de código: la regex `^(0|[1-9]\d*)(\.\d+)?%$` matchea sintácticamente `"150%"`, pero la comprobación de rango `value is >= 0 and <= 100 ? value : null` en `TryParsePercentageUsed` descarta cualquier valor sintácticamente válido fuera de `[0, 100]`, devolviendo `null` — es decir, `RateLimitWindowParser.Parse` produce un `PercentageUsed = null` para esa ventana (equivalente a `RateLimitWindow.Unavailable.PercentageUsed`), exactamente el mismo tratamiento que una cabecera ausente o malformada. El caso `"150%"` ya forma parte del `[Theory]`/`InlineData` de `Parse_ConUtilizationEnFormatoInesperado_DevuelvePercentageUsedNull` en "Estrategia de Testing", por lo que queda cubierto por un test explícito sin cambios adicionales al plan.

3. **Analizador Roslyn `BannedSymbols.txt`** — **Confirmado: SÍ, se añade ahora, dentro del alcance de este mismo issue.** Se especifica en detalle en la nueva sección "Blindaje de pureza determinista: `BannedSymbols.txt` en `ClaudeMeter.Domain`" (dentro de "Modelo de Datos / Interfaces"): `Microsoft.CodeAnalysis.BannedApiAnalyzers` referenciado desde `src/ClaudeMeter.Domain/ClaudeMeter.Domain.csproj`, con `src/ClaudeMeter.Domain/BannedSymbols.txt` prohibiendo `DateTime.Now`, `DateTime.UtcNow`, `DateTimeOffset.Now` y `DateTimeOffset.UtcNow` únicamente dentro de `ClaudeMeter.Domain`. Se implementa como parte de esta misma tarea de `sdlc-development` (paso 4 de "Implementation Plan"), no como un issue separado.

**Decisiones adicionales de esta fase de Diseño — todas resueltas, ninguna bloqueante**: forma exacta de `RateLimitWindow` (record posicional simple, sin patrón fábricas-estáticas), tipo de `PercentageUsed` (`double?`, escala 0-100), formato exacto aceptado para `Utilization` (regex estricta), estrategia de parseo de `Reset` (tolerante vía `DateTimeOffset.TryParse`, sin regex adicional), firma de la función de parseo (`Parse` + `ParseSnapshot`) y ubicación (`ClaudeMeter.Domain.Usage`, un solo fichero). Todas están documentadas con su rationale y alternativas rechazadas en "Decisiones Técnicas Clave".

**Riesgos heredados del documento de requisitos (no bloquean el diseño ni el desarrollo; son aceptados explícitamente, no items pendientes de decisión — el desarrollador debe tenerlos presentes)**:
- **Data quality**: el formato de `anthropic-ratelimit-unified-*` no está documentado oficialmente por Anthropic y puede cambiar sin aviso (heredado de Issue #3); un cambio de formato futuro (p. ej. de `%` a fracción decimal) sería absorbido por el tratamiento de "formato inesperado" ya implementado (resultado `null`, nunca una excepción), pero dejaría de calcularse el porcentaje real hasta que se actualice la regex — este riesgo no se elimina, solo se degrada de forma segura.
- **Data quality**: al no cruzar `Utilization` con `Remaining`, un `Utilization` corrupto mientras `Remaining` es correcto no se detecta ni se señala — riesgo ya aceptado explícitamente por la decisión del documento de requisitos, heredado sin cambios.
- **Impact**: sin este parser, F1 no podría mostrar minutos/porcentaje reales — mitigado por este mismo issue.

## Summary

Se crea `src/ClaudeMeter.Domain/Usage/RateLimitWindow.cs` con el value object `RateLimitWindow` (solo `PercentageUsed`/`MinutesRemaining`, nunca `Reset` crudo) y la función pura `RateLimitWindowParser.Parse`/`ParseSnapshot`, que convierte `RawRateLimitHeaders` (+ `DateTimeOffset now` explícito) en `RateLimitWindow`, tratando cualquier campo ausente o con formato inesperado como `null` en vez de lanzar o inventar un valor. Se documenta explícitamente por qué `AnthropicApiUsageDataSource` no debe (y no necesita) invocar este parser, dejando ese cableado para un futuro caso de uso de Application en F1. Ningún tipo existente (`UsageSnapshot`, `RawRateLimitHeaders`) se modifica. El alcance de este issue incluye también añadir `Microsoft.CodeAnalysis.BannedApiAnalyzers` + `BannedSymbols.txt` a `ClaudeMeter.Domain`, para que ninguna lectura directa del reloj del sistema pueda colarse en Domain sin romper la build en `Release`. No quedan decisiones de diseño abiertas: las tres cuestiones planteadas para confirmación (redondeo de `MinutesRemaining`, tratamiento de `Utilization` fuera de rango, adopción del analizador Roslyn) están confirmadas — ver "Risks & Open Decisions".

Próximo paso sugerido: `sdlc-development` con este documento (`docs/sdlc/design/issue-04-parsear-ratelimit-headers.md`) como entrada.
