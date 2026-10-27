using System.IO;
using ClaudeMeter.Desktop.Configuration;
using ClaudeMeter.Desktop.Polling;
using ClaudeMeter.Desktop.Startup;
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

    private static (TrayIconService Tray, PollingControlService PollingControl, ClickThroughService ClickThrough, AutoStartService AutoStart, UsagePollingCoordinator Coordinator, FakeUsageDataSource Fake)
        CreateService(bool autoStartEnabled = false)
    {
        var fake = new FakeUsageDataSource(UsageSnapshot.Success(SampleHeaders, SampleHeaders));
        var coordinator = new UsagePollingCoordinator(fake, LongInterval, NullLogger<UsagePollingCoordinator>.Instance);
        var pollingControl = new PollingControlService();
        pollingControl.AttachCoordinator(coordinator);
        var clickThrough = new ClickThroughService(NullLogger<ClickThroughService>.Instance);
        var initialConfig = AppConfig.Default with { AutoStartEnabled = autoStartEnabled };
        var autoStart = new AutoStartService(initialConfig, new AppConfigStore(NullLogger<AppConfigStore>.Instance), NullLogger<AutoStartService>.Instance);
        var tray = new TrayIconService(pollingControl, clickThrough, autoStart, NullLogger<TrayIconService>.Instance);

        return (tray, pollingControl, clickThrough, autoStart, coordinator, fake);
    }

    [Fact]
    public void Constructor_EsInstanciableSinSesionDeEscritorioReal_YConstruyeLosCincoItemsDelMenu()
    {
        // El propio constructor de producción está diseñado para no tocar
        // NotifyIcon.Visible (solo construye el menú y suscribe eventos) --
        // si tocase Visible=true aquí, esta llamada ya fallaría/lanzaría en
        // el entorno de CI sin bandeja real. Se verifica además que los
        // cinco ítems del menú (AC: "Pausar/Reanudar, Recargar,
        // click-through, autoarranque (F6/US-3), Salir") quedan realmente
        // construidos.
        UsagePollingCoordinator? coordinator = null;
        TrayIconService? tray = null;

        var exception = Record.Exception(() =>
        {
            (tray, _, _, _, coordinator, _) = CreateService();
        });

        Assert.Null(exception);
        Assert.NotNull(tray);
        Assert.Equal("Pausar", tray!.PauseResumeItemForTests.Text);
        Assert.Equal("Recargar", tray.ReloadItemForTests.Text);
        Assert.Equal("Ignorar clics", tray.ClickThroughItemForTests.Text);
        Assert.Equal("Iniciar con Windows", tray.AutoStartItemForTests.Text);
        Assert.Equal("Salir", tray.ExitItemForTests.Text);

        coordinator?.Dispose();
        tray?.Dispose();
    }

    [Fact]
    public void PauseResumeItem_AlConstruir_MuestraElTextoPausar()
    {
        var (tray, _, _, _, coordinator, _) = CreateService();

        Assert.Equal("Pausar", tray.PauseResumeItemForTests.Text);

        coordinator.Dispose();
        tray.Dispose();
    }

    [Fact]
    public void PauseResumeItem_PerformClick_AlternaTextoYIsPausedDePausarAReanudar()
    {
        var (tray, pollingControl, _, _, coordinator, _) = CreateService();

        tray.PauseResumeItemForTests.PerformClick();

        Assert.True(pollingControl.IsPaused);
        Assert.Equal("Reanudar", tray.PauseResumeItemForTests.Text);

        coordinator.Dispose();
        tray.Dispose();
    }

    [Fact]
    public void PauseResumeItem_PerformClickDosVeces_VuelveAPausarConSuTextoOriginal()
    {
        var (tray, pollingControl, _, _, coordinator, _) = CreateService();

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
        var (tray, pollingControl, _, _, coordinator, fake) = CreateService();

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
        var (tray, _, _, _, coordinator, _) = CreateService();

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
        var (tray, _, clickThrough, _, coordinator, _) = CreateService();

        clickThrough.SetEnabledForTests(true);

        Assert.True(tray.ClickThroughItemForTests.Checked);

        clickThrough.SetEnabledForTests(false);

        Assert.False(tray.ClickThroughItemForTests.Checked);

        coordinator.Dispose();
        tray.Dispose();
    }

    [Fact]
    public void AutoStartItem_AlConstruirConAutoStartEnabledFalse_NoEstaMarcado()
    {
        var (tray, _, _, _, coordinator, _) = CreateService(autoStartEnabled: false);

        Assert.False(tray.AutoStartItemForTests.Checked);

        coordinator.Dispose();
        tray.Dispose();
    }

    [Fact]
    public void Constructor_ConAutoStartEnabledTruePersistido_AunNoMarcaElItem()
    {
        // AC F6/US-3: a diferencia de pausa/click-through, el Checked
        // inicial de este ítem sí debe reflejar el estado ya persistido en
        // config.json -- pero eso ocurre en Initialize() (línea 90 de
        // TrayIconService), no en el constructor. Initialize() queda fuera
        // de esta cobertura (toca NotifyIcon.Visible/Icon reales -- mismo
        // gap ya documentado en el XMLDoc de la clase), así que este test
        // solo confirma la mitad determinista y testeable sin escritorio
        // real: el constructor por sí solo NO adelanta el Checked.
        var (tray, _, _, _, coordinator, _) = CreateService(autoStartEnabled: true);

        Assert.False(tray.AutoStartItemForTests.Checked);

        coordinator.Dispose();
        tray.Dispose();
    }

    [Fact]
    public void AutoStartItem_PerformClick_InvocaToggleYActualizaElChecked()
    {
        // AutoStartService.Toggle() -> SetEnabled(!IsEnabled) -> levanta
        // StateChanged, al que TrayIconService está suscrito
        // (OnAutoStartStateChanged) -- mismo patrón ya cubierto para
        // ClickThroughItem. Redirige config.json a una ruta temporal
        // (ConfigPathForTests) para no mutar el config.json real de la
        // máquina que ejecuta el test.
        var (tray, _, _, autoStart, coordinator, _) = CreateService(autoStartEnabled: false);
        var tempDir = Path.Combine(Path.GetTempPath(), "ClaudeMeterTests", Guid.NewGuid().ToString("N"));
        autoStart.ConfigPathForTests = Path.Combine(tempDir, "config.json");

        try
        {
            tray.AutoStartItemForTests.PerformClick();

            Assert.True(tray.AutoStartItemForTests.Checked);
            Assert.True(autoStart.IsEnabled);
        }
        finally
        {
            coordinator.Dispose();
            tray.Dispose();
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public void ExitItem_Existe_ConElTextoSalirYSinInvocarPerformClick()
    {
        // AC del menú de bandeja: "Salir" debe existir como último elemento.
        // Deliberadamente no se llama a PerformClick() aquí -- dispararía
        // Application.Current.Shutdown() real (ver XMLDoc de la clase).
        var (tray, _, _, _, coordinator, _) = CreateService();

        Assert.Equal("Salir", tray.ExitItemForTests.Text);

        coordinator.Dispose();
        tray.Dispose();
    }

    [Fact]
    public void Dispose_LlamadoDosVeces_EsIdempotenteYNoLanza()
    {
        var (tray, _, _, _, coordinator, _) = CreateService();

        tray.Dispose();
        var exception = Record.Exception(() => tray.Dispose());

        Assert.Null(exception);
        coordinator.Dispose();
    }

    [Fact]
    public void Dispose_DesuscribeLosEventosDePollingControlServiceYClickThroughServiceYAutoStartService()
    {
        // Tras Dispose(), el menú ya no debe reaccionar a más transiciones
        // de estado -- confirma que Dispose() desuscribe de verdad
        // PauseStateChanged/StateChanged (incluido el de AutoStartService,
        // F6/US-3), no solo libera el NotifyIcon.
        var (tray, pollingControl, clickThrough, autoStart, coordinator, _) = CreateService();
        var tempDir = Path.Combine(Path.GetTempPath(), "ClaudeMeterTests", Guid.NewGuid().ToString("N"));
        autoStart.ConfigPathForTests = Path.Combine(tempDir, "config.json");
        tray.Dispose();

        try
        {
            pollingControl.Pause();
            clickThrough.SetEnabledForTests(true);
            autoStart.SetEnabled(true);

            Assert.Equal("Pausar", tray.PauseResumeItemForTests.Text); // no se actualizó tras Dispose()
            Assert.False(tray.ClickThroughItemForTests.Checked); // idem
            Assert.False(tray.AutoStartItemForTests.Checked); // idem
        }
        finally
        {
            coordinator.Dispose();
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }
}
