namespace ClaudeMeter.Domain.Usage;

/// <summary>
/// Estados explícitos en los que puede resolverse el intento de obtener
/// el snapshot de uso/rate-limit real de la cuenta desde la API de
/// Anthropic.
/// </summary>
public enum UsageSnapshotStatus
{
    /// <summary>Se obtuvo el snapshot con las cabeceras de rate-limit esperadas.</summary>
    Success,

    /// <summary>
    /// No había un token OAuth utilizable (<see cref="ClaudeMeter.Application.Abstractions.ITokenProvider"/>
    /// no devolvió éxito); no se llegó a realizar ninguna llamada HTTP.
    /// </summary>
    TokenUnavailable,

    /// <summary>
    /// La API respondió 401 o 403: el token fue rechazado. Nunca se
    /// intenta refrescar el token por cuenta propia (regla de <c>CLAUDE.md</c>).
    /// </summary>
    Unauthorized,

    /// <summary>
    /// La petición falló por un motivo distinto a 401/403: error de red,
    /// timeout, código 5xx, u otra respuesta 2xx sin las cabeceras
    /// <c>anthropic-ratelimit-unified-*</c> mínimas esperadas.
    /// </summary>
    RequestFailed
}

/// <summary>
/// Cabeceras de rate-limit "en crudo" (sin parsear a minutos/porcentaje)
/// de una única ventana (sesión de 5h o semanal de 7d). Cualquier
/// propiedad puede ser <c>null</c> si la cabecera concreta no vino en la
/// respuesta — el nombre exacto de la cabecera "cantidad" (<c>utilization</c>
/// vs <c>remaining</c>) no está confirmado oficialmente (ver documento de
/// requisitos, Technical Notes).
/// </summary>
public sealed record RawRateLimitHeaders(
    string? Status,
    string? Utilization,
    string? Remaining,
    string? Reset);

/// <summary>
/// Representa el resultado de intentar obtener el snapshot de uso/rate-limit
/// real de la cuenta. Modela el caso "sin datos" como un tipo de dominio
/// explícito, análogo a <c>TokenResult</c>, de forma que los consumidores
/// nunca necesitan lanzar ni capturar excepciones para distinguir éxito de
/// los distintos fallos esperados. No calcula countdowns ni porcentajes —
/// esa responsabilidad queda fuera de alcance de este tipo.
/// </summary>
public sealed record UsageSnapshot
{
    /// <summary>Estado explícito del resultado.</summary>
    public UsageSnapshotStatus Status { get; }

    /// <summary>
    /// Cabeceras crudas de la ventana de sesión (5h) cuando
    /// <see cref="Status"/> es <see cref="UsageSnapshotStatus.Success"/>;
    /// <c>null</c> en cualquier otro estado.
    /// </summary>
    public RawRateLimitHeaders? Session { get; }

    /// <summary>
    /// Cabeceras crudas de la ventana semanal (7d) cuando
    /// <see cref="Status"/> es <see cref="UsageSnapshotStatus.Success"/>;
    /// <c>null</c> en cualquier otro estado.
    /// </summary>
    public RawRateLimitHeaders? Weekly { get; }

    /// <summary>
    /// Comprobación uniforme de si el snapshot contiene datos utilizables,
    /// sin necesidad de inspeccionar <see cref="Status"/>.
    /// </summary>
    public bool IsSuccess => Status == UsageSnapshotStatus.Success;

    private UsageSnapshot(UsageSnapshotStatus status, RawRateLimitHeaders? session, RawRateLimitHeaders? weekly)
    {
        Status = status;
        Session = session;
        Weekly = weekly;
    }

    /// <summary>Crea un resultado de éxito con las cabeceras crudas de ambas ventanas.</summary>
    public static UsageSnapshot Success(RawRateLimitHeaders session, RawRateLimitHeaders weekly) =>
        new(UsageSnapshotStatus.Success, session, weekly);

    /// <summary>Crea un resultado de "sin token disponible"; no hubo llamada HTTP.</summary>
    public static UsageSnapshot TokenUnavailable() =>
        new(UsageSnapshotStatus.TokenUnavailable, session: null, weekly: null);

    /// <summary>Crea un resultado de "no autorizado" (401/403).</summary>
    public static UsageSnapshot Unauthorized() =>
        new(UsageSnapshotStatus.Unauthorized, session: null, weekly: null);

    /// <summary>Crea un resultado de "fallo de la petición" (red, 5xx, cabeceras ausentes).</summary>
    public static UsageSnapshot RequestFailed() =>
        new(UsageSnapshotStatus.RequestFailed, session: null, weekly: null);
}
