using ClaudeMeter.Domain.Usage;

namespace ClaudeMeter.Domain.Tests.Usage;

/// <summary>
/// Pruebas puras (sin E/S, sin dependencias de UI) de
/// <see cref="UsageThresholdClassifier"/>: verifican, para cada caso
/// GIVEN-WHEN-THEN de US-3 del documento de requisitos
/// (<c>docs/sdlc/requirements/f1-widget-visual-base.md</c>), que
/// <see cref="UsageThresholdClassifier.Classify"/> nunca lanza y clasifica
/// cada porcentaje en el <see cref="UsageThreshold"/> esperado, incluyendo
/// ambas fronteras exactas (69.9/70/89.9/90) y el caso <c>null</c> ("sin
/// datos").
/// </summary>
public sealed class UsageThresholdClassifierTests
{
    [Theory]
    [InlineData(0.0)]
    [InlineData(1.0)]
    [InlineData(50.0)]
    [InlineData(69.0)]
    [InlineData(69.9)]
    public void Classify_ConPercentageUsedMenorA70_DevuelveNormal(double percentageUsed)
    {
        var result = UsageThresholdClassifier.Classify(percentageUsed);

        Assert.Equal(UsageThreshold.Normal, result);
    }

    [Theory]
    [InlineData(70.0)] // frontera inferior inclusive
    [InlineData(70.1)]
    [InlineData(80.0)]
    [InlineData(89.9)] // frontera superior exclusive
    public void Classify_ConPercentageUsedEntre70Y90_DevuelveWarning(double percentageUsed)
    {
        var result = UsageThresholdClassifier.Classify(percentageUsed);

        Assert.Equal(UsageThreshold.Warning, result);
    }

    [Theory]
    [InlineData(90.0)] // frontera inclusive
    [InlineData(90.1)]
    [InlineData(95.0)]
    [InlineData(100.0)]
    public void Classify_ConPercentageUsedDe90OMas_DevuelveCritical(double percentageUsed)
    {
        var result = UsageThresholdClassifier.Classify(percentageUsed);

        Assert.Equal(UsageThreshold.Critical, result);
    }

    [Fact]
    public void Classify_ConPercentageUsedExactamente70_DevuelveWarningNoNormal()
    {
        // AC explícito de US-3: "70 → ámbar", sin zona ambigua con Normal.
        var result = UsageThresholdClassifier.Classify(70.0);

        Assert.Equal(UsageThreshold.Warning, result);
        Assert.NotEqual(UsageThreshold.Normal, result);
    }

    [Fact]
    public void Classify_ConPercentageUsedExactamente90_DevuelveCriticalNoWarning()
    {
        // AC explícito de US-3: "90 → rojo", sin zona ambigua con Warning.
        var result = UsageThresholdClassifier.Classify(90.0);

        Assert.Equal(UsageThreshold.Critical, result);
        Assert.NotEqual(UsageThreshold.Warning, result);
    }

    [Fact]
    public void Classify_ConPercentageUsedNull_DevuelveNullSinAsignarNingunUmbral()
    {
        // "Sin datos" (ventana no disponible): ningún umbral de color aplica.
        var result = UsageThresholdClassifier.Classify(percentageUsed: null);

        Assert.Null(result);
    }
}
