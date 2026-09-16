using ClaudeMeter.Application.Abstractions;
using ClaudeMeter.Domain.Usage;

namespace ClaudeMeter.ConsoleApp.Tests.Polling;

/// <summary>
/// Test double de <see cref="IUsageDataSource"/> que, según cómo se
/// construya, o bien devuelve un <see cref="UsageSnapshot"/>
/// pre-configurado, o bien lanza una excepción pre-configurada — nunca
/// realiza una llamada HTTP real ni lee un token real. Permite testear
/// <see cref="ClaudeMeter.ConsoleApp.Polling.UsagePollingLoop.ExecuteIterationAsync"/>
/// para cada <see cref="UsageSnapshotStatus"/> y para el caso de excepción
/// no controlada, sin depender de Infrastructure.
/// </summary>
internal sealed class FakeUsageDataSource : IUsageDataSource
{
    private readonly UsageSnapshot? _result;
    private readonly Exception? _exceptionToThrow;

    private FakeUsageDataSource(UsageSnapshot? result, Exception? exceptionToThrow)
    {
        _result = result;
        _exceptionToThrow = exceptionToThrow;
    }

    /// <summary>Crea un doble que devuelve <paramref name="result"/> con éxito.</summary>
    public static FakeUsageDataSource Returning(UsageSnapshot result) => new(result, exceptionToThrow: null);

    /// <summary>Crea un doble que lanza <paramref name="exception"/> al ser invocado.</summary>
    public static FakeUsageDataSource Throwing(Exception exception) => new(result: null, exception);

    public Task<UsageSnapshot> GetUsageAsync(CancellationToken cancellationToken = default)
    {
        if (_exceptionToThrow is not null)
        {
            throw _exceptionToThrow;
        }

        return Task.FromResult(_result!);
    }
}
