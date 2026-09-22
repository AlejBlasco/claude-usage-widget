namespace ClaudeMeter.Desktop.Configuration;

/// <summary>Posición de ventana ya resuelta (no necesariamente válida para las pantallas actuales — esa validación la hace <see cref="Windowing.WindowPositionResolver"/>).</summary>
/// <param name="Left">Coordenada Left en unidades independientes de dispositivo de WPF (1/96"), tal cual <c>Window.Left</c>.</param>
/// <param name="Top">Coordenada Top en unidades independientes de dispositivo de WPF (1/96"), tal cual <c>Window.Top</c>.</param>
public sealed record WindowPosition(double Left, double Top);

/// <summary>
/// Configuración de la aplicación ya resuelta en memoria, con los valores
/// por defecto aplicados donde <c>config.json</c> no exista, esté corrupto,
/// o tenga campos concretos fuera de rango (ver <see cref="AppConfigStore"/>).
/// </summary>
/// <param name="PollingInterval">Intervalo de refresco del poll. Por defecto 60s (idéntico al hardcodeado hasta este ciclo).</param>
/// <param name="Position"><c>null</c> = "sin posición configurada, calcular la esquina inferior derecha por defecto", igual que el comportamiento actual de <c>MainWindow</c>.</param>
/// <param name="ChimeEnabled">Por defecto <c>false</c> — un sonido inesperado en el primer arranque sería mala primera experiencia (decisión ya fijada por el documento de requisitos).</param>
public sealed record AppConfig(TimeSpan PollingInterval, WindowPosition? Position, bool ChimeEnabled)
{
    /// <summary>Valores por defecto actuales del widget antes de este ciclo: intervalo 60s, sin posición configurada, chime desactivado.</summary>
    public static AppConfig Default { get; } = new(TimeSpan.FromSeconds(60), Position: null, ChimeEnabled: false);
}
