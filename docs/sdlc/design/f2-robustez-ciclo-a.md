# Design: F2 — Robustez, Ciclo A

## Requirements Reference

`docs/sdlc/requirements/f2-robustez-ciclo-a.md` — cubre US-1..US-3 (issues
#10, #11, #12), tratadas como un único ciclo SDLC con cadena de dependencia
interna #10 → #11 → #12. Documento cerrado, sin "Open question" pendientes:
el modelo de 3 categorías de reintento y el tratamiento de
`TokenUnavailable`/`Unauthorized` ya vienen confirmados por el usuario. La
issue #13 (`config.json` + arrastre del widget) queda fuera de alcance.

## Architecture Overview

F2 no añade ninguna capa nueva ni rompe la separación reading/calling/
computing/rendering que ya exige `CLAUDE.md`. Toca 4 piezas existentes de
forma acotada y añade 4 piezas nuevas, todas en Infrastructure/Desktop
(ningún cambio en Application):

1. **`UsageSnapshot`/`UsageSnapshotStatus` (Domain, único cambio fuera de
   Infrastructure/Desktop)** — se añade un nuevo estado explícito
   `MalformedResponse` (categoría 3 del modelo de reintento: 2xx sin las
   cabeceras `anthropic-ratelimit-unified-*` esperadas), separándolo de
   `RequestFailed`, que a partir de ahora significa **únicamente** "fallo
   transitorio" (red, timeout, 5xx — categoría 1). Es el cambio de dominio
   mínimo necesario para que la política de reintento pueda decidir "¿es
   esto un fallo de red o un contrato roto?" sin inspeccionar ningún string
   de error ni excepción cruda.
2. **`AnthropicApiUsageDataSource` (Infrastructure, modificado)** — sigue
   siendo la única clase que "llama a la API": no gana ninguna
   responsabilidad de reintento. Sus únicos dos cambios son (a) devolver
   `UsageSnapshot.MalformedResponse()` en vez de `RequestFailed()` para el
   caso "2xx sin cabeceras", y (b) recibir un `ILogger<AnthropicApiUsageDataSource>`
   para registrar cada intento HTTP individual con el detalle que solo esta
   clase conoce (excepción de red, código de estado) — US-3.
3. **`RetryingUsageDataSource` (Infrastructure, nueva — decorator de
   `IUsageDataSource`)** — implementa la política de reintento con backoff
   de US-2 envolviendo cualquier `IUsageDataSource` (hoy
   `AnthropicApiUsageDataSource`, mañana un `BleUsageSink` de F7 si algún
   día también necesitara reintentos). Ver rationale de ubicación del
   backoff en Technology Choices — es la decisión que el documento de
   requisitos delega explícitamente a Design.
4. **`SerilogConfigurator` (Desktop, nuevo)** — construye el
   `Serilog.ILogger` con el sink de fichero en `%LOCALAPPDATA%\ClaudeMeter\logs`,
   con la ruta del directorio como parámetro inyectable para poder testear
   sin tocar el `%LOCALAPPDATA%` real de la máquina de CI (AC de US-3).
5. **`App.xaml.cs` (Desktop, modificado)** — composition root: inicializa
   Serilog, lo conecta a `Microsoft.Extensions.Logging` vía
   `AddLogging(...).AddSerilog(...)`, y compone la cadena de
   `IUsageDataSource` real: `AnthropicApiUsageDataSource` envuelto por
   `RetryingUsageDataSource`, que es lo único que se registra como
   `IUsageDataSource` — ni `UsagePollingCoordinator` ni `UsagePage.razor`
   saben que existe un decorator de por medio.
6. **`UsagePollingCoordinator` (Desktop, modificado)** — gana un
   `ILogger<UsagePollingCoordinator>` para el log de heartbeat de US-3 (una
   entrada `Information` por tick de 60s). No cambia su guarda `_isPolling`
   ni su temporización: como el backoff vive **dentro** de la llamada única
   que ya envuelve (`IUsageDataSource.GetUsageAsync()`), la guarda existente
   ya cubre correctamente toda la secuencia de reintentos sin ningún cambio
   estructural — ver Risks & Open Decisions sobre el acotamiento temporal.
7. **`UsagePage.razor` (Desktop, modificado) + `ReauthNotice.razor` (Desktop,
   nuevo)** — US-1: `UsagePage` ahora recuerda el último
   `UsageSnapshotStatus` recibido (no solo `IsSuccess`) y, cuando es
   `Unauthorized`, sustituye las dos `UsageBar` por un único componente
   `ReauthNotice` con mensaje y estilo distintos — nunca aplica el sufijo
   "(desactualizado)" a un 401/403, consistente con el AC 3 de US-1 ("un
   401/403 no es un dato desactualizado, es una credencial inválida").
8. **`UsagePollingLoop` (`ClaudeMeter.Console`, F0 — modificado, fuera de
   Desktop/Infrastructure pero comparte `UsageSnapshotStatus`)** — se añade
   el `case UsageSnapshotStatus.MalformedResponse` al `switch` existente
   (hoy un `switch` *statement*, no *expression*, por lo que añadir el
   nuevo miembro del enum no rompe la compilación, pero sin este `case` el
   estado quedaría silenciosamente sin renderizar en consola). Reutiliza
   `IUsagePollingRenderer.RenderRequestFailed` — no se añade ningún método
   de renderer nuevo para este caso, ver rationale en Implementation Plan.

Diagrama de dependencias resultante (piezas nuevas/modificadas resaltadas):

```
Domain.Usage
 └── UsageSnapshot.cs (modificado: + UsageSnapshotStatus.MalformedResponse)

Infrastructure.Usage
 ├── AnthropicApiUsageDataSource (modificado: ILogger, MalformedResponse)
 ├── RetryPolicyOptions (nuevo, record de configuración)
 └── RetryingUsageDataSource (nuevo, decorator de IUsageDataSource)
      └── envuelve -> AnthropicApiUsageDataSource

Desktop
 ├── Logging/SerilogConfigurator (nuevo) ──> Serilog, Serilog.Sinks.File
 ├── App.xaml.cs (modificado) ──> Log.Logger, AddLogging().AddSerilog(),
 │                                  registra RetryingUsageDataSource como
 │                                  único IUsageDataSource
 ├── Polling/UsagePollingCoordinator (modificado: + ILogger, heartbeat Info)
 └── Pages/UsagePage.razor (modificado: rastrea Status, no solo IsSuccess)
      ├── Pages/UsageBar.razor (sin cambios)
      └── Pages/ReauthNotice.razor (nuevo)

ClaudeMeter.Console (F0, fuera de alcance de F2 salvo este ajuste puntual)
 └── Polling/UsagePollingLoop.cs (modificado: + case MalformedResponse)
```

## Technology Choices

| Choice | Rationale | Alternative(s) considered |
|---|---|---|
| Backoff de US-2 como **decorator `RetryingUsageDataSource : IUsageDataSource`** en Infrastructure, en vez de dentro de `AnthropicApiUsageDataSource` o de `UsagePollingCoordinator` | Decisión explícitamente delegada a Design por el documento de requisitos. Un decorator preserva la regla de `CLAUDE.md` ("reading the token, calling the API, computing the countdown, and rendering are separate classes — don't collapse them") de la forma más literal: en vez de *hacer crecer* la responsabilidad de `AnthropicApiUsageDataSource` (que hoy es solo "una llamada HTTP → un `UsageSnapshot`", ya testeada así), se añade una clase nueva y pequeña cuya única responsabilidad es "decidir si reintentar un `IUsageDataSource` ya existente". Además encaja de forma natural con la guarda `_isPolling` de `UsagePollingCoordinator`: como el backoff ocurre *dentro* de la única llamada `await GetUsageAsync()` que la guarda ya serializa, no hace falta tocar `UsagePollingCoordinator` para nada relativo a solapes — la guarda ya cubre toda la secuencia de reintentos sin cambios. Es también el mismo patrón de sustituibilidad que ya exige `CLAUDE.md` para F7 (`BleUsageSink` debe encajar en Infrastructure sin tocar Domain/Application): un decorator de `IUsageDataSource` es "una fuente de datos más" desde el punto de vista del consumidor. | (1) Reintentar dentro de `AnthropicApiUsageDataSource` — rechazada: mezclaría "traducir una respuesta HTTP" con "orquestar reintentos", ensanchando una clase que hoy es deliberadamente estrecha y ya tiene su propia suite de tests centrada en el mapeo HTTP→`UsageSnapshot`. (2) Reintentar en `UsagePollingCoordinator` (reintentar `GetUsageAsync()` completo desde el orquestador de polling) — rechazada: mezclaría "temporizar cada 60s" con "reintentar con backoff", dos políticas temporales distintas en la misma clase, y complicaría razonar sobre cuánto tiempo puede tardar un tick frente a la guarda `_isPolling` (con el decorator, esa pregunta se responde mirando solo `RetryingUsageDataSource`). |
| Refinar `UsageSnapshotStatus` con un nuevo miembro `MalformedResponse`, separado de `RequestFailed` | El requisito ya anticipa esta necesidad ("Design probablemente necesite refinar `RequestFailed()`... para distinguir esta subcausa"). Con el modelo de 3 categorías, `RetryingUsageDataSource` necesita distinguir "reintentable" de "no reintentable, tipo bug" sin acoplarse a ningún string de error ni excepción cruda — la forma más limpia es que el propio estado de dominio lo diga explícitamente, igual que ya hace con `Unauthorized` frente a `TokenUnavailable`. Con este cambio, la política de reintento se reduce a una única condición trivial (`if (snapshot.Status == UsageSnapshotStatus.RequestFailed)`), sin ningún objeto "razón"/"causa" adicional que mantener sincronizado. | (1) Añadir un campo `string? FailureReason` a `UsageSnapshot` en vez de un nuevo miembro de enum — rechazada: reintroduce lo mismo que ya se rechazó para `Unauthorized`/`TokenUnavailable` en F0/F1 (estados explícitos de dominio en vez de strings sueltos), y obligaría a la política de reintento a hacer *string matching* en vez de comparar un enum. (2) Que `RetryingUsageDataSource` reciba directamente la excepción/código HTTP en vez de un `UsageSnapshot` — rechazada: rompería el contrato de `IUsageDataSource` (que ya modela "sin datos" sin lanzar, regla de sustituibilidad Liskov de `CLAUDE.md`) y acoplaría el decorator a detalles HTTP que no debería conocer. |
| Backoff hecho a mano (`Task.Delay` + multiplicador) en vez de Polly | La política es deliberadamente simple (una condición binaria "reintentable sí/no" + backoff exponencial con techo), ~30 líneas sin dependencias externas. Añadir Polly (`Polly.Core`/`Microsoft.Extensions.Http.Resilience`) para esto sería ceremonia sin beneficio real hoy, mismo criterio ya aplicado en F1 para rechazar MediatR prematuramente ("se reconsiderará cuando exista un caso de uso... que lo justifique"). Mantiene además el testeo trivial: la suite de tests inyecta una función de espera falsa (ver Data Model) sin necesidad de configurar ningún `Context`/`ResiliencePipeline` de Polly. | Polly (`Polly.Core` o `Microsoft.Extensions.Http.Resilience`) — descartada por prematura para una única política de reintento sobre una única fuente de datos; candidata real a reconsiderar en F5 (multi-cuenta, varias fuentes a la vez) si la lógica de resiliencia crece en complejidad real. |
| Serilog + `Serilog.Sinks.File`, inicializado en `App.xaml.cs` (`OnStartup`) y conectado a `Microsoft.Extensions.Logging` vía `Serilog.Extensions.Logging` (`AddLogging(b => b.AddSerilog(...))`) | Ya fijado como tecnología por `CLAUDE.md` ("structured logging (Serilog) to `%LOCALAPPDATA%`") y por el propio issue #12. El puente a `Microsoft.Extensions.Logging` permite que `AnthropicApiUsageDataSource`/`RetryingUsageDataSource`/`UsagePollingCoordinator` reciban `ILogger<T>` por constructor sin que Infrastructure dependa de Serilog concreto — solo de `Microsoft.Extensions.Logging.Abstractions` (paquete ligero, ya en el ecosistema estándar de .NET), preservando Clean Architecture: **solo** Desktop (composition root) referencia Serilog/el sink de fichero; Infrastructure solo conoce la abstracción `ILogger<T>`. Los 5 paquetes nuevos que este diseño fija por nombre son: `Serilog` (núcleo), `Serilog.Sinks.File` (sink de fichero, Desktop), `Serilog.Extensions.Logging` (puente a `Microsoft.Extensions.Logging`, Desktop), `Microsoft.Extensions.Logging` (para `AddLogging`, Desktop) y `Microsoft.Extensions.Logging.Abstractions` (para `ILogger<T>`, Infrastructure). **Este documento fija los nombres de los 5 paquetes pero no sus números de versión exactos** (decisión confirmada por el usuario, ver Risks & Open Decisions): Development debe confirmar en `api.nuget.org` el último patch estable de la serie `8.0.x` disponible en el momento de implementar para cada uno — la serie 8.x se elige explícitamente por ser la más reciente compatible con el TFM `net8.0`(-`windows`) ya fijado por `Directory.Build.props` (las series 9.x/10.x de algunos de estos paquetes, como `Serilog.Extensions.Logging`, siguen el ciclo de versión de .NET 9/10 y no son compatibles), mismo criterio ya aplicado en el documento de diseño de F1 para `Microsoft.AspNetCore.Components.WebView.Wpf`. | (1) `Microsoft.Extensions.Logging` con un `ConsoleLoggerProvider`/`DebugLoggerProvider` en vez de Serilog — rechazada porque `CLAUDE.md` ya fija Serilog explícitamente, no es una decisión abierta. (2) `Serilog.Extensions.Hosting` (`UseSerilog()` sobre un `IHostBuilder`) — rechazada: el composition root de F1 ya decidió deliberadamente no introducir el Generic Host (`Host.CreateDefaultBuilder`), construyendo `IServiceCollection`/`IServiceProvider` a mano; `Serilog.Extensions.Logging` da el mismo resultado (`ILogger<T>` inyectable) sin forzar esa migración fuera de alcance de F2. |
| `SerilogConfigurator.CreateLogger(string? logDirectory = null)` como fábrica pura/testeable, separada de la asignación al `Log.Logger` estático global | AC explícito de US-3: "la configuración de Serilog es verificable sin escribir logs reales en el `%LOCALAPPDATA%` de la máquina de CI... mediante un sink en memoria o un directorio temporal inyectado". Al devolver un `Serilog.ILogger` normal (no tocar el estático `Log.Logger`), un test puede construirlo apuntando a un directorio temporal, escribir, volcar (`Dispose`) y comprobar el fichero, sin contaminar estado global entre tests (paralelizables). Solo `App.xaml.cs.OnStartup` hace `Log.Logger = SerilogConfigurator.CreateLogger();`, el único punto no testeable por diseño (igual que `MainWindow`/`OnStartup` ya son intocables por los agentes de este pipeline, ver Definition of Done). | Usar directamente un sink en memoria de terceros (`Serilog.Sinks.InMemory`) para el test — rechazada: añade una dependencia de test solo para verificar que el `WriteTo.File(...)` apunta al directorio correcto; un directorio temporal real + `Serilog.Sinks.File` (ya necesario en producción) verifica exactamente lo mismo con menos superficie. |
| `ReauthNotice.razor` como componente nuevo que **sustituye** ambas `UsageBar` (no las oscurece ni añade un texto adicional junto a ellas) cuando `Status == Unauthorized` | El "so that" del propio US-1 es explícito: "sepa que tengo que volver a autenticarme... **en lugar de ver datos rancios** o el widget colgado". Mostrar las barras (aunque sea con datos previos) junto al aviso contradice ese objetivo — un 401/403 no es "dato desactualizado", es una credencial inválida, y el AC 3 ya prohíbe expresamente el sufijo "(desactualizado)" para este caso. Sustituir por un único componente centrado, visualmente distinto (icono + texto, paleta de alerta ya usada para el umbral `red`), es la forma más inequívoca de cumplir "visualmente y textualmente distinto" sin introducir un tercer color de umbral nuevo. Extraerlo como componente propio (en vez de un `@if` inline en `UsagePage.razor`) sigue el mismo patrón ya usado para `UsageBar` en F1 (aislar responsabilidad de render + testeable con bUnit en aislamiento). | (1) Añadir un parámetro `Unauthorized` a `UsageBar.razor` y que cada barra muestre su propio texto de reautenticación — rechazada: duplicaría el mensaje dos veces (una por barra) y mezclaría en un mismo componente dos semánticas distintas (umbral de consumo vs. estado de autenticación). (2) Mantener las barras visibles con un banner superpuesto — rechazada por la razón de producto ya explicada (el usuario no debe ver "datos rancios" en este caso concreto, a diferencia de `RequestFailed`/`TokenUnavailable` tras un éxito previo, donde sí se acepta mostrarlos marcados "(desactualizado)"). |

## Data Model / Interfaces

### `UsageSnapshot.cs` (Domain, modificado)

```csharp
// src/ClaudeMeter.Domain/Usage/UsageSnapshot.cs
public enum UsageSnapshotStatus
{
    Success,
    TokenUnavailable,
    Unauthorized,

    /// <summary>
    /// La petición falló por un motivo transitorio: error de red, timeout,
    /// o código 5xx. Categoría 1 del modelo de reintento de US-2 — es el
    /// único estado que <c>RetryingUsageDataSource</c> reintenta con
    /// backoff. Ya NO incluye el caso "2xx sin cabeceras" (ver
    /// <see cref="MalformedResponse"/>).
    /// </summary>
    RequestFailed,

    /// <summary>
    /// La API respondió 2xx pero sin las cabeceras
    /// <c>anthropic-ratelimit-unified-*</c> mínimas esperadas: un contrato
    /// de API roto o persistente, no un fallo de red. Categoría 3 del
    /// modelo de reintento de US-2 — <c>RetryingUsageDataSource</c> nunca
    /// reintenta este estado (reintentar no lo arregla, solo retrasa
    /// mostrar el problema).
    /// </summary>
    MalformedResponse
}
// RawRateLimitHeaders sin cambios.

public sealed record UsageSnapshot
{
    // ... Status/Session/Weekly/IsSuccess sin cambios ...

    /// <summary>Crea un resultado de "respuesta 2xx con contrato roto" (categoría 3, ver <see cref="UsageSnapshotStatus.MalformedResponse"/>).</summary>
    public static UsageSnapshot MalformedResponse() =>
        new(UsageSnapshotStatus.MalformedResponse, session: null, weekly: null);

    // Success()/TokenUnavailable()/Unauthorized()/RequestFailed() sin cambios de firma;
    // solo cambia el XMLDoc de RequestFailed() para reflejar el alcance reducido.
}
```

### `AnthropicApiUsageDataSource.cs` (Infrastructure, modificado — excerpt)

```csharp
public sealed class AnthropicApiUsageDataSource : IUsageDataSource
{
    private readonly ITokenProvider _tokenProvider;
    private readonly HttpClient _httpClient;
    private readonly ILogger<AnthropicApiUsageDataSource> _logger; // NUEVO

    public AnthropicApiUsageDataSource(
        ITokenProvider tokenProvider, HttpClient httpClient, ILogger<AnthropicApiUsageDataSource> logger)
    {
        _tokenProvider = tokenProvider;
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<UsageSnapshot> GetUsageAsync(CancellationToken cancellationToken = default)
    {
        var tokenResult = await _tokenProvider.GetTokenAsync(cancellationToken);
        if (!tokenResult.IsSuccess)
        {
            // Warning, no Error (decisión confirmada por el usuario, ver
            // Risks & Open Decisions): es un estado esperable antes del
            // primer login (aún no hay token), no necesariamente un fallo
            // real — a diferencia de Unauthorized (token presente pero
            // rechazado por la API), que sí se registra como Error.
            _logger.LogWarning("No se pudo obtener un token OAuth utilizable; no se realiza ninguna llamada HTTP");
            return UsageSnapshot.TokenUnavailable();
        }

        using var request = BuildRequest(tokenResult.AccessToken!);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Fallo de red llamando a la API de Anthropic");
            return UsageSnapshot.RequestFailed();
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Timeout llamando a la API de Anthropic");
            return UsageSnapshot.RequestFailed();
        }

        using (response)
        {
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                _logger.LogError("La API de Anthropic devolvió {StatusCode}: token rechazado, no se reintenta", (int)response.StatusCode);
                return UsageSnapshot.Unauthorized();
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("La API de Anthropic devolvió {StatusCode}", (int)response.StatusCode);
                return UsageSnapshot.RequestFailed();
            }

            var session = ReadWindow(response.Headers, "5h");
            var weekly = ReadWindow(response.Headers, "7d");

            if (session.Status is null || weekly.Status is null)
            {
                _logger.LogError("Respuesta 2xx sin las cabeceras anthropic-ratelimit-unified-* esperadas");
                return UsageSnapshot.MalformedResponse(); // CAMBIO: antes RequestFailed()
            }

            _logger.LogDebug("Snapshot de uso obtenido correctamente");
            return UsageSnapshot.Success(session, weekly);
        }
    }
    // BuildRequest/ReadWindow/GetHeaderValue/DTOs sin cambios.
}
```

### `RetryPolicyOptions.cs` (Infrastructure, nuevo)

```csharp
// src/ClaudeMeter.Infrastructure/Usage/RetryPolicyOptions.cs
namespace ClaudeMeter.Infrastructure.Usage;

/// <summary>
/// Parámetros de la política de reintento de <see cref="RetryingUsageDataSource"/>.
/// Ni el número de intentos ni el backoff vienen especificados en el issue
/// original (US-2) — <see cref="Default"/> son los valores que este
/// documento de diseño fija; no son configurables desde <c>config.json</c>
/// en este ciclo (issue #13, fuera de alcance).
/// </summary>
/// <param name="MaxAttempts">Intentos totales, incluido el primero (no reintentos adicionales). Mínimo 1.</param>
/// <param name="InitialDelay">Espera antes del primer reintento.</param>
/// <param name="BackoffMultiplier">Factor multiplicador aplicado tras cada reintento fallido (backoff exponencial).</param>
/// <param name="MaxDelay">Techo de espera entre reintentos, para acotar el tiempo total de la secuencia.</param>
public sealed record RetryPolicyOptions(int MaxAttempts, TimeSpan InitialDelay, double BackoffMultiplier, TimeSpan MaxDelay)
{
    /// <summary>
    /// 3 intentos totales (1 + 2 reintentos), 2s/4s de espera (backoff x2,
    /// techo 10s nunca alcanzado con estos valores). Peor caso ~6s de
    /// espera total además del tiempo de las 3 llamadas HTTP — muy por
    /// debajo del intervalo de poll de 60s. Valores confirmados por el
    /// usuario (ver Risks & Open Decisions), no una estimación pendiente
    /// de validar.
    /// </summary>
    public static readonly RetryPolicyOptions Default =
        new(MaxAttempts: 3, InitialDelay: TimeSpan.FromSeconds(2), BackoffMultiplier: 2.0, MaxDelay: TimeSpan.FromSeconds(10));
}
```

### `RetryingUsageDataSource.cs` (Infrastructure, nuevo)

```csharp
// src/ClaudeMeter.Infrastructure/Usage/RetryingUsageDataSource.cs
namespace ClaudeMeter.Infrastructure.Usage;

/// <summary>
/// Decorator de <see cref="IUsageDataSource"/> que aplica el modelo de 3
/// categorías de reintento de US-2 sobre cualquier fuente envuelta (hoy
/// <see cref="AnthropicApiUsageDataSource"/>): únicamente
/// <see cref="UsageSnapshotStatus.RequestFailed"/> (fallo transitorio) se
/// reintenta con backoff exponencial; <c>Success</c>,
/// <c>TokenUnavailable</c>, <c>Unauthorized</c> y <c>MalformedResponse</c>
/// se devuelven inmediatamente, sin ningún reintento — las dos últimas
/// categorías por regla explícita de <c>CLAUDE.md</c>/US-1 (nunca
/// reintentar ni refrescar el token) y de US-2 (un contrato roto no se
/// arregla reintentando).
/// </summary>
public sealed class RetryingUsageDataSource : IUsageDataSource
{
    private readonly IUsageDataSource _inner;
    private readonly RetryPolicyOptions _options;
    private readonly ILogger<RetryingUsageDataSource> _logger;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;

    public RetryingUsageDataSource(IUsageDataSource inner, RetryPolicyOptions options, ILogger<RetryingUsageDataSource> logger)
        : this(inner, options, logger, Task.Delay)
    {
    }

    /// <summary>
    /// Constructor <c>internal</c> con la función de espera inyectable,
    /// visible vía <c>InternalsVisibleTo</c> hacia
    /// <c>ClaudeMeter.Infrastructure.Tests</c> (mismo patrón ya usado para
    /// otros colaboradores testeables de este repo): permite a los tests
    /// simular varios reintentos sin esperar segundos reales.
    /// </summary>
    internal RetryingUsageDataSource(
        IUsageDataSource inner, RetryPolicyOptions options, ILogger<RetryingUsageDataSource> logger,
        Func<TimeSpan, CancellationToken, Task> delay)
    {
        _inner = inner;
        _options = options;
        _logger = logger;
        _delay = delay;
    }

    public async Task<UsageSnapshot> GetUsageAsync(CancellationToken cancellationToken = default)
    {
        var attempt = 1;
        var delay = _options.InitialDelay;

        while (true)
        {
            var snapshot = await _inner.GetUsageAsync(cancellationToken);

            // Categorías 2 y 3 (Success incluido): nunca se reintentan.
            if (snapshot.Status != UsageSnapshotStatus.RequestFailed)
            {
                return snapshot;
            }

            // Categoría 1 (RequestFailed = solo fallo transitorio tras el
            // refinamiento de Domain): reintentar hasta agotar MaxAttempts.
            if (attempt >= _options.MaxAttempts)
            {
                _logger.LogError(
                    "Se agotaron los {MaxAttempts} intentos tras fallos transitorios; se propaga el fallo", _options.MaxAttempts);
                return snapshot;
            }

            _logger.LogWarning(
                "Fallo transitorio (intento {Attempt}/{MaxAttempts}); reintentando en {DelaySeconds:0.#}s",
                attempt, _options.MaxAttempts, delay.TotalSeconds);

            await _delay(delay, cancellationToken);

            attempt++;
            var nextDelayTicks = (long)(delay.Ticks * _options.BackoffMultiplier);
            delay = TimeSpan.FromTicks(Math.Min(nextDelayTicks, _options.MaxDelay.Ticks));
        }
    }
}
```

### `App.xaml.cs` (Desktop, modificado)

```csharp
using System.Net.Http;
using System.Windows;
using ClaudeMeter.Desktop.Logging;
using ClaudeMeter.Infrastructure.Authentication;
using ClaudeMeter.Infrastructure.Usage;
using Microsoft.AspNetCore.Components.WebView.Wpf;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace ClaudeMeter.Desktop;

public partial class App : System.Windows.Application
{
    private HttpClient? _httpClient;

    public IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        Log.Logger = SerilogConfigurator.CreateLogger(); // US-3
        Log.Information("ClaudeMeter iniciado");

        _httpClient = new HttpClient();

        var services = new ServiceCollection();
        services.AddWpfBlazorWebView();
        services.AddLogging(builder => builder.AddSerilog(Log.Logger, dispose: false)); // US-3
        services.AddSingleton(_httpClient);
        services.AddSingleton<ITokenProvider, CredentialsFileTokenProvider>();

        // AnthropicApiUsageDataSource se registra como tipo concreto (no
        // como IUsageDataSource) porque es RetryingUsageDataSource, no ella,
        // quien se expone como el IUsageDataSource real de la aplicación —
        // ver rationale del decorator en Technology Choices (US-2).
        services.AddSingleton<AnthropicApiUsageDataSource>();
        services.AddSingleton<IUsageDataSource>(sp => new RetryingUsageDataSource(
            sp.GetRequiredService<AnthropicApiUsageDataSource>(),
            RetryPolicyOptions.Default,
            sp.GetRequiredService<ILogger<RetryingUsageDataSource>>()));

        Services = services.BuildServiceProvider();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _httpClient?.Dispose();
        Log.CloseAndFlush(); // US-3: garantiza el volcado del sink de fichero
        base.OnExit(e);
    }
}
```

### `Logging/SerilogConfigurator.cs` (Desktop, nuevo)

```csharp
// src/ClaudeMeter.Desktop/Logging/SerilogConfigurator.cs
namespace ClaudeMeter.Desktop.Logging;

/// <summary>
/// Construye el <see cref="Serilog.ILogger"/> de la aplicación. Separada de
/// <c>App.xaml.cs</c> (que es quien la asigna a <c>Log.Logger</c>) para
/// poder testear la configuración con un directorio inyectado, sin escribir
/// en el <c>%LOCALAPPDATA%</c> real de la máquina de CI (AC de US-3).
/// </summary>
public static class SerilogConfigurator
{
    private const string LogFileNameTemplate = "claudemeter-.log";
    private const int RetainedFileCountLimit = 7; // ~1 semana de logs, mitiga el riesgo de I/O sin límite (US-3)

    public static Serilog.ILogger CreateLogger(string? logDirectory = null)
    {
        var directory = logDirectory ?? DefaultLogDirectory();
        Directory.CreateDirectory(directory);

        return new LoggerConfiguration()
            .MinimumLevel.Information()
            .Enrich.FromLogContext()
            .WriteTo.File(
                Path.Combine(directory, LogFileNameTemplate),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: RetainedFileCountLimit,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }

    /// <summary><c>%LOCALAPPDATA%\ClaudeMeter\logs</c> — ruta sugerida por el issue #12, adoptada tal cual.</summary>
    public static string DefaultLogDirectory() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClaudeMeter", "logs");
}
```

### `UsagePollingCoordinator.cs` (Desktop, modificado — excerpt)

```csharp
public sealed class UsagePollingCoordinator : IDisposable
{
    private readonly IUsageDataSource _usageDataSource;
    private readonly ILogger<UsagePollingCoordinator> _logger; // NUEVO
    // ... _timer/_isPolling/_disposed sin cambios ...

    public UsagePollingCoordinator(
        IUsageDataSource usageDataSource, TimeSpan interval, ILogger<UsagePollingCoordinator> logger)
    {
        _usageDataSource = usageDataSource;
        _logger = logger;
        _timer = new System.Timers.Timer(interval.TotalMilliseconds) { AutoReset = true };
        _timer.Elapsed += OnTimerElapsed;
    }

    public void Start()
    {
        _logger.LogInformation("Polling de uso iniciado (intervalo {IntervalSeconds}s)", _timer.Interval / 1000);
        _ = PollAsync();
        _timer.Start();
    }

    private async Task PollAsync()
    {
        if (_isPolling) return;

        _isPolling = true;
        try
        {
            var now = DateTimeOffset.UtcNow;
            var snapshot = await _usageDataSource.GetUsageAsync();
            _logger.LogInformation("Poll completado: Status={Status}", snapshot.Status); // NUEVO: heartbeat de US-3
            SnapshotReceived?.Invoke(snapshot, now);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Excepción no controlada durante un ciclo de poll"); // NUEVO
        }
        finally
        {
            _isPolling = false;
        }
    }
    // Dispose()/PollOnceForTestsAsync/IsRunningForTests sin cambios.
}
```

### `UsagePage.razor` (Desktop, modificado — excerpt)

```razor
@implements IDisposable
@inject IUsageDataSource UsageDataSource
@inject ILogger<UsagePollingCoordinator> Logger

@if (_status == UsageSnapshotStatus.Unauthorized)
{
    <ReauthNotice />
}
else
{
    <div class="usage-widget">
        <UsageBar Title="Sesión" Window="@_session" Stale="@_isStale" />
        <UsageBar Title="Semana" Window="@_weekly" Stale="@_isStale" />
    </div>
}

@code {
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(60);

    private UsagePollingCoordinator? _coordinator;
    private RateLimitWindow _session = RateLimitWindow.Unavailable;
    private RateLimitWindow _weekly = RateLimitWindow.Unavailable;
    private UsageSnapshotStatus? _status; // NUEVO: US-1 necesita el estado completo, no solo IsSuccess
    private bool _hasEverSucceeded;
    private bool _isStale;

    protected override void OnInitialized()
    {
        _coordinator = new UsagePollingCoordinator(UsageDataSource, RefreshInterval, Logger);
        _coordinator.SnapshotReceived += OnSnapshotReceived;
        _coordinator.Start();
    }

    // OnSnapshotReceived sin cambios (sigue envolviendo Apply+StateHasChanged en InvokeAsync con el mismo try/catch defensivo de F1).

    internal void ApplyForTests(UsageSnapshot snapshot, DateTimeOffset now) => Apply(snapshot, now);
    internal bool IsPollingActiveForTests => _coordinator?.IsRunningForTests ?? false;
    internal UsageSnapshotStatus? StatusForTests => _status; // NUEVO, para bUnit

    private void Apply(UsageSnapshot snapshot, DateTimeOffset now)
    {
        _status = snapshot.Status; // NUEVO

        var (session, weekly) = RateLimitWindowParser.ParseSnapshot(snapshot, now);

        if (snapshot.IsSuccess)
        {
            _session = session;
            _weekly = weekly;
            _isStale = false;
            _hasEverSucceeded = true;
        }
        else if (snapshot.Status == UsageSnapshotStatus.Unauthorized)
        {
            // AC 3 de US-1: nunca "(desactualizado)" — es una credencial
            // inválida, no un dato viejo. El bloque @if de arriba ya
            // sustituye las barras por ReauthNotice; no hace falta tocar
            // _session/_weekly aquí.
            _isStale = false;
        }
        else if (_hasEverSucceeded)
        {
            _isStale = true;
        }
        else
        {
            _session = session;
            _weekly = weekly;
        }
    }

    public void Dispose() { /* sin cambios */ }
}
```

### `Pages/ReauthNotice.razor` (Desktop, nuevo)

```razor
@* Mensaje explícito de "hay que volver a autenticarse" (US-1), visual y
   textualmente distinto de "No disponible" (UsageBar) y sin el sufijo
   "(desactualizado)" que sí aplica a un RequestFailed/TokenUnavailable
   tras un éxito previo. *@

<div class="usage-reauth">
    <span class="usage-reauth__icon" aria-hidden="true">⚠</span>
    <span class="usage-reauth__message">
        Vuelve a iniciar sesión en Claude Code para seguir viendo tu consumo.
    </span>
</div>
```

### `wwwroot/css/app.css` (Desktop, adición)

```css
/* US-1: aviso de reautenticación — reutiliza el rojo ya usado para el
   umbral crítico (.usage-bar--red), consistente con "credencial inválida"
   como el estado más severo del widget. */
.usage-reauth {
    height: 100%;
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    gap: 8px;
    text-align: center;
    padding: 12px 16px;
    box-sizing: border-box;
}

.usage-reauth__icon {
    font-size: 22px;
    color: #f85149;
}

.usage-reauth__message {
    font-size: 13px;
    color: #e8e8e8;
    line-height: 1.4;
}
```

### `UsagePollingLoop.cs` (`ClaudeMeter.Console`, F0 — adición puntual)

```csharp
private void Render(DateTimeOffset now, UsageSnapshot snapshot)
{
    switch (snapshot.Status)
    {
        case UsageSnapshotStatus.Success: /* sin cambios */ break;
        case UsageSnapshotStatus.TokenUnavailable: _renderer.RenderTokenUnavailable(now); break;
        case UsageSnapshotStatus.Unauthorized: _renderer.RenderUnauthorized(now); break;
        case UsageSnapshotStatus.RequestFailed: _renderer.RenderRequestFailed(now); break;
        case UsageSnapshotStatus.MalformedResponse: _renderer.RenderRequestFailed(now); break; // NUEVO
    }
}
```

No se añade ningún método `RenderMalformedResponse` nuevo a `IUsagePollingRenderer`: desde la perspectiva de la consola (F0, no forma parte del alcance funcional de este ciclo), un contrato de API roto y un fallo de red se muestran igual ("no se pudieron obtener datos ahora mismo") — la distinción fina solo importa para decidir si reintentar (US-2, Infrastructure) y para el log (US-3), no para el texto de consola.

## Implementation Plan

1. **Domain (US-2, base para lo demás):** modificar
   `src/ClaudeMeter.Domain/Usage/UsageSnapshot.cs` añadiendo
   `UsageSnapshotStatus.MalformedResponse` + `UsageSnapshot.MalformedResponse()`,
   y actualizar el XMLDoc de `RequestFailed`/`RequestFailed()` para reflejar
   su alcance reducido (solo red/timeout/5xx).
2. **Tests de Domain:** actualizar
   `test/ClaudeMeter.Domain.Tests/Usage/UsageSnapshotTests.cs` con el nuevo
   factory method (estado, `IsSuccess == false`, `Session`/`Weekly` nulos).
3. **`ClaudeMeter.Console` (consumidor existente de `UsageSnapshotStatus`):**
   añadir el `case UsageSnapshotStatus.MalformedResponse` en
   `src/ClaudeMeter.Console/Polling/UsagePollingLoop.cs` (ver Data Model).
   Revisar `test/ClaudeMeter.Console.Tests/Polling/UsagePollingLoopTests.cs`
   y su `FakeUsageDataSource` por si hace falta un caso de test nuevo para
   no perder cobertura de este `switch`.
4. **`AnthropicApiUsageDataSource` (US-2 base + US-3):** añadir el parámetro
   `ILogger<AnthropicApiUsageDataSource>` al constructor; cambiar el caso
   "2xx sin cabeceras" para devolver `UsageSnapshot.MalformedResponse()`
   (antes `RequestFailed()`); añadir las llamadas a `_logger` descritas en
   Data Model (incluyendo `TokenUnavailable` como `Warning`, no `Error`).
   Añadir el `PackageReference` de `Microsoft.Extensions.Logging.Abstractions`
   al `.csproj` de Infrastructure, fijando el último patch estable de la
   serie 8.0.x disponible en `api.nuget.org` en el momento de implementar
   (nombre de paquete decidido en este documento, versión exacta no —
   decisión confirmada por el usuario, ver Risks & Open Decisions).
5. **Tests de `AnthropicApiUsageDataSource`:** actualizar
   `test/ClaudeMeter.Infrastructure.Tests/Usage/AnthropicApiUsageDataSourceTests.cs`
   — (a) pasar `NullLogger<AnthropicApiUsageDataSource>.Instance` (paquete
   `Microsoft.Extensions.Logging.Abstractions`, ya añadido en el paso
   anterior) al constructor en cada test existente; (b) cambiar el test que
   hoy espera `RequestFailed()` para el caso "2xx sin cabeceras" para que
   ahora espere `MalformedResponse()` — es un cambio de comportamiento
   observable, no solo de firma, revisar con cuidado para no dejar una
   regresión silenciosa.
6. **`RetryPolicyOptions`/`RetryingUsageDataSource` (US-2, pieza nueva
   central):** crear ambos ficheros en
   `src/ClaudeMeter.Infrastructure/Usage/` tal cual la sección Data Model.
7. **Tests de `RetryingUsageDataSource` (US-2, xUnit puro, sin
   `HttpMessageHandler`):** crear
   `test/ClaudeMeter.Infrastructure.Tests/Usage/FakeUsageDataSource.cs`
   (test double con una cola de `UsageSnapshot` a devolver en orden, y
   `CallCount`, mismo espíritu que `StubHttpMessageHandler` ya existente en
   este mismo proyecto de tests) y
   `test/ClaudeMeter.Infrastructure.Tests/Usage/RetryingUsageDataSourceTests.cs`
   usando el constructor `internal` con una función de espera falsa
   (`(delay, ct) => Task.CompletedTask`, capturando los `TimeSpan` pasados
   para poder aserir sobre el backoff sin esperas reales). Casos mínimos
   (mapean 1:1 con los AC de US-2): secuencia de `RequestFailed` seguida de
   `Success` dentro de `MaxAttempts` → resultado final `Success`,
   `CallCount` esperado; `MaxAttempts` fallos consecutivos → se propaga el
   último `RequestFailed()`, sin más llamadas; `Unauthorized` /
   `TokenUnavailable` / `MalformedResponse` → nunca se reintenta
   (`CallCount == 1` en cada caso); delays pasados a la función de espera
   siguen la progresión exponencial esperada (2s, 4s con los valores por
   defecto) sin superar `MaxDelay`.
8. **`Logging/SerilogConfigurator.cs` (US-3):** crear en
   `src/ClaudeMeter.Desktop/Logging/`. Añadir `PackageReference` de
   `Serilog`, `Serilog.Sinks.File`, `Serilog.Extensions.Logging` y
   `Microsoft.Extensions.Logging` al `.csproj` de Desktop, fijando en cada
   uno el último patch estable de la serie 8.0.x disponible en
   `api.nuget.org` en el momento de implementar (mismos nombres de paquete
   ya decididos en Technology Choices; las versiones exactas se dejan
   deliberadamente sin fijar en este documento — decisión confirmada por el
   usuario, ver Risks & Open Decisions).
9. **Test de `SerilogConfigurator` (US-3):** crear
   `test/ClaudeMeter.Desktop.Tests/Logging/SerilogConfiguratorTests.cs`
   (xUnit puro, no necesita bUnit): construir el logger con
   `Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())` como
   directorio, escribir una entrada, hacer `Dispose()` del logger devuelto
   (fuerza el flush del sink de fichero) y comprobar que aparece un fichero
   `claudemeter-*.log` con contenido no vacío bajo ese directorio temporal;
   limpiar el directorio en un `finally`. Nunca toca `%LOCALAPPDATA%` real
   ni el `Log.Logger` estático global (AC explícito de US-3).
10. **`UsagePollingCoordinator.cs` (US-3):** añadir el parámetro
    `ILogger<UsagePollingCoordinator>` al constructor y los dos logs
    descritos en Data Model (`Start()` y `PollAsync()`), incluyendo el log
    `Error` con excepción en el `catch` de red de seguridad (hoy silencioso).
11. **`App.xaml.cs` (US-2 + US-3, composition root):** implementar tal cual
    la sección Data Model — inicialización de Serilog, `AddLogging`, y la
    composición `AnthropicApiUsageDataSource` → `RetryingUsageDataSource`
    como único `IUsageDataSource` registrado.
12. **`UsagePage.razor` (US-1):** añadir el campo `_status`, la rama
    `Unauthorized` en `Apply()`, el `@if` de sustitución por `ReauthNotice`,
    el nuevo `@inject ILogger<UsagePollingCoordinator> Logger` (necesario
    para el nuevo parámetro del constructor de `UsagePollingCoordinator`,
    paso 10), y `StatusForTests`.
13. **`Pages/ReauthNotice.razor` + adición a `wwwroot/css/app.css` (US-1):**
    tal cual la sección Data Model.
14. **Tests bUnit de US-1:** en
    `test/ClaudeMeter.Desktop.Tests/Pages/UsagePageTests.cs`, añadir/ajustar
    casos: snapshot `Unauthorized` sin éxito previo → se renderiza
    `ReauthNotice` (no las dos `UsageBar`), sin sufijo "(desactualizado)";
    snapshot `Unauthorized` **después** de un snapshot `Success` previo
    (`_hasEverSucceeded == true`) → también `ReauthNotice`, nunca
    `(desactualizado)` (AC 3, el caso que más fácil se rompería con una
    implementación ingenua reusando solo `_isStale`); snapshot
    `RequestFailed`/`TokenUnavailable`/`MalformedResponse` → comportamiento
    sin cambios respecto a F1 (siguen usando `UsageBar` con "No disponible"
    o "(desactualizado)"). Actualizar también las instanciaciones de
    `UsagePollingCoordinator` en
    `test/ClaudeMeter.Desktop.Tests/Polling/UsagePollingCoordinatorTests.cs`
    y el `TestDoubles/FakeUsageDataSource.cs` existente para pasar
    `NullLogger<UsagePollingCoordinator>.Instance` al nuevo parámetro del
    constructor.
15. **Validación de compilación:** `dotnet build ClaudeMeter.sln` en Debug y
    Release (Release activa `TreatWarningsAsErrors`) para confirmar que los
    5 `PackageReference` nuevos y el enum ampliado no introducen
    advertencias de analizadores (prestar atención especial a posibles
    avisos de "switch no exhaustivo" en cualquier otro consumidor de
    `UsageSnapshotStatus` no listado arriba — búsqueda recomendada:
    `grep -rn "UsageSnapshotStatus\." src/`).

## Cross-Cutting Concerns

- **Seguridad:** ningún cambio respecto al invariante ya vigente — el token
  nunca se registra en logs (ninguno de los mensajes de `_logger` añadidos
  en Data Model incluye el token ni cabeceras `Authorization`; solo código
  de estado HTTP, nombre de excepción y contadores de intento),
  `AnthropicApiUsageDataSource` sigue sin refrescar el token, y
  `RetryingUsageDataSource` refuerza el invariante al nivel de política:
  `Unauthorized`/`TokenUnavailable` están estructuralmente excluidos del
  bucle de reintento (la condición `!= RequestFailed` los descarta antes de
  cualquier `Task.Delay`), no solo "no se reintentan por convención".
- **Rendimiento / costo de API:** cada reintento de `RetryingUsageDataSource`
  es una llamada HTTP real con cuota real (`PingModel`/`MaxTokens: 1`, ya
  documentado en F0). Los valores de `RetryPolicyOptions.Default`
  (3 intentos, 2s/4s de espera, techo 10s), confirmados por el usuario,
  acotan el peor caso a ~3 llamadas y ~6s de espera adicional por ciclo de
  poll.
- **Manejo de errores:** `RetryingUsageDataSource` nunca lanza — se limita a
  propagar el último `UsageSnapshot` no exitoso tal cual lo devuelve
  `_inner`, preservando la regla de sustituibilidad de `IUsageDataSource`
  (nunca excepción para el caso esperado). El `catch` de última instancia en
  `UsagePollingCoordinator.PollAsync` pasa de ser silencioso a loguear con
  `LogError` (antes F1 no tenía logging en absoluto) — mejora observable sin
  cambiar el comportamiento de control de flujo.
- **Observabilidad/logging (US-3):** cada evento se registra exactamente
  una vez, en la clase que tiene el detalle más rico sobre él, para evitar
  el riesgo de "ruido excesivo" ya marcado en el documento de requisitos —
  `AnthropicApiUsageDataSource` registra el resultado de cada intento HTTP
  individual (incluida la excepción cruda si la hay); `RetryingUsageDataSource`
  registra solo eventos de nivel "ciclo de reintento" (aviso antes de
  reintentar, error al agotar intentos) sin repetir el detalle ya logueado
  por la capa de abajo; `UsagePollingCoordinator` registra un único
  `Information` de arranque y un único `Information` de heartbeat por tick
  de 60s (`Status=...`), nunca por intento individual. Niveles: `Debug`
  (éxito HTTP individual, filtrado por el `MinimumLevel.Information` por
  defecto — no genera ruido en producción, pero queda disponible si se baja
  el nivel para depurar), `Information` (arranque, heartbeat por tick),
  `Warning` (fallo transitorio individual, aviso de reintento en curso, y
  `TokenUnavailable` — decisión confirmada por el usuario: es un estado
  esperable antes del primer login, no necesariamente un fallo real, a
  diferencia de `Unauthorized`), `Error` (`Unauthorized`, `MalformedResponse`,
  reintentos agotados, excepción no controlada).
- **Compatibilidad hacia atrás:** los 3 constructores modificados
  (`AnthropicApiUsageDataSource`, `UsagePollingCoordinator`, y el nuevo
  `RetryingUsageDataSource`) son cambios de firma que rompen cualquier sitio
  que los instancie directamente — ya identificados y listados en
  Implementation Plan (composition root de Desktop, ambos proyectos de
  tests). No hay ningún otro consumidor en el repositorio (verificado por
  `Grep` de `UsageSnapshotStatus`/`AnthropicApiUsageDataSource`/
  `UsagePollingCoordinator` antes de escribir este documento). El único
  consumidor externo del enum ampliado es `ClaudeMeter.Console` (F0),
  actualizado en el paso 3 del plan.

## Risks & Open Decisions

**Riesgos (heredados del documento de requisitos, sin cambios de
valoración):**

- **Costo/cuota de API (US-2):** cada reintento consume cuota real. Impact:
  Medium, ya marcado por el propio issue #11. Mitigado por diseño con un
  techo bajo (3 intentos, backoff acotado a 10s, valores confirmados por el
  usuario — ver más abajo) pero no eliminado del todo: la validación manual
  de US-2 en la Definition of Done sigue siendo el punto para confirmar que
  se comportan bien contra la API real.
- **Interacción con el ciclo de poll de 60s (US-2):** mitigado
  estructuralmente por la decisión de este documento (backoff dentro de la
  llamada única que ya serializa `_isPolling`, sin cambios en
  `UsagePollingCoordinator` más allá del logging) y por los valores de
  `RetryPolicyOptions.Default` ya confirmados (peor caso ~6s, muy por debajo
  de los 60s del poll). Impact: Low tras la mitigación de diseño (bajó desde
  Medium en el documento de requisitos).
- **I/O de logging (US-3):** mitigado por diseño con
  `retainedFileCountLimit: 7` (rolling diario, ~1 semana de retención) y
  por la política de "un evento, un log" de Cross-Cutting Concerns (nunca
  un log por cada tick si no hay nada que reportar más allá del heartbeat
  de una línea). Impact: Low, igual que en el documento de requisitos.
- **UX / confusión de estados (US-1):** mitigado por diseño sustituyendo
  las barras por `ReauthNotice` en vez de superponer un aviso (ver
  Technology Choices) — la validación real de si es "suficientemente
  distinguible" sigue siendo manual (ver Definition of Done, ningún agente
  de este pipeline puede juzgar percepción visual). Impact: Medium hasta esa
  validación manual, igual que en el documento de requisitos.
- **Regresión entre user stories:** sin cambios de valoración (Low,
  mitigado por seguir la cadena de dependencias US-1 → US-2 → US-3 en
  Development, igual que se hizo en F1).

**Decisiones de diseño ya tomadas en este documento (con rationale, no
requieren validación humana adicional antes de Development):**

- Dónde vive el backoff de US-2 (decorator `RetryingUsageDataSource`, no
  `AnthropicApiUsageDataSource` ni `UsagePollingCoordinator`) — la decisión
  que el documento de requisitos delegaba explícitamente a Design, resuelta
  con su rationale en Technology Choices.
- Refinar `UsageSnapshotStatus` con `MalformedResponse` en vez de un campo
  de "razón" adicional — resuelve la ambigüedad que el propio documento de
  requisitos anticipaba como probable trabajo de Design.
- `ReauthNotice` sustituye las barras en vez de superponerse — decisión de
  mecanismo de UI que el documento de requisitos delegaba explícitamente a
  Design ("el requisito funcional es únicamente que el mensaje sea
  distinguible").
- Backoff hecho a mano en vez de Polly, y sin usar `Serilog.Extensions.Hosting`
  (Generic Host) — ambas por continuidad con decisiones ya tomadas en F1
  (evitar dependencias/ceremonia sin justificación real todavía).

**Decisiones que este documento dejaba abiertas en una versión anterior,
confirmadas por el usuario — ya cerradas, no bloquean Development:**

- **Valores de `RetryPolicyOptions.Default`: confirmados tal cual la
  propuesta de este diseño** — 3 intentos totales, backoff 2s → 4s, techo
  10s (peor caso ~6s de espera adicional, muy por debajo de los 60s del
  poll). Dejan de ser "una estimación razonada sin validar" y pasan a ser
  una decisión de producto confirmada; la validación manual de US-2 en la
  Definition of Done se mantiene como comprobación de comportamiento real
  contra la API, no como pendiente de aprobación de los números en sí.
- **Nivel de log de `TokenUnavailable`: `Warning`, no `Error`.** El usuario
  eligió la alternativa que este documento ya señalaba como defendible:
  `TokenUnavailable` es un estado esperable antes del primer login (aún no
  hay token), no necesariamente un fallo real — a diferencia de
  `Unauthorized` (token presente pero rechazado por la API), que se
  mantiene en `Error`. Reflejado en `AnthropicApiUsageDataSource`
  (Data Model) y en la clasificación de niveles de Cross-Cutting Concerns.
- **Versiones exactas de los 5 paquetes NuGet nuevos: no se fijan en este
  documento.** Se fijan aquí únicamente los **nombres** de los 5 paquetes
  (`Serilog`, `Serilog.Sinks.File`, `Serilog.Extensions.Logging`,
  `Microsoft.Extensions.Logging`, `Microsoft.Extensions.Logging.Abstractions`)
  y la serie a usar (8.0.x, por compatibilidad de TFM — ver Technology
  Choices); Development debe confirmar el último patch estable de esa serie
  en `api.nuget.org` en el momento de implementar, mismo criterio ya
  aceptado en el documento de diseño de F1 para
  `Microsoft.AspNetCore.Components.WebView.Wpf`. No es una decisión de
  arquitectura, es una comprobación mecánica de packaging que no tiene
  sentido congelar por adelantado.

No queda ninguna decisión genuinamente abierta en este documento: las 3
delegadas a validación humana en una versión anterior de este mismo
apartado quedan resueltas arriba.

## Definition of Done

- [ ] Todas las Acceptance Criteria de US-1, US-2 y US-3 del documento de
      requisitos se cumplen
- [ ] Cobertura de tests automatizados (xUnit puro para
      `RetryingUsageDataSource`, `UsageSnapshot.MalformedResponse()` y
      `SerilogConfigurator`; bUnit para `ReauthNotice`/`UsagePage` con
      `Unauthorized`) según el mínimo configurado en
      `.claude/sdlc.config.yaml` (`testingCoverage: 70`)
- [ ] Ninguna prueba automatizada usa un token OAuth real ni realiza
      llamadas reales a la API de Anthropic, ni escribe logs reales bajo el
      `%LOCALAPPDATA%` real de la máquina de CI
- [ ] `dotnet build ClaudeMeter.sln` sin advertencias nuevas en Debug ni
      Release (`TreatWarningsAsErrors`), prestando atención a switches no
      exhaustivos sobre `UsageSnapshotStatus` en cualquier fichero no
      listado en este documento
- [ ] **Validación manual (US-1):** se confirma, invalidando el token local
      o simulando un 401/403 contra un mock de larga duración, que
      `ReauthNotice` aparece en la UI real y es visualmente distinguible de
      "No disponible" — ningún agente de este pipeline puede verificar
      renderizado real de WPF/WebView2
- [ ] **Validación manual (US-2):** se confirma, cortando la conectividad de
      red brevemente, que el widget no muestra error inmediato y se
      recupera solo tras el reintento sin parpadeo visible, con los valores
      ya confirmados de `RetryPolicyOptions` (3 intentos, 2s/4s, techo 10s)
- [ ] **Validación manual (US-3):** se confirma que el fichero de log
      aparece en `%LOCALAPPDATA%\ClaudeMeter\logs` tras ejecutar la
      aplicación real en Windows, que su contenido es legible/estructurado,
      y que los reintentos de US-2 quedan trazados en él
