using ClaudeMeter.Domain.Usage;

namespace ClaudeMeter.Domain.Tests.Usage;

/// <summary>
/// Pruebas puras (sin E/S, sin dependencias externas) de
/// <see cref="ThresholdTransition.EnteredCritical"/> — pieza de dominio
/// nueva de F2/Ciclo B (US-1): decide si dos lecturas consecutivas de
/// <see cref="UsageThreshold"/> representan una transición "nueva" hacia
/// <see cref="UsageThreshold.Critical"/>, el disparador exacto del chime en
/// <c>UsagePage.razor</c>. Cubre todas las combinaciones relevantes,
/// incluyendo <c>null</c> ("sin lectura previa"/"sin datos") en ambos
/// parámetros.
/// </summary>
public sealed class ThresholdTransitionTests
{
    [Theory]
    [InlineData(null, UsageThreshold.Critical)] // primera lectura ya en Crítico: SÍ cuenta como transición
    [InlineData(UsageThreshold.Normal, UsageThreshold.Critical)]
    [InlineData(UsageThreshold.Warning, UsageThreshold.Critical)]
    public void EnteredCritical_ConCurrentCriticalYPreviousNoCritical_DevuelveTrue(UsageThreshold? previous, UsageThreshold? current)
    {
        var result = ThresholdTransition.EnteredCritical(previous, current);

        Assert.True(result);
    }

    [Fact]
    public void EnteredCritical_ConPreviousYCurrentAmbosCritical_DevuelveFalse()
    {
        // AC explícito: mantenerse en Crítico ciclo tras ciclo NO es una
        // transición nueva -- es exactamente el caso que evita que el
        // chime se repita mientras el estado siga en rojo.
        var result = ThresholdTransition.EnteredCritical(UsageThreshold.Critical, UsageThreshold.Critical);

        Assert.False(result);
    }

    [Theory]
    [InlineData(UsageThreshold.Critical, UsageThreshold.Normal)]
    [InlineData(UsageThreshold.Critical, UsageThreshold.Warning)]
    [InlineData(UsageThreshold.Critical, null)] // Crítico -> "sin datos": tampoco es una transición HACIA Crítico
    public void EnteredCritical_ConPreviousCriticalYCurrentNoCritical_DevuelveFalse(UsageThreshold? previous, UsageThreshold? current)
    {
        var result = ThresholdTransition.EnteredCritical(previous, current);

        Assert.False(result);
    }

    [Theory]
    [InlineData(null, null)] // sin lectura previa ni actual: "sin datos" en ambos lados
    [InlineData(null, UsageThreshold.Normal)]
    [InlineData(null, UsageThreshold.Warning)]
    [InlineData(UsageThreshold.Normal, null)]
    [InlineData(UsageThreshold.Warning, null)]
    public void EnteredCritical_ConCurrentNuloOAmbosSinDatos_DevuelveFalse(UsageThreshold? previous, UsageThreshold? current)
    {
        // "Sin datos" nunca puede ser una transición HACIA Crítico, sea
        // cual sea la lectura previa.
        var result = ThresholdTransition.EnteredCritical(previous, current);

        Assert.False(result);
    }

    [Theory]
    [InlineData(UsageThreshold.Normal, UsageThreshold.Normal)]
    [InlineData(UsageThreshold.Normal, UsageThreshold.Warning)]
    [InlineData(UsageThreshold.Warning, UsageThreshold.Normal)]
    [InlineData(UsageThreshold.Warning, UsageThreshold.Warning)]
    public void EnteredCritical_SinQueCurrentSeaCritical_DevuelveFalse(UsageThreshold? previous, UsageThreshold? current)
    {
        var result = ThresholdTransition.EnteredCritical(previous, current);

        Assert.False(result);
    }
}
