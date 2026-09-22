using ClaudeMeter.ConsoleApp.Polling;
using ClaudeMeter.Domain.Usage;

namespace ClaudeMeter.ConsoleApp.Tests.Polling;

/// <summary>
/// Pruebas unitarias de <see cref="UsagePollingLoop.ExecuteIterationAsync"/>,
/// invocado directamente gracias a <c>InternalsVisibleTo</c> (ver
/// <c>ClaudeMeter.Console.csproj</c>) para no depender del bucle infinito
/// real (<see cref="UsagePollingLoop.RunAsync"/>, con <c>Task.Delay</c> de
/// 60s reales) ni de <c>Program.cs</c>. Verifican, para cada
/// <see cref="UsageSnapshotStatus"/> del documento de requisitos del Issue
/// #5, que el despacho hacia <see cref="IUsagePollingRenderer"/> (aquí un
/// <see cref="SpyUsagePollingRenderer"/>) es exacto y exclusivo, y —
/// crítico para el criterio de aceptación "el bucle sigue funcionando tras
/// un error" — que una excepción inesperada del <see cref="IUsageDataSource"/>
/// nunca escapa de <see cref="UsagePollingLoop.ExecuteIterationAsync"/>.
/// </summary>
public sealed class UsagePollingLoopTests
{
    [Fact]
    public async Task ExecuteIterationAsync_ConSnapshotDeExito_LlamaARenderSuccessConLasVentanasParseadas()
    {
        // Arrange: Reset ausente en ambas ventanas para que MinutesRemaining
        // sea null de forma determinista, independiente de la hora real en
        // la que corra el test (ExecuteIterationAsync captura
        // DateTimeOffset.UtcNow internamente y no es inyectable).
        var sessionHeaders = new RawRateLimitHeaders("allowed", "42%", "58%", Reset: null);
        var weeklyHeaders = new RawRateLimitHeaders("allowed", "10%", "90%", Reset: null);
        var snapshot = UsageSnapshot.Success(sessionHeaders, weeklyHeaders);
        var dataSource = FakeUsageDataSource.Returning(snapshot);
        var renderer = new SpyUsagePollingRenderer();
        var sut = new UsagePollingLoop(dataSource, renderer, TimeSpan.FromSeconds(60));

        var before = DateTimeOffset.UtcNow;

        // Act
        await sut.ExecuteIterationAsync(CancellationToken.None);

        var after = DateTimeOffset.UtcNow;

        // Assert: solo RenderSuccess se invoca, una única vez.
        Assert.Equal(1, renderer.RenderSuccessCallCount);
        Assert.Equal(1, renderer.TotalCallCount);

        Assert.NotNull(renderer.LastTimestamp);
        Assert.InRange(renderer.LastTimestamp!.Value, before, after);

        Assert.NotNull(renderer.LastSession);
        Assert.Equal(42.0, renderer.LastSession!.PercentageUsed);
        Assert.Null(renderer.LastSession.MinutesRemaining);

        Assert.NotNull(renderer.LastWeekly);
        Assert.Equal(10.0, renderer.LastWeekly!.PercentageUsed);
        Assert.Null(renderer.LastWeekly.MinutesRemaining);
    }

    [Fact]
    public async Task ExecuteIterationAsync_ConTokenUnavailable_LlamaSoloARenderTokenUnavailable()
    {
        // Arrange
        var dataSource = FakeUsageDataSource.Returning(UsageSnapshot.TokenUnavailable());
        var renderer = new SpyUsagePollingRenderer();
        var sut = new UsagePollingLoop(dataSource, renderer, TimeSpan.FromSeconds(60));

        // Act
        await sut.ExecuteIterationAsync(CancellationToken.None);

        // Assert
        Assert.Equal(1, renderer.RenderTokenUnavailableCallCount);
        Assert.Equal(1, renderer.TotalCallCount);
        Assert.NotNull(renderer.LastTimestamp);
    }

