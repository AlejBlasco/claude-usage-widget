using ClaudeMeter.Desktop.Polling;
using ClaudeMeter.Desktop.Tests.TestDoubles;
using ClaudeMeter.Domain.Usage;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClaudeMeter.Desktop.Tests.Polling;

/// <summary>
/// Pruebas de <see cref="PollingControlService"/> (US-2, F3/Ciclo B): puente
/// de DI singleton entre el icono de bandeja (fuera de Blazor) y la
/// instancia real de <see cref="UsagePollingCoordinator"/> que posee cada
/// <c>UsagePage</c>. xUnit puro, sin bUnit -- usa un
/// <see cref="UsagePollingCoordinator"/> real respaldado por
/// <see cref="FakeUsageDataSource"/> (mismo patrón que
/// <see cref="UsagePollingCoordinatorTests"/>) en vez de un mock, porque
/// <see cref="UsagePollingCoordinator"/> es una clase concreta sin interfaz
/// (misma decisión ya documentada para <c>AppConfigStore</c> en
/// <c>WindowDragServiceTests</c>).
/// </summary>
public sealed class PollingControlServiceTests
{
    private static readonly RawRateLimitHeaders SampleHeaders = new("allowed", "0.1", "0.9", "1789560000");

    // Intervalo largo: ningún test de este fichero depende de un tick real
    // del timer, solo de las llamadas explícitas Pause()/Start()/PollNow()
    // que PollingControlService dispara.
    private static readonly TimeSpan LongInterval = TimeSpan.FromHours(1);

    private static UsagePollingCoordinator NewCoordinator(FakeUsageDataSource fake) =>
        new(fake, LongInterval, NullLogger<UsagePollingCoordinator>.Instance);

    [Fact]
    public void AttachCoordinator_DejaIsPausedEnFalse()
    {
        // AC: "arranca siempre con el polling activo (no persistido)".
        var fake = new FakeUsageDataSource(UsageSnapshot.RequestFailed());
        using var coordinator = NewCoordinator(fake);
        var service = new PollingControlService();

        service.AttachCoordinator(coordinator);

        Assert.False(service.IsPaused);
    }

    [Fact]
    public void Pause_ConCoordinadorAdjunto_MarcaIsPausedYDisparaPauseStateChangedUnaVez()
    {
        var fake = new FakeUsageDataSource(UsageSnapshot.RequestFailed());
        using var coordinator = NewCoordinator(fake);
        var service = new PollingControlService();
        service.AttachCoordinator(coordinator);
        var eventCount = 0;
        service.PauseStateChanged += () => eventCount++;

        service.Pause();

        Assert.True(service.IsPaused);
        Assert.Equal(1, eventCount);
        Assert.False(coordinator.IsRunningForTests); // el timer subyacente se detuvo de verdad
    }

    [Fact]
    public void Pause_LlamadoDosVecesConsecutivas_LaSegundaEsUnNoOpSinRepetirElEvento()
    {
        var fake = new FakeUsageDataSource(UsageSnapshot.RequestFailed());
        using var coordinator = NewCoordinator(fake);
        var service = new PollingControlService();
        service.AttachCoordinator(coordinator);
        var eventCount = 0;
        service.PauseStateChanged += () => eventCount++;

        service.Pause();
        service.Pause();

        Assert.True(service.IsPaused);
        Assert.Equal(1, eventCount);
    }

    [Fact]
    public void Resume_TrasPause_MarcaIsPausedFalseYDisparaPauseStateChangedDeNuevo()
    {
        // AC de "Reanudar": mismo comportamiento que el Start() inicial --
        // PollingControlService.Resume() reutiliza Start(), no un método
        // aparte.
        var fake = new FakeUsageDataSource(UsageSnapshot.RequestFailed());
        using var coordinator = NewCoordinator(fake);
        var service = new PollingControlService();
        service.AttachCoordinator(coordinator);
        var eventCount = 0;
        service.PauseStateChanged += () => eventCount++;
        service.Pause();

        service.Resume();

        Assert.False(service.IsPaused);
        Assert.Equal(2, eventCount); // Pause + Resume
        Assert.True(coordinator.IsRunningForTests); // Start() reinicia el timer subyacente
    }

    [Fact]
    public void Resume_SinHaberPausadoAntes_EsUnNoOpSinDispararElEvento()
    {
        var fake = new FakeUsageDataSource(UsageSnapshot.RequestFailed());
        using var coordinator = NewCoordinator(fake);
        var service = new PollingControlService();
        service.AttachCoordinator(coordinator);
        var eventCount = 0;
        service.PauseStateChanged += () => eventCount++;

        service.Resume();

        Assert.False(service.IsPaused);
        Assert.Equal(0, eventCount);
    }

    [Fact]
    public void TogglePause_AlternaEntrePausarYReanudar()
    {
        var fake = new FakeUsageDataSource(UsageSnapshot.RequestFailed());
        using var coordinator = NewCoordinator(fake);
        var service = new PollingControlService();
        service.AttachCoordinator(coordinator);

        service.TogglePause();
        Assert.True(service.IsPaused);

        service.TogglePause();
        Assert.False(service.IsPaused);
    }

    [Fact]
    public void Pause_SinCoordinadorAdjunto_NoLanzaNiCambiaIsPausedNiDisparaElEvento()
    {
        var service = new PollingControlService();
        var eventCount = 0;
        service.PauseStateChanged += () => eventCount++;

        var exception = Record.Exception(() => service.Pause());

        Assert.Null(exception);
        Assert.False(service.IsPaused);
        Assert.Equal(0, eventCount);
    }

    [Fact]
    public void Resume_SinCoordinadorAdjunto_NoLanzaNiCambiaIsPaused()
    {
        var service = new PollingControlService();

        var exception = Record.Exception(() => service.Resume());

        Assert.Null(exception);
        Assert.False(service.IsPaused);
    }

    [Fact]
    public void RequestReload_SinCoordinadorAdjunto_NoLanza()
    {
        var service = new PollingControlService();

        var exception = Record.Exception(() => service.RequestReload());

        Assert.Null(exception);
    }

    [Fact]
    public async Task RequestReload_ConCoordinadorAdjunto_DisparaUnPollAdicionalSinTocarIsPaused()
    {
        // AC de "Recargar": ciclo de poll adicional, sin tocar el estado de
        // pausa ni el timer.
        var fake = new FakeUsageDataSource(UsageSnapshot.Success(SampleHeaders, SampleHeaders));
        using var coordinator = NewCoordinator(fake);
        var service = new PollingControlService();
        service.AttachCoordinator(coordinator);

        service.RequestReload(); // fire-and-forget, igual que en producción (TrayIconService)

        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (fake.CallCount == 0 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(5);
        }

        Assert.Equal(1, fake.CallCount);
        Assert.False(service.IsPaused);
        Assert.False(coordinator.IsRunningForTests); // RequestReload no arranca el timer
    }
}
