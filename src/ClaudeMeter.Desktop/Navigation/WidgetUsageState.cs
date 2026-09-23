using ClaudeMeter.Domain.Usage;

namespace ClaudeMeter.Desktop.Navigation;

/// <summary>
/// Datos ya calculados por <see cref="ScreenNavigator"/> en cada ciclo de
/// poll, pasados como único <c>[Parameter]</c> compartido a la pantalla
/// activa (<c>UsagePage</c>/<c>MascotPage</c>) -- evita que ambas dependan
/// de nombres de clave sueltos en <c>DynamicComponent.Parameters</c>.
/// </summary>
public sealed record WidgetUsageState(
    RateLimitWindow Session,
    RateLimitWindow Weekly,
    UsageSnapshotStatus? Status,
    bool IsStale)
{
    /// <summary>Estado antes de que se complete el primer ciclo de poll: ambas ventanas sin dato, sin estado, no desactualizado.</summary>
    public static WidgetUsageState Initial { get; } =
        new(RateLimitWindow.Unavailable, RateLimitWindow.Unavailable, Status: null, IsStale: false);
}
