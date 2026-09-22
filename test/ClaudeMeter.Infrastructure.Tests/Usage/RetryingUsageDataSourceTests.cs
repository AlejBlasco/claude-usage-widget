using ClaudeMeter.Domain.Usage;
using ClaudeMeter.Infrastructure.Usage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClaudeMeter.Infrastructure.Tests.Usage;

/// <summary>
/// Pruebas de <see cref="RetryingUsageDataSource"/> (US-2, F2): modelo de 3
/// categorías de reintento — únicamente <see cref="UsageSnapshotStatus.RequestFailed"/>
/// (fallo transitorio) se reintenta con backoff exponencial (2s -&gt; 4s,
/// techo 10s, 3 intentos totales, ver <see cref="RetryPolicyOptions.Default"/>);
/// <c>Success</c>, <c>TokenUnavailable</c>, <c>Unauthorized</c> y
/// <c>MalformedResponse</c> se devuelven de inmediato, sin disparar nunca un
/// segundo intento. Usa el constructor <c>internal</c> con función de espera
/// inyectable (<c>InternalsVisibleTo</c> ya declarado en
/// <c>ClaudeMeter.Infrastructure.csproj</c>) para no depender de
/// temporizadores/segundos reales — la suite completa corre en
/// milisegundos.
/// </summary>
public sealed class RetryingUsageDataSourceTests
{
    private static readonly RawRateLimitHeaders SampleHeaders = new("allowed", "10%", "90%", null);
    private static readonly RetryPolicyOptions DefaultPolicy = RetryPolicyOptions.Default;

    /// <summary>
    /// Función de espera falsa: no espera nada (<see cref="Task.CompletedTask"/>)
    /// pero registra cada <see cref="TimeSpan"/> solicitado, para poder
    /// verificar la secuencia exacta de backoff sin ralentizar la suite.
    /// </summary>
    private static (Func<TimeSpan, CancellationToken, Task> Delay, List<TimeSpan> Requested) NoOpDelay()
    {
        var requested = new List<TimeSpan>();

        Task Delay(TimeSpan delay, CancellationToken cancellationToken)
        {
            requested.Add(delay);
            return Task.CompletedTask;
        }

        return (Delay, requested);
    }

    [Fact]
    public async Task GetUsageAsync_ConExitoEnElPrimerIntento_DevuelveSuccessSinEsperarNiReintentar()
    {
        // Arrange
        var inner = new FakeUsageDataSource(UsageSnapshot.Success(SampleHeaders, SampleHeaders));
        var (delay, requested) = NoOpDelay();
        var sut = new RetryingUsageDataSource(inner, DefaultPolicy, NullLogger<RetryingUsageDataSource>.Instance, delay);

        // Act
        var result = await sut.GetUsageAsync();

        // Assert
        Assert.Equal(UsageSnapshotStatus.Success, result.Status);
        Assert.Equal(1, inner.CallCount);
        Assert.Empty(requested);
    }

    [Fact]
    public async Task GetUsageAsync_ConUnRequestFailedSeguidoDeExito_ReintentaUnaVezConDosSegundosDeEsperaYDevuelveSuccess()
    {
        // Arrange
        var inner = new FakeUsageDataSource(
            UsageSnapshot.RequestFailed(),
            UsageSnapshot.Success(SampleHeaders, SampleHeaders));
        var (delay, requested) = NoOpDelay();
        var sut = new RetryingUsageDataSource(inner, DefaultPolicy, NullLogger<RetryingUsageDataSource>.Instance, delay);

        // Act
        var result = await sut.GetUsageAsync();

        // Assert
        Assert.Equal(UsageSnapshotStatus.Success, result.Status);
        Assert.Equal(2, inner.CallCount);
        Assert.Equal(new[] { TimeSpan.FromSeconds(2) }, requested);
    }

