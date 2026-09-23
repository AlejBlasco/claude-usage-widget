using Bunit;
using ClaudeMeter.Desktop.Navigation;
using ClaudeMeter.Desktop.Pages;
using ClaudeMeter.Domain.Usage;

namespace ClaudeMeter.Desktop.Tests.Pages;

/// <summary>
/// Pruebas bUnit de <see cref="UsagePage"/> tras la reescritura de F3/Ciclo
/// C (US-1, issue #18): desde este ciclo, <c>UsagePage</c> es un componente
/// puramente presentacional (<c>[Parameter, EditorRequired] WidgetUsageState
/// State</c>, <c>@implements IWidgetScreen</c>) -- ya no gestiona su propio
/// polling, tema, chime ni JS interop (todo eso vive ahora en
/// <see cref="ScreenNavigator"/>, cubierto por
/// <c>Navigation/ScreenNavigatorTests.cs</c>). Por eso este fichero, mucho
/// más simple que su versión anterior, monta <c>UsagePage</c> directamente
/// pasando un <see cref="WidgetUsageState"/> ya construido, sin ningún
/// servicio inyectado -- ver el resumen de implementación de F3 Ciclo C
/// (paso 12 del Implementation Plan de diseño) para el rationale completo
/// de la división entre este fichero y <c>ScreenNavigatorTests.cs</c>.
/// </summary>
public sealed class UsagePageTests : BunitContext
{
    private static WidgetUsageState State(
        double? sessionPercentage,
        double? weeklyPercentage,
        UsageSnapshotStatus? status = UsageSnapshotStatus.Success,
        bool isStale = false) =>
        new(
            Session: new RateLimitWindow(sessionPercentage, MinutesRemaining: null),
            Weekly: new RateLimitWindow(weeklyPercentage, MinutesRemaining: null),
            Status: status,
            IsStale: isStale);

    [Fact]
    public void UsagePage_ConSnapshotSuccessYAmbosPercentages_MuestraDosBarrasConSuColor()
    {
        var cut = Render<UsagePage>(p => p.Add(x => x.State,
            State(sessionPercentage: 75.0, weeklyPercentage: 95.0))); // Warning/ámbar, Critical/rojo

        var bars = cut.FindAll("div.usage-bar");
        Assert.Equal(2, bars.Count);
        Assert.Contains("usage-bar--amber", bars[0].ClassList);
        Assert.Contains("75%", bars[0].TextContent);
        Assert.Contains("usage-bar--red", bars[1].ClassList);
        Assert.Contains("95%", bars[1].TextContent);
    }

    [Fact]
    public void UsagePage_ConAmbasVentanasEnElMismoUmbral_AplicaLaMismaClaseDeColorAAmbasBarras()
    {
        // AC heredado de US-3: sesión y semana usan exactamente la misma
        // regla de umbral.
        var cut = Render<UsagePage>(p => p.Add(x => x.State,
            State(sessionPercentage: 95.0, weeklyPercentage: 95.0)));

        var bars = cut.FindAll("div.usage-bar");
        Assert.Contains("usage-bar--red", bars[0].ClassList);
        Assert.Contains("usage-bar--red", bars[1].ClassList);
    }

    [Fact]
    public void UsagePage_ConPercentageUsedNuloEnUnaVentana_MuestraEsaBarraComoNoDisponible()
    {
        var cut = Render<UsagePage>(p => p.Add(x => x.State,
            State(sessionPercentage: null, weeklyPercentage: 20.0)));

        var bars = cut.FindAll("div.usage-bar");
        Assert.Contains("usage-bar--neutral", bars[0].ClassList);
        Assert.Contains("No disponible", bars[0].TextContent);
        Assert.Contains("20%", bars[1].TextContent);
    }

