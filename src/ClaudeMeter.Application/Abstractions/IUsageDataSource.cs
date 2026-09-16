using ClaudeMeter.Domain.Usage;

namespace ClaudeMeter.Application.Abstractions;

/// <summary>
/// Puerto de Application para obtener el snapshot de uso/rate-limit real
/// de la cuenta sin conocer el origen de los datos (API real de
/// Anthropic, futura fuente simulada, futuro <c>BleUsageSink</c>, etc.).
/// </summary>
public interface IUsageDataSource
{
    /// <summary>
    /// Obtiene el snapshot de uso actual. Nunca lanza una excepción para
    /// los casos esperados de "sin datos" (sin token, no autorizado,
    /// fallo de la petición) — esos casos se representan en el
    /// <see cref="UsageSnapshot"/> devuelto, de forma que cualquier
    /// implementación (real, simulada, BLE) sea sustituible por otra sin
    /// cambiar el comportamiento observable de este contrato (regla de
    /// sustituibilidad tipo Liskov, igual que <c>ITokenProvider</c>).
    /// </summary>
    Task<UsageSnapshot> GetUsageAsync(CancellationToken cancellationToken = default);
}
