using ClaudeMeter.Desktop.Polling;
using ClaudeMeter.Desktop.Tests.TestDoubles;
using ClaudeMeter.Domain.Usage;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClaudeMeter.Desktop.Tests.Polling;

/// <summary>
/// Pruebas de <see cref="UsagePollingCoordinator"/> (US-4): timer
/// start/stop, guard anti-solape, manejo de excepciones y liberación en
/// <see cref="UsagePollingCoordinator.Dispose"/> — sin bUnit, es una clase
/// C# plana sin ninguna dependencia de Blazor/WPF. Usa
/// <see cref="FakeUsageDataSource"/> para no depender de red ni de un
/// timer real de 60s (intervalos largos + los ganchos
/// <c>internal</c>/<c>ForTests</c> ya expuestos por el diseño). El tercer
/// parámetro del constructor (<see cref="ILogger{TCategoryName}"/>, F2/US-3)
/// se satisface siempre con <see cref="NullLogger{T}.Instance"/> -- ningún
/// test de esta clase verifica logging, solo la orquestación del poll.
/// </summary>
public sealed class UsagePollingCoordinatorTests
{
    private static readonly RawRateLimitHeaders SampleHeaders = new("allowed", "0.1", "0.9", "1789560000");

    // Intervalo deliberadamente largo: ningún test depende de un tick real
    // del timer salvo el explícito de "Dispose detiene el timer", que usa
    // un intervalo corto y comprueba que el recuento deja de crecer.
    private static readonly TimeSpan LongInterval = TimeSpan.FromHours(1);

    [Fact]
    public void Start_DisparaElPrimerFetchInmediatamente_SinEsperarAlTimer()
    {
        var fake = new FakeUsageDataSource(UsageSnapshot.Success(SampleHeaders, SampleHeaders));
        using var signal = new ManualResetEventSlim(initialState: false);
        UsageSnapshot? received = null;

        using var coordinator = new UsagePollingCoordinator(fake, LongInterval, NullLogger<UsagePollingCoordinator>.Instance);
        coordinator.SnapshotReceived += (snapshot, _) =>
        {
            received = snapshot;
            signal.Set();
        };

        coordinator.Start();

        Assert.True(signal.Wait(TimeSpan.FromSeconds(2)), "SnapshotReceived no se disparó tras Start().");
        Assert.Equal(1, fake.CallCount);
        Assert.True(received!.IsSuccess);
    }

    [Fact]
    public void Start_DejaElTimerSubyacenteActivo()
    {
        var fake = new FakeUsageDataSource(UsageSnapshot.RequestFailed());
        using var coordinator = new UsagePollingCoordinator(fake, LongInterval, NullLogger<UsagePollingCoordinator>.Instance);

        coordinator.Start();

        Assert.True(coordinator.IsRunningForTests);
    }

    [Fact]
    public void Dispose_DetieneElTimerSubyacente()
    {
        var fake = new FakeUsageDataSource(UsageSnapshot.RequestFailed());
        var coordinator = new UsagePollingCoordinator(fake, LongInterval, NullLogger<UsagePollingCoordinator>.Instance);
        coordinator.Start();

        coordinator.Dispose();

        Assert.False(coordinator.IsRunningForTests);
    }

    [Fact]
    public void Dispose_LlamadoDosVeces_EsIdempotenteYNoLanza()
    {
        // El guard `_disposed` debe hacer de Dispose() una operación segura
        // de invocar más de una vez (idioma estándar de IDisposable), algo
        // plausible en el ciclo de vida real de un componente Razor si
        // Dispose() se disparase más de una vez durante el desmontaje.
        var fake = new FakeUsageDataSource(UsageSnapshot.RequestFailed());
        var coordinator = new UsagePollingCoordinator(fake, LongInterval, NullLogger<UsagePollingCoordinator>.Instance);
        coordinator.Start();

        coordinator.Dispose();
        var exception = Record.Exception(() => coordinator.Dispose());

        Assert.Null(exception);
        Assert.False(coordinator.IsRunningForTests);
    }

