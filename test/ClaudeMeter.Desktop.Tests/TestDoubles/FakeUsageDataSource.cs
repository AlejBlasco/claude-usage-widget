using ClaudeMeter.Application.Abstractions;
using ClaudeMeter.Domain.Usage;

namespace ClaudeMeter.Desktop.Tests.TestDoubles;

/// <summary>
/// Test double de <see cref="IUsageDataSource"/> con resultado
/// reconfigurable entre llamadas (a diferencia de
/// <c>ClaudeMeter.ConsoleApp.Tests.Polling.FakeUsageDataSource</c> de F0,
/// que es de un solo uso): permite encolar resultados/excepciones
/// distintos para simular varios ciclos de refresco consecutivos (AC de
/// US-4 sobre dos ciclos consecutivos), y opcionalmente bloquear una
/// llamada hasta ser señalada explícitamente, para probar el guard
/// anti-solape de <c>UsagePollingCoordinator</c>. Nunca realiza una
/// llamada HTTP real ni lee un token real.
/// </summary>
internal sealed class FakeUsageDataSource : IUsageDataSource
{
    private readonly object _gate = new();
    private readonly Queue<Func<Task<UsageSnapshot>>> _queuedResults = new();
    private Func<Task<UsageSnapshot>> _default;

    public FakeUsageDataSource(UsageSnapshot initialResult)
    {
        _default = () => Task.FromResult(initialResult);
    }

    /// <summary>Número de veces que se ha invocado <see cref="GetUsageAsync"/>.</summary>
    public int CallCount { get; private set; }

    /// <summary>Encola un resultado exitoso para la siguiente llamada.</summary>
    public void SetNextResult(UsageSnapshot snapshot) => Enqueue(() => Task.FromResult(snapshot));

    /// <summary>Encola una excepción para la siguiente llamada.</summary>
    public void SetNextException(Exception exception) => Enqueue(() => Task.FromException<UsageSnapshot>(exception));

    /// <summary>
    /// Encola una llamada que no completa hasta que se resuelva el
    /// <see cref="TaskCompletionSource{TResult}"/> devuelto — usado para
    /// verificar de forma determinista el guard anti-solape (<c>_isPolling</c>)
    /// de <c>UsagePollingCoordinator</c> manteniendo una llamada "en vuelo".
    /// </summary>
    public TaskCompletionSource<UsageSnapshot> ArmBlockingCall()
    {
        var completionSource = new TaskCompletionSource<UsageSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        Enqueue(() => completionSource.Task);
        return completionSource;
    }

    public Task<UsageSnapshot> GetUsageAsync(CancellationToken cancellationToken = default)
    {
        Func<Task<UsageSnapshot>> factory;
        lock (_gate)
        {
            CallCount++;
            factory = _queuedResults.Count > 0 ? _queuedResults.Dequeue() : _default;
        }

        return factory();
    }

    private void Enqueue(Func<Task<UsageSnapshot>> factory)
    {
        lock (_gate)
        {
            _queuedResults.Enqueue(factory);
        }
    }
}
