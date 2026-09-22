using ClaudeMeter.Desktop.Polling;
using ClaudeMeter.Desktop.Tests.TestDoubles;
using ClaudeMeter.Desktop.Tray;
using ClaudeMeter.Desktop.Windowing;
using ClaudeMeter.Domain.Usage;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClaudeMeter.Desktop.Tests.Tray;

/// <summary>
/// Pruebas de <see cref="TrayIconService"/> (US-2, F3/Ciclo B). El propio
/// constructor de producción está diseñado explícitamente para no tocar
/// <c>NotifyIcon.Visible</c> (solo construye el menú y suscribe eventos), lo
/// que lo hace instanciable en xUnit sin sesión de escritorio real -- mismo
/// criterio explícito del Implementation Plan de diseño ("Definition of
/// Done: sin interacción de ratón real de Windows"). Los tests invocan
/// <c>PerformClick()</c> sobre los <c>ToolStripMenuItem</c> expuestos por los
/// ganchos <c>internal</c> <c>*ForTests</c>, nunca un clic físico real.
///
/// <see cref="TrayIconService.Initialize"/> (que sí activa
/// <c>NotifyIcon.Visible = true</c> y resuelve el icono real del ejecutable)
/// y <c>ExitItemForTests.PerformClick()</c> (que dispararía
/// <see cref="System.Windows.Application.Shutdown()"/> real, terminando el
/// propio proceso de test) quedan deliberadamente fuera de esta cobertura --
/// gap aceptado, mismo criterio que <c>WindowCloseService.RequestClose</c>
/// con una ventana real (ver Gaps del informe de testing).
/// </summary>
public sealed class TrayIconServiceTests
{
    private static readonly RawRateLimitHeaders SampleHeaders = new("allowed", "0.1", "0.9", "1789560000");
    private static readonly TimeSpan LongInterval = TimeSpan.FromHours(1);

    private static (TrayIconService Tray, PollingControlService PollingControl, ClickThroughService ClickThrough, UsagePollingCoordinator Coordinator, FakeUsageDataSource Fake)
        CreateService()
    {
        var fake = new FakeUsageDataSource(UsageSnapshot.Success(SampleHeaders, SampleHeaders));
        var coordinator = new UsagePollingCoordinator(fake, LongInterval, NullLogger<UsagePollingCoordinator>.Instance);
        var pollingControl = new PollingControlService();
        pollingControl.AttachCoordinator(coordinator);
        var clickThrough = new ClickThroughService(NullLogger<ClickThroughService>.Instance);
        var tray = new TrayIconService(pollingControl, clickThrough, NullLogger<TrayIconService>.Instance);

        return (tray, pollingControl, clickThrough, coordinator, fake);
    }

    [Fact]
    public void Constructor_EsInstanciableSinSesionDeEscritorioReal_YConstruyeLosCuatroItemsDelMenu()
    {
        // El propio constructor de producción está diseñado para no tocar
        // NotifyIcon.Visible (solo construye el menú y suscribe eventos) --
        // si tocase Visible=true aquí, esta llamada ya fallaría/lanzaría en
        // el entorno de CI sin bandeja real. Se verifica además que los
        // cuatro ítems del menú (AC: "Pausar/Reanudar, Recargar,
        // click-through, Salir") quedan realmente construidos.
        UsagePollingCoordinator? coordinator = null;
        TrayIconService? tray = null;

        var exception = Record.Exception(() =>
        {
            (tray, _, _, coordinator, _) = CreateService();
        });

        Assert.Null(exception);
        Assert.NotNull(tray);
        Assert.Equal("Pausar", tray!.PauseResumeItemForTests.Text);
        Assert.Equal("Recargar", tray.ReloadItemForTests.Text);
        Assert.Equal("Ignorar clics", tray.ClickThroughItemForTests.Text);
        Assert.Equal("Salir", tray.ExitItemForTests.Text);

        coordinator?.Dispose();
        tray?.Dispose();
    }

    [Fact]
    public void PauseResumeItem_AlConstruir_MuestraElTextoPausar()
    {
        var (tray, _, _, coordinator, _) = CreateService();

        Assert.Equal("Pausar", tray.PauseResumeItemForTests.Text);

        coordinator.Dispose();
        tray.Dispose();
    }

