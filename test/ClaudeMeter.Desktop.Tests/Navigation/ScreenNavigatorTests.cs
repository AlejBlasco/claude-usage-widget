using Bunit;
using ClaudeMeter.Application.Abstractions;
using ClaudeMeter.Desktop.Audio;
using ClaudeMeter.Desktop.Configuration;
using ClaudeMeter.Desktop.Navigation;
using ClaudeMeter.Desktop.Pages;
using ClaudeMeter.Desktop.Polling;
using ClaudeMeter.Desktop.Tests.TestDoubles;
using ClaudeMeter.Desktop.Windowing;
using ClaudeMeter.Domain.Usage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClaudeMeter.Desktop.Tests.Navigation;

/// <summary>
/// Pruebas bUnit de <see cref="ScreenNavigator"/> (F3/Ciclo C, US-1, issue
/// #18): desde este ciclo, <c>ScreenNavigator</c> es el nuevo
/// <c>RootComponent</c> real -- absorbe íntegramente el ciclo de vida
/// (polling/chime/tema/JS interop de arrastre-resize-cierre) que hasta F3
/// Ciclo B gestionaba <c>UsagePage</c> directamente. Este fichero es la
/// migración exigida por el paso 12 del Implementation Plan del documento de
/// diseño: recibe literalmente los tests que dependían de
/// <c>ApplyForTests</c>/<c>IsPollingActiveForTests</c> (polling, chime,
/// tema, botón de cierre, pausa, desmontaje) del antiguo
/// <c>UsagePageTests.cs</c>, montando ahora <see cref="ScreenNavigator"/> en
/// vez de <see cref="UsagePage"/>. También cubre el ciclado entre
/// <see cref="IWidgetScreen"/> simuladas (paso 13) y la corrección de la
/// rama <c>Unauthorized</c> de <c>Apply()</c> introducida en este ciclo.
///
/// <see cref="RegisterCoreServices"/> reutiliza el mismo patrón que ya
/// aplicaba <c>UsagePageTests</c> de F2/F3 Ciclos A/B (mismas dependencias,
/// ahora las exige <c>ScreenNavigator</c> en vez de <c>UsagePage</c>).
/// <see cref="BunitContext.JSInterop"/> se pone en <see cref="JSRuntimeMode.Loose"/>
/// porque <c>OnAfterRenderAsync</c> invoca los tres listeners JS de
/// arrastre/resize/cierre en cada render -- ningún test de este fichero
/// verifica esos listeners en sí (requeriría un navegador real), solo que
/// su registro no rompe el montaje del componente.
/// </summary>
public sealed class ScreenNavigatorTests : BunitContext
{
    private static readonly DateTimeOffset Now = new(2026, 9, 16, 10, 0, 0, TimeSpan.Zero);

