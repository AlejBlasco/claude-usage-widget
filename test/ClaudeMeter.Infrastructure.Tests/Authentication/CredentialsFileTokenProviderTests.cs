using ClaudeMeter.Domain.Authentication;
using ClaudeMeter.Infrastructure.Authentication;

namespace ClaudeMeter.Infrastructure.Tests.Authentication;

/// <summary>
/// Pruebas unitarias de <see cref="CredentialsFileTokenProvider"/>. Usa
/// siempre un fichero de fixture temporal real bajo <see cref="Path.GetTempPath"/>
/// (nunca la ruta fija de producción ni un token real), construido a través
/// del constructor <c>internal</c> habilitado para este proyecto de test
/// mediante <c>InternalsVisibleTo</c> en el <c>.csproj</c> de
/// <c>ClaudeMeter.Infrastructure</c>.
/// </summary>
public sealed class CredentialsFileTokenProviderTests : IDisposable
{
    private const string FakeAccessToken = "fake-access-token-for-tests";

    private readonly string _credentialsFilePath =
        Path.Combine(Path.GetTempPath(), $"claudemeter-tests-{Guid.NewGuid()}.json");

    public void Dispose()
    {
        if (File.Exists(_credentialsFilePath))
        {
            File.Delete(_credentialsFilePath);
        }
    }

    [Fact]
    public async Task GetTokenAsync_ConFicheroValido_DevuelveSuccessConElTokenExacto()
    {
        // Arrange
        await File.WriteAllTextAsync(
            _credentialsFilePath,
            $$"""
            {
              "claudeAiOauth": {
                "accessToken": "{{FakeAccessToken}}",
                "refreshToken": "fake-refresh-token-for-tests",
                "expiresAt": "2099-01-01T00:00:00Z"
              }
            }
            """);
        var sut = new CredentialsFileTokenProvider(_credentialsFilePath);

        // Act
        var result = await sut.GetTokenAsync();

        // Assert
        Assert.Equal(TokenResultStatus.Success, result.Status);
        Assert.Equal(FakeAccessToken, result.AccessToken);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task GetTokenAsync_SinFichero_DevuelveFileNotFound()
    {
        // Arrange: deliberadamente no se escribe ningún fichero en la ruta.
        var sut = new CredentialsFileTokenProvider(_credentialsFilePath);

        // Act
        var result = await sut.GetTokenAsync();

        // Assert
        Assert.Equal(TokenResultStatus.FileNotFound, result.Status);
        Assert.Null(result.AccessToken);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task GetTokenAsync_ConFicheroBloqueadoPorOtroProceso_DevuelveFileNotFound()
    {
        // Arrange: el fichero existe (pasa el File.Exists inicial) pero
        // queda bloqueado con acceso exclusivo justo antes de leerlo, para
        // forzar la IOException que captura CredentialsFileTokenProvider en
        // la condición de carrera documentada en el diseño (fichero borrado
        // o bloqueado por otro proceso entre el chequeo y la lectura).
        await File.WriteAllTextAsync(_credentialsFilePath, "{}");
        var sut = new CredentialsFileTokenProvider(_credentialsFilePath);

        await using (new FileStream(
            _credentialsFilePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.None))
        {
            // Act
            var result = await sut.GetTokenAsync();

            // Assert
            Assert.Equal(TokenResultStatus.FileNotFound, result.Status);
            Assert.Null(result.AccessToken);
            Assert.False(result.IsSuccess);
        }
    }

    [Fact]
    public async Task GetTokenAsync_ConJsonMalFormado_DevuelveInvalidJson()
    {
        // Arrange
        await File.WriteAllTextAsync(_credentialsFilePath, "{ esto no es JSON válido ");
        var sut = new CredentialsFileTokenProvider(_credentialsFilePath);

        // Act
        var result = await sut.GetTokenAsync();

        // Assert
        Assert.Equal(TokenResultStatus.InvalidJson, result.Status);
        Assert.Null(result.AccessToken);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task GetTokenAsync_SinCampoAccessToken_DevuelveTokenMissing()
    {
        // Arrange: JSON válido pero sin el objeto claudeAiOauth.
        await File.WriteAllTextAsync(_credentialsFilePath, """{ "otraCosa": true }""");
        var sut = new CredentialsFileTokenProvider(_credentialsFilePath);

        // Act
        var result = await sut.GetTokenAsync();

        // Assert
        Assert.Equal(TokenResultStatus.TokenMissing, result.Status);
        Assert.Null(result.AccessToken);
        Assert.False(result.IsSuccess);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetTokenAsync_ConAccessTokenVacioOEnBlanco_DevuelveTokenMissing(string blankToken)
    {
        // Arrange
        await File.WriteAllTextAsync(
            _credentialsFilePath,
            $$"""{ "claudeAiOauth": { "accessToken": "{{blankToken}}" } }""");
        var sut = new CredentialsFileTokenProvider(_credentialsFilePath);

        // Act
        var result = await sut.GetTokenAsync();

        // Assert
        Assert.Equal(TokenResultStatus.TokenMissing, result.Status);
        Assert.Null(result.AccessToken);
        Assert.False(result.IsSuccess);
    }

    [Theory]
    [InlineData(null)] // Fichero ausente -> FileNotFound
    [InlineData("{ json corrupto")] // -> InvalidJson
    [InlineData("""{ "claudeAiOauth": {} }""")] // -> TokenMissing
    public async Task GetTokenAsync_ParaCualquierEstadoDeFallo_IsSuccessEsFalse(string? fileContent)
    {
        // Arrange
        if (fileContent is not null)
        {
            await File.WriteAllTextAsync(_credentialsFilePath, fileContent);
        }
        var sut = new CredentialsFileTokenProvider(_credentialsFilePath);

        // Act
        var result = await sut.GetTokenAsync();

        // Assert: comprobación uniforme sin necesidad de inspeccionar Status,
        // válida para los tres estados de fallo (ningún caso lanza excepción).
        Assert.False(result.IsSuccess);
        Assert.NotEqual(TokenResultStatus.Success, result.Status);
    }

    [Fact]
    public async Task GetTokenAsync_ConJsonValidoNulo_DevuelveTokenMissing()
    {
        // Arrange: caso límite documentado en el diseño — JSON válido (el
        // literal "null") que deserializa a dto == null vía null-conditional,
        // en vez de lanzar una excepción.
        await File.WriteAllTextAsync(_credentialsFilePath, "null");
        var sut = new CredentialsFileTokenProvider(_credentialsFilePath);

        // Act
        var result = await sut.GetTokenAsync();

        // Assert
        Assert.Equal(TokenResultStatus.TokenMissing, result.Status);
        Assert.False(result.IsSuccess);
    }
}