    [Fact]
    public async Task GetUsageAsync_ConDosRequestFailedSeguidosDeExito_ReintentaDosVecesConBackoffExponencialYDevuelveSuccess()
    {
        // Arrange: verifica la secuencia de backoff completa (2s, luego 4s
        // -- factor x2, sin llegar al techo de 10s con solo 2 reintentos).
        var inner = new FakeUsageDataSource(
            UsageSnapshot.RequestFailed(),
            UsageSnapshot.RequestFailed(),
            UsageSnapshot.Success(SampleHeaders, SampleHeaders));
        var (delay, requested) = NoOpDelay();
        var sut = new RetryingUsageDataSource(inner, DefaultPolicy, NullLogger<RetryingUsageDataSource>.Instance, delay);

        // Act
        var result = await sut.GetUsageAsync();

        // Assert
        Assert.Equal(UsageSnapshotStatus.Success, result.Status);
        Assert.Equal(3, inner.CallCount);
        Assert.Equal(new[] { TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4) }, requested);
    }

    [Fact]
    public async Task GetUsageAsync_ConRequestFailedEnLosTresIntentos_AgotaLosReintentosYDevuelveElUltimoRequestFailed()
    {
        // Arrange: los 3 intentos de RetryPolicyOptions.Default (1 + 2
        // reintentos) fallan -- se propaga el último fallo transitorio sin
        // lanzar, y no se espera una cuarta vez tras el último intento.
        var inner = new FakeUsageDataSource(
            UsageSnapshot.RequestFailed(),
            UsageSnapshot.RequestFailed(),
            UsageSnapshot.RequestFailed());
        var (delay, requested) = NoOpDelay();
        var sut = new RetryingUsageDataSource(inner, DefaultPolicy, NullLogger<RetryingUsageDataSource>.Instance, delay);

        // Act
        var result = await sut.GetUsageAsync();

        // Assert
        Assert.Equal(UsageSnapshotStatus.RequestFailed, result.Status);
        Assert.False(result.IsSuccess);
        Assert.Equal(DefaultPolicy.MaxAttempts, inner.CallCount);
        // Solo se espera ENTRE intentos (2 esperas para 3 intentos), nunca
        // tras agotar el último.
        Assert.Equal(new[] { TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4) }, requested);
    }

    [Theory]
    [InlineData(nameof(UsageSnapshotStatus.TokenUnavailable))]
    [InlineData(nameof(UsageSnapshotStatus.Unauthorized))]
    [InlineData(nameof(UsageSnapshotStatus.MalformedResponse))]
    public async Task GetUsageAsync_ConUnEstadoQueNuncaSeReintenta_DevuelveDeInmediatoSinSegundoIntentoNiEspera(
        string statusName)
    {
        // Arrange: si RetryingUsageDataSource reintentase por error, el
        // segundo resultado en cola (RequestFailed) demostraría un segundo
        // intento real al aparecer como resultado final.
        var firstResult = statusName switch
        {
            nameof(UsageSnapshotStatus.TokenUnavailable) => UsageSnapshot.TokenUnavailable(),
            nameof(UsageSnapshotStatus.Unauthorized) => UsageSnapshot.Unauthorized(),
            nameof(UsageSnapshotStatus.MalformedResponse) => UsageSnapshot.MalformedResponse(),
            _ => throw new ArgumentOutOfRangeException(nameof(statusName)),
        };
        var inner = new FakeUsageDataSource(firstResult, UsageSnapshot.RequestFailed());
        var (delay, requested) = NoOpDelay();
        var sut = new RetryingUsageDataSource(inner, DefaultPolicy, NullLogger<RetryingUsageDataSource>.Instance, delay);

        // Act
        var result = await sut.GetUsageAsync();

        // Assert
        Assert.Equal(firstResult.Status, result.Status);
        Assert.Equal(1, inner.CallCount);
        Assert.Empty(requested);
    }

    [Fact]
    public async Task GetUsageAsync_ConElEstadoSuccess_DevuelveDeInmediatoSinSegundoIntentoNiEspera()
    {
        // Arrange: mismo caso que la Theory anterior pero para Success --
        // se separa porque Success no puede construirse con el switch por
        // nombre sin cabeceras válidas.
        var inner = new FakeUsageDataSource(
            UsageSnapshot.Success(SampleHeaders, SampleHeaders), UsageSnapshot.RequestFailed());
        var (delay, requested) = NoOpDelay();
        var sut = new RetryingUsageDataSource(inner, DefaultPolicy, NullLogger<RetryingUsageDataSource>.Instance, delay);

        // Act
        var result = await sut.GetUsageAsync();

        // Assert
        Assert.Equal(UsageSnapshotStatus.Success, result.Status);
        Assert.Equal(1, inner.CallCount);
        Assert.Empty(requested);
    }

    // --- US-3 (F2): nivel de log exacto por reintento/agotamiento. ---

    [Fact]
    public async Task GetUsageAsync_ConUnRequestFailedSeguidoDeExito_RegistraWarningPorElReintentoSinError()
    {
        var inner = new FakeUsageDataSource(
            UsageSnapshot.RequestFailed(),
            UsageSnapshot.Success(SampleHeaders, SampleHeaders));
        var (delay, _) = NoOpDelay();
        var logger = new CapturingLogger<RetryingUsageDataSource>();
        var sut = new RetryingUsageDataSource(inner, DefaultPolicy, logger, delay);

        await sut.GetUsageAsync();

        Assert.Contains(LogLevel.Warning, logger.LoggedLevels);
        Assert.DoesNotContain(LogLevel.Error, logger.LoggedLevels);
    }

    [Fact]
    public async Task GetUsageAsync_ConRequestFailedEnLosTresIntentos_RegistraErrorAlAgotarLosReintentos()
    {
        var inner = new FakeUsageDataSource(
            UsageSnapshot.RequestFailed(),
            UsageSnapshot.RequestFailed(),
            UsageSnapshot.RequestFailed());
        var (delay, _) = NoOpDelay();
        var logger = new CapturingLogger<RetryingUsageDataSource>();
        var sut = new RetryingUsageDataSource(inner, DefaultPolicy, logger, delay);

        await sut.GetUsageAsync();

        // 2 Warning (uno por cada reintento) + 1 Error final (agotamiento).
        Assert.Equal(2, logger.LoggedLevels.Count(l => l == LogLevel.Warning));
        Assert.Contains(LogLevel.Error, logger.LoggedLevels);
    }

    [Theory]
    [InlineData(nameof(UsageSnapshotStatus.TokenUnavailable))]
    [InlineData(nameof(UsageSnapshotStatus.Unauthorized))]
    [InlineData(nameof(UsageSnapshotStatus.MalformedResponse))]
    public async Task GetUsageAsync_ConUnEstadoQueNuncaSeReintenta_NoRegistraNingunLogPropio(string statusName)
    {
        // El decorator no añade ningún log propio cuando no reintenta -- el
        // logging de la categoría (Warning/Error) ya lo hizo la fuente
        // envuelta (ver AnthropicApiUsageDataSourceTests).
        var firstResult = statusName switch
        {
            nameof(UsageSnapshotStatus.TokenUnavailable) => UsageSnapshot.TokenUnavailable(),
            nameof(UsageSnapshotStatus.Unauthorized) => UsageSnapshot.Unauthorized(),
            nameof(UsageSnapshotStatus.MalformedResponse) => UsageSnapshot.MalformedResponse(),
            _ => throw new ArgumentOutOfRangeException(nameof(statusName)),
        };
        var inner = new FakeUsageDataSource(firstResult);
        var (delay, _) = NoOpDelay();
        var logger = new CapturingLogger<RetryingUsageDataSource>();
        var sut = new RetryingUsageDataSource(inner, DefaultPolicy, logger, delay);

        await sut.GetUsageAsync();

        Assert.Empty(logger.LoggedLevels);
    }

    [Fact]
    public async Task GetUsageAsync_ConElConstructorPublico_FuncionaSinReintentarUsandoTaskDelayReal()
    {
        // Cubre el constructor público (el que usa Task.Delay real en vez
        // de la función inyectable) — deliberadamente solo el camino sin
        // reintento, para no introducir una espera real de segundos en la
        // suite de tests.
        var inner = new FakeUsageDataSource(UsageSnapshot.Success(SampleHeaders, SampleHeaders));
        var sut = new RetryingUsageDataSource(inner, DefaultPolicy, NullLogger<RetryingUsageDataSource>.Instance);

        var result = await sut.GetUsageAsync();

        Assert.Equal(UsageSnapshotStatus.Success, result.Status);
        Assert.Equal(1, inner.CallCount);
    }
}
