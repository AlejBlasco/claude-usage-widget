using System.Globalization;
using ClaudeMeter.Domain.Usage;

namespace ClaudeMeter.ConsoleApp.Rendering;

/// <summary>
/// Construye cada línea de texto como un <see cref="string"/>, sin tocar
/// <see cref="Console"/> en ningún punto — permite testear el formato
/// exacto sin capturar streams de consola.
/// </summary>
public static class UsagePollingLineFormatter
{
    private const string NotAvailable = "no disponible";

    /// <summary>
    /// Formatea la línea de éxito para una iteración con ambas ventanas
    /// (sesión y semanal) ya parseadas por <see cref="RateLimitWindowParser"/>.
    /// </summary>
    public static string FormatSuccessLine(DateTimeOffset timestamp, RateLimitWindow session, RateLimitWindow weekly) =>
        $"[{FormatTimestamp(timestamp)}] Sesión: {FormatWindow(session)} | Semana: {FormatWindow(weekly)}";

    /// <summary>
    /// Formatea la línea de error para el caso "token no disponible"
    /// (fichero de credenciales ausente, JSON inválido o token vacío —
    /// todos colapsados en un único mensaje genérico).
    /// </summary>
    public static string FormatTokenUnavailableLine(DateTimeOffset timestamp) =>
        $"[{FormatTimestamp(timestamp)}] ERROR - Token no disponible: no se pudo leer un token OAuth válido desde .credentials.json.";

    /// <summary>
    /// Formatea la línea de error para el caso "token inválido/expirado"
    /// (401/403). Deja explícito que no se intentará un refresco
    /// automático, reforzando la regla de <c>CLAUDE.md</c> también de cara
    /// al usuario final que lea la consola.
    /// </summary>
    public static string FormatUnauthorizedLine(DateTimeOffset timestamp) =>
        $"[{FormatTimestamp(timestamp)}] ERROR - Token inválido/expirado (401/403): la API rechazó el token; no se intentará refrescarlo automáticamente.";

    /// <summary>
    /// Formatea la línea de error para un fallo de la petición (red,
    /// timeout, 5xx o cabeceras de rate-limit ausentes en una respuesta 2xx).
    /// </summary>
    public static string FormatRequestFailedLine(DateTimeOffset timestamp) =>
        $"[{FormatTimestamp(timestamp)}] ERROR - Fallo de conexión con la API: no se pudo completar la petición (red, timeout, error 5xx o cabeceras de rate-limit ausentes).";

    /// <summary>
    /// Formatea la línea de error para una excepción no controlada por las
    /// capas inferiores, capturada en <see cref="Polling.UsagePollingLoop.ExecuteIterationAsync"/>.
    /// </summary>
    public static string FormatUnexpectedErrorLine(DateTimeOffset timestamp, Exception exception) =>
        $"[{FormatTimestamp(timestamp)}] ERROR - Fallo inesperado: {exception.GetType().Name}: {exception.Message}";

    private static string FormatTimestamp(DateTimeOffset timestamp) =>
        timestamp.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture);

    private static string FormatWindow(RateLimitWindow window) =>
        $"{FormatPercentage(window.PercentageUsed)} ({FormatMinutes(window.MinutesRemaining)})";

    private static string FormatPercentage(double? value) =>
        value is null ? NotAvailable : $"{value.Value.ToString("0.##", CultureInfo.InvariantCulture)}%";

    private static string FormatMinutes(int? value) =>
        value is null ? NotAvailable : $"{value.Value.ToString(CultureInfo.InvariantCulture)} min";
}
