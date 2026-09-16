namespace ClaudeMeter.Infrastructure.Tests.Usage;

/// <summary>
/// Test double de <see cref="HttpMessageHandler"/> que simula un fallo de
/// red (DNS/conexión rechazada/etc.) lanzando siempre
/// <see cref="HttpRequestException"/> desde <c>SendAsync</c>, sin realizar
/// ninguna llamada de red real.
/// </summary>
internal sealed class ThrowingHttpMessageHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken) =>
        throw new HttpRequestException("Fallo de red simulado para tests.");
}

/// <summary>
/// Test double de <see cref="HttpMessageHandler"/> que simula el timeout
/// interno de <see cref="HttpClient"/> (<c>HttpClient.Timeout</c>) lanzando
/// siempre <see cref="TaskCanceledException"/> sin que el llamante haya
/// pedido la cancelación (<see cref="CancellationToken.IsCancellationRequested"/>
/// permanece en <c>false</c>), distinguiéndose así de una cancelación
/// genuina pedida por el consumidor.
/// </summary>
internal sealed class TimeoutHttpMessageHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken) =>
        throw new TaskCanceledException("Timeout simulado para tests.");
}
