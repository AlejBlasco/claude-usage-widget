namespace ClaudeMeter.Infrastructure.Tests.Usage;

/// <summary>
/// Test double de <see cref="HttpMessageHandler"/> que devuelve una
/// respuesta pre-configurada sin realizar ninguna llamada de red real, y
/// captura la última petición enviada para poder hacer aserciones sobre
/// sus headers/URL/método/cuerpo.
/// </summary>
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

    public HttpRequestMessage? LastRequest { get; private set; }

    /// <summary>
    /// Copia del cuerpo de la última petición, leída antes de que
    /// <see cref="AnthropicApiUsageDataSource.GetUsageAsync"/> haga
    /// <c>dispose</c> del <see cref="HttpRequestMessage"/> original (su
    /// <c>StringContent</c> deja de ser legible después de eso).
    /// </summary>
    public string? LastRequestBody { get; private set; }

    public int CallCount { get; private set; }

    public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        _responder = responder;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        CallCount++;
        LastRequest = request;
        LastRequestBody = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);
        return _responder(request);
    }
}
