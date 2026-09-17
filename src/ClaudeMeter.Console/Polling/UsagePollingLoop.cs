using ClaudeMeter.Application.Abstractions;
using ClaudeMeter.ConsoleApp.Rendering;
using ClaudeMeter.Domain.Usage;

namespace ClaudeMeter.ConsoleApp.Polling;

/// <summary>
/// Orquesta el bucle de polling: pide un <see cref="UsageSnapshot"/> a
/// <see cref="IUsageDataSource"/>, calcula las ventanas con
/// <see cref="RateLimitWindowParser"/> y delega el resultado (o el error)
/// en <see cref="IUsagePollingRenderer"/>. No lee el token ni llama a la
/// API directamente, y no formatea ni imprime texto — únicamente
/// coordina, cumpliendo la separación de responsabilidades de CLAUDE.md.
/// </summary>
public sealed class UsagePollingLoop
{
    private readonly IUsageDataSource _usageDataSource;
    private readonly IUsagePollingRenderer _renderer;
    private readonly TimeSpan _interval;

    /// <summary>
    /// Crea el orquestador con los colaboradores ya construidos por el
    /// composition root (<c>Program</c>) y el intervalo de espera entre
    /// iteraciones.
    /// </summary>
    public UsagePollingLoop(IUsageDataSource usageDataSource, IUsagePollingRenderer renderer, TimeSpan interval)
    {
        _usageDataSource = usageDataSource;
        _renderer = renderer;
        _interval = interval;
    }

    /// <summary>
    /// Ejecuta el bucle indefinidamente. Ninguna excepción de una
    /// iteración individual propaga fuera de este método — ver
    /// <see cref="ExecuteIterationAsync"/>. F0 no requiere apagado
    /// controlado: el bucle solo termina si se cancela
    /// <paramref name="cancellationToken"/> o se mata el proceso.
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            await ExecuteIterationAsync(cancellationToken);
            await Task.Delay(_interval, cancellationToken);
        }
    }

    /// <summary>
    /// Ejecuta una única iteración (una llamada al core + un render).
    /// <c>internal</c> + <c>InternalsVisibleTo</c> hacia
    /// <c>ClaudeMeter.Console.Tests</c> únicamente para poder testear la
    /// lógica de despacho sin esperar 60s reales por iteración de test.
    /// </summary>
    internal async Task ExecuteIterationAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        try
        {
            var snapshot = await _usageDataSource.GetUsageAsync(cancellationToken);
            Render(now, snapshot);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Cualquier excepción no controlada por las capas inferiores
            // (que hoy no deberían lanzar ninguna para los casos
            // esperados, ver UsageSnapshot/TokenResult) no debe terminar
            // el proceso — se muestra y el bucle sigue en la siguiente
            // iteración. OperationCanceledException genuina se propaga
            // (mismo criterio que AnthropicApiUsageDataSource).
            _renderer.RenderUnexpectedError(now, ex);
        }
    }

    private void Render(DateTimeOffset now, UsageSnapshot snapshot)
    {
        switch (snapshot.Status)
        {
            case UsageSnapshotStatus.Success:
                var (session, weekly) = RateLimitWindowParser.ParseSnapshot(snapshot, now);
                _renderer.RenderSuccess(now, session, weekly);
                break;
            case UsageSnapshotStatus.TokenUnavailable:
                _renderer.RenderTokenUnavailable(now);
                break;
            case UsageSnapshotStatus.Unauthorized:
                _renderer.RenderUnauthorized(now);
                break;
            case UsageSnapshotStatus.RequestFailed:
                _renderer.RenderRequestFailed(now);
                break;
            case UsageSnapshotStatus.MalformedResponse:
                // Reutiliza el mismo renderer que RequestFailed (F2): desde
                // la consola (F0), un contrato de API roto y un fallo de
                // red se muestran igual ("no se pudieron obtener datos
                // ahora mismo") — la distinción fina solo importa para
                // decidir si reintentar (US-2, Infrastructure) y para el
                // log (US-3), no para el texto de consola.
                _renderer.RenderRequestFailed(now);
                break;
        }
    }
}
