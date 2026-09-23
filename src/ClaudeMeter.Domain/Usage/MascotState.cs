namespace ClaudeMeter.Domain.Usage;

/// <summary>
/// Estado visual de la mascota ("Clawd", issue #19), derivado exclusivamente
/// del snapshot de uso ACTUAL — sin ninguna noción de color/texto/UI (mismo
/// estándar que <see cref="UsageThreshold"/>) y sin ningún dato histórico
/// (restricción explícita de CLAUDE.md: "MascotPage.razor driven only by
/// the current snapshot, no history dependency").
/// </summary>
public enum MascotState
{
    /// <summary>Ambas ventanas en <see cref="UsageThreshold.Normal"/> (o una Normal y la otra sin dato individual).</summary>
    Calm,

    /// <summary>Al menos una ventana en <see cref="UsageThreshold.Warning"/>, ninguna en Critical.</summary>
    Alert,

    /// <summary>Al menos una ventana en <see cref="UsageThreshold.Critical"/>, con independencia de la otra.</summary>
    NearLimit,

    /// <summary>Ninguna ventana tiene un porcentaje interpretable (nunca hubo éxito, o el snapshot es Unauthorized).</summary>
    NoData
}

/// <summary>
/// Deriva <see cref="MascotState"/> de las dos ventanas ya parseadas.
/// Función pura, sin E/S, sin reloj propio — mismo estándar que
/// <see cref="UsageThresholdClassifier"/>/<see cref="RateLimitWindowParser"/>.
/// Reutiliza <see cref="UsageThresholdClassifier"/> como única fuente de
/// verdad del umbral (evita que la mascota y las <c>UsageBar</c> "cuenten
/// historias distintas" sobre el mismo porcentaje).
/// </summary>
public static class MascotStateClassifier
{
    /// <summary>
    /// "Peor caso" (Technical Notes de Requirements): si <paramref name="session"/>
    /// y <paramref name="weekly"/> clasifican en umbrales distintos, se usa el
    /// más severo. Si una de las dos no tiene dato individual (percentage
    /// null dentro de un snapshot por lo demás exitoso), se usa la otra sin
    /// penalizar. Solo si NINGUNA de las dos tiene dato se resuelve
    /// <see cref="MascotState.NoData"/> (AC de US-2: nunca "Calm" por
    /// defecto cuando en realidad no hay dato real).
    /// </summary>
    public static MascotState Classify(RateLimitWindow session, RateLimitWindow weekly)
    {
        var sessionThreshold = UsageThresholdClassifier.Classify(session.PercentageUsed);
        var weeklyThreshold = UsageThresholdClassifier.Classify(weekly.PercentageUsed);

        if (sessionThreshold is null && weeklyThreshold is null)
        {
            return MascotState.NoData;
        }

        return Worse(sessionThreshold, weeklyThreshold) switch
        {
            UsageThreshold.Critical => MascotState.NearLimit,
            UsageThreshold.Warning => MascotState.Alert,
            _ => MascotState.Calm,
        };
    }

    // UsageThreshold se declara Normal=0 < Warning=1 < Critical=2 (orden
    // ascendente de severidad) -- Math.Max sobre los valores enteros ya
    // expresa "el más severo de los dos" sin una tabla de comparación aparte.
    private static UsageThreshold Worse(UsageThreshold? a, UsageThreshold? b)
    {
        if (a is null) return b!.Value;
        if (b is null) return a.Value;
        return (UsageThreshold)Math.Max((int)a.Value, (int)b.Value);
    }
}
