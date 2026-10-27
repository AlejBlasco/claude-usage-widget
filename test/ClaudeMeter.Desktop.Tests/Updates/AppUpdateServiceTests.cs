using ClaudeMeter.Desktop.Tests.TestDoubles;
using ClaudeMeter.Desktop.Updates;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Velopack;

namespace ClaudeMeter.Desktop.Tests.Updates;

/// <summary>
/// Pruebas de <see cref="AppUpdateService"/> (F6/US-3, issue #26), en
/// particular la AC de degradación silenciosa ante fallos ("sin conexión a
/// internet o con el feed de releases inaccesible ... sigue funcionando con
/// la versión actual sin bloquearse ni mostrar un error intrusivo"). Usa
/// <see cref="FakeUpdateManager"/> (ver su XMLDoc para el rationale
/// completo de por qué es seguro/determinista) en vez de una llamada real a
/// GitHub, y el constructor <c>internal</c> de <see cref="AppUpdateService"/>
/// más <see cref="AppUpdateService.CheckAndApplyForTestsAsync"/> (ambos
/// cambios aditivos de esta fase de testing, ver su XMLDoc) para poder
/// esperar el flujo asíncrono de forma determinista sin pasar por el
/// temporizador de 4h real ni por el disparo "fire-and-forget" de
/// <see cref="AppUpdateService.Start"/>.
/// </summary>
public sealed class AppUpdateServiceTests
{
    private static UpdateInfo SampleUpdateInfo() =>
        new(
            targetRelease: new VelopackAsset { PackageId = "ClaudeMeter", Version = new SemanticVersion(1, 1, 0, string.Empty, string.Empty), FileName = "ClaudeMeter-1.1.0-full.nupkg" },
            isDowngrade: false);

    [Fact]
    public async Task CheckAndApply_ConIsInstalledFalse_NuncaComprueebaActualizaciones()
    {
        // AC implícita (misma que RetryingUsageDataSource, F2): sin una
        // instalación Velopack real (p. ej. dotnet run en desarrollo local),
        // ni siquiera se intenta comprobar -- evita ruido/tráfico de red
        // innecesario cuando no hay nada que aplicar.
        var fake = new FakeUpdateManager();
        fake.SetIsInstalledForTests(false);
        var logger = new CapturingLogger<AppUpdateService>();
        var service = new AppUpdateService(logger, fake);

        await service.CheckAndApplyForTestsAsync();

        Assert.Equal(0, fake.CheckForUpdatesCallCount);
        Assert.DoesNotContain(LogLevel.Warning, logger.LoggedLevels);
    }

    [Fact]
    public async Task CheckAndApply_SinActualizacionDisponible_NoDescargaNiRegistraWarning()
    {
        var fake = new FakeUpdateManager { UpdateToReturn = null };
        var logger = new CapturingLogger<AppUpdateService>();
        var service = new AppUpdateService(logger, fake);

        var exception = await Record.ExceptionAsync(service.CheckAndApplyForTestsAsync);

        Assert.Null(exception);
        Assert.Equal(1, fake.CheckForUpdatesCallCount);
        Assert.Equal(0, fake.DownloadUpdatesCallCount);
        Assert.DoesNotContain(LogLevel.Warning, logger.LoggedLevels);
    }

    [Fact]
    public async Task CheckAndApply_ConFeedDeReleasesInaccesible_DegradaEnSilencioSinLanzar()
    {
        // AC central de US-3: "sin conexión a internet o con el feed de
        // releases inaccesible ... la aplicación sigue funcionando con la
        // versión actual sin bloquearse ni mostrar un error intrusivo".
        var fake = new FakeUpdateManager { ThrowOnCheck = true };
        var logger = new CapturingLogger<AppUpdateService>();
        var service = new AppUpdateService(logger, fake);

        var exception = await Record.ExceptionAsync(service.CheckAndApplyForTestsAsync);

        Assert.Null(exception); // nunca se propaga -- ninguna excepción intrusiva
        Assert.Contains(LogLevel.Warning, logger.LoggedLevels); // pero sí queda constancia en el log
    }

    [Fact]
    public async Task CheckAndApply_ConActualizacionDisponibleYFalloDeRedEnLaDescarga_DegradaEnSilencioSinLanzar()
    {
        // Mismo AC que el escenario anterior, pero con el fallo ocurriendo
        // un paso más tarde del flujo (Check OK, Download falla) --
        // confirma que el catch-all cubre todo CheckAndApplyAsync, no solo
        // CheckForUpdatesAsync. Deliberadamente nunca llega a
        // ApplyUpdatesAndRestart (no es virtual/mockeable -- ver XMLDoc de
        // FakeUpdateManager): si DownloadUpdatesAsync lanza, la línea de
        // ApplyUpdatesAndRestart nunca se ejecuta.
        var fake = new FakeUpdateManager { UpdateToReturn = SampleUpdateInfo(), ThrowOnDownload = true };
        var logger = new CapturingLogger<AppUpdateService>();
        var service = new AppUpdateService(logger, fake);

        var exception = await Record.ExceptionAsync(service.CheckAndApplyForTestsAsync);

        Assert.Null(exception);
        Assert.Equal(1, fake.CheckForUpdatesCallCount);
        Assert.Equal(1, fake.DownloadUpdatesCallCount);
        Assert.Contains(LogLevel.Warning, logger.LoggedLevels);
    }

    [Fact]
    public async Task CheckAndApply_LlamadoVariasVecesTrasFallos_SigueSiendoDeterministaYNoLanza()
    {
        // Confirma que un fallo no deja al servicio en un estado roto para
        // la siguiente comprobación periódica (timer de 4h) -- mismo
        // criterio de robustez que RetryingUsageDataSource (F2).
        var fake = new FakeUpdateManager { ThrowOnCheck = true };
        var service = new AppUpdateService(NullLogger<AppUpdateService>.Instance, fake);

        var exception = await Record.ExceptionAsync(async () =>
        {
            await service.CheckAndApplyForTestsAsync();
            await service.CheckAndApplyForTestsAsync();
        });

        Assert.Null(exception);
        Assert.Equal(2, fake.CheckForUpdatesCallCount);
    }

    [Fact]
    public void Dispose_SinHaberLlamadoAStart_NoLanza()
    {
        // Start() es lo único que crea el System.Timers.Timer interno --
        // Dispose() debe seguir siendo seguro (no NullReferenceException)
        // si se llama antes de Start() o si Start() nunca llegó a
        // invocarse, mismo criterio que TrayIconService.Dispose (idempotente).
        var service = new AppUpdateService(NullLogger<AppUpdateService>.Instance, new FakeUpdateManager());

        var exception = Record.Exception(() =>
        {
            service.Dispose();
            service.Dispose();
        });

        Assert.Null(exception);
    }
}
