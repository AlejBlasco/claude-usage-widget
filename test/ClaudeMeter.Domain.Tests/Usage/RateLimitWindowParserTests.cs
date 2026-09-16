using ClaudeMeter.Domain.Usage;

namespace ClaudeMeter.Domain.Tests.Usage;

/// <summary>
/// Pruebas puras (sin E/S, sin reloj real) de <see cref="RateLimitWindowParser"/>:
/// verifican, para cada caso GIVEN-WHEN-THEN del documento de requisitos del
/// Issue #4 (<c>docs/sdlc/requirements/issue-04-parsear-ratelimit-headers.md</c>),
/// que <see cref="RateLimitWindowParser.Parse"/> y
/// <see cref="RateLimitWindowParser.ParseSnapshot"/> nunca lanzan una excepción
/// y producen exactamente el <see cref="RateLimitWindow"/> esperado. Todas las
/// fechas de referencia ("ahora") son literales fijos — nunca
/// <c>DateTimeOffset.UtcNow</c> — para que estos tests sean deterministas.
/// </summary>
public sealed class RateLimitWindowParserTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 16, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Parse_ConUtilizationYResetValidos_CalculaPercentageUsedYMinutesRemaining()
    {
        // Reset exactamente 120 minutos después de "ahora".
        var headers = new RawRateLimitHeaders("allowed", "10%", "90%", "2026-09-16T12:00:00Z");

        var result = RateLimitWindowParser.Parse(headers, Now);

        Assert.Equal(10.0, result.PercentageUsed);
        Assert.Equal(120, result.MinutesRemaining);
    }

    [Fact]
    public void Parse_ConUtilizationYRemainingNoComplementarios_IgnoraRemainingPorCompleto()
    {
        // Utilization=10% y Remaining=80% no suman 100%: Remaining debe
        // ignorarse por completo, sin validación cruzada ni excepción.
        var headers = new RawRateLimitHeaders("allowed", "10%", "80%", "2026-09-16T12:00:00Z");

        var result = RateLimitWindowParser.Parse(headers, Now);

        Assert.Equal(10.0, result.PercentageUsed);
    }

    [Fact]
    public void Parse_ConUtilizationCero_DevuelvePercentageUsedCeroSinNegativos()
    {
        var headers = new RawRateLimitHeaders("allowed", "0%", "100%", "2026-09-16T12:00:00Z");

        var result = RateLimitWindowParser.Parse(headers, Now);

        Assert.Equal(0.0, result.PercentageUsed);
        Assert.NotNull(result.MinutesRemaining);
        Assert.True(result.MinutesRemaining >= 0);
    }

    [Fact]
    public void Parse_ConUtilizacionCienYResetYaPasado_DevuelveMinutesRemainingCero()
    {
        // Reset 30 minutos antes de "ahora": ya ha pasado.
        var headers = new RawRateLimitHeaders("allowed", "100%", "0%", "2026-09-16T09:30:00Z");

        var result = RateLimitWindowParser.Parse(headers, Now);

        Assert.Equal(100.0, result.PercentageUsed);
        Assert.Equal(0, result.MinutesRemaining);
    }

    [Fact]
    public void Parse_ConResetExactamenteIgualAAhora_DevuelveMinutesRemainingCero()
    {
        // Caso límite explícito del Acceptance Criteria: "igual o anterior".
        var headers = new RawRateLimitHeaders("allowed", "100%", "0%", "2026-09-16T10:00:00Z");

        var result = RateLimitWindowParser.Parse(headers, Now);

        Assert.Equal(0, result.MinutesRemaining);
    }

    [Fact]
    public void Parse_ConMinutosFraccionarios_RedondeaHaciaArribaConMathCeiling()
    {
        // Reset 4 minutos y 10 segundos después de "ahora" (4.1666... min):
        // Math.Ceiling debe redondear a 5, nunca truncar a 4.
        var headers = new RawRateLimitHeaders("allowed", "50%", "50%", "2026-09-16T10:04:10Z");

        var result = RateLimitWindowParser.Parse(headers, Now);

        Assert.Equal(5, result.MinutesRemaining);
    }

    [Fact]
    public void Parse_ConResetAusente_DevuelveMinutesRemainingNullSinAfectarPercentageUsed()
    {
        var headers = new RawRateLimitHeaders("allowed", "10%", "90%", Reset: null);

        var result = RateLimitWindowParser.Parse(headers, Now);

        Assert.Equal(10.0, result.PercentageUsed);
        Assert.Null(result.MinutesRemaining);
    }

    [Fact]
    public void Parse_ConUtilizationAusente_DevuelvePercentageUsedNullSinAfectarMinutesRemaining()
    {
        var headers = new RawRateLimitHeaders("allowed", Utilization: null, "90%", "2026-09-16T12:00:00Z");

        var result = RateLimitWindowParser.Parse(headers, Now);

        Assert.Null(result.PercentageUsed);
        Assert.Equal(120, result.MinutesRemaining);
    }

    [Fact]
    public void Parse_ConHeadersNull_DevuelveRateLimitWindowUnavailable()
    {
        var result = RateLimitWindowParser.Parse(headers: null, Now);

        Assert.Equal(RateLimitWindow.Unavailable, result);
        Assert.Null(result.PercentageUsed);
        Assert.Null(result.MinutesRemaining);
    }

    [Theory]
    [InlineData("0.1")]      // fracción en vez de porcentaje
    [InlineData("10,5%")]    // separador decimal distinto de '.'
    [InlineData(" 10% ")]    // espacios extra
    [InlineData("abc%")]     // texto no numérico
    [InlineData("150%")]     // sintácticamente válido pero fuera de [0,100]
    [InlineData("")]         // cadena vacía
    public void Parse_ConUtilizationEnFormatoInesperado_DevuelvePercentageUsedNull(string malformedUtilization)
    {
        var headers = new RawRateLimitHeaders("allowed", malformedUtilization, "90%", "2026-09-16T12:00:00Z");

        var result = RateLimitWindowParser.Parse(headers, Now);

        Assert.Null(result.PercentageUsed);
        // El formato inesperado de Utilization nunca debe afectar a MinutesRemaining.
        Assert.Equal(120, result.MinutesRemaining);
    }

    [Theory]
    [InlineData("not-a-date")]
    [InlineData("")]
    public void Parse_ConResetEnFormatoInesperado_DevuelveMinutesRemainingNullSinAfectarPercentageUsed(string malformedReset)
    {
        var headers = new RawRateLimitHeaders("allowed", "10%", "90%", malformedReset);

        var result = RateLimitWindowParser.Parse(headers, Now);

        Assert.Equal(10.0, result.PercentageUsed);
        Assert.Null(result.MinutesRemaining);
    }

    [Theory]
    [InlineData("TokenUnavailable")]
    [InlineData("Unauthorized")]
    [InlineData("RequestFailed")]
    public void ParseSnapshot_ConUsageSnapshotEnEstadoDeFallo_DevuelveAmbasVentanasUnavailable(string failureKind)
    {
        var snapshot = failureKind switch
        {
            "TokenUnavailable" => UsageSnapshot.TokenUnavailable(),
            "Unauthorized" => UsageSnapshot.Unauthorized(),
            "RequestFailed" => UsageSnapshot.RequestFailed(),
            _ => throw new ArgumentOutOfRangeException(nameof(failureKind)),
        };

        var (session, weekly) = RateLimitWindowParser.ParseSnapshot(snapshot, Now);

        Assert.Equal(RateLimitWindow.Unavailable, session);
        Assert.Equal(RateLimitWindow.Unavailable, weekly);
    }

    [Fact]
    public void ParseSnapshot_ConSessionYWeeklyCompletos_CalculaAmbasVentanasCorrectamente()
    {
        var session = new RawRateLimitHeaders("allowed", "10%", "90%", "2026-09-16T12:00:00Z"); // +120 min
        var weekly = new RawRateLimitHeaders("allowed", "42%", "58%", "2026-09-20T10:00:00Z");  // +4 días = 5760 min

        var snapshot = UsageSnapshot.Success(session, weekly);

        var (sessionResult, weeklyResult) = RateLimitWindowParser.ParseSnapshot(snapshot, Now);

        Assert.Equal(10.0, sessionResult.PercentageUsed);
        Assert.Equal(120, sessionResult.MinutesRemaining);
        Assert.Equal(42.0, weeklyResult.PercentageUsed);
        Assert.Equal(5760, weeklyResult.MinutesRemaining);
    }

    [Fact]
    public void ParseSnapshot_ConSessionCompletaYWeeklyConCamposAusentes_CalculaCadaVentanaDeFormaIndependiente()
    {
        var sessionHeaders = new RawRateLimitHeaders("allowed", "10%", "90%", "2026-09-16T12:00:00Z");
        var weeklyHeaders = new RawRateLimitHeaders("allowed", Utilization: null, "58%", Reset: null);

        var snapshot = UsageSnapshot.Success(sessionHeaders, weeklyHeaders);

        var (sessionResult, weeklyResult) = RateLimitWindowParser.ParseSnapshot(snapshot, Now);

        // La ventana de sesión se calcula por completo, sin verse afectada
        // por los campos ausentes de la ventana semanal.
        Assert.Equal(10.0, sessionResult.PercentageUsed);
        Assert.Equal(120, sessionResult.MinutesRemaining);

        // La ventana semanal refleja sus propios campos ausentes, de forma
        // independiente del resultado (correcto) de la ventana de sesión.
        Assert.Null(weeklyResult.PercentageUsed);
        Assert.Null(weeklyResult.MinutesRemaining);
    }
}
