using Microsoft.Extensions.Logging;

namespace ClaudeMeter.Desktop.Tests.TestDoubles;

/// <summary>
/// Test double de <see cref="ILogger{TCategoryName}"/> que solo registra el
/// <see cref="LogLevel"/> de cada llamada (nunca escribe a consola/fichero
/// real) -- mismo patrón ya usado en
/// <c>ClaudeMeter.Infrastructure.Tests.Usage.CapturingLogger{T}</c> (F2
/// Ciclo A), reimplementado aquí porque los test doubles `internal` no se
/// comparten entre ensamblados de test distintos.
/// </summary>
internal sealed class CapturingLogger<T> : ILogger<T>
{
    public List<LogLevel> LoggedLevels { get; } = new();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        LoggedLevels.Add(logLevel);
    }
}
