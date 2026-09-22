namespace ClaudeMeter.Domain.Usage;

/// <summary>
/// Detecta transiciones de severidad entre dos lecturas consecutivas de
/// <see cref="UsageThreshold"/>. Función pura, sin dependencias externas,
/// sin estado propio (el estado "anterior" lo conserva el llamador, igual
/// que <see cref="UsageThresholdClassifier"/> no conserva ningún historial).
/// </summary>
public static class ThresholdTransition
{
    /// <summary>
    /// <c>true</c> únicamente cuando <paramref name="current"/> es
    /// <see cref="UsageThreshold.Critical"/> y <paramref name="previous"/>
    /// no lo era (incluido el caso "no había lectura previa todavía",
    /// <c>previous == null</c>, que SÍ cuenta como transición la primera
    /// vez que se observa Crítico). No dispara de nuevo mientras
    /// <paramref name="current"/> se mantenga en Crítico ciclo tras ciclo.
    /// </summary>
    public static bool EnteredCritical(UsageThreshold? previous, UsageThreshold? current) =>
        current == UsageThreshold.Critical && previous != UsageThreshold.Critical;
}
