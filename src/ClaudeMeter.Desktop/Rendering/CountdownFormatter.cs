namespace ClaudeMeter.Desktop.Rendering;

/// <summary>
/// Formatea <see cref="ClaudeMeter.Domain.Usage.RateLimitWindow.MinutesRemaining"/>
/// de forma compacta para un widget de 280x140px (F3/Ciclo A, US-2). Función
/// pura, sin dependencias externas ni acceso al reloj — mismo estándar que
/// <see cref="ClaudeMeter.Domain.Usage.RateLimitWindowParser"/>. Recibe un
/// <see cref="int"/> no anulable a propósito: el llamador
/// (<c>UsageBar.razor</c>) solo la invoca tras desenvolver un
/// <c>int? MinutesRemaining</c> no nulo vía pattern matching — "sin datos"
/// se resuelve en el propio Razor sin llamar a esta función (AC de US-2).
/// </summary>
public static class CountdownFormatter
{
    private const int MinutesPerHour = 60;
    private const int MinutesPerDay = 24 * MinutesPerHour;

    /// <summary>
    /// &gt;=1 día -> "{d}d {h}h"; &gt;=1 hora -> "{h}h {m}m"; en caso
    /// contrario -> "{m}m" (incluye el caso 0 -> "0m", AC explícita de
    /// US-2 sobre el estado de cero).
    /// </summary>
    public static string Format(int minutesRemaining)
    {
        if (minutesRemaining <= 0)
        {
            return "0m";
        }

        var days = minutesRemaining / MinutesPerDay;
        var hours = minutesRemaining % MinutesPerDay / MinutesPerHour;
        var minutes = minutesRemaining % MinutesPerHour;

        if (days > 0)
        {
            return $"{days}d {hours}h";
        }

        if (hours > 0)
        {
            return $"{hours}h {minutes}m";
        }

        return $"{minutes}m";
    }
}
