namespace ClaudeMeter.Desktop.Navigation;

/// <summary>
/// Ciclado por índice sobre una lista fija de tipos de pantalla. Sin
/// ninguna dependencia de Blazor/WPF -- mismo criterio de separación que
/// <c>Windowing.WindowPositionResolver</c> frente a <c>MainWindow</c> -- para
/// poder probarse con xUnit puro usando tipos cualesquiera como sustitutos
/// (AC de US-1: una única pantalla registrada nunca lanza al ciclar).
/// </summary>
public sealed class ScreenCycle
{
    private readonly IReadOnlyList<Type> _screens;

    public ScreenCycle(IReadOnlyList<Type> screens)
    {
        if (screens.Count == 0)
        {
            throw new ArgumentException("ScreenCycle requiere al menos una pantalla registrada.", nameof(screens));
        }

        _screens = screens;
        Current = _screens[0];
    }

    public Type Current { get; private set; }

    /// <summary>Número de pantallas registradas -- usado por <c>ScreenNavigator</c> para decidir si muestra el control de cambio de pantalla.</summary>
    public int ScreenCount => _screens.Count;

    /// <summary>Avanza al siguiente tipo en orden cíclico (tras el último, vuelve al primero) y lo devuelve. Con una única pantalla registrada, siempre devuelve la misma sin lanzar (AC de caso límite de US-1).</summary>
    public Type Next()
    {
        Current = _screens[(IndexOfCurrent() + 1) % _screens.Count];
        return Current;
    }

    // IReadOnlyList<Type> no expone IndexOf (solo IList<T> lo hace) -- se
    // resuelve con una búsqueda lineal propia en vez de forzar el tipo del
    // campo a IList<Type> (que ampliaría innecesariamente el contrato del
    // constructor más allá de "cualquier lista de solo lectura de tipos").
    private int IndexOfCurrent()
    {
        for (var i = 0; i < _screens.Count; i++)
        {
            if (_screens[i] == Current)
            {
                return i;
            }
        }

        return -1;
    }
}
