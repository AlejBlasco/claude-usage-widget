using ClaudeMeter.Domain.Authentication;

namespace ClaudeMeter.Application.Abstractions;

/// <summary>
/// Puerto de Application para obtener el token OAuth de Claude Code sin
/// conocer su origen ni formato de almacenamiento (fichero, futuro
/// Credential Manager, etc.).
/// </summary>
public interface ITokenProvider
{
    /// <summary>
    /// Obtiene el token OAuth actual. Nunca lanza una excepción para los
    /// casos esperados de "sin datos" (fichero ausente, JSON inválido,
    /// token ausente/vacío) — esos casos se representan en el
    /// <see cref="TokenResult"/> devuelto.
    /// </summary>
    Task<TokenResult> GetTokenAsync(CancellationToken cancellationToken = default);
}
