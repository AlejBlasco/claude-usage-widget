using Microsoft.Extensions.Logging;

namespace ClaudeMeter.Infrastructure.Tests.Usage;

/// <summary>
/// Test double de <see cref="ILogger{TCategoryName}"/> que solo registra el
/// <see cref="LogLevel"/> de cada llamada (nunca escribe a consola/fichero
/// real), usado para verificar los AC de US-3 (F2) sobre qué nivel exacto
/// se registra por cada categoría de resultado -- p. ej. <c>Warning</c>
/// para un fallo transitorio/reintento, <c>Error</c> para
/// <c>Unauthorized</c>/<c>MalformedResponse</c>/reintentos agotados.
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
