using ClaudeMeter.Domain.Usage;

namespace ClaudeMeter.Domain.Tests.Usage;

/// <summary>
/// Pruebas puras (sin E/S, sin UI) de <see cref="MascotStateClassifier"/>:
/// verifican, para cada caso GIVEN-WHEN-THEN de US-2 del documento de
/// requisitos (<c>docs/sdlc/requirements/f3-ux-mascota-ciclo-c.md</c>), que
/// <see cref="MascotStateClassifier.Classify"/> nunca lanza, reutiliza
/// <see cref="UsageThresholdClassifier"/> como única fuente de verdad del
/// umbral, y aplica correctamente la regla de "peor caso" (Technical Notes)
/// entre <c>Session</c> y <c>Weekly</c>.
/// </summary>
public sealed class MascotStateClassifierTests
{
    private static RateLimitWindow Window(double? percentageUsed) => new(percentageUsed, MinutesRemaining: null);

    [Fact]
    public void Classify_ConAmbasVentanasNormal_DevuelveCalm()
    {
        var result = MascotStateClassifier.Classify(Window(10.0), Window(20.0));

        Assert.Equal(MascotState.Calm, result);
    }

    [Theory]
    [InlineData(75.0, 10.0)] // sesión Warning, semana Normal
    [InlineData(10.0, 80.0)] // sesión Normal, semana Warning -- simétrico
    [InlineData(75.0, 80.0)] // ambas Warning
    public void Classify_ConAlMenosUnaVentanaEnWarningYNingunaEnCritical_DevuelveAlert(
        double sessionPercentage, double weeklyPercentage)
    {
        var result = MascotStateClassifier.Classify(Window(sessionPercentage), Window(weeklyPercentage));

        Assert.Equal(MascotState.Alert, result);
    }

    [Theory]
    [InlineData(95.0, 10.0)] // sesión Critical, semana Normal
    [InlineData(10.0, 95.0)] // sesión Normal, semana Critical -- simétrico (AC de US-2)
    [InlineData(95.0, 80.0)] // sesión Critical, semana Warning
    [InlineData(80.0, 95.0)] // sesión Warning, semana Critical
    [InlineData(95.0, 95.0)] // ambas Critical
    public void Classify_ConAlMenosUnaVentanaEnCritical_DevuelveNearLimitConIndependenciaDeLaOtra(
        double sessionPercentage, double weeklyPercentage)
    {
        // AC de US-2: "gana el más severo de los dos", nunca un promedio ni
        // solo una de las dos ventanas fija.
        var result = MascotStateClassifier.Classify(Window(sessionPercentage), Window(weeklyPercentage));

        Assert.Equal(MascotState.NearLimit, result);
    }

    [Fact]
    public void Classify_ConAmbasVentanasSinDatoInterpretable_DevuelveNoData()
    {
        // AC de US-2: nunca hubo un snapshot con éxito, o el snapshot está
        // en Unauthorized -- ninguna de las dos ventanas tiene percentage.
        var result = MascotStateClassifier.Classify(RateLimitWindow.Unavailable, RateLimitWindow.Unavailable);

        Assert.Equal(MascotState.NoData, result);
    }

    [Theory]
    [InlineData(10.0, MascotState.Calm)] // la ventana con dato es Normal
    [InlineData(75.0, MascotState.Alert)] // la ventana con dato es Warning
    [InlineData(95.0, MascotState.NearLimit)] // la ventana con dato es Critical
    public void Classify_ConUnaVentanaSinDatoYLaOtraConDatoReal_UsaElUmbralDeLaQueSiTieneDatoSinCaerANoData(
        double otherWindowPercentage, MascotState expected)
    {
        // Caso de snapshot parcialmente parseable (percentage null dentro de
        // un snapshot por lo demás exitoso) -- distinto del "ninguna tiene
        // dato" cubierto arriba; no debe penalizarse ni resolver NoData.
        var sessionMissing = MascotStateClassifier.Classify(Window(null), Window(otherWindowPercentage));
        var weeklyMissing = MascotStateClassifier.Classify(Window(otherWindowPercentage), Window(null));

        Assert.Equal(expected, sessionMissing);
        Assert.Equal(expected, weeklyMissing);
    }

    [Fact]
    public void Classify_EnLaFronteraExactaDeCritical_DevuelveNearLimitConsistenteConUsageThresholdClassifier()
    {
        // Reutiliza el mismo umbral que UsageBar (>=90 -> Critical) -- sin
        // duplicar la regla ni introducir una frontera distinta.
        var result = MascotStateClassifier.Classify(Window(90.0), Window(0.0));

        Assert.Equal(MascotState.NearLimit, result);
    }
}
