using ClaudeMeter.Domain.Usage;

namespace ClaudeMeter.ConsoleApp.Rendering;

/// <summary>
/// Renderizado de una iteración del bucle de polling (éxito o uno de los
/// cuatro casos de error) hacia una superficie de salida. Existe
/// exclusivamente como costura de test: permite testear la lógica de
/// despacho de <see cref="Polling.UsagePollingLoop"/> con un doble de test,
/// sin capturar <see cref="Console.Out"/>/<see cref="Console.Error"/> reales.
/// </summary>
public interface IUsagePollingRenderer
{
    /// <summary>Renderiza una iteración exitosa con ambas ventanas ya parseadas.</summary>
    void RenderSuccess(DateTimeOffset timestamp, RateLimitWindow session, RateLimitWindow weekly);

    /// <summary>Renderiza el error de "token no disponible".</summary>
    void RenderTokenUnavailable(DateTimeOffset timestamp);

    /// <summary>Renderiza el error de "token inválido/expirado" (401/403).</summary>
    void RenderUnauthorized(DateTimeOffset timestamp);

    /// <summary>Renderiza el error de "fallo de la petición" (red, 5xx, cabeceras ausentes).</summary>
    void RenderRequestFailed(DateTimeOffset timestamp);

    /// <summary>Renderiza una excepción no controlada por las capas inferiores.</summary>
    void RenderUnexpectedError(DateTimeOffset timestamp, Exception exception);
}

/// <summary>
/// Única implementación real: delega todo el formateo de texto en
/// <see cref="UsagePollingLineFormatter"/> (puro) y añade exclusivamente
/// la I/O de consola — éxito por <see cref="Console.Out"/>, los cuatro
/// casos de error por <see cref="Console.Error"/>.
/// </summary>
public sealed class ConsoleUsagePollingRenderer : IUsagePollingRenderer
{
    /// <inheritdoc />
    public void RenderSuccess(DateTimeOffset timestamp, RateLimitWindow session, RateLimitWindow weekly) =>
        Console.WriteLine(UsagePollingLineFormatter.FormatSuccessLine(timestamp, session, weekly));

    /// <inheritdoc />
    public void RenderTokenUnavailable(DateTimeOffset timestamp) =>
        Console.Error.WriteLine(UsagePollingLineFormatter.FormatTokenUnavailableLine(timestamp));

    /// <inheritdoc />
    public void RenderUnauthorized(DateTimeOffset timestamp) =>
        Console.Error.WriteLine(UsagePollingLineFormatter.FormatUnauthorizedLine(timestamp));

    /// <inheritdoc />
    public void RenderRequestFailed(DateTimeOffset timestamp) =>
        Console.Error.WriteLine(UsagePollingLineFormatter.FormatRequestFailedLine(timestamp));

    /// <inheritdoc />
    public void RenderUnexpectedError(DateTimeOffset timestamp, Exception exception) =>
        Console.Error.WriteLine(UsagePollingLineFormatter.FormatUnexpectedErrorLine(timestamp, exception));
}
