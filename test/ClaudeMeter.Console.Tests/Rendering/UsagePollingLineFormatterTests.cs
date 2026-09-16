using System.Globalization;
using ClaudeMeter.ConsoleApp.Rendering;
using ClaudeMeter.Domain.Usage;

namespace ClaudeMeter.ConsoleApp.Tests.Rendering;

/// <summary>
/// Pruebas puras (sin tocar <see cref="Console"/>) de
/// <see cref="UsagePollingLineFormatter"/>: verifican, para cada caso
/// GIVEN-WHEN-THEN del documento de requisitos del Issue #5, el texto
/// exacto producido por cada uno de los cinco métodos de formateo —
/// incluyendo los casos límite de campos <c>null</c>/<see cref="RateLimitWindow.Unavailable"/>
/// que deben degradar a "no disponible" sin lanzar.
/// </summary>
public sealed class UsagePollingLineFormatterTests
{
    private static readonly DateTimeOffset Timestamp = new(2026, 9, 16, 14, 32, 7, TimeSpan.Zero);

    // Misma conversión que usa la implementación (FormatTimestamp privado):
    // hora local en formato "HH:mm:ss", cultura invariante. Se calcula aquí
    // en vez de fijar un literal para que el test no dependa de la zona
    // horaria de la máquina que lo ejecuta, sin dejar de ejercitar el
    // método real sobre el mismo valor de timestamp en todos los casos.
    private static string ExpectedTimestamp =>
        Timestamp.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture);

    [Fact]
    public void FormatSuccessLine_ConAmbasVentanasCompletas_FormateaPorcentajeYMinutosDeAmbas()
    {
        var session = new RateLimitWindow(PercentageUsed: 42, MinutesRemaining: 23);
        var weekly = new RateLimitWindow(PercentageUsed: 10, MinutesRemaining: 620);

        var line = UsagePollingLineFormatter.FormatSuccessLine(Timestamp, session, weekly);

        Assert.Equal(
            $"[{ExpectedTimestamp}] Sesión: 42% (23 min) | Semana: 10% (620 min)",
            line);
    }

    [Fact]
    public void FormatSuccessLine_ConPercentageUsedConDecimales_FormateaConHastaDosDecimalesSinCerosSobrantes()
    {
        // PercentageUsed 42.5 debe mostrarse como "42.5%", no "42.50%" ni
        // "42,5%" (formato "0.##" con cultura invariante).
        var session = new RateLimitWindow(PercentageUsed: 42.5, MinutesRemaining: 23);
        var weekly = new RateLimitWindow(PercentageUsed: 10, MinutesRemaining: 620);

        var line = UsagePollingLineFormatter.FormatSuccessLine(Timestamp, session, weekly);

        Assert.Equal(
            $"[{ExpectedTimestamp}] Sesión: 42.5% (23 min) | Semana: 10% (620 min)",
            line);
    }

    [Fact]
    public void FormatSuccessLine_ConMinutesRemainingNullEnUnaVentana_MuestraSoloEsaVentanaComoNoDisponible()
    {
        // Semana con Reset no interpretable: PercentageUsed sí llegó, pero
        // MinutesRemaining es null — no debe afectar a la ventana de sesión.
        var session = new RateLimitWindow(PercentageUsed: 42, MinutesRemaining: 23);
        var weekly = new RateLimitWindow(PercentageUsed: 10, MinutesRemaining: null);

        var line = UsagePollingLineFormatter.FormatSuccessLine(Timestamp, session, weekly);

        Assert.Equal(
            $"[{ExpectedTimestamp}] Sesión: 42% (23 min) | Semana: 10% (no disponible)",
            line);
    }

    [Fact]
    public void FormatSuccessLine_ConPercentageUsedNullEnUnaVentana_MuestraSoloEsaVentanaComoNoDisponible()
    {
        var session = new RateLimitWindow(PercentageUsed: 42, MinutesRemaining: 23);
        var weekly = new RateLimitWindow(PercentageUsed: null, MinutesRemaining: 620);

        var line = UsagePollingLineFormatter.FormatSuccessLine(Timestamp, session, weekly);

        Assert.Equal(
            $"[{ExpectedTimestamp}] Sesión: 42% (23 min) | Semana: no disponible (620 min)",
            line);
    }

    [Fact]
    public void FormatSuccessLine_ConUnaVentanaUnavailable_MuestraAmbosCamposDeEsaVentanaComoNoDisponible()
    {
        var session = new RateLimitWindow(PercentageUsed: 42, MinutesRemaining: 23);
        var weekly = RateLimitWindow.Unavailable;

        var line = UsagePollingLineFormatter.FormatSuccessLine(Timestamp, session, weekly);

        Assert.Equal(
            $"[{ExpectedTimestamp}] Sesión: 42% (23 min) | Semana: no disponible (no disponible)",
            line);
    }

    [Fact]
    public void FormatSuccessLine_ConAmbasVentanasUnavailable_MuestraNoDisponibleEnTodosLosCampos()
    {
        var line = UsagePollingLineFormatter.FormatSuccessLine(
            Timestamp, RateLimitWindow.Unavailable, RateLimitWindow.Unavailable);

        Assert.Equal(
            $"[{ExpectedTimestamp}] Sesión: no disponible (no disponible) | Semana: no disponible (no disponible)",
            line);
    }

    [Fact]
    public void FormatTokenUnavailableLine_DevuelveElMensajeExactoDeTokenNoDisponible()
    {
        var line = UsagePollingLineFormatter.FormatTokenUnavailableLine(Timestamp);

        Assert.Equal(
            $"[{ExpectedTimestamp}] ERROR - Token no disponible: no se pudo leer un token OAuth válido desde .credentials.json.",
            line);
    }

    [Fact]
    public void FormatUnauthorizedLine_DevuelveElMensajeExactoDeTokenInvalidoYNoIntentaRefrescar()
    {
        var line = UsagePollingLineFormatter.FormatUnauthorizedLine(Timestamp);

        Assert.Equal(
            $"[{ExpectedTimestamp}] ERROR - Token inválido/expirado (401/403): la API rechazó el token; no se intentará refrescarlo automáticamente.",
            line);
    }

    [Fact]
    public void FormatRequestFailedLine_DevuelveElMensajeExactoDeFalloDeConexion()
    {
        var line = UsagePollingLineFormatter.FormatRequestFailedLine(Timestamp);

        Assert.Equal(
            $"[{ExpectedTimestamp}] ERROR - Fallo de conexión con la API: no se pudo completar la petición (red, timeout, error 5xx o cabeceras de rate-limit ausentes).",
            line);
    }

    [Fact]
    public void FormatUnexpectedErrorLine_IncluyeElTipoYElMensajeDeLaExcepcion()
    {
        var exception = new InvalidOperationException("algo salió mal");

        var line = UsagePollingLineFormatter.FormatUnexpectedErrorLine(Timestamp, exception);

        Assert.Equal(
            $"[{ExpectedTimestamp}] ERROR - Fallo inesperado: {nameof(InvalidOperationException)}: algo salió mal",
            line);
    }

    [Fact]
    public void FormatUnexpectedErrorLine_ConDistintoTipoDeExcepcion_UsaElNombreDeEseTipoExacto()
    {
        // No debe hardcodear un tipo de excepción concreto: el nombre debe
        // reflejar el tipo real recibido, sea cual sea.
        var exception = new TimeoutException("se agotó el tiempo de espera");

        var line = UsagePollingLineFormatter.FormatUnexpectedErrorLine(Timestamp, exception);

        Assert.Equal(
            $"[{ExpectedTimestamp}] ERROR - Fallo inesperado: {nameof(TimeoutException)}: se agotó el tiempo de espera",
            line);
    }

    [Theory]
    [InlineData(0.0, "0%")]
    [InlineData(100.0, "100%")]
    public void FormatSuccessLine_ConPercentageUsedEnLosExtremos_FormateaSinSufijosDecimalesSobrantes(
        double percentage, string expectedFormatted)
    {
        var session = new RateLimitWindow(PercentageUsed: percentage, MinutesRemaining: 0);
        var weekly = RateLimitWindow.Unavailable;

        var line = UsagePollingLineFormatter.FormatSuccessLine(Timestamp, session, weekly);

        Assert.Equal(
            $"[{ExpectedTimestamp}] Sesión: {expectedFormatted} (0 min) | Semana: no disponible (no disponible)",
            line);
    }
}
