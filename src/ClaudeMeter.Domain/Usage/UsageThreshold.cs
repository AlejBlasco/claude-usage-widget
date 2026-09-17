namespace ClaudeMeter.Domain.Usage;

/// <summary>
/// Nivel de severidad de un porcentaje de consumo, sin ninguna noción de
/// color ni de UI — el mapeo a color concreto es responsabilidad exclusiva
/// de la capa de presentación (Desktop).
/// </summary>
public enum UsageThreshold
{
    Normal,
    Warning,
    Critical
}

/// <summary>
/// Clasifica un <see cref="RateLimitWindow.PercentageUsed"/> en un
/// <see cref="UsageThreshold"/>. Función pura, sin dependencias externas,
/// sin acceso a reloj ni E/S — cumple el mismo estándar de "lógica pura y
/// determinista" que <see cref="RateLimitWindowParser"/>.
/// </summary>
public static class UsageThresholdClassifier
{
    private const double WarningThreshold = 70;
    private const double CriticalThreshold = 90;

    /// <summary>
    /// Devuelve <c>null</c> cuando <paramref name="percentageUsed"/> es
    /// <c>null</c> ("sin datos" — ningún umbral de color aplica, AC de
    /// US-3). En caso contrario: &lt;70 → Normal, [70,90) → Warning,
    /// &gt;=90 → Critical.
    /// </summary>
    public static UsageThreshold? Classify(double? percentageUsed)
    {
        if (percentageUsed is not { } value)
        {
            return null;
        }

        if (value >= CriticalThreshold)
        {
            return UsageThreshold.Critical;
        }

        return value >= WarningThreshold ? UsageThreshold.Warning : UsageThreshold.Normal;
    }
}
