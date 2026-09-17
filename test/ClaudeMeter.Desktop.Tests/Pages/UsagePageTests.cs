using Bunit;
using ClaudeMeter.Application.Abstractions;
using ClaudeMeter.Desktop.Pages;
using ClaudeMeter.Desktop.Tests.TestDoubles;
using ClaudeMeter.Domain.Usage;
using Microsoft.Extensions.DependencyInjection;

namespace ClaudeMeter.Desktop.Tests.Pages;

/// <summary>
/// Pruebas bUnit de <see cref="UsagePage"/> (US-2/US-4): montaje con
/// <see cref="IUsageDataSource"/> simulado, aplicación de snapshots vía los
/// ganchos <c>internal</c> <c>ApplyForTests</c>/<c>IsPollingActiveForTests</c>
/// (expuestos por diseño para no depender de un timer real de 60s), y
/// verificación de que el componente libera su polling al desmontarse. Usa
/// la API v2 de bUnit (<see cref="BunitContext"/> + <c>Render&lt;T&gt;</c> +
/// <c>DisposeComponentsAsync()</c>), nunca la v1.
/// </summary>
public sealed class UsagePageTests : BunitContext
{
    private static readonly DateTimeOffset Now = new(2026, 9, 16, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void UsagePage_AlMontarse_InvocaGetUsageAsyncYRenderizaElPrimerSnapshotDeExito()
    {
        // AC de US-4: se reutiliza directamente IUsageDataSource (sin
        // reimplementar la llamada) y el primer fetch se dispara al montar,
        // sin que el usuario recargue nada manualmente.
        var fake = new FakeUsageDataSource(UsageSnapshot.Success(
            new RawRateLimitHeaders("allowed", "0.10", "0.90", null),
            new RawRateLimitHeaders("allowed", "0.42", "0.58", null)));
        Services.AddSingleton<IUsageDataSource>(fake);

        var cut = Render<UsagePage>();

        // WaitForAssertion: el primer fetch de Start() se dispara de forma
        // asíncrona (evento del coordinador -> InvokeAsync); se evita
        // depender de que la continuación corra síncronamente.
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("10%", cut.Markup);
            Assert.Contains("42%", cut.Markup);
        });
        Assert.True(fake.CallCount >= 1);
    }

    [Fact]
    public void UsagePage_ConSnapshotSuccessYAmbosPercentages_MuestraDosBarrasConSuColor()
    {
        Services.AddSingleton<IUsageDataSource>(new FakeUsageDataSource(UsageSnapshot.RequestFailed()));
        var cut = Render<UsagePage>();

        var snapshot = UsageSnapshot.Success(
            session: new RawRateLimitHeaders("allowed", "0.75", "0.25", null), // Warning/ámbar
            weekly: new RawRateLimitHeaders("allowed", "0.95", "0.05", null)); // Critical/rojo

        cut.InvokeAsync(() => cut.Instance.ApplyForTests(snapshot, Now));
        cut.Render();

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
        // AC de US-3: sesión y semana usan exactamente la misma regla de umbral.
        Services.AddSingleton<IUsageDataSource>(new FakeUsageDataSource(UsageSnapshot.RequestFailed()));
        var cut = Render<UsagePage>();

        var snapshot = UsageSnapshot.Success(
            session: new RawRateLimitHeaders("allowed", "0.95", "0.05", null),
            weekly: new RawRateLimitHeaders("allowed", "0.95", "0.05", null));

        cut.InvokeAsync(() => cut.Instance.ApplyForTests(snapshot, Now));
        cut.Render();

        var bars = cut.FindAll("div.usage-bar");
        Assert.Contains("usage-bar--red", bars[0].ClassList);
        Assert.Contains("usage-bar--red", bars[1].ClassList);
    }

    [Fact]
    public void UsagePage_ConPercentageUsedNuloEnUnaVentana_MuestraEsaBarraComoNoDisponible()
    {
        Services.AddSingleton<IUsageDataSource>(new FakeUsageDataSource(UsageSnapshot.RequestFailed()));
        var cut = Render<UsagePage>();

        var snapshot = UsageSnapshot.Success(
            session: new RawRateLimitHeaders("allowed", Utilization: null, "0.90", null), // dato no parseable
            weekly: new RawRateLimitHeaders("allowed", "0.20", "0.80", null));

        cut.InvokeAsync(() => cut.Instance.ApplyForTests(snapshot, Now));
        cut.Render();

        var bars = cut.FindAll("div.usage-bar");
        Assert.Contains("usage-bar--neutral", bars[0].ClassList);
        Assert.Contains("No disponible", bars[0].TextContent);
        Assert.Contains("20%", bars[1].TextContent);
    }

    [Theory]
    [InlineData(nameof(UsageSnapshotStatus.TokenUnavailable))]
    [InlineData(nameof(UsageSnapshotStatus.Unauthorized))]
    [InlineData(nameof(UsageSnapshotStatus.RequestFailed))]
    public void UsagePage_ConSnapshotDeFalloYSinExitoPrevio_MuestraAmbasBarrasNoDisponible(string failureKind)
    {
        Services.AddSingleton<IUsageDataSource>(new FakeUsageDataSource(UsageSnapshot.RequestFailed()));
        var cut = Render<UsagePage>();

        var failureSnapshot = failureKind switch
        {
            nameof(UsageSnapshotStatus.TokenUnavailable) => UsageSnapshot.TokenUnavailable(),
            nameof(UsageSnapshotStatus.Unauthorized) => UsageSnapshot.Unauthorized(),
            nameof(UsageSnapshotStatus.RequestFailed) => UsageSnapshot.RequestFailed(),
            _ => throw new ArgumentOutOfRangeException(nameof(failureKind)),
        };

        cut.InvokeAsync(() => cut.Instance.ApplyForTests(failureSnapshot, Now));
        cut.Render();

        var bars = cut.FindAll("div.usage-bar");
        Assert.All(bars, bar => Assert.Contains("No disponible", bar.TextContent));
        Assert.DoesNotContain("desactualizado", cut.Markup);
    }

    [Fact]
    public void UsagePage_TrasUnCicloExitosoSeguidoDeUnFallo_ConservaElValorAnteriorMarcadoDesactualizado()
    {
        Services.AddSingleton<IUsageDataSource>(new FakeUsageDataSource(UsageSnapshot.RequestFailed()));
        var cut = Render<UsagePage>();

        var successSnapshot = UsageSnapshot.Success(
            session: new RawRateLimitHeaders("allowed", "0.33", "0.67", null),
            weekly: new RawRateLimitHeaders("allowed", "0.44", "0.56", null));
        cut.InvokeAsync(() => cut.Instance.ApplyForTests(successSnapshot, Now));
        cut.Render();

        Assert.Contains("33%", cut.Markup);
        Assert.DoesNotContain("desactualizado", cut.Markup);

        cut.InvokeAsync(() => cut.Instance.ApplyForTests(UsageSnapshot.Unauthorized(), Now));
        cut.Render();

        // El AC exige conservar el último valor válido, marcado como
        // potencialmente desactualizado -- no debe caer a "No disponible".
        Assert.Contains("33%", cut.Markup);
        Assert.Contains("desactualizado", cut.Markup);
    }

    [Fact]
    public void UsagePage_TrasDosCiclosConsecutivosConExito_MuestraSiempreElUltimoSnapshotSinMezclar()
    {
        Services.AddSingleton<IUsageDataSource>(new FakeUsageDataSource(UsageSnapshot.RequestFailed()));
        var cut = Render<UsagePage>();

        var firstSnapshot = UsageSnapshot.Success(
            session: new RawRateLimitHeaders("allowed", "0.20", "0.80", null),
            weekly: new RawRateLimitHeaders("allowed", "0.30", "0.70", null));
        cut.InvokeAsync(() => cut.Instance.ApplyForTests(firstSnapshot, Now));
        cut.Render();
        Assert.Contains("20%", cut.Markup);

        var secondSnapshot = UsageSnapshot.Success(
            session: new RawRateLimitHeaders("allowed", "0.55", "0.45", null),
            weekly: new RawRateLimitHeaders("allowed", "0.60", "0.40", null));
        cut.InvokeAsync(() => cut.Instance.ApplyForTests(secondSnapshot, Now));
        cut.Render();

        Assert.DoesNotContain("20%", cut.Markup);
        Assert.DoesNotContain("30%", cut.Markup);
        Assert.Contains("55%", cut.Markup);
        Assert.Contains("60%", cut.Markup);
    }

    [Fact]
    public async Task UsagePage_TrasDesmontarse_DetieneElPollingSubyacente()
    {
        // AC de US-4: al cerrarse el componente (IDisposable), el
        // timer/temporizador subyacente se detiene y no queda programando
        // más llamadas HTTP.
        var fake = new FakeUsageDataSource(UsageSnapshot.RequestFailed());
        Services.AddSingleton<IUsageDataSource>(fake);
        var cut = Render<UsagePage>();
        // Se guarda la referencia .NET directa al componente: tras
        // DisposeComponentsAsync(), bUnit ya no permite acceder a
        // cut.Instance (ComponentDisposedException), pero el objeto C#
        // subyacente sigue siendo válido para comprobar que Dispose()
        // realmente paró el coordinador (no es lo mismo que "bUnit ya no
        // deja inspeccionarlo").
        var page = cut.Instance;

        Assert.True(page.IsPollingActiveForTests);

        await DisposeComponentsAsync();

        Assert.False(page.IsPollingActiveForTests);
    }
}
