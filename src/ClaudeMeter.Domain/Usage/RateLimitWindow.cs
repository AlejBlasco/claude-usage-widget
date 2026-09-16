using System.Globalization;
using System.Text.RegularExpressions;

namespace ClaudeMeter.Domain.Usage;

/// <summary>
/// Datos de rate-limit ya calculados para una única ventana (sesión de 5h
/// o semanal de 7d): únicamente porcentaje consumido y minutos restantes
/// hasta el reset. No expone el <c>Reset</c> crudo ni ningún otro campo —
/// mantenido deliberadamente mínimo (Acceptance Criteria del Issue #4).
/// Cada propiedad es independientemente <c>null</c> cuando el dato
/// correspondiente no está disponible (cabecera ausente o con un formato
/// no interpretable), en vez de un valor por defecto que pudiera inducir
/// a error a quien lo consuma.
/// </summary>
/// <param name="PercentageUsed">
/// Porcentaje consumido en escala 0-100, calculado exclusivamente a
/// partir de <see cref="RawRateLimitHeaders.Utilization"/> (nunca de
/// <see cref="RawRateLimitHeaders.Remaining"/>, que se ignora por
/// completo); <c>null</c> si <c>Utilization</c> está ausente o no es
/// interpretable.
/// </param>
/// <param name="MinutesRemaining">
/// Minutos restantes hasta <see cref="RawRateLimitHeaders.Reset"/>,
/// calculados de forma determinista a partir de la fecha de referencia
/// pasada a <see cref="RateLimitWindowParser.Parse"/>; nunca negativo
/// (mínimo 0); <c>null</c> si <c>Reset</c> está ausente o no es
/// interpretable.
/// </param>
public sealed record RateLimitWindow(double? PercentageUsed, int? MinutesRemaining)
{
    /// <summary>
    /// Instancia compartida que representa "sin datos disponibles" para
    /// ambos campos — usada cuando no hay <see cref="RawRateLimitHeaders"/>
    /// de la que partir (p. ej. <see cref="UsageSnapshot"/> en un estado
    /// distinto de <see cref="UsageSnapshotStatus.Success"/>).
    /// </summary>
    public static readonly RateLimitWindow Unavailable = new(PercentageUsed: null, MinutesRemaining: null);
}

/// <summary>
/// Convierte <see cref="RawRateLimitHeaders"/> en <see cref="RateLimitWindow"/>.
/// Lógica pura y determinista: sin HTTP, sin E/S, sin leer el reloj del
/// sistema — la fecha de referencia ("ahora") se recibe siempre como
/// parámetro explícito.
/// </summary>
public static class RateLimitWindowParser
{
    // Exige exactamente dígitos + '.' opcional + dígitos + '%', sin
    // espacios internos ni externos — cualquier desviación (fracción sin
    // '%', separador decimal distinto, espacios extra, texto no numérico)
    // no matchea y se trata como "no disponible" (ver Decisiones Técnicas
    // Clave del diseño).
    private static readonly Regex UtilizationPattern =
        new(@"^(0|[1-9]\d*)(\.\d+)?%$", RegexOptions.Compiled);

    /// <summary>
    /// Parsea una única ventana. Si <paramref name="headers"/> es
    /// <c>null</c> (ventana no disponible en absoluto, p. ej.
    /// <see cref="UsageSnapshot"/> sin éxito), devuelve
    /// <see cref="RateLimitWindow.Unavailable"/> sin lanzar.
    /// </summary>
    /// <param name="headers">
    /// Cabeceras crudas de la ventana a parsear, o <c>null</c> si la
    /// ventana no está disponible en absoluto.
    /// </param>
    /// <param name="now">
    /// Fecha/hora de referencia explícita usada para calcular
    /// <see cref="RateLimitWindow.MinutesRemaining"/>. Nunca se lee del
    /// reloj del sistema dentro de esta función.
    /// </param>
    public static RateLimitWindow Parse(RawRateLimitHeaders? headers, DateTimeOffset now)
    {
        if (headers is null)
        {
            return RateLimitWindow.Unavailable;
        }

        return new RateLimitWindow(
            PercentageUsed: TryParsePercentageUsed(headers.Utilization),
            MinutesRemaining: TryParseMinutesRemaining(headers.Reset, now));
    }

    /// <summary>
    /// Conveniencia: parsea ambas ventanas de un <see cref="UsageSnapshot"/>
    /// de una sola vez. Cuando <paramref name="snapshot"/> no está en
    /// estado <see cref="UsageSnapshotStatus.Success"/>, <c>Session</c> y
    /// <c>Weekly</c> ya son <c>null</c> por contrato de
    /// <see cref="UsageSnapshot"/>, por lo que ambas ventanas resultantes
    /// son <see cref="RateLimitWindow.Unavailable"/> sin ningún caso
    /// especial adicional aquí.
    /// </summary>
    /// <param name="snapshot">Snapshot de uso ya obtenido de <c>IUsageDataSource</c>.</param>
    /// <param name="now">
    /// Fecha/hora de referencia explícita, propagada tal cual a
    /// <see cref="Parse"/> para ambas ventanas.
    /// </param>
    public static (RateLimitWindow Session, RateLimitWindow Weekly) ParseSnapshot(
        UsageSnapshot snapshot, DateTimeOffset now) =>
        (Parse(snapshot.Session, now), Parse(snapshot.Weekly, now));

    private static double? TryParsePercentageUsed(string? utilization)
    {
        if (utilization is null || !UtilizationPattern.IsMatch(utilization))
        {
            return null;
        }

        var numericPart = utilization[..^1]; // quita el '%' final, ya validado por la regex
        if (!double.TryParse(numericPart, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
        {
            return null;
        }

        return value is >= 0 and <= 100 ? value : null;
    }

    private static int? TryParseMinutesRemaining(string? reset, DateTimeOffset now)
    {
        if (reset is null ||
            !DateTimeOffset.TryParse(reset, CultureInfo.InvariantCulture, DateTimeStyles.None, out var resetTime))
        {
            return null;
        }

        var remaining = resetTime - now;
        return remaining <= TimeSpan.Zero ? 0 : (int)Math.Ceiling(remaining.TotalMinutes);
    }
}
