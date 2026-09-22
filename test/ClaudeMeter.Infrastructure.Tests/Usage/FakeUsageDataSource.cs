using ClaudeMeter.Application.Abstractions;
using ClaudeMeter.Domain.Usage;

namespace ClaudeMeter.Infrastructure.Tests.Usage;

/// <summary>
/// Test double de <see cref="IUsageDataSource"/> que devuelve una secuencia
/// de resultados pre-configurada (uno por llamada, repitiendo el último una
/// vez agotada la cola) — usado por <c>RetryingUsageDataSourceTests</c> para
/// simular varios intentos consecutivos de la fuente envuelta sin red ni
/// token real. A diferencia de <c>ClaudeMeter.ConsoleApp.Tests.Polling.FakeUsageDataSource</c>
/// (F0, un único resultado de un solo uso), este necesita una secuencia
/// porque el decorator bajo prueba invoca la fuente envuelta más de una vez
/// por intento de reintento.
/// </summary>
internal sealed class FakeUsageDataSource : IUsageDataSource
{
    private readonly Queue<UsageSnapshot> _queuedResults;
    private readonly UsageSnapshot _lastResult;

    public FakeUsageDataSource(params UsageSnapshot[] results)
    {
        if (results.Length == 0)
        {
            throw new ArgumentException("Debe proporcionarse al menos un resultado.", nameof(results));
        }

        _queuedResults = new Queue<UsageSnapshot>(results);
        _lastResult = results[^1];
    }

    /// <summary>Número de veces que se ha invocado <see cref="GetUsageAsync"/>.</summary>
    public int CallCount { get; private set; }

    public Task<UsageSnapshot> GetUsageAsync(CancellationToken cancellationToken = default)
    {
        CallCount++;
        var result = _queuedResults.Count > 0 ? _queuedResults.Dequeue() : _lastResult;
        return Task.FromResult(result);
    }
}