    [Fact]
    public async Task Dispose_EvitaQueSiganLlegandoMasSnapshotReceivedTrasElUltimoTick()
    {
        // Intervalo corto para forzar varios ticks reales del timer si no
        // se detuviera correctamente; se compara el recuento justo antes y
        // bastante después de Dispose() para tolerar jitter sin depender de
        // temporización exacta.
        var fake = new FakeUsageDataSource(UsageSnapshot.RequestFailed());
        var receivedCount = 0;
        var coordinator = new UsagePollingCoordinator(fake, TimeSpan.FromMilliseconds(25), NullLogger<UsagePollingCoordinator>.Instance);
        coordinator.SnapshotReceived += (_, _) => Interlocked.Increment(ref receivedCount);

        coordinator.Start();
        await Task.Delay(60); // deja pasar el fetch inmediato y, probablemente, algún tick.

        coordinator.Dispose();
        var countAtDispose = Volatile.Read(ref receivedCount);

        // Si el timer no se hubiese detenido, a 25ms de intervalo habrían
        // llegado varios ticks más en esta ventana.
        await Task.Delay(300);

        Assert.Equal(countAtDispose, Volatile.Read(ref receivedCount));
    }

    [Fact]
    public async Task PollAsync_ConLlamadaEnVuelo_IgnoraElSegundoDisparoHastaQueLaPrimeraCompleta()
    {
        // AC de US-4: "nunca dos timers/llamadas en paralelo".
        var fake = new FakeUsageDataSource(UsageSnapshot.Success(SampleHeaders, SampleHeaders));
        var blockingCall = fake.ArmBlockingCall();
        using var coordinator = new UsagePollingCoordinator(fake, LongInterval, NullLogger<UsagePollingCoordinator>.Instance);

        var firstPoll = coordinator.PollOnceForTestsAsync(); // se queda "en vuelo" hasta que se resuelva blockingCall.

        // Espera activa breve para asegurar que el primer GetUsageAsync ya se invocó.
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (fake.CallCount == 0 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(5);
        }

        Assert.Equal(1, fake.CallCount);

        var secondPoll = coordinator.PollOnceForTestsAsync(); // debe hacer no-op por el guard _isPolling.
        await secondPoll;

        Assert.Equal(1, fake.CallCount); // el segundo disparo no llegó a invocar GetUsageAsync.

        blockingCall.SetResult(UsageSnapshot.Success(SampleHeaders, SampleHeaders));
        await firstPoll;
    }

    [Fact]
    public async Task PollAsync_ConExcepcionDelDataSource_NoSePropagaNiSeDisparaSnapshotReceived()
    {
        // Red de seguridad de último recurso: ninguna capa inferior debería
        // lanzar para los casos esperados, pero PollAsync no debe dejar
        // escapar la excepción ni disparar el evento si ocurre igualmente.
        var fake = new FakeUsageDataSource(UsageSnapshot.RequestFailed());
        fake.SetNextException(new InvalidOperationException("fallo inesperado simulado"));
        var eventFired = false;
        using var coordinator = new UsagePollingCoordinator(fake, LongInterval, NullLogger<UsagePollingCoordinator>.Instance);
        coordinator.SnapshotReceived += (_, _) => eventFired = true;

        var exception = await Record.ExceptionAsync(() => coordinator.PollOnceForTestsAsync());

        Assert.Null(exception);
        Assert.False(eventFired);
    }

    [Fact]
    public async Task PollAsync_TrasUnaExcepcion_SigueAceptandoCiclosPosterioresConExito()
    {
        var fake = new FakeUsageDataSource(UsageSnapshot.RequestFailed());
        fake.SetNextException(new InvalidOperationException("fallo transitorio simulado"));
        fake.SetNextResult(UsageSnapshot.Success(SampleHeaders, SampleHeaders));
        UsageSnapshot? lastReceived = null;
        using var coordinator = new UsagePollingCoordinator(fake, LongInterval, NullLogger<UsagePollingCoordinator>.Instance);
        coordinator.SnapshotReceived += (snapshot, _) => lastReceived = snapshot;

        await coordinator.PollOnceForTestsAsync(); // lanza y se traga la excepción, sin evento.
        await coordinator.PollOnceForTestsAsync(); // ciclo siguiente, con éxito.

        Assert.NotNull(lastReceived);
        Assert.True(lastReceived!.IsSuccess);
    }

    [Fact]
    public void Pause_DetieneElTimerSubyacenteSinDisponerLaInstancia()
    {
        // AC de "Pausar" (F3/Ciclo B): a diferencia de Dispose(), la
        // instancia sigue viva -- Start() debe poder volver a invocarse
        // después.
        var fake = new FakeUsageDataSource(UsageSnapshot.RequestFailed());
        using var coordinator = new UsagePollingCoordinator(fake, LongInterval, NullLogger<UsagePollingCoordinator>.Instance);
        coordinator.Start();

        var exception = Record.Exception(() => coordinator.Pause());

        Assert.Null(exception);
        Assert.False(coordinator.IsRunningForTests);
    }

