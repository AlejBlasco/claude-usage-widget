using Bunit;
using ClaudeMeter.Application.Abstractions;
using ClaudeMeter.Desktop.Audio;
using ClaudeMeter.Desktop.Configuration;
using ClaudeMeter.Desktop.Pages;
using ClaudeMeter.Desktop.Tests.TestDoubles;
using ClaudeMeter.Desktop.Windowing;
using ClaudeMeter.Domain.Usage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClaudeMeter.Desktop.Tests.Pages;

/// <summary>
/// Pruebas bUnit de <see cref="UsagePage"/> (US-2/US-4, y desde F2 Ciclo B
/// también US-1 de config.json/chime y US-2 de arrastre): montaje con
/// <see cref="IUsageDataSource"/> simulado, aplicación de snapshots vía los
/// ganchos <c>internal</c> <c>ApplyForTests</c>/<c>IsPollingActiveForTests</c>
/// (expuestos por diseño para no depender de un timer real de 60s), y
/// verificación de que el componente libera su polling al desmontarse. Usa
/// la API v2 de bUnit (<see cref="BunitContext"/> + <c>Render&lt;T&gt;</c> +
/// <c>DisposeComponentsAsync()</c>), nunca la v1.
///
/// Desde F2 Ciclo B, <c>UsagePage</c> exige además <see cref="AppConfig"/>,
/// <see cref="IChimePlayer"/>, <see cref="WindowDragService"/> e
/// <see cref="IJSRuntime"/> vía <c>@inject</c> -- <see cref="RegisterCoreServices"/>
/// centraliza ese registro (arreglo mecánico de compilación/wiring, mismo
/// patrón ya usado en F2 Ciclo A con <c>NullLogger&lt;T&gt;.Instance</c>) para
/// que los tests ya existentes de este fichero, que no verifican nada de
/// config/chime/arrastre, sigan compilando y pasando sin cambiar su
/// comportamiento. <see cref="BunitContext.JSInterop"/> se pone en modo
/// <see cref="JSRuntimeMode.Loose"/> porque <c>OnAfterRenderAsync</c> invoca
/// <c>claudeMeterDrag.init</c> vía JS interop en cada render -- ningún test
/// de este fichero verifica ese listener JS en sí (requeriría un navegador
/// real), solo que su registro no rompe el montaje del componente.
/// </summary>
public sealed class UsagePageTests : BunitContext
{
    private static readonly DateTimeOffset Now = new(2026, 9, 16, 10, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Registra <see cref="IUsageDataSource"/> (el doble bajo control del
    /// test) más las tres dependencias nuevas de F2 Ciclo B que
    /// <c>UsagePage</c> exige vía <c>@inject</c> y pone el JSInterop de
    /// bUnit en modo Loose (US-2: el listener de arrastre no se verifica en
    /// estos tests, solo no debe impedir el montaje). <paramref name="config"/>
    /// por defecto usa <see cref="AppConfig.Default"/> (chime desactivado,
    /// intervalo de 60s) para no alterar el comportamiento de los tests
    /// preexistentes que no son sobre config/chime.
    /// </summary>
    private void RegisterCoreServices(IUsageDataSource dataSource, AppConfig? config = null, IChimePlayer? chimePlayer = null)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton(dataSource);
        Services.AddSingleton(config ?? AppConfig.Default);
        Services.AddSingleton(chimePlayer ?? new FakeChimePlayer());
        // WindowDragService real, pero jamás se invoca BeginDrag() en estos
        // tests bUnit (no hay gesto de ratón real) -- ver
        // WindowDragServiceTests para la cobertura dedicada de esa clase.
        Services.AddSingleton(new WindowDragService(
            new AppConfigStore(NullLogger<AppConfigStore>.Instance), NullLogger<WindowDragService>.Instance));
    }

    [Fact]
    public void UsagePage_AlMontarse_InvocaGetUsageAsyncYRenderizaElPrimerSnapshotDeExito()
    {
        // AC de US-4: se reutiliza directamente IUsageDataSource (sin
        // reimplementar la llamada) y el primer fetch se dispara al montar,
        // sin que el usuario recargue nada manualmente.
        var fake = new FakeUsageDataSource(UsageSnapshot.Success(
            new RawRateLimitHeaders("allowed", "0.10", "0.90", null),
            new RawRateLimitHeaders("allowed", "0.42", "0.58", null)));
        RegisterCoreServices(fake);

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
        RegisterCoreServices(new FakeUsageDataSource(UsageSnapshot.RequestFailed()));
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
        RegisterCoreServices(new FakeUsageDataSource(UsageSnapshot.RequestFailed()));
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
        RegisterCoreServices(new FakeUsageDataSource(UsageSnapshot.RequestFailed()));
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
    [InlineData(nameof(UsageSnapshotStatus.RequestFailed))]
    [InlineData(nameof(UsageSnapshotStatus.MalformedResponse))]
    public void UsagePage_ConSnapshotDeFalloYSinExitoPrevio_MuestraAmbasBarrasNoDisponible(string failureKind)
    {
        // Nota F2: Unauthorized se excluyó deliberadamente de esta Theory --
        // ya no renderiza UsageBar en absoluto (ver
        // UsagePage_ConUnauthorizedYSinExitoPrevio_* más abajo). Mantenerlo
        // aquí habría dejado Assert.All pasando de forma vacía (0
        // elementos), sin verificar comportamiento real.
        RegisterCoreServices(new FakeUsageDataSource(UsageSnapshot.RequestFailed()));
        var cut = Render<UsagePage>();

        var failureSnapshot = failureKind switch
        {
            nameof(UsageSnapshotStatus.TokenUnavailable) => UsageSnapshot.TokenUnavailable(),
            nameof(UsageSnapshotStatus.RequestFailed) => UsageSnapshot.RequestFailed(),
            nameof(UsageSnapshotStatus.MalformedResponse) => UsageSnapshot.MalformedResponse(),
            _ => throw new ArgumentOutOfRangeException(nameof(failureKind)),
        };

        cut.InvokeAsync(() => cut.Instance.ApplyForTests(failureSnapshot, Now));
        cut.Render();

        var bars = cut.FindAll("div.usage-bar");
        Assert.Equal(2, bars.Count);
        Assert.All(bars, bar => Assert.Contains("No disponible", bar.TextContent));
        Assert.DoesNotContain("desactualizado", cut.Markup);
        Assert.Empty(cut.FindAll("div.usage-reauth"));
    }

    [Fact]
    public void UsagePage_TrasUnCicloExitosoSeguidoDeUnFalloTransitorio_ConservaElValorAnteriorMarcadoDesactualizado()
    {
        // F2: se usa RequestFailed (fallo transitorio) como estado de fallo,
        // en vez de Unauthorized -- desde F2, Unauthorized ya no marca
        // "(desactualizado)" (AC 3 de US-1, ver el test dedicado a
        // Unauthorized más abajo); este test conserva el comportamiento
        // original de F1 para el resto de fallos.
        RegisterCoreServices(new FakeUsageDataSource(UsageSnapshot.RequestFailed()));
        var cut = Render<UsagePage>();

        var successSnapshot = UsageSnapshot.Success(
            session: new RawRateLimitHeaders("allowed", "0.33", "0.67", null),
            weekly: new RawRateLimitHeaders("allowed", "0.44", "0.56", null));
        cut.InvokeAsync(() => cut.Instance.ApplyForTests(successSnapshot, Now));
        cut.Render();

        Assert.Contains("33%", cut.Markup);
        Assert.DoesNotContain("desactualizado", cut.Markup);

        cut.InvokeAsync(() => cut.Instance.ApplyForTests(UsageSnapshot.RequestFailed(), Now));
        cut.Render();

        // El AC exige conservar el último valor válido, marcado como
        // potencialmente desactualizado -- no debe caer a "No disponible".
        Assert.Contains("33%", cut.Markup);
        Assert.Contains("desactualizado", cut.Markup);
    }

    [Fact]
    public void UsagePage_ConUnauthorizedYSinExitoPrevio_RenderizaReauthNoticeYOcultaLasBarras()
    {
        // AC de US-1 (F2): un 401/403 no es "sin datos" ni "desactualizado"
        // -- sustituye por completo las dos UsageBar por ReauthNotice.
        RegisterCoreServices(new FakeUsageDataSource(UsageSnapshot.RequestFailed()));
        var cut = Render<UsagePage>();

        cut.InvokeAsync(() => cut.Instance.ApplyForTests(UsageSnapshot.Unauthorized(), Now));
        cut.Render();

        Assert.Empty(cut.FindAll("div.usage-bar"));
        var notice = cut.Find("div.usage-reauth");
        Assert.Contains("Vuelve a iniciar sesión", notice.TextContent);
        Assert.DoesNotContain("desactualizado", cut.Markup);
        Assert.DoesNotContain("No disponible", cut.Markup);
    }

    [Fact]
    public void UsagePage_ConUnauthorizedTrasUnCicloExitoso_SustituyeLasBarrasPorReauthNoticeSinMarcarDesactualizado()
    {
        // AC 3 de US-1 (F2): incluso habiendo mostrado datos válidos antes,
        // un 401/403 nunca se trata como "dato viejo" (a diferencia de
        // RequestFailed/TokenUnavailable/MalformedResponse tras un éxito
        // previo, ver el test de arriba) -- las barras desaparecen del todo.
        RegisterCoreServices(new FakeUsageDataSource(UsageSnapshot.RequestFailed()));
        var cut = Render<UsagePage>();

        var successSnapshot = UsageSnapshot.Success(
            session: new RawRateLimitHeaders("allowed", "0.33", "0.67", null),
            weekly: new RawRateLimitHeaders("allowed", "0.44", "0.56", null));
        cut.InvokeAsync(() => cut.Instance.ApplyForTests(successSnapshot, Now));
        cut.Render();
        Assert.Contains("33%", cut.Markup);

        cut.InvokeAsync(() => cut.Instance.ApplyForTests(UsageSnapshot.Unauthorized(), Now));
        cut.Render();

        Assert.Empty(cut.FindAll("div.usage-bar"));
        Assert.DoesNotContain("33%", cut.Markup);
        Assert.DoesNotContain("desactualizado", cut.Markup);
        Assert.NotEmpty(cut.FindAll("div.usage-reauth"));
        Assert.Equal(UsageSnapshotStatus.Unauthorized, cut.Instance.StatusForTests);
    }

    [Fact]
    public void UsagePage_TrasDosCiclosConsecutivosConExito_MuestraSiempreElUltimoSnapshotSinMezclar()
    {
        RegisterCoreServices(new FakeUsageDataSource(UsageSnapshot.RequestFailed()));
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
        RegisterCoreServices(fake);
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

    [Fact]
    public void UsagePage_ConIntervaloCortoEnAppConfig_LoUsaEnVezDeUnValorHardcodeado()
    {
        // F2 Ciclo B (US-1): el intervalo ya no es una constante interna
        // fija (RefreshInterval, eliminada) -- viene de Config.PollingInterval.
        // AppConfig.Default usa 60s, que nunca completaría un segundo ciclo
        // dentro del timeout de este test; solo un intervalo corto inyectado
        // explícitamente puede hacerlo, así que observar un segundo
        // GetUsageAsync() aquí confirma que UsagePage reenvía de verdad
        // Config.PollingInterval al UsagePollingCoordinator, en vez de
        // ignorarlo.
        var fake = new FakeUsageDataSource(UsageSnapshot.RequestFailed());
        var shortIntervalConfig = AppConfig.Default with { PollingInterval = TimeSpan.FromMilliseconds(30) };
        RegisterCoreServices(fake, config: shortIntervalConfig);

        var cut = Render<UsagePage>();

        cut.WaitForAssertion(() => Assert.True(fake.CallCount >= 2, $"CallCount fue {fake.CallCount}"), timeout: TimeSpan.FromSeconds(2));
    }

    private static UsageSnapshot CriticalSnapshot() => UsageSnapshot.Success(
        session: new RawRateLimitHeaders("allowed", "0.95", "0.05", null), // 95% -> Crítico
        weekly: new RawRateLimitHeaders("allowed", "0.10", "0.90", null)); // 10% -> Normal, no interfiere

    private static UsageSnapshot NormalSnapshot() => UsageSnapshot.Success(
        session: new RawRateLimitHeaders("allowed", "0.10", "0.90", null), // 10% -> Normal
        weekly: new RawRateLimitHeaders("allowed", "0.10", "0.90", null));

    [Fact]
    public void UsagePage_ConTransicionDeNormalACriticalYChimeHabilitado_DisparaElChimeExactamenteUnaVez()
    {
        // AC principal de US-1: el chime suena al ENTRAR en Crítico.
        var chimePlayer = new FakeChimePlayer();
        RegisterCoreServices(
            new FakeUsageDataSource(UsageSnapshot.RequestFailed()),
            config: AppConfig.Default with { ChimeEnabled = true },
            chimePlayer: chimePlayer);
        var cut = Render<UsagePage>();

        cut.InvokeAsync(() => cut.Instance.ApplyForTests(NormalSnapshot(), Now));
        cut.Render();
        Assert.Equal(0, chimePlayer.PlayCount);

        cut.InvokeAsync(() => cut.Instance.ApplyForTests(CriticalSnapshot(), Now));
        cut.Render();
        Assert.Equal(1, chimePlayer.PlayCount);
    }

    [Fact]
    public void UsagePage_ConElEstadoCriticalQuePersisteEnElSiguienteCiclo_NoRepiteElChime()
    {
        // AC MÁS IMPORTANTE de US-1 (explícitamente señalado en el
        // Implementation Plan de diseño): mientras el estado se mantenga en
        // Crítico ciclo tras ciclo (sin una transición nueva), el chime NO
        // debe volver a sonar -- solo una vez por entrada a Crítico.
        var chimePlayer = new FakeChimePlayer();
        RegisterCoreServices(
            new FakeUsageDataSource(UsageSnapshot.RequestFailed()),
            config: AppConfig.Default with { ChimeEnabled = true },
            chimePlayer: chimePlayer);
        var cut = Render<UsagePage>();

        cut.InvokeAsync(() => cut.Instance.ApplyForTests(CriticalSnapshot(), Now)); // primera entrada a Crítico
        cut.Render();
        Assert.Equal(1, chimePlayer.PlayCount);

        cut.InvokeAsync(() => cut.Instance.ApplyForTests(CriticalSnapshot(), Now)); // siguiente ciclo de poll, sigue en Crítico
        cut.Render();
        Assert.Equal(1, chimePlayer.PlayCount); // NO se repite

        cut.InvokeAsync(() => cut.Instance.ApplyForTests(CriticalSnapshot(), Now)); // un tercer ciclo, sigue igual
        cut.Render();
        Assert.Equal(1, chimePlayer.PlayCount);
    }

    [Fact]
    public void UsagePage_ConChimeDeshabilitadoEnAppConfig_NuncaSuenaAunEnTransicionACritical()
    {
        var chimePlayer = new FakeChimePlayer();
        RegisterCoreServices(
            new FakeUsageDataSource(UsageSnapshot.RequestFailed()),
            config: AppConfig.Default with { ChimeEnabled = false },
            chimePlayer: chimePlayer);
        var cut = Render<UsagePage>();

        cut.InvokeAsync(() => cut.Instance.ApplyForTests(NormalSnapshot(), Now));
        cut.Render();
        cut.InvokeAsync(() => cut.Instance.ApplyForTests(CriticalSnapshot(), Now));
        cut.Render();

        Assert.Equal(0, chimePlayer.PlayCount);
    }

    [Fact]
    public void UsagePage_ConElPrimerSnapshotYaEnCriticalYChimeHabilitado_SuenaEnElPrimerCiclo()
    {
        // ThresholdTransition.EnteredCritical trata "sin lectura previa"
        // (null) igual que "no era Crítico" -- la primerísima observación en
        // rojo también debe avisar, no solo a partir de la segunda.
        var chimePlayer = new FakeChimePlayer();
        RegisterCoreServices(
            new FakeUsageDataSource(UsageSnapshot.RequestFailed()),
            config: AppConfig.Default with { ChimeEnabled = true },
            chimePlayer: chimePlayer);
        var cut = Render<UsagePage>();

        cut.InvokeAsync(() => cut.Instance.ApplyForTests(CriticalSnapshot(), Now));
        cut.Render();

        Assert.Equal(1, chimePlayer.PlayCount);
    }

    [Fact]
    public void UsagePage_ConSoloLaVentanaSemanalEntrandoEnCritical_TambienDisparaElChime()
    {
        // El OR de UsagePage.razor evalúa la transición de sesión Y de
        // semana por separado -- una transición a Crítico de CUALQUIERA de
        // las dos debe disparar el chime, no solo la de sesión (ya cubierta
        // por los tests de arriba).
        var chimePlayer = new FakeChimePlayer();
        RegisterCoreServices(
            new FakeUsageDataSource(UsageSnapshot.RequestFailed()),
            config: AppConfig.Default with { ChimeEnabled = true },
            chimePlayer: chimePlayer);
        var cut = Render<UsagePage>();

        var sessionNormalWeeklyNormal = UsageSnapshot.Success(
            session: new RawRateLimitHeaders("allowed", "0.10", "0.90", null),
            weekly: new RawRateLimitHeaders("allowed", "0.10", "0.90", null));
        cut.InvokeAsync(() => cut.Instance.ApplyForTests(sessionNormalWeeklyNormal, Now));
        cut.Render();
        Assert.Equal(0, chimePlayer.PlayCount);

        var sessionNormalWeeklyCritical = UsageSnapshot.Success(
            session: new RawRateLimitHeaders("allowed", "0.10", "0.90", null), // sigue Normal, no dispara por sí sola
            weekly: new RawRateLimitHeaders("allowed", "0.95", "0.05", null)); // entra en Crítico
        cut.InvokeAsync(() => cut.Instance.ApplyForTests(sessionNormalWeeklyCritical, Now));
        cut.Render();

        Assert.Equal(1, chimePlayer.PlayCount);
    }

    [Fact]
    public void UsagePage_ConUnFalloTransitorioMientrasElUltimoEstadoEraCritical_NoReseteaLaDeteccionAlRecuperarse()
    {
        // Rationale explícito del propio código de producción (comentario en
        // UsagePage.razor): el chime solo se evalúa sobre snapshots con
        // éxito, para que un fallo transitorio de red no "resetee" la
        // detección de transición y dispare un chime falso al recuperarse
        // en el mismo estado Crítico.
        var chimePlayer = new FakeChimePlayer();
        RegisterCoreServices(
            new FakeUsageDataSource(UsageSnapshot.RequestFailed()),
            config: AppConfig.Default with { ChimeEnabled = true },
            chimePlayer: chimePlayer);
        var cut = Render<UsagePage>();

        cut.InvokeAsync(() => cut.Instance.ApplyForTests(CriticalSnapshot(), Now));
        cut.Render();
        Assert.Equal(1, chimePlayer.PlayCount);

        cut.InvokeAsync(() => cut.Instance.ApplyForTests(UsageSnapshot.RequestFailed(), Now)); // fallo transitorio
        cut.Render();
        Assert.Equal(1, chimePlayer.PlayCount);

        cut.InvokeAsync(() => cut.Instance.ApplyForTests(CriticalSnapshot(), Now)); // se recupera, sigue en Crítico
        cut.Render();
        Assert.Equal(1, chimePlayer.PlayCount); // NO vuelve a sonar
    }

    [Fact]
    public void UsagePage_ConTransicionCriticalANormalYDeVueltaACritical_SuenaDeNuevo()
    {
        // Comportamiento simétrico al AC de "no repetir": si el estado sale
        // de Crítico y vuelve a entrar, sí es una transición nueva -- el
        // chime debe volver a sonar.
        var chimePlayer = new FakeChimePlayer();
        RegisterCoreServices(
            new FakeUsageDataSource(UsageSnapshot.RequestFailed()),
            config: AppConfig.Default with { ChimeEnabled = true },
            chimePlayer: chimePlayer);
        var cut = Render<UsagePage>();

        cut.InvokeAsync(() => cut.Instance.ApplyForTests(CriticalSnapshot(), Now));
        cut.Render();
        Assert.Equal(1, chimePlayer.PlayCount);

        cut.InvokeAsync(() => cut.Instance.ApplyForTests(NormalSnapshot(), Now));
        cut.Render();
        Assert.Equal(1, chimePlayer.PlayCount); // salir de Crítico no suena

        cut.InvokeAsync(() => cut.Instance.ApplyForTests(CriticalSnapshot(), Now));
        cut.Render();
        Assert.Equal(2, chimePlayer.PlayCount); // re-entrada: transición nueva
    }
}
