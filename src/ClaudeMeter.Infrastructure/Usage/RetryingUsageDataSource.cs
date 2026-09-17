using ClaudeMeter.Application.Abstractions;
using ClaudeMeter.Domain.Usage;
using Microsoft.Extensions.Logging;

namespace ClaudeMeter.Infrastructure.Usage;

/// <summary>
/// Decorator de <see cref="IUsageDataSource"/> que aplica el modelo de 3
/// categorías de reintento de US-2 (F2) sobre cualquier fuente envuelta
/// (hoy <see cref="AnthropicApiUsageDataSource"/>): únicamente
/// <see cref="UsageSnapshotStatus.RequestFailed"/> (fallo transitorio) se
/// reintenta con backoff exponencial; <c>Success</c>, <c>TokenUnavailable</c>,
/// <c>Unauthorized</c> y <c>MalformedResponse</c> se devuelven
/// inmediatamente, sin ningún reintento — las dos últimas categorías por
/// regla explícita de <c>CLAUDE.md</c>/US-1 (nunca reintentar ni refrescar
/// el token) y de US-2 (un contrato roto no se arregla reintentando). Nunca
/// lanza: se limita a propagar el último <see cref="UsageSnapshot"/> no
/// exitoso devuelto por la fuente envuelta, preservando la regla de
/// sustituibilidad de <see cref="IUsageDataSource"/>.
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
    /// otros colaboradores testeables de este repo, p. ej.
    /// <c>CredentialsFileTokenProvider</c>): permite a los tests simular
    /// varios reintentos sin esperar segundos reales.
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

    /// <inheritdoc />
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
