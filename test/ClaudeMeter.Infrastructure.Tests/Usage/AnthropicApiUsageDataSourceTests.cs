using System.Net;
using System.Text.Json;
using ClaudeMeter.Domain.Authentication;
using ClaudeMeter.Domain.Usage;
using ClaudeMeter.Infrastructure.Usage;

namespace ClaudeMeter.Infrastructure.Tests.Usage;

/// <summary>
/// Pruebas unitarias de <see cref="AnthropicApiUsageDataSource"/>. Usa
/// siempre un <see cref="StubHttpMessageHandler"/>/<see cref="ThrowingHttpMessageHandler"/>
/// (nunca una llamada de red real) y un <see cref="FakeTokenProvider"/>
/// (nunca un token real), conforme a la regla de QA de <c>CLAUDE.md</c> y
/// al Acceptance Criteria del issue original ("Tests con HttpMessageHandler
/// fake, sin token real").
/// </summary>
public sealed class AnthropicApiUsageDataSourceTests
{
    private const string FakeAccessToken = "fake-access-token-for-tests";

    [Fact]
    public async Task GetUsageAsync_ConTokenDeExitoY200ConCabecerasCompletas_DevuelveSuccessConLosValoresExactos()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            response.Headers.Add("anthropic-ratelimit-unified-5h-status", "allowed");
            response.Headers.Add("anthropic-ratelimit-unified-5h-utilization", "10%");
            response.Headers.Add("anthropic-ratelimit-unified-5h-remaining", "90%");
            response.Headers.Add("anthropic-ratelimit-unified-5h-reset", "2026-09-16T12:00:00Z");
            response.Headers.Add("anthropic-ratelimit-unified-7d-status", "allowed");
            response.Headers.Add("anthropic-ratelimit-unified-7d-utilization", "5%");
            response.Headers.Add("anthropic-ratelimit-unified-7d-remaining", "95%");
            response.Headers.Add("anthropic-ratelimit-unified-7d-reset", "2026-09-20T00:00:00Z");
            return response;
        });
        var sut = new AnthropicApiUsageDataSource(
            new FakeTokenProvider(TokenResult.Success(FakeAccessToken)),
            new HttpClient(handler));

        // Act
        var result = await sut.GetUsageAsync();

        // Assert
        Assert.Equal(UsageSnapshotStatus.Success, result.Status);
        Assert.True(result.IsSuccess);

        Assert.NotNull(result.Session);
        Assert.Equal("allowed", result.Session!.Status);
        Assert.Equal("10%", result.Session!.Utilization);
        Assert.Equal("90%", result.Session!.Remaining);
        Assert.Equal("2026-09-16T12:00:00Z", result.Session!.Reset);

        Assert.NotNull(result.Weekly);
        Assert.Equal("allowed", result.Weekly!.Status);
        Assert.Equal("5%", result.Weekly!.Utilization);
        Assert.Equal("95%", result.Weekly!.Remaining);
        Assert.Equal("2026-09-20T00:00:00Z", result.Weekly!.Reset);
    }

    [Theory]
    [InlineData(nameof(TokenResultStatus.FileNotFound))]
    [InlineData(nameof(TokenResultStatus.InvalidJson))]
    [InlineData(nameof(TokenResultStatus.TokenMissing))]
    public async Task GetUsageAsync_ConTokenEnEstadoDeFallo_DevuelveTokenUnavailableSinLlamarAlHandler(
        string failureStatusName)
    {
        // Arrange
        var tokenResult = failureStatusName switch
        {
            nameof(TokenResultStatus.FileNotFound) => TokenResult.FileNotFound(),
            nameof(TokenResultStatus.InvalidJson) => TokenResult.InvalidJson(),
            nameof(TokenResultStatus.TokenMissing) => TokenResult.TokenMissing(),
            _ => throw new ArgumentOutOfRangeException(nameof(failureStatusName)),
        };
        var handler = new StubHttpMessageHandler(_ =>
            throw new InvalidOperationException("No debería llamarse al handler HTTP sin token."));
        var sut = new AnthropicApiUsageDataSource(new FakeTokenProvider(tokenResult), new HttpClient(handler));

        // Act
        var result = await sut.GetUsageAsync();

        // Assert
        Assert.Equal(UsageSnapshotStatus.TokenUnavailable, result.Status);
        Assert.False(result.IsSuccess);
        Assert.Null(result.Session);
        Assert.Null(result.Weekly);
        Assert.Equal(0, handler.CallCount);
        Assert.Null(handler.LastRequest);
    }

    [Fact]
    public async Task GetUsageAsync_Con401_DevuelveUnauthorized()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var sut = new AnthropicApiUsageDataSource(
            new FakeTokenProvider(TokenResult.Success(FakeAccessToken)),
            new HttpClient(handler));

        // Act
        var result = await sut.GetUsageAsync();

        // Assert
        Assert.Equal(UsageSnapshotStatus.Unauthorized, result.Status);
        Assert.False(result.IsSuccess);
        Assert.Null(result.Session);
        Assert.Null(result.Weekly);
    }

    [Fact]
    public async Task GetUsageAsync_Con403_DevuelveUnauthorized()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Forbidden));
        var sut = new AnthropicApiUsageDataSource(
            new FakeTokenProvider(TokenResult.Success(FakeAccessToken)),
            new HttpClient(handler));

        // Act
        var result = await sut.GetUsageAsync();

        // Assert
        Assert.Equal(UsageSnapshotStatus.Unauthorized, result.Status);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task GetUsageAsync_Con500_DevuelveRequestFailed()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var sut = new AnthropicApiUsageDataSource(
            new FakeTokenProvider(TokenResult.Success(FakeAccessToken)),
            new HttpClient(handler));

        // Act
        var result = await sut.GetUsageAsync();

        // Assert
        Assert.Equal(UsageSnapshotStatus.RequestFailed, result.Status);
        Assert.False(result.IsSuccess);
        Assert.Null(result.Session);
        Assert.Null(result.Weekly);
    }

    [Fact]
    public async Task GetUsageAsync_Con200SinCabecerasUnified_DevuelveRequestFailed()
    {
        // Arrange: 200 sin ninguna cabecera anthropic-ratelimit-unified-*.
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var sut = new AnthropicApiUsageDataSource(
            new FakeTokenProvider(TokenResult.Success(FakeAccessToken)),
            new HttpClient(handler));

        // Act
        var result = await sut.GetUsageAsync();

        // Assert
        Assert.Equal(UsageSnapshotStatus.RequestFailed, result.Status);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task GetUsageAsync_Con200ConSoloCabeceraDe5hStatus_DevuelveRequestFailed()
    {
        // Arrange: caso límite explícito del diseño — solo llega la ventana
        // de 5h, falta la de 7d; no debe clasificarse como éxito.
        var handler = new StubHttpMessageHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            response.Headers.Add("anthropic-ratelimit-unified-5h-status", "allowed");
            return response;
        });
        var sut = new AnthropicApiUsageDataSource(
            new FakeTokenProvider(TokenResult.Success(FakeAccessToken)),
            new HttpClient(handler));

        // Act
        var result = await sut.GetUsageAsync();

        // Assert
        Assert.Equal(UsageSnapshotStatus.RequestFailed, result.Status);
    }

    [Fact]
    public async Task GetUsageAsync_AnteHttpRequestException_DevuelveRequestFailedSinLanzar()
    {
        // Arrange
        var sut = new AnthropicApiUsageDataSource(
            new FakeTokenProvider(TokenResult.Success(FakeAccessToken)),
            new HttpClient(new ThrowingHttpMessageHandler()));

        // Act
        var result = await sut.GetUsageAsync();

        // Assert: ninguna excepción no controlada escapa del método.
        Assert.Equal(UsageSnapshotStatus.RequestFailed, result.Status);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task GetUsageAsync_AnteTimeoutInternoSinCancelacionPedida_DevuelveRequestFailedSinLanzar()
    {
        // Arrange: TaskCanceledException lanzada por el handler sin que el
        // cancellationToken por defecto (CancellationToken.None) esté
        // marcado como cancelado — simula HttpClient.Timeout, no una
        // cancelación genuina pedida por el consumidor.
        var sut = new AnthropicApiUsageDataSource(
            new FakeTokenProvider(TokenResult.Success(FakeAccessToken)),
            new HttpClient(new TimeoutHttpMessageHandler()));

        // Act
        var result = await sut.GetUsageAsync();

        // Assert
        Assert.Equal(UsageSnapshotStatus.RequestFailed, result.Status);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task GetUsageAsync_ConTokenDeExito_EnviaLosHeadersEsperados()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            response.Headers.Add("anthropic-ratelimit-unified-5h-status", "allowed");
            response.Headers.Add("anthropic-ratelimit-unified-7d-status", "allowed");
            return response;
        });
        var sut = new AnthropicApiUsageDataSource(
            new FakeTokenProvider(TokenResult.Success(FakeAccessToken)),
            new HttpClient(handler));

        // Act
        await sut.GetUsageAsync();

        // Assert
        var request = handler.LastRequest;
        Assert.NotNull(request);
        Assert.Equal("Bearer", request!.Headers.Authorization?.Scheme);
        Assert.Equal(FakeAccessToken, request.Headers.Authorization?.Parameter);
        Assert.Equal(new[] { "2023-06-01" }, request.Headers.GetValues("anthropic-version"));
        Assert.Equal(new[] { "oauth-2025-04-20" }, request.Headers.GetValues("anthropic-beta"));
        Assert.Equal("claude-code/0.1.0", request.Headers.UserAgent.ToString());
        Assert.Equal("application/json", request.Content?.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GetUsageAsync_ConTokenDeExito_EnviaPostALaUrlYPayloadEsperados()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            response.Headers.Add("anthropic-ratelimit-unified-5h-status", "allowed");
            response.Headers.Add("anthropic-ratelimit-unified-7d-status", "allowed");
            return response;
        });
        var sut = new AnthropicApiUsageDataSource(
            new FakeTokenProvider(TokenResult.Success(FakeAccessToken)),
            new HttpClient(handler));

        // Act
        await sut.GetUsageAsync();

        // Assert
        var request = handler.LastRequest;
        Assert.NotNull(request);
        Assert.Equal(HttpMethod.Post, request!.Method);
        Assert.Equal("https://api.anthropic.com/v1/messages", request.RequestUri?.ToString());

        var bodyJson = handler.LastRequestBody;
        Assert.NotNull(bodyJson);
        using var body = JsonDocument.Parse(bodyJson!);
        var root = body.RootElement;

        Assert.Equal(1, root.GetProperty("max_tokens").GetInt32());
        var messages = root.GetProperty("messages");
        Assert.Equal(1, messages.GetArrayLength());
        Assert.Equal("user", messages[0].GetProperty("role").GetString());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("model").GetString()));
    }
}
