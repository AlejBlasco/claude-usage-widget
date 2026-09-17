using Bunit;
using ClaudeMeter.Desktop.Pages;
using ClaudeMeter.Domain.Usage;

namespace ClaudeMeter.Desktop.Tests.Pages;

/// <summary>
/// Pruebas bUnit de <see cref="UsageBar"/> (US-2/US-3): verifican que la
/// barra refleja <c>PercentageUsed</c>, que la clase de color depende
/// exclusivamente de <see cref="UsageThresholdClassifier"/> (incluidas
/// ambas fronteras exactas 70/90) y que el estado "sin datos" se muestra
/// sin lanzar cuando <c>PercentageUsed</c> es <c>null</c>. Usa la API v2 de
/// bUnit (<see cref="BunitContext"/> + <c>Render&lt;T&gt;</c>), nunca la v1
/// (<c>TestContext</c>/<c>RenderComponent</c>), tal como fija el documento
/// de diseño.
/// </summary>
public sealed class UsageBarTests : BunitContext
{
    [Theory]
    [InlineData(0.0, "usage-bar--green")]
    [InlineData(50.0, "usage-bar--green")]
    [InlineData(69.9, "usage-bar--green")]
    [InlineData(70.0, "usage-bar--amber")] // frontera inclusive de US-3
    [InlineData(85.0, "usage-bar--amber")]
    [InlineData(89.9, "usage-bar--amber")]
    [InlineData(90.0, "usage-bar--red")] // frontera inclusive de US-3
    [InlineData(100.0, "usage-bar--red")]
    public void UsageBar_ConPercentageUsedDado_AplicaLaClaseDeColorDelUmbralCorrespondiente(
        double percentageUsed, string expectedColorClass)
    {
        var window = new RateLimitWindow(percentageUsed, MinutesRemaining: 30);

        var cut = Render<UsageBar>(parameters => parameters
            .Add(p => p.Title, "Sesión")
            .Add(p => p.Window, window));

        var bar = cut.Find("div.usage-bar");
        Assert.Contains(expectedColorClass, bar.ClassList);
    }

    [Fact]
    public void UsageBar_ConPercentageUsedDado_MuestraElPorcentajeRedondeado()
    {
        var window = new RateLimitWindow(PercentageUsed: 42.0, MinutesRemaining: 10);

        var cut = Render<UsageBar>(parameters => parameters
            .Add(p => p.Title, "Semana")
            .Add(p => p.Window, window));

        var value = cut.Find(".usage-bar__value").TextContent.Trim();
        Assert.Equal("42%", value);
    }

    [Fact]
    public void UsageBar_ConWindowUnavailable_MuestraNoDisponibleEnEstadoNeutroSinLanzar()
    {
        var cut = Render<UsageBar>(parameters => parameters
            .Add(p => p.Title, "Sesión")
            .Add(p => p.Window, RateLimitWindow.Unavailable));

        var bar = cut.Find("div.usage-bar");
        Assert.Contains("usage-bar--neutral", bar.ClassList);

        var value = cut.Find(".usage-bar__value--unavailable");
        Assert.Equal("No disponible", value.TextContent.Trim());
    }

    [Fact]
    public void UsageBar_ConStaleYPercentageUsedValido_AñadeLaEtiquetaDesactualizado()
    {
        var window = new RateLimitWindow(PercentageUsed: 55.0, MinutesRemaining: 5);

        var cut = Render<UsageBar>(parameters => parameters
            .Add(p => p.Title, "Sesión")
            .Add(p => p.Window, window)
            .Add(p => p.Stale, true));

        var value = cut.Find(".usage-bar__value").TextContent.Trim();
        Assert.Equal("55% (desactualizado)", value);
    }

    [Fact]
    public void UsageBar_SinStale_NoAñadeLaEtiquetaDesactualizado()
    {
        var window = new RateLimitWindow(PercentageUsed: 55.0, MinutesRemaining: 5);

        var cut = Render<UsageBar>(parameters => parameters
            .Add(p => p.Title, "Sesión")
            .Add(p => p.Window, window)
            .Add(p => p.Stale, false));

        var value = cut.Find(".usage-bar__value").TextContent.Trim();
        Assert.Equal("55%", value);
    }

    [Fact]
    public void UsageBar_MuestraElTituloRecibidoPorParametro()
    {
        var cut = Render<UsageBar>(parameters => parameters
            .Add(p => p.Title, "Semana")
            .Add(p => p.Window, RateLimitWindow.Unavailable));

        Assert.Equal("Semana", cut.Find(".usage-bar__title").TextContent);
    }
}