    [Fact]
    public async Task ExecuteIterationAsync_ConUnauthorized_LlamaSoloARenderUnauthorized()
    {
        // Arrange
        var dataSource = FakeUsageDataSource.Returning(UsageSnapshot.Unauthorized());
        var renderer = new SpyUsagePollingRenderer();
        var sut = new UsagePollingLoop(dataSource, renderer, TimeSpan.FromSeconds(60));

        // Act
        await sut.ExecuteIterationAsync(CancellationToken.None);

        // Assert
        Assert.Equal(1, renderer.RenderUnauthorizedCallCount);
        Assert.Equal(1, renderer.TotalCallCount);
    }

    [Fact]
    public async Task ExecuteIterationAsync_ConRequestFailed_LlamaSoloARenderRequestFailed()
    {
        // Arrange
        var dataSource = FakeUsageDataSource.Returning(UsageSnapshot.RequestFailed());
        var renderer = new SpyUsagePollingRenderer();
        var sut = new UsagePollingLoop(dataSource, renderer, TimeSpan.FromSeconds(60));

        // Act
        await sut.ExecuteIterationAsync(CancellationToken.None);

        // Assert
        Assert.Equal(1, renderer.RenderRequestFailedCallCount);
        Assert.Equal(1, renderer.TotalCallCount);
    }

    [Fact]
    public async Task ExecuteIterationAsync_ConMalformedResponse_ReutilizaRenderRequestFailed()
    {
        // Arrange: F2 -- nuevo caso UsageSnapshotStatus.MalformedResponse
        // (2xx sin cabeceras unified-* esperadas). El diseño fija
        // explícitamente que la consola reutiliza RenderRequestFailed (sin
        // método de renderer nuevo): desde este nivel, un contrato de API
        // roto y un fallo de red se muestran igual.
        var dataSource = FakeUsageDataSource.Returning(UsageSnapshot.MalformedResponse());
        var renderer = new SpyUsagePollingRenderer();
        var sut = new UsagePollingLoop(dataSource, renderer, TimeSpan.FromSeconds(60));

        // Act
        await sut.ExecuteIterationAsync(CancellationToken.None);

        // Assert
        Assert.Equal(1, renderer.RenderRequestFailedCallCount);
        Assert.Equal(1, renderer.TotalCallCount);
        Assert.Equal(0, renderer.RenderUnexpectedErrorCallCount);
    }

    [Fact]
    public async Task ExecuteIterationAsync_AnteExcepcionInesperadaDelDataSource_LaCapturaYLlamaARenderUnexpectedErrorSinPropagar()
    {
        // Arrange: este es el test que prueba directamente el criterio de
        // aceptación "el bucle sigue funcionando tras un error" — una
        // excepción no controlada por las capas inferiores (que hoy no
        // deberían lanzar ninguna para los casos esperados) no debe
        // terminar el proceso.
        var thrownException = new InvalidOperationException("fallo simulado no controlado");
        var dataSource = FakeUsageDataSource.Throwing(thrownException);
        var renderer = new SpyUsagePollingRenderer();
        var sut = new UsagePollingLoop(dataSource, renderer, TimeSpan.FromSeconds(60));

        // Act: si ExecuteIterationAsync dejase escapar la excepción, este
        // await la relanzaría y el test fallaría aquí mismo — el hecho de
        // llegar a los Asserts ya demuestra que no propaga.
        await sut.ExecuteIterationAsync(CancellationToken.None);

        // Assert
        Assert.Equal(1, renderer.RenderUnexpectedErrorCallCount);
        Assert.Equal(1, renderer.TotalCallCount);
        Assert.Same(thrownException, renderer.LastException);
    }

    [Fact]
    public async Task ExecuteIterationAsync_AnteOperationCanceledExceptionGenuina_LaPropagaSinRenderizarNada()
    {
        // Arrange: mismo criterio que AnthropicApiUsageDataSource (ver
        // diseño) — una OperationCanceledException genuina no se trata
        // como "fallo inesperado a mostrar", se deja propagar tal cual.
        var dataSource = FakeUsageDataSource.Throwing(new OperationCanceledException());
        var renderer = new SpyUsagePollingRenderer();
        var sut = new UsagePollingLoop(dataSource, renderer, TimeSpan.FromSeconds(60));

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => sut.ExecuteIterationAsync(CancellationToken.None));

        Assert.Equal(0, renderer.TotalCallCount);
    }
}
