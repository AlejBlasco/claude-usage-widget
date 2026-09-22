namespace ClaudeMeter.Infrastructure.Usage;

/// <summary>
/// Parámetros de la política de reintento de <see cref="RetryingUsageDataSource"/>.
/// Ni el número de intentos ni el backoff vienen especificados en el issue
/// original (US-2) — <see cref="Default"/> son los valores fijados por el
/// documento de diseño de F2, confirmados por el usuario; no son
/// configurables desde <c>config.json</c> en este ciclo (issue #13, fuera
/// de alcance).
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
    /// usuario (ver documento de diseño, Risks &amp; Open Decisions), no
    /// una estimación pendiente de validar.
    /// </summary>
    public static readonly RetryPolicyOptions Default =
        new(MaxAttempts: 3, InitialDelay: TimeSpan.FromSeconds(2), BackoffMultiplier: 2.0, MaxDelay: TimeSpan.FromSeconds(10));
}
