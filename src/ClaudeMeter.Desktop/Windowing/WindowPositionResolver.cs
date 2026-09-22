using System.Windows;
using ClaudeMeter.Desktop.Configuration;

namespace ClaudeMeter.Desktop.Windowing;

/// <summary>
/// Decide la posición final (Left, Top) de <c>MainWindow</c> a partir de la
/// posición configurada (si la hay) y las áreas de trabajo de las pantallas
/// actualmente conectadas. Función pura, sin ninguna dependencia de WPF real
/// ni de Win32 — testeable con listas de <see cref="Rect"/> simuladas (AC de
/// "posición fuera de pantalla" de US-1 y del último punto de la Definition
/// of Done heredada: "sin monitor/resolución real").
/// </summary>
public static class WindowPositionResolver
{
    /// <summary>
    /// Devuelve la posición configurada tal cual si cabe entera en alguna de
    /// <paramref name="allScreenWorkAreas"/>; en caso contrario (sin posición
    /// configurada, o configurada pero fuera de toda pantalla conectada hoy)
    /// aplica el mismo cálculo de esquina inferior derecha del monitor
    /// principal que ya existía antes de este ciclo.
    /// </summary>
    public static (double Left, double Top) Resolve(
        WindowPosition? configuredPosition,
        double windowWidth,
        double windowHeight,
        double screenMargin,
        Rect primaryScreenWorkArea,
        IReadOnlyList<Rect> allScreenWorkAreas)
    {
        if (configuredPosition is { } position &&
            FitsWithinAnyScreen(position.Left, position.Top, windowWidth, windowHeight, allScreenWorkAreas))
        {
            return (position.Left, position.Top);
        }

        // Sin posición configurada, o configurada pero fuera de toda pantalla
        // conectada hoy (AC explícito: fallback al monitor PRINCIPAL, no a
        // cualquier monitor disponible).
        return (
            primaryScreenWorkArea.Right - windowWidth - screenMargin,
            primaryScreenWorkArea.Bottom - windowHeight - screenMargin);
    }

    private static bool FitsWithinAnyScreen(double left, double top, double width, double height, IReadOnlyList<Rect> screens) =>
        screens.Any(screen =>
            left >= screen.Left && top >= screen.Top &&
            left + width <= screen.Right && top + height <= screen.Bottom);
}
