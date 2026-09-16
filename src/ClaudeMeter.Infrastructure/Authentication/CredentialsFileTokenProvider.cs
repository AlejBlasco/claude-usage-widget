using System.Text.Json;
using System.Text.Json.Serialization;
using ClaudeMeter.Application.Abstractions;
using ClaudeMeter.Domain.Authentication;

namespace ClaudeMeter.Infrastructure.Authentication;

/// <summary>
/// Implementación de <see cref="ITokenProvider"/> que lee el token OAuth
/// en texto plano desde el fichero de credenciales del CLI de Claude Code
/// (<c>%USERPROFILE%\.claude\.credentials.json</c> en Windows). Su única
/// responsabilidad es leer y parsear ese fichero: no realiza llamadas a la
/// API de Anthropic ni calcula countdowns o porcentajes de rate limit.
/// </summary>
public sealed class CredentialsFileTokenProvider : ITokenProvider
{
    private readonly string _credentialsFilePath;

    /// <summary>
    /// Constructor de producción: usa siempre la ruta fija
    /// <c>%USERPROFILE%\.claude\.credentials.json</c>. No expone forma
    /// alguna de configurar la ruta en runtime (F0 la deja hardcodeada
    /// por diseño; la configurabilidad llega en F2 con <c>config.json</c>).
    /// </summary>
    public CredentialsFileTokenProvider()
        : this(GetDefaultCredentialsFilePath())
    {
    }

    /// <summary>
    /// Constructor interno usado únicamente por
    /// <c>ClaudeMeter.Infrastructure.Tests</c> (vía
    /// <c>InternalsVisibleTo</c>) para apuntar a un fichero de fixture
    /// temporal en los tests, sin tocar el fichero real del usuario.
    /// No forma parte de la superficie pública del ensamblado.
    /// </summary>
    internal CredentialsFileTokenProvider(string credentialsFilePath)
    {
        _credentialsFilePath = credentialsFilePath;
    }

    private static string GetDefaultCredentialsFilePath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".claude",
            ".credentials.json");

    /// <inheritdoc />
    public async Task<TokenResult> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_credentialsFilePath))
        {
            return TokenResult.FileNotFound();
        }

        string json;
        try
        {
            json = await File.ReadAllTextAsync(_credentialsFilePath, cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // El fichero existía en el check anterior pero dejó de ser
            // accesible (borrado/bloqueado por otro proceso justo entre
            // medias, permisos, etc.). Para el consumidor de ITokenProvider
            // el efecto es indistinguible de "no hay fichero disponible".
            return TokenResult.FileNotFound();
        }

        CredentialsFileDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<CredentialsFileDto>(json);
        }
        catch (JsonException)
        {
            return TokenResult.InvalidJson();
        }

        var accessToken = dto?.ClaudeAiOauth?.AccessToken;
        return string.IsNullOrWhiteSpace(accessToken)
            ? TokenResult.TokenMissing()
            : TokenResult.Success(accessToken);
    }

    private sealed record CredentialsFileDto(
        [property: JsonPropertyName("claudeAiOauth")] OAuthSectionDto? ClaudeAiOauth);

    private sealed record OAuthSectionDto(
        [property: JsonPropertyName("accessToken")] string? AccessToken);
}