    [Fact]
    public void PauseResumeItem_PerformClick_AlternaTextoYIsPausedDePausarAReanudar()
    {
        var (tray, pollingControl, _, coordinator, _) = CreateService();

        tray.PauseResumeItemForTests.PerformClick();

        Assert.True(pollingControl.IsPaused);
        Assert.Equal("Reanudar", tray.PauseResumeItemForTests.Text);

        coordinator.Dispose();
        tray.Dispose();
    }

    [Fact]
    public void PauseResumeItem_PerformClickDosVeces_VuelveAPausarConSuTextoOriginal()
    {
        var (tray, pollingControl, _, coordinator, _) = CreateService();

        tray.PauseResumeItemForTests.PerformClick(); // Pausar -> Reanudar
        tray.PauseResumeItemForTests.PerformClick(); // Reanudar -> Pausar

        Assert.False(pollingControl.IsPaused);
        Assert.Equal("Pausar", tray.PauseResumeItemForTests.Text);

        coordinator.Dispose();
        tray.Dispose();
    }

    [Fact]
    public async Task ReloadItem_PerformClick_DisparaUnPollAdicionalSinTocarIsPaused()
    {
        var (tray, pollingControl, _, coordinator, fake) = CreateService();

        tray.ReloadItemForTests.PerformClick();

        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (fake.CallCount == 0 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(5);
        }

        Assert.Equal(1, fake.CallCount);
        Assert.False(pollingControl.IsPaused);

        coordinator.Dispose();
        tray.Dispose();
    }

    [Fact]
    public void ClickThroughItem_AlConstruir_NoEstaMarcado()
    {
        var (tray, _, _, coordinator, _) = CreateService();

        Assert.False(tray.ClickThroughItemForTests.Checked);

        coordinator.Dispose();
        tray.Dispose();
    }

    [Fact]
    public void ClickThroughItem_TrasSetEnabledForTests_ReflejaElEstadoDeClickThrough()
    {
        // Verifica la suscripción real de TrayIconService a
        // ClickThroughService.StateChanged sin pasar por user32.dll real --
        // SetEnabledForTests es el gancho internal expuesto exactamente para
        // esto (ver ClickThroughServiceTests).
        var (tray, _, clickThrough, coordinator, _) = CreateService();

        clickThrough.SetEnabledForTests(true);

        Assert.True(tray.ClickThroughItemForTests.Checked);

        clickThrough.SetEnabledForTests(false);

        Assert.False(tray.ClickThroughItemForTests.Checked);

        coordinator.Dispose();
        tray.Dispose();
    }

    [Fact]
    public void ExitItem_Existe_ConElTextoSalirYSinInvocarPerformClick()
    {
        // AC del menú de bandeja: "Salir" debe existir como cuarto elemento.
        // Deliberadamente no se llama a PerformClick() aquí -- dispararía
        // Application.Current.Shutdown() real (ver XMLDoc de la clase).
        var (tray, _, _, coordinator, _) = CreateService();

        Assert.Equal("Salir", tray.ExitItemForTests.Text);

        coordinator.Dispose();
        tray.Dispose();
    }

    [Fact]
    public void Dispose_LlamadoDosVeces_EsIdempotenteYNoLanza()
    {
        var (tray, _, _, coordinator, _) = CreateService();

        tray.Dispose();
        var exception = Record.Exception(() => tray.Dispose());

        Assert.Null(exception);
        coordinator.Dispose();
    }

    [Fact]
    public void Dispose_DesuscribeLosEventosDePollingControlServiceYClickThroughService()
    {
        // Tras Dispose(), el menú ya no debe reaccionar a más transiciones
        // de estado -- confirma que Dispose() desuscribe de verdad
        // PauseStateChanged/StateChanged, no solo libera el NotifyIcon.
        var (tray, pollingControl, clickThrough, coordinator, _) = CreateService();
        tray.Dispose();

        pollingControl.Pause();
        clickThrough.SetEnabledForTests(true);

        Assert.Equal("Pausar", tray.PauseResumeItemForTests.Text); // no se actualizó tras Dispose()
        Assert.False(tray.ClickThroughItemForTests.Checked); // idem

        coordinator.Dispose();
    }
}
