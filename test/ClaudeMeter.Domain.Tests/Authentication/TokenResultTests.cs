using ClaudeMeter.Domain.Authentication;

namespace ClaudeMeter.Domain.Tests.Authentication;

/// <summary>
/// Pruebas puras (sin E/S) de las cuatro fábricas estáticas de
/// <see cref="TokenResult"/>: verifican que cada una produce el
/// <c>Status</c>/<c>AccessToken</c>/<c>IsSuccess</c> esperado, y que el
/// contrato "sin datos" nunca requiere lanzar ni capturar excepciones.
/// </summary>
public sealed class TokenResultTests
{
    [Fact]
    public void Success_ConToken_DevuelveStatusSuccessYElTokenExacto()
    {
        var result = TokenResult.Success("un-token-cualquiera");

        Assert.Equal(TokenResultStatus.Success, result.Status);
        Assert.Equal("un-token-cualquiera", result.AccessToken);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void FileNotFound_DevuelveStatusFileNotFoundSinToken()
    {
        var result = TokenResult.FileNotFound();

        Assert.Equal(TokenResultStatus.FileNotFound, result.Status);
        Assert.Null(result.AccessToken);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void InvalidJson_DevuelveStatusInvalidJsonSinToken()
    {
        var result = TokenResult.InvalidJson();

        Assert.Equal(TokenResultStatus.InvalidJson, result.Status);
        Assert.Null(result.AccessToken);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void TokenMissing_DevuelveStatusTokenMissingSinToken()
    {
        var result = TokenResult.TokenMissing();

        Assert.Equal(TokenResultStatus.TokenMissing, result.Status);
        Assert.Null(result.AccessToken);
        Assert.False(result.IsSuccess);
    }

    [Theory]
    [InlineData(TokenResultStatus.FileNotFound)]
    [InlineData(TokenResultStatus.InvalidJson)]
    [InlineData(TokenResultStatus.TokenMissing)]
    public void IsSuccess_ParaCualquierEstadoDeFallo_EsSiempreFalse(TokenResultStatus failureStatus)
    {
        var result = failureStatus switch
        {
            TokenResultStatus.FileNotFound => TokenResult.FileNotFound(),
            TokenResultStatus.InvalidJson => TokenResult.InvalidJson(),
            TokenResultStatus.TokenMissing => TokenResult.TokenMissing(),
            _ => throw new ArgumentOutOfRangeException(nameof(failureStatus)),
        };

        Assert.False(result.IsSuccess);
    }
}
