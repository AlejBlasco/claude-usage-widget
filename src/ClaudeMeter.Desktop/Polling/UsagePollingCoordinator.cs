using ClaudeMeter.Application.Abstractions;
using ClaudeMeter.Domain.Usage;
using Microsoft.Extensions.Logging;

namespace ClaudeMeter.Desktop.Polling;

/// <summary>
/// Orquesta el poll periódico de <see cref="IUsageDataSource"/> sin conocer
/// Blazor ni WPF: gestiona un <see cref="System.Timers.Timer"/> y notifica
/// cada <see cref="UsageSnapshot"/> obtenido (junto al instante <c>now</c>
/// usado como referencia) mediante un evento .NET. Cualquier página Razor
/// (hoy <c>UsagePage</c>, en F3 <c>MascotPage</c>) puede suscribirse sin
/// duplicar temporización. Se crea una instancia nueva por cada componente
/// que la usa — nunca un singleton de DI — para que su ciclo de vida quede
/// atado 1:1 al del componente (ver AC de US-4 y Cross-Cutting/Error
/// handling del documento de diseño).
/// </summary>
public sealed class UsagePollingCoordinator : IDisposable
{
    private readonly IUsageDataSource _usageDataSource;
    private readonly ILogger<UsagePollingCoordinator> _logger;

    // Tipo completamente cualificado (sin `using System.Timers;`) para que,
    // si en el futuro alguna refactorización añade `using System.Threading;`
    // a este fichero, no se reabra una ambigüedad de nombre `Timer` entre
    // System.Timers.Timer y System.Threading.Timer (norma defensiva del
    // documento de diseño).
    private readonly System.Timers.Timer _timer;
    private volatile bool _isPolling;
    private bool _disposed;

    /// <summary>
    /// Se levanta cada vez que se completa un ciclo de poll, con éxito o
    /// no — el suscriptor decide cómo tratar cada <see cref="UsageSnapshot.Status"/>.
    /// </summary>
    public event Action<UsageSnapshot, DateTimeOffset>? SnapshotReceived;

    public UsagePollingCoordinator(IUsageDataSource usageDataSource, TimeSpan interval, ILogger<UsagePollingCoordinator> logger)
    {
        _usageDataSource = usageDataSource;
        _logger = logger;
        _timer = new System.Timers.Timer(interval.TotalMilliseconds) { AutoReset = true };
        _timer.Elapsed += OnTimerElapsed;
    }

    /// <summary>Dispara el primer fetch inmediatamente y arranca el timer para los siguientes.</summary>
    public void Start()
    {
        _logger.LogInformation("Polling de uso iniciado (intervalo {IntervalSeconds}s)", _timer.Interval / 1000); // US-3
        _ = PollAsync();
        _timer.Start();
    }

    /// <summary>
    /// <c>internal</c> + <c>InternalsVisibleTo</c> hacia
    /// <c>ClaudeMeter.Desktop.Tests</c>: permite a bUnit simular un ciclo de
    /// refresco concreto sin esperar el timer real (mismo patrón que
    /// <c>UsagePollingLoop.ExecuteIterationAsync</c> en F0).
    /// </summary>
    internal Task PollOnceForTestsAsync() => PollAsync();

    /// <summary><c>internal</c> + <c>InternalsVisibleTo</c>: expone si el timer subyacente sigue activo, para el test bUnit de "el timer se detiene al desmontar".</summary>
    internal bool IsRunningForTests => _timer.Enabled;

    private void OnTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e) => _ = PollAsync();

    private async Task PollAsync()
    {
        if (_isPolling)
        {
            return; // evita solapes si una llamada tarda más que el intervalo (AC: nunca dos timers/llamadas en paralelo)
        }

        _isPolling = true;
        try
        {
            var now = DateTimeOffset.UtcNow;
            var snapshot = await _usageDataSource.GetUsageAsync();
            _logger.LogInformation("Poll completado: Status={Status}", snapshot.Status); // US-3: heartbeat por tick
            SnapshotReceived?.Invoke(snapshot, now);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Ninguna capa inferior debería lanzar para los casos esperados
            // (ver UsageSnapshot); esto es solo la red de seguridad final,
            // igual que en UsagePollingLoop (F0). Sin renderer al que
            // delegar aquí: simplemente no se levanta ningún evento y el
            // timer sigue vivo para el siguiente ciclo.
            _logger.LogError(ex, "Excepción no controlada durante un ciclo de poll"); // US-3
        }
        finally
        {
            _isPolling = false;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _timer.Elapsed -= OnTimerElapsed;
        _timer.Stop();
        _timer.Dispose();
        _disposed = true;
    }
}