    [Fact]
    public void Pause_LlamadoSinHaberArrancadoAntes_NoLanza()
    {
        // El icono de bandeja podría, en teoría, llegar a pausar antes de
        // que el primer Start() del componente complete -- Pause() no debe
        // asumir que el timer está corriendo.
        var fake = new FakeUsageDataSource(UsageSnapshot.RequestFailed());
        using var coordinator = new UsagePollingCoordinator(fake, LongInterval, NullLogger<UsagePollingCoordinator>.Instance);

        var exception = Record.Exception(() => coordinator.Pause());

        Assert.Null(exception);
        Assert.False(coordinator.IsRunningForTests);
    }

    [Fact]
    public void Start_TrasPause_ReanudaElTimerYDisparaUnFetchInmediato()
    {
        // AC de "Reanudar": mismo comportamiento que el Start() inicial --
        // dispara un fetch inmediato sin esperar a que se cumpla un ciclo
        // completo del timer.
        var fake = new FakeUsageDataSource(UsageSnapshot.Success(SampleHeaders, SampleHeaders));
        using var firstSignal = new ManualResetEventSlim(initialState: false);
        using var resumeSignal = new ManualResetEventSlim(initialState: false);
        var receivedCount = 0;
        using var coordinator = new UsagePollingCoordinator(fake, LongInterval, NullLogger<UsagePollingCoordinator>.Instance);
        coordinator.SnapshotReceived += (_, _) =>
        {
            if (Interlocked.Increment(ref receivedCount) == 1)
            {
                firstSignal.Set();
            }
            else
            {
                resumeSignal.Set();
            }
        };

        coordinator.Start();
        Assert.True(firstSignal.Wait(TimeSpan.FromSeconds(2)), "El fetch inicial de Start() no se disparó.");
        coordinator.Pause();
        Assert.False(coordinator.IsRunningForTests);

        coordinator.Start(); // AC: "reanudar" == mismo comportamiento que Start() inicial

        Assert.True(resumeSignal.Wait(TimeSpan.FromSeconds(2)), "Start() tras Pause() no disparó un fetch inmediato.");
        Assert.True(coordinator.IsRunningForTests);
        Assert.Equal(2, receivedCount);
    }

    [Fact]
    public async Task PollNow_SinHaberLlamadoAStartAntes_DisparaGetUsageAsyncUnaVezSinTocarElTimer()
    {
        // AC de "Recargar": ciclo de poll adicional, "fuera de la cadencia
        // normal del timer, sin reiniciar el propio timer" -- se comprueba
        // aquí con el timer ni siquiera arrancado, para confirmar que
        // PollNow() no lo arranca como efecto colateral.
        var fake = new FakeUsageDataSource(UsageSnapshot.Success(SampleHeaders, SampleHeaders));
        using var coordinator = new UsagePollingCoordinator(fake, LongInterval, NullLogger<UsagePollingCoordinator>.Instance);

        await coordinator.PollNow();

        Assert.Equal(1, fake.CallCount);
        Assert.False(coordinator.IsRunningForTests);
    }

    [Fact]
    public async Task PollNow_ConUnPollYaEnVuelo_RespetaElGuardAntiSolapeYEsUnNoOp()
    {
        // Mismo guard _isPolling que ya protege al timer -- PollNow() (vía
        // el menú "Recargar") no debe poder disparar una segunda llamada
        // HTTP en paralelo con una ya en curso (manual o del propio timer).
        var fake = new FakeUsageDataSource(UsageSnapshot.Success(SampleHeaders, SampleHeaders));
        var blockingCall = fake.ArmBlockingCall();
        using var coordinator = new UsagePollingCoordinator(fake, LongInterval, NullLogger<UsagePollingCoordinator>.Instance);

        var firstPoll = coordinator.PollNow();

        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (fake.CallCount == 0 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(5);
        }
        Assert.Equal(1, fake.CallCount);

        var secondPoll = coordinator.PollNow(); // debe ser un no-op silencioso
        await secondPoll;

        Assert.Equal(1, fake.CallCount); // el segundo PollNow() no llegó a invocar GetUsageAsync

        blockingCall.SetResult(UsageSnapshot.Success(SampleHeaders, SampleHeaders));
        await firstPoll;
    }
}
