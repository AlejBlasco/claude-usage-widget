using ClaudeMeter.Application.Abstractions;
using ClaudeMeter.Domain.Authentication;

namespace ClaudeMeter.Infrastructure.Tests.Usage;

/// <summary>
/// Test double de <see cref="ITokenProvider"/> que devuelve siempre un
/// <see cref="TokenResult"/> pre-configurado (nunca lee un fichero real ni
/// contiene un token real).
/// </summary>
internal sealed class FakeTokenProvider : ITokenProvider
{
    private readonly TokenResult _result;

    public FakeTokenProvider(TokenResult result) => _result = result;

    public Task<TokenResult> GetTokenAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_result);
}