    [Theory]
    [InlineData(nameof(UsageSnapshotStatus.TokenUnavailable))]
    [InlineData(nameof(UsageSnapshotStatus.RequestFailed))]
    [InlineData(nameof(UsageSnapshotStatus.MalformedResponse))]
    public void UsagePage_ConEstadoDeFalloYAmbasVentanasSinDato_MuestraAmbasBarrasNoDisponible(string failureKind)
    {
        var status = failureKind switch
        {
            nameof(UsageSnapshotStatus.TokenUnavailable) => UsageSnapshotStatus.TokenUnavailable,
            nameof(UsageSnapshotStatus.RequestFailed) => UsageSnapshotStatus.RequestFailed,
            nameof(UsageSnapshotStatus.MalformedResponse) => UsageSnapshotStatus.MalformedResponse,
            _ => throw new ArgumentOutOfRangeException(nameof(failureKind)),
        };

        var cut = Render<UsagePage>(p => p.Add(x => x.State,
            State(sessionPercentage: null, weeklyPercentage: null, status: status)));

        var bars = cut.FindAll("div.usage-bar");
        Assert.Equal(2, bars.Count);
        Assert.All(bars, bar => Assert.Contains("No disponible", bar.TextContent));
        Assert.DoesNotContain("desactualizado", cut.Markup);
        Assert.Empty(cut.FindAll("div.usage-reauth"));
    }

    [Fact]
    public void UsagePage_ConIsStaleTrue_MarcaAmbasBarrasComoDesactualizadasSinPerderElUltimoValor()
    {
        var cut = Render<UsagePage>(p => p.Add(x => x.State,
            State(sessionPercentage: 33.0, weeklyPercentage: 44.0, isStale: true)));

        Assert.Contains("33%", cut.Markup);
        Assert.Contains("desactualizado", cut.Markup);
    }

    [Fact]
    public void UsagePage_ConIsStaleFalse_NoMarcaLasBarrasComoDesactualizadas()
    {
        var cut = Render<UsagePage>(p => p.Add(x => x.State,
            State(sessionPercentage: 33.0, weeklyPercentage: 44.0, isStale: false)));

        Assert.DoesNotContain("desactualizado", cut.Markup);
    }

    [Fact]
    public void UsagePage_ConStatusUnauthorized_RenderizaReauthNoticeYOcultaLasBarras()
    {
        // AC heredado de US-1 (F2): un 401/403 no es "sin datos" ni
        // "desactualizado" -- sustituye por completo las dos UsageBar por
        // ReauthNotice.
        var cut = Render<UsagePage>(p => p.Add(x => x.State,
            State(sessionPercentage: null, weeklyPercentage: null, status: UsageSnapshotStatus.Unauthorized)));

        Assert.Empty(cut.FindAll("div.usage-bar"));
        var notice = cut.Find("div.usage-reauth");
        Assert.Contains("Vuelve a iniciar sesión", notice.TextContent);
        Assert.DoesNotContain("desactualizado", cut.Markup);
        Assert.DoesNotContain("No disponible", cut.Markup);
    }

    [Fact]
    public void UsagePage_ImplementaIWidgetScreenConScreenIdUsage()
    {
        var cut = Render<UsagePage>(p => p.Add(x => x.State, State(10.0, 20.0)));

        var screen = Assert.IsAssignableFrom<IWidgetScreen>(cut.Instance);
        Assert.Equal("usage", screen.ScreenId);
    }

    [Fact]
    public void UsagePage_ConWidgetUsageStateInitial_MuestraAmbasBarrasNoDisponibleSinLanzar()
    {
        // Estado antes del primer ciclo de poll (Status null) -- el @if
        // solo compara contra Unauthorized, así que Status == null también
        // renderiza las dos UsageBar (sin dato), no ReauthNotice.
        var cut = Render<UsagePage>(p => p.Add(x => x.State, WidgetUsageState.Initial));

        var bars = cut.FindAll("div.usage-bar");
        Assert.Equal(2, bars.Count);
        Assert.All(bars, bar => Assert.Contains("No disponible", bar.TextContent));
    }
}