    private void RegisterCoreServices(IUsageDataSource dataSource, AppConfig? config = null, IChimePlayer? chimePlayer = null)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton(dataSource);
        Services.AddSingleton(config ?? AppConfig.Default);
        Services.AddSingleton(chimePlayer ?? new FakeChimePlayer());
        Services.AddSingleton(new WindowDragService(
            new AppConfigStore(NullLogger<AppConfigStore>.Instance), NullLogger<WindowDragService>.Instance));
        Services.AddSingleton(new WindowResizeService(NullLogger<WindowResizeService>.Instance));
        Services.AddSingleton(new PollingControlService());
        Services.AddSingleton(new WindowCloseService(NullLogger<WindowCloseService>.Instance));
    }

    // ------------------------------------------------------------------
    // Polling / primer fetch (migrados de UsagePageTests)
    // ------------------------------------------------------------------

    [Fact]
    public void ScreenNavigator_AlMontarse_InvocaGetUsageAsyncYRenderizaElPrimerSnapshotDeExitoEnLaPantallaPorDefecto()
    {
        // AC de US-4 (heredada): se reutiliza directamente IUsageDataSource,
        // y el primer fetch se dispara al montar. La pantalla por defecto
        // (DefaultScreens[0]) sigue siendo UsagePage -- cero regresión del
        // estado inicial (Cross-Cutting Concerns del documento de diseño).
        var fake = new FakeUsageDataSource(UsageSnapshot.Success(
            new RawRateLimitHeaders("allowed", "0.10", "0.90", null),
            new RawRateLimitHeaders("allowed", "0.42", "0.58", null)));
        RegisterCoreServices(fake);

        var cut = Render<ScreenNavigator>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("10%", cut.Markup);
            Assert.Contains("42%", cut.Markup);
        });
        Assert.True(fake.CallCount >= 1);
    }

    [Fact]
    public async Task ScreenNavigator_TrasDesmontarse_DetieneElPollingSubyacente()
    {
        var fake = new FakeUsageDataSource(UsageSnapshot.RequestFailed());
        RegisterCoreServices(fake);
        var cut = Render<ScreenNavigator>();
        var navigator = cut.Instance;

        Assert.True(navigator.IsPollingActiveForTests);

        await DisposeComponentsAsync();

        Assert.False(navigator.IsPollingActiveForTests);
    }

    [Fact]
    public void ScreenNavigator_ConIntervaloCortoEnAppConfig_LoUsaEnVezDeUnValorHardcodeado()
    {
        // F2 Ciclo B (US-1): el intervalo viene de Config.PollingInterval,
        // ahora reenviado por ScreenNavigator -- AppConfig.Default usa 60s,
        // que nunca completaría un segundo ciclo dentro del timeout de este
        // test; solo un intervalo corto inyectado explícitamente puede
        // hacerlo.
        var fake = new FakeUsageDataSource(UsageSnapshot.RequestFailed());
        var shortIntervalConfig = AppConfig.Default with { PollingInterval = TimeSpan.FromMilliseconds(30) };
        RegisterCoreServices(fake, config: shortIntervalConfig);

        var cut = Render<ScreenNavigator>();

        cut.WaitForAssertion(() => Assert.True(fake.CallCount >= 2, $"CallCount fue {fake.CallCount}"), timeout: TimeSpan.FromSeconds(2));
    }

    // ------------------------------------------------------------------
    // Corrección de Apply()/Unauthorized (nuevo en este ciclo) + "ciclos
    // consecutivos"/"fallo transitorio" migrados de UsagePageTests
    // ------------------------------------------------------------------

    private static UsageSnapshot SuccessSnapshot(string sessionPercentage, string weeklyPercentage) => UsageSnapshot.Success(
        session: new RawRateLimitHeaders("allowed", sessionPercentage, "0", null),
        weekly: new RawRateLimitHeaders("allowed", weeklyPercentage, "0", null));

    [Fact]
    public void ScreenNavigator_TrasUnCicloExitosoSeguidoDeUnFalloTransitorio_ConservaElValorAnteriorMarcadoDesactualizado()
    {
        RegisterCoreServices(new FakeUsageDataSource(UsageSnapshot.RequestFailed()));
        var cut = Render<ScreenNavigator>();

        cut.InvokeAsync(() => cut.Instance.ApplyForTests(SuccessSnapshot("0.33", "0.44"), Now));
        cut.Render();
        Assert.Contains("33%", cut.Markup);
        Assert.DoesNotContain("desactualizado", cut.Markup);

        cut.InvokeAsync(() => cut.Instance.ApplyForTests(UsageSnapshot.RequestFailed(), Now));
        cut.Render();

        // AC heredado: conserva el último valor válido, marcado como
        // potencialmente desactualizado -- no cae a "No disponible".
        Assert.Contains("33%", cut.Markup);
        Assert.Contains("desactualizado", cut.Markup);
    }

    [Fact]
    public void ScreenNavigator_TrasDosCiclosConsecutivosConExito_MuestraSiempreElUltimoSnapshotSinMezclar()
    {
        RegisterCoreServices(new FakeUsageDataSource(UsageSnapshot.RequestFailed()));
        var cut = Render<ScreenNavigator>();

        cut.InvokeAsync(() => cut.Instance.ApplyForTests(SuccessSnapshot("0.20", "0.30"), Now));
        cut.Render();
        Assert.Contains("20%", cut.Markup);

        cut.InvokeAsync(() => cut.Instance.ApplyForTests(SuccessSnapshot("0.55", "0.60"), Now));
        cut.Render();

        Assert.DoesNotContain("20%", cut.Markup);
        Assert.DoesNotContain("30%", cut.Markup);
        Assert.Contains("55%", cut.Markup);
        Assert.Contains("60%", cut.Markup);
    }

    [Fact]
    public void ScreenNavigator_ConUnauthorizedTrasUnCicloExitoso_SustituyeLasBarrasPorReauthNoticeSinMarcarDesactualizado()
    {
        RegisterCoreServices(new FakeUsageDataSource(UsageSnapshot.RequestFailed()));
        var cut = Render<ScreenNavigator>();

        cut.InvokeAsync(() => cut.Instance.ApplyForTests(SuccessSnapshot("0.33", "0.44"), Now));
        cut.Render();
        Assert.Contains("33%", cut.Markup);

        cut.InvokeAsync(() => cut.Instance.ApplyForTests(UsageSnapshot.Unauthorized(), Now));
        cut.Render();

        Assert.Empty(cut.FindAll("div.usage-bar"));
        Assert.DoesNotContain("33%", cut.Markup);
        Assert.DoesNotContain("desactualizado", cut.Markup);
        Assert.NotEmpty(cut.FindAll("div.usage-reauth"));
    }

    [Fact]
    public void ScreenNavigator_ConRamaUnauthorized_ReseteaSessionYWeeklyAUnavailableEnVezDeConservarElUltimoValorParseado()
    {
        // Test explícito exigido por el paso 12 del Implementation Plan de
        // diseño: la CORRECCIÓN de F3/Ciclo C respecto a F2 -- verificada
        // aquí indirectamente montando ScreenNavigator con ScreenTypes
        // apuntando a MascotPage, y comprobando que un ciclo
        // Success->Unauthorized resuelve NoData (no la severidad Critical
        // del ciclo anterior). Si Apply() no reseteara _session/_weekly,
        // MascotStateClassifier vería el 95% del último snapshot exitoso y
        // resolvería NearLimit en vez de NoData -- exactamente el bug que
        // este ciclo corrige (ver Technology Choices del documento de
        // diseño).
        RegisterCoreServices(new FakeUsageDataSource(UsageSnapshot.RequestFailed()));
        var cut = Render<ScreenNavigator>(p => p.Add(x => x.ScreenTypes, new[] { typeof(MascotPage) }));

        var criticalSnapshot = UsageSnapshot.Success(
            session: new RawRateLimitHeaders("allowed", "0.95", "0.05", null),
            weekly: new RawRateLimitHeaders("allowed", "0.10", "0.90", null));
        cut.InvokeAsync(() => cut.Instance.ApplyForTests(criticalSnapshot, Now));
        cut.Render();
        Assert.Contains("mascot--near-limit", cut.Find("div.mascot").ClassList);

        cut.InvokeAsync(() => cut.Instance.ApplyForTests(UsageSnapshot.Unauthorized(), Now));
        cut.Render();

        Assert.Contains("mascot--no-data", cut.Find("div.mascot").ClassList);
        Assert.DoesNotContain("mascot--near-limit", cut.Find("div.mascot").ClassList);
    }

    // ------------------------------------------------------------------
    // Chime (migrados de UsagePageTests, F2/Ciclo B)
    // ------------------------------------------------------------------

    private static UsageSnapshot CriticalSnapshot() => UsageSnapshot.Success(
        session: new RawRateLimitHeaders("allowed", "0.95", "0.05", null),
        weekly: new RawRateLimitHeaders("allowed", "0.10", "0.90", null));

    private static UsageSnapshot NormalSnapshot() => UsageSnapshot.Success(
        session: new RawRateLimitHeaders("allowed", "0.10", "0.90", null),
        weekly: new RawRateLimitHeaders("allowed", "0.10", "0.90", null));

    [Fact]
    public void ScreenNavigator_ConTransicionDeNormalACriticalYChimeHabilitado_DisparaElChimeExactamenteUnaVez()
    {
        var chimePlayer = new FakeChimePlayer();
        RegisterCoreServices(
            new FakeUsageDataSource(UsageSnapshot.RequestFailed()),
            config: AppConfig.Default with { ChimeEnabled = true },
            chimePlayer: chimePlayer);
        var cut = Render<ScreenNavigator>();

        cut.InvokeAsync(() => cut.Instance.ApplyForTests(NormalSnapshot(), Now));
        cut.Render();
        Assert.Equal(0, chimePlayer.PlayCount);

        cut.InvokeAsync(() => cut.Instance.ApplyForTests(CriticalSnapshot(), Now));
        cut.Render();
        Assert.Equal(1, chimePlayer.PlayCount);
    }

    [Fact]
    public void ScreenNavigator_ConElEstadoCriticalQuePersisteEnElSiguienteCiclo_NoRepiteElChime()
    {
        var chimePlayer = new FakeChimePlayer();
        RegisterCoreServices(
            new FakeUsageDataSource(UsageSnapshot.RequestFailed()),
            config: AppConfig.Default with { ChimeEnabled = true },
            chimePlayer: chimePlayer);
        var cut = Render<ScreenNavigator>();

        cut.InvokeAsync(() => cut.Instance.ApplyForTests(CriticalSnapshot(), Now));
        cut.Render();
        Assert.Equal(1, chimePlayer.PlayCount);

        cut.InvokeAsync(() => cut.Instance.ApplyForTests(CriticalSnapshot(), Now));
        cut.Render();
        Assert.Equal(1, chimePlayer.PlayCount);

        cut.InvokeAsync(() => cut.Instance.ApplyForTests(CriticalSnapshot(), Now));
        cut.Render();
        Assert.Equal(1, chimePlayer.PlayCount);
    }

    [Fact]
    public void ScreenNavigator_ConChimeDeshabilitadoEnAppConfig_NuncaSuenaAunEnTransicionACritical()
    {
        var chimePlayer = new FakeChimePlayer();
        RegisterCoreServices(
            new FakeUsageDataSource(UsageSnapshot.RequestFailed()),
            config: AppConfig.Default with { ChimeEnabled = false },
            chimePlayer: chimePlayer);
        var cut = Render<ScreenNavigator>();

        cut.InvokeAsync(() => cut.Instance.ApplyForTests(NormalSnapshot(), Now));
        cut.Render();
        cut.InvokeAsync(() => cut.Instance.ApplyForTests(CriticalSnapshot(), Now));
        cut.Render();

        Assert.Equal(0, chimePlayer.PlayCount);
    }

    [Fact]
    public void ScreenNavigator_ConElPrimerSnapshotYaEnCriticalYChimeHabilitado_SuenaEnElPrimerCiclo()
    {
        var chimePlayer = new FakeChimePlayer();
        RegisterCoreServices(
            new FakeUsageDataSource(UsageSnapshot.RequestFailed()),
            config: AppConfig.Default with { ChimeEnabled = true },
            chimePlayer: chimePlayer);
        var cut = Render<ScreenNavigator>();

        cut.InvokeAsync(() => cut.Instance.ApplyForTests(CriticalSnapshot(), Now));
        cut.Render();

        Assert.Equal(1, chimePlayer.PlayCount);
    }

    [Fact]
    public void ScreenNavigator_ConSoloLaVentanaSemanalEntrandoEnCritical_TambienDisparaElChime()
    {
        var chimePlayer = new FakeChimePlayer();
        RegisterCoreServices(
            new FakeUsageDataSource(UsageSnapshot.RequestFailed()),
            config: AppConfig.Default with { ChimeEnabled = true },
            chimePlayer: chimePlayer);
        var cut = Render<ScreenNavigator>();

        var sessionNormalWeeklyNormal = NormalSnapshot();
        cut.InvokeAsync(() => cut.Instance.ApplyForTests(sessionNormalWeeklyNormal, Now));
        cut.Render();
        Assert.Equal(0, chimePlayer.PlayCount);

        var sessionNormalWeeklyCritical = UsageSnapshot.Success(
            session: new RawRateLimitHeaders("allowed", "0.10", "0.90", null),
            weekly: new RawRateLimitHeaders("allowed", "0.95", "0.05", null));
        cut.InvokeAsync(() => cut.Instance.ApplyForTests(sessionNormalWeeklyCritical, Now));
        cut.Render();

        Assert.Equal(1, chimePlayer.PlayCount);
    }

    [Fact]
    public void ScreenNavigator_ConUnFalloTransitorioMientrasElUltimoEstadoEraCritical_NoReseteaLaDeteccionAlRecuperarse()
    {
        var chimePlayer = new FakeChimePlayer();
        RegisterCoreServices(
            new FakeUsageDataSource(UsageSnapshot.RequestFailed()),
            config: AppConfig.Default with { ChimeEnabled = true },
            chimePlayer: chimePlayer);
        var cut = Render<ScreenNavigator>();

        cut.InvokeAsync(() => cut.Instance.ApplyForTests(CriticalSnapshot(), Now));
        cut.Render();
        Assert.Equal(1, chimePlayer.PlayCount);

        cut.InvokeAsync(() => cut.Instance.ApplyForTests(UsageSnapshot.RequestFailed(), Now));
        cut.Render();
        Assert.Equal(1, chimePlayer.PlayCount);

        cut.InvokeAsync(() => cut.Instance.ApplyForTests(CriticalSnapshot(), Now));
        cut.Render();
        Assert.Equal(1, chimePlayer.PlayCount);
    }

    [Fact]
    public void ScreenNavigator_ConTransicionCriticalANormalYDeVueltaACritical_SuenaDeNuevo()
    {
        var chimePlayer = new FakeChimePlayer();
        RegisterCoreServices(
            new FakeUsageDataSource(UsageSnapshot.RequestFailed()),
            config: AppConfig.Default with { ChimeEnabled = true },
            chimePlayer: chimePlayer);
        var cut = Render<ScreenNavigator>();

        cut.InvokeAsync(() => cut.Instance.ApplyForTests(CriticalSnapshot(), Now));
        cut.Render();
        Assert.Equal(1, chimePlayer.PlayCount);

        cut.InvokeAsync(() => cut.Instance.ApplyForTests(NormalSnapshot(), Now));
        cut.Render();
        Assert.Equal(1, chimePlayer.PlayCount);

        cut.InvokeAsync(() => cut.Instance.ApplyForTests(CriticalSnapshot(), Now));
        cut.Render();
        Assert.Equal(2, chimePlayer.PlayCount);
    }

    // ------------------------------------------------------------------
    // Tema (migrados de UsagePageTests, F3/Ciclo A)
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(AppTheme.Dark, "theme-dark")]
    [InlineData(AppTheme.Light, "theme-light")]
    public void ScreenNavigator_ConTemaEnAppConfig_AplicaLaClaseCssCorrespondienteAlContenedorRaiz(
        AppTheme theme, string expectedCssClass)
    {
        RegisterCoreServices(
            new FakeUsageDataSource(UsageSnapshot.RequestFailed()),
            config: AppConfig.Default with { Theme = theme });

        var cut = Render<ScreenNavigator>();

        var root = cut.Find("div.claudemeter-root");
        Assert.Contains(expectedCssClass, root.ClassList);
    }

    [Fact]
    public void ScreenNavigator_ConTemaClaroYUnauthorized_AplicaLaClaseDeTemaTambienCuandoSeRenderizaReauthNotice()
    {
        RegisterCoreServices(
            new FakeUsageDataSource(UsageSnapshot.RequestFailed()),
            config: AppConfig.Default with { Theme = AppTheme.Light });
        var cut = Render<ScreenNavigator>();

        cut.InvokeAsync(() => cut.Instance.ApplyForTests(UsageSnapshot.Unauthorized(), Now));
        cut.Render();

        var root = cut.Find("div.claudemeter-root");
        Assert.Contains("theme-light", root.ClassList);
        Assert.NotEmpty(cut.FindAll("div.usage-reauth"));
    }

    // ------------------------------------------------------------------
    // Botón de cierre (migrados de UsagePageTests, F3/Ciclo B)
    // ------------------------------------------------------------------

    [Fact]
    public void ScreenNavigator_ConSnapshotSuccess_RenderizaElBotonDeCierreSiempreVisibleEnElMarkup()
    {
        RegisterCoreServices(new FakeUsageDataSource(UsageSnapshot.RequestFailed()));
        var cut = Render<ScreenNavigator>();

        var closeButton = cut.Find("button.claudemeter-close");

        Assert.Equal("Cerrar ClaudeMeter", closeButton.GetAttribute("aria-label"));
    }

    [Fact]
    public void ScreenNavigator_ConUnauthorized_TambienRenderizaElBotonDeCierreSobreReauthNotice()
    {
        RegisterCoreServices(new FakeUsageDataSource(UsageSnapshot.RequestFailed()));
        var cut = Render<ScreenNavigator>();

        cut.InvokeAsync(() => cut.Instance.ApplyForTests(UsageSnapshot.Unauthorized(), Now));
        cut.Render();

        Assert.NotEmpty(cut.FindAll("button.claudemeter-close"));
        Assert.NotEmpty(cut.FindAll("div.usage-reauth"));
    }

    // ------------------------------------------------------------------
    // Pausar / desmontaje (migrados de UsagePageTests, F3/Ciclo B)
    // ------------------------------------------------------------------

    [Fact]
    public void ScreenNavigator_AlPausarDesdePollingControlService_MarcaLasBarrasComoDesactualizadasSinPerderElUltimoValor()
    {
        var fake = new FakeUsageDataSource(UsageSnapshot.RequestFailed());
        RegisterCoreServices(fake);
        var cut = Render<ScreenNavigator>();

        cut.InvokeAsync(() => cut.Instance.ApplyForTests(SuccessSnapshot("0.33", "0.44"), Now));
        cut.Render();
        Assert.DoesNotContain("desactualizado", cut.Markup);

        var pollingControl = Services.GetRequiredService<PollingControlService>();
        pollingControl.Pause();

        cut.WaitForAssertion(() => Assert.Contains("desactualizado", cut.Markup));
        Assert.Contains("33%", cut.Markup);
        Assert.True(pollingControl.IsPaused);
    }

    [Fact]
    public async Task ScreenNavigator_TrasDesmontarse_DesadjuntaSuCoordinadorDePollingControlService()
    {
        var fake = new FakeUsageDataSource(UsageSnapshot.RequestFailed());
        RegisterCoreServices(fake);
        var cut = Render<ScreenNavigator>();
        var pollingControl = Services.GetRequiredService<PollingControlService>();

        await cut.InvokeAsync(() => cut.Instance.ApplyForTests(UsageSnapshot.RequestFailed(), Now));
        cut.Render();

        await DisposeComponentsAsync();

        var exception = Record.Exception(() => pollingControl.Pause());

        Assert.Null(exception);
        Assert.False(pollingControl.IsPaused);
    }

    // ------------------------------------------------------------------
    // Ciclado entre IWidgetScreen simuladas (US-1, paso 13 del Implementation Plan)
    // ------------------------------------------------------------------

    [Fact]
    public void ScreenNavigator_ConDosPantallasSimuladas_RenderizaLaPrimeraAlMontarse()
    {
        RegisterCoreServices(new FakeUsageDataSource(UsageSnapshot.RequestFailed()));

        var cut = Render<ScreenNavigator>(p => p.Add(x => x.ScreenTypes, new[] { typeof(DummyScreenA), typeof(DummyScreenB) }));

        var span = cut.Find("[data-testid='dummy-screen']");
        Assert.StartsWith("dummy-a", span.TextContent);
    }

    [Fact]
    public void ScreenNavigator_ConClicEnBotonDeCambioDePantalla_AlternaEntreLasDosPantallasSimuladas()
    {
        RegisterCoreServices(new FakeUsageDataSource(UsageSnapshot.RequestFailed()));
        var cut = Render<ScreenNavigator>(p => p.Add(x => x.ScreenTypes, new[] { typeof(DummyScreenA), typeof(DummyScreenB) }));

        cut.Find("button.claudemeter-cycle").Click();
        Assert.StartsWith("dummy-b", cut.Find("[data-testid='dummy-screen']").TextContent);

        cut.Find("button.claudemeter-cycle").Click();
        Assert.StartsWith("dummy-a", cut.Find("[data-testid='dummy-screen']").TextContent);
    }

    [Fact]
    public void ScreenNavigator_ConUnaUnicaPantallaRegistrada_NoRenderizaElBotonDeCambioDePantalla()
    {
        // AC de caso límite de US-1: con una única pantalla, el ciclado no
        // debe fallar, pero el control no se muestra (decisión de UX de
        // Design, ver Risks & Open Decisions del documento de diseño).
        RegisterCoreServices(new FakeUsageDataSource(UsageSnapshot.RequestFailed()));

        var cut = Render<ScreenNavigator>(p => p.Add(x => x.ScreenTypes, new[] { typeof(DummyScreenA) }));

        Assert.Empty(cut.FindAll("button.claudemeter-cycle"));
        Assert.StartsWith("dummy-a", cut.Find("[data-testid='dummy-screen']").TextContent);
    }

    [Fact]
    public void ScreenNavigator_AlCambiarDePantallaTrasUnSnapshotYaRecibido_ConservaElMismoEstadoSinEsperarUnNuevoPoll()
    {
        // AC de US-1/US-2: cambiar de pantalla no reinicia el polling ni
        // "vacía" el estado -- la pantalla entrante debe reflejar el mismo
        // WidgetUsageState ya calculado por el último ciclo de poll.
        RegisterCoreServices(new FakeUsageDataSource(UsageSnapshot.RequestFailed()));
        var cut = Render<ScreenNavigator>(p => p.Add(x => x.ScreenTypes, new[] { typeof(DummyScreenA), typeof(DummyScreenB) }));

        cut.InvokeAsync(() => cut.Instance.ApplyForTests(SuccessSnapshot("0.77", "0.11"), Now));
        cut.Render();
        Assert.Contains("dummy-a:77", cut.Find("[data-testid='dummy-screen']").TextContent);

        cut.Find("button.claudemeter-cycle").Click();

        Assert.Contains("dummy-b:77", cut.Find("[data-testid='dummy-screen']").TextContent);
    }
}
