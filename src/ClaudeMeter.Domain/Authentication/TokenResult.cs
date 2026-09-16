namespace ClaudeMeter.Domain.Authentication;

/// <summary>
/// Estados explícitos en los que puede resolverse el intento de obtener
/// el token OAuth de Claude Code desde su fuente de credenciales.
/// </summary>
public enum TokenResultStatus
{
    /// <summary>Se obtuvo un token utilizable.</summary>
    Success,

    /// <summary>El fichero de credenciales no existe en la ruta esperada.</summary>
    FileNotFound,

    /// <summary>El fichero existe pero su contenido no es JSON válido.</summary>
    InvalidJson,

    /// <summary>
    /// El JSON es válido pero falta el objeto/campo del token, o el token
    /// está vacío o en blanco.
    /// </summary>
    TokenMissing
}

/// <summary>
/// Representa el resultado de intentar obtener el token OAuth. Modela el
/// caso "sin datos" como un tipo de dominio explícito en vez de
/// <c>string?</c> o un <c>Result&lt;string&gt;</c> genérico, de forma que
/// los consumidores nunca necesitan lanzar ni capturar excepciones para
/// distinguir éxito de los distintos fallos esperados.
/// </summary>
public sealed record TokenResult
{
    /// <summary>Estado explícito del resultado.</summary>
    public TokenResultStatus Status { get; }

    /// <summary>
    /// Valor del token cuando <see cref="Status"/> es
    /// <see cref="TokenResultStatus.Success"/>; <c>null</c> en cualquier
    /// otro estado.
    /// </summary>
    public string? AccessToken { get; }

    /// <summary>
    /// Comprobación uniforme de si el resultado contiene un token
    /// utilizable, sin necesidad de inspeccionar <see cref="Status"/>.
    /// </summary>
    public bool IsSuccess => Status == TokenResultStatus.Success;

    private TokenResult(TokenResultStatus status, string? accessToken)
    {
        Status = status;
        AccessToken = accessToken;
    }

    /// <summary>Crea un resultado de éxito con el token leído, sin transformarlo.</summary>
    public static TokenResult Success(string accessToken) =>
        new(TokenResultStatus.Success, accessToken);

    /// <summary>Crea un resultado de "fichero no encontrado".</summary>
    public static TokenResult FileNotFound() =>
        new(TokenResultStatus.FileNotFound, accessToken: null);

    /// <summary>Crea un resultado de "JSON inválido".</summary>
    public static TokenResult InvalidJson() =>
        new(TokenResultStatus.InvalidJson, accessToken: null);

    /// <summary>Crea un resultado de "token ausente o vacío".</summary>
    public static TokenResult TokenMissing() =>
        new(TokenResultStatus.TokenMissing, accessToken: null);
}
