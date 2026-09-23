using Bunit;
using ClaudeMeter.Desktop.Navigation;
using ClaudeMeter.Desktop.Pages;
using ClaudeMeter.Domain.Usage;

namespace ClaudeMeter.Desktop.Tests.Pages;

/// <summary>
/// Pruebas bUnit de <see cref="MascotPage"/> (US-2, issue #19): componente
/// puramente presentacional -- se monta directamente pasando un
/// <see cref="WidgetUsageState"/> ya construido, sin ningún servicio
/// inyectado (a diferencia de <see cref="ScreenNavigatorTests"/>, que sí
/// necesita <c>RegisterCoreServices</c>). Cubre los 4 estados de
/// <see cref="MascotState"/> más el caso simétrico de "peor caso" exigido
/// por la AC de US-2.
/// </summary>
public sealed class MascotPageTests : BunitContext
{
    private static WidgetUsageState State(double? sessionPercentage, double? weeklyPercentage, UsageSnapshotStatus? status = UsageSnapshotStatus.Success) =>
        new(
            Session: new RateLimitWindow(sessionPercentage, MinutesRemaining: null),
            Weekly: new RateLimitWindow(weeklyPercentage, MinutesRemaining: null),
            Status: status,
            IsStale: false);

    [Fact]
    public void MascotPage_ConAmbasVentanasNormal_RenderizaElEstadoCalm()
    {
        var cut = Render<MascotPage>(p => p.Add(x => x.State, State(10.0, 20.0)));

        var root = cut.Find("div.mascot");
        Assert.Contains("mascot--calm", root.ClassList);
    }

    [Fact]
    public void MascotPage_ConAlMenosUnaVentanaEnWarningYNingunaEnCritical_RenderizaElEstadoAlert()
    {
        var cut = Render<MascotPage>(p => p.Add(x => x.State, State(75.0, 10.0)));

        var root = cut.Find("div.mascot");
        Assert.Contains("mascot--alert", root.ClassList);
    }

    [Fact]
    public void MascotPage_ConSesionEnCriticalYSemanaNormal_RenderizaElEstadoNearLimit()
    {
        var cut = Render<MascotPage>(p => p.Add(x => x.State, State(95.0, 10.0)));

        var root = cut.Find("div.mascot");
        Assert.Contains("mascot--near-limit", root.ClassList);
    }

    [Fact]
    public void MascotPage_ConSemanaEnCriticalYSesionNormal_RenderizaElEstadoNearLimitTambien()
    {
        // AC de US-2 ("peor caso" simétrico): con independencia de cuál de
        // las dos ventanas sea la que está en Critical, el resultado visual
        // es el mismo estado "cerca del límite".
        var cut = Render<MascotPage>(p => p.Add(x => x.State, State(10.0, 95.0)));

        var root = cut.Find("div.mascot");
        Assert.Contains("mascot--near-limit", root.ClassList);
    }

    [Fact]
    public void MascotPage_SinDatosDisponiblesAunNoHuboExito_RenderizaElEstadoNoData()
    {
        var cut = Render<MascotPage>(p => p.Add(x => x.State, WidgetUsageState.Initial));

        var root = cut.Find("div.mascot");
        Assert.Contains("mascot--no-data", root.ClassList);
    }

    [Fact]
    public void MascotPage_ConSnapshotUnauthorized_RenderizaElEstadoNoDataNoElUmbralAnterior()
    {
        // Tal como lo deja ScreenNavigator.Apply() corregido en este ciclo:
        // Unauthorized resetea Session/Weekly a Unavailable, así que
        // MascotPage debe mostrar "sin datos", nunca la severidad de un
        // ciclo anterior con éxito.
        var cut = Render<MascotPage>(p => p.Add(x => x.State,
            State(sessionPercentage: null, weeklyPercentage: null, status: UsageSnapshotStatus.Unauthorized)));

        var root = cut.Find("div.mascot");
        Assert.Contains("mascot--no-data", root.ClassList);
    }

    [Fact]
    public void MascotPage_RenderizaUnIconoYUnaEtiquetaDentroDelContenedor()
    {
        var cut = Render<MascotPage>(p => p.Add(x => x.State, State(10.0, 20.0)));

        Assert.NotEmpty(cut.Find("span.mascot__icon").TextContent);
        Assert.NotEmpty(cut.Find("span.mascot__label").TextContent);
    }

    [Fact]
    public void MascotPage_ImplementaIWidgetScreenConScreenIdMascot()
    {
        var cut = Render<MascotPage>(p => p.Add(x => x.State, State(10.0, 20.0)));

        var screen = Assert.IsAssignableFrom<IWidgetScreen>(cut.Instance);
        Assert.Equal("mascot", screen.ScreenId);
    }
}
