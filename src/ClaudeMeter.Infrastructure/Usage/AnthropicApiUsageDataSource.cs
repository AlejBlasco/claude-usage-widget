using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClaudeMeter.Application.Abstractions;
using ClaudeMeter.Domain.Usage;
using Microsoft.Extensions.Logging;

namespace ClaudeMeter.Infrastructure.Usage;

/// <summary>
/// Implementación de <see cref="IUsageDataSource"/> que llama a
/// <c>POST https://api.anthropic.com/v1/messages</c> con el token OAuth
/// obtenido de <see cref="ITokenProvider"/> y traduce la respuesta cruda
/// (código de estado + cabeceras <c>anthropic-ratelimit-unified-*</c>) a
/// un <see cref="UsageSnapshot"/>. Su única responsabilidad es construir
/// la petición, enviarla y traducir la respuesta: no calcula countdowns
/// ni porcentajes, ni lee el token directamente de ningún fichero (eso
/// sigue siendo responsabilidad exclusiva de <see cref="ITokenProvider"/>),
/// ni reintenta ante fallos transitorios (delegado a
/// <see cref="RetryingUsageDataSource"/>, F2), ni intenta refrescar el
/// token OAuth ante 401/403. Registra cada intento HTTP individual con el
/// detalle más rico que solo esta clase conoce (excepción de red, código de
/// estado) — US-3 de F2.
/// </summary>
public sealed class AnthropicApiUsageDataSource : IUsageDataSource
{
    private const string MessagesEndpoint = "https://api.anthropic.com/v1/messages";
    private const string AnthropicVersion = "2023-06-01";
    private const string AnthropicBetaOAuth = "oauth-2025-04-20";

    // Modelo vigente más barato/rápido disponible: minimiza la cuota real
    // consumida por cada llamada (ver Risk "Cost/Quota" del documento de
    // requisitos). Valor provisional — confirmar en la validación manual
    // ya exigida por el Acceptance Criteria del issue original.
    private const string PingModel = "claude-haiku-4-5-20251001";

    // Formato "claude-code/<versión>" confirmado como obligatorio (evita
    // un bucket de rate-limit más agresivo). El número de versión exacto
    // es un literal hasta que F6 introduzca Nerdbank.GitVersioning.
    private const string UserAgentValue = "claude-code/0.1.0";

    private readonly ITokenProvider _tokenProvider;
    private readonly HttpClient _httpClient;
    private readonly ILogger<AnthropicApiUsageDataSource> _logger;

    /// <summary>
    /// Recibe tanto <see cref="ITokenProvider"/> como el <see cref="HttpClient"/>
    /// ya construido (compartido, de larga vida) por constructor, además de
    /// un <see cref="ILogger{TCategoryName}"/> (F2/US-3) — Infrastructure
    /// solo depende de la abstracción <c>Microsoft.Extensions.Logging</c>,
    /// nunca de Serilog concreto. La creación/configuración del
    /// <see cref="HttpClient"/> (incluida su vida útil como singleton) es
    /// responsabilidad del composition root (Desktop, fuera de alcance de
    /// este issue) — ver el diseño para el rationale de esta elección
    /// frente a <c>IHttpClientFactory</c> o un <see cref="HttpClient"/>
    /// propio.
    /// </summary>
    public AnthropicApiUsageDataSource(
        ITokenProvider tokenProvider, HttpClient httpClient, ILogger<AnthropicApiUsageDataSource> logger)
    {
        _tokenProvider = tokenProvider;
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<UsageSnapshot> GetUsageAsync(CancellationToken cancellationToken = default)
    {
        var tokenResult = await _tokenProvider.GetTokenAsync(cancellationToken);
        if (!tokenResult.IsSuccess)
        {
            // Warning, no Error: es un estado esperable antes del primer
            // login (aún no hay token), no necesariamente un fallo real —
            // a diferencia de Unauthorized (token presente pero rechazado
            // por la API), que sí se registra como Error. Regla de
            // CLAUDE.md / AC: sin token utilizable, nunca se realiza
            // ninguna llamada HTTP.
            _logger.LogWarning("No se pudo obtener un token OAuth utilizable; no se realiza ninguna llamada HTTP");
            return UsageSnapshot.TokenUnavailable();
        }

        using var request = BuildRequest(tokenResult.AccessToken!);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            // Error de red / DNS / conexión rechazada, etc.
            _logger.LogWarning(ex, "Fallo de red llamando a la API de Anthropic");
            return UsageSnapshot.RequestFailed();
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // Timeout interno de HttpClient (no cancelación pedida por el
            // llamante) — se traduce como fallo de la petición. La
            // OperationCanceledException genuina (cancellationToken
            // solicitado) se propaga sin capturar, igual que en
            // CredentialsFileTokenProvider.
            _logger.LogWarning(ex, "Timeout llamando a la API de Anthropic");
            return UsageSnapshot.RequestFailed();
        }

        using (response)
        {
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                // Nunca se intenta refrescar el token — regla de CLAUDE.md.
                _logger.LogError(
                    "La API de Anthropic devolvió {StatusCode}: token rechazado, no se reintenta", (int)response.StatusCode);
                return UsageSnapshot.Unauthorized();
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("La API de Anthropic devolvió {StatusCode}", (int)response.StatusCode);
                return UsageSnapshot.RequestFailed();
            }

            var session = ReadWindow(response.Headers, "5h");
            var weekly = ReadWindow(response.Headers, "7d");

            if (session.Status is null || weekly.Status is null)
            {
                // 200 pero sin las cabeceras unified-* mínimas esperadas:
                // contrato roto, no fallo de red (F2 — categoría 3 del
                // modelo de reintento, nunca se reintenta este caso).
                _logger.LogError("Respuesta 2xx sin las cabeceras anthropic-ratelimit-unified-* esperadas");
                return UsageSnapshot.MalformedResponse();
            }

            _logger.LogDebug("Snapshot de uso obtenido correctamente");
            return UsageSnapshot.Success(session, weekly);
        }
    }

    private static HttpRequestMessage BuildRequest(string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, MessagesEndpoint);

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Add("anthropic-version", AnthropicVersion);
        request.Headers.Add("anthropic-beta", AnthropicBetaOAuth);
        request.Headers.UserAgent.ParseAdd(UserAgentValue);

        var body = new CreateMessageRequestDto(
            Model: PingModel,
            MaxTokens: 1,
            Messages: [new MessageDto(Role: "user", Content: "ping")]);

        request.Content = new StringContent(
            JsonSerializer.Serialize(body),
            Encoding.UTF8,
            "application/json");

        return request;
    }

    private static RawRateLimitHeaders ReadWindow(HttpResponseHeaders headers, string windowSuffix) =>
        new(
            Status: GetHeaderValue(headers, $"anthropic-ratelimit-unified-{windowSuffix}-status"),
            Utilization: GetHeaderValue(headers, $"anthropic-ratelimit-unified-{windowSuffix}-utilization"),
            Remaining: GetHeaderValue(headers, $"anthropic-ratelimit-unified-{windowSuffix}-remaining"),
            Reset: GetHeaderValue(headers, $"anthropic-ratelimit-unified-{windowSuffix}-reset"));

    private static string? GetHeaderValue(HttpResponseHeaders headers, string name) =>
        headers.TryGetValues(name, out var values) ? values.FirstOrDefault() : null;

    private sealed record CreateMessageRequestDto(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("max_tokens")] int MaxTokens,
        [property: JsonPropertyName("messages")] IReadOnlyList<MessageDto> Messages);

    private sealed record MessageDto(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);
}
