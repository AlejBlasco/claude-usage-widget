using ClaudeMeter.ConsoleApp.Rendering;
using ClaudeMeter.Domain.Usage;

namespace ClaudeMeter.ConsoleApp.Tests.Polling;

/// <summary>
/// Test double de <see cref="IUsagePollingRenderer"/> que registra qué
/// método fue invocado y con qué argumentos, sin escribir nada en
/// <see cref="Console.Out"/>/<see cref="Console.Error"/> reales. Permite
/// verificar la lógica de despacho de
/// <see cref="ClaudeMeter.ConsoleApp.Polling.UsagePollingLoop"/> sin
/// capturar streams de consola.
/// </summary>
internal sealed class SpyUsagePollingRenderer : IUsagePollingRenderer
{
    public int RenderSuccessCallCount { get; private set; }
    public int RenderTokenUnavailableCallCount { get; private set; }
    public int RenderUnauthorizedCallCount { get; private set; }
    public int RenderRequestFailedCallCount { get; private set; }
    public int RenderUnexpectedErrorCallCount { get; private set; }

    public DateTimeOffset? LastTimestamp { get; private set; }
    public RateLimitWindow? LastSession { get; private set; }
    public RateLimitWindow? LastWeekly { get; private set; }
    public Exception? LastException { get; private set; }

    /// <summary>Número total de invocaciones a cualquiera de los métodos de renderizado.</summary>
    public int TotalCallCount =>
        RenderSuccessCallCount + RenderTokenUnavailableCallCount + RenderUnauthorizedCallCount +
        RenderRequestFailedCallCount + RenderUnexpectedErrorCallCount;

    public void RenderSuccess(DateTimeOffset timestamp, RateLimitWindow session, RateLimitWindow weekly)
    {
        RenderSuccessCallCount++;
        LastTimestamp = timestamp;
        LastSession = session;
        LastWeekly = weekly;
    }

    public void RenderTokenUnavailable(DateTimeOffset timestamp)
    {
        RenderTokenUnavailableCallCount++;
        LastTimestamp = timestamp;
    }

    public void RenderUnauthorized(DateTimeOffset timestamp)
    {
        RenderUnauthorizedCallCount++;
        LastTimestamp = timestamp;
    }

    public void RenderRequestFailed(DateTimeOffset timestamp)
    {
        RenderRequestFailedCallCount++;
        LastTimestamp = timestamp;
    }

    public void RenderUnexpectedError(DateTimeOffset timestamp, Exception exception)
    {
        RenderUnexpectedErrorCallCount++;
        LastTimestamp = timestamp;
        LastException = exception;
    }
}
