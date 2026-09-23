namespace ClaudeMeter.Desktop.Configuration;

/// <summary>Posición de ventana ya resuelta (no necesariamente válida para las pantallas actuales — esa validación la hace <see cref="Windowing.WindowPositionResolver"/>).</summary>
/// <param name="Left">Coordenada Left en unidades independientes de dispositivo de WPF (1/96"), tal cual <c>Window.Left</c>.</param>
/// <param name="Top">Coordenada Top en unidades independientes de dispositivo de WPF (1/96"), tal cual <c>Window.Top</c>.</param>
public sealed record WindowPosition(double Left, double Top);

/// <summary>
/// Tema visual del widget (F3/Ciclo A, US-1). El nombre de cada miembro es,
/// por convención, la única fuente de verdad de dos representaciones
/// derivadas: la clase CSS (<see cref="AppThemeExtensions.ToCssClass"/>,
/// "theme-{nombre en minúsculas}") y el valor de texto de "theme" en
/// <c>config.json</c> (ver <see cref="AppConfigStore"/>, también el nombre
/// en minúsculas) — evita que ambas representaciones diverjan si se añade
/// un tercer tema en el futuro.
/// </summary>
public enum AppTheme
{
    Dark,
    Light
}

/// <summary>
/// Traduce <see cref="AppTheme"/> a la clase CSS que consume
/// <c>app.css</c>. Única fuente de verdad del mapeo para que cualquier
/// página Razor futura (p. ej. <c>MascotPage</c>, Ciclo C) no lo duplique
/// (riesgo "Alcance/consistencia futura" del documento de requisitos).
/// </summary>
public static class AppThemeExtensions
{
    public static string ToCssClass(this AppTheme theme) =>
        theme switch
        {
            AppTheme.Light => "theme-light",
            _ => "theme-dark",
        };
}

/// <summary>
/// Configuración de la aplicación ya resuelta en memoria, con los valores
/// por defecto aplicados donde <c>config.json</c> no exista, esté corrupto,
/// o tenga campos concretos fuera de rango (ver <see cref="AppConfigStore"/>).
/// </summary>
/// <param name="PollingInterval">Intervalo de refresco del poll. Por defecto 60s (idéntico al hardcodeado hasta este ciclo).</param>
/// <param name="Position"><c>null</c> = "sin posición configurada, calcular la esquina inferior derecha por defecto", igual que el comportamiento actual de <c>MainWindow</c>.</param>
/// <param name="ChimeEnabled">Por defecto <c>false</c> — un sonido inesperado en el primer arranque sería mala primera experiencia (decisión ya fijada por el documento de requisitos).</param>
/// <param name="Theme">
/// Por defecto <see cref="AppTheme.Dark"/> — paleta actual sin cambios
/// (AC de US-1: sin <c>config.json</c>/campo, tema oscuro, sin regresión).
/// El parámetro lleva valor por defecto <see cref="AppTheme.Dark"/> (no
/// presente literalmente en el Data Model del documento de diseño) para
/// que las construcciones posicionales de <c>AppConfig</c> ya existentes en
/// <c>test/ClaudeMeter.Desktop.Tests</c> (anteriores a este ciclo, sin
/// argumento <c>Theme</c>) sigan compilando sin tocar ningún fichero de
/// test — ver Deviations del resumen de implementación.
/// </param>
public sealed record AppConfig(TimeSpan PollingInterval, WindowPosition? Position, bool ChimeEnabled, AppTheme Theme = AppTheme.Dark)
{
    /// <summary>Valores por defecto actuales del widget antes de este ciclo: intervalo 60s, sin posición configurada, chime desactivado, tema oscuro.</summary>
    public static AppConfig Default { get; } =
        new(TimeSpan.FromSeconds(60), Position: null, ChimeEnabled: false, Theme: AppTheme.Dark);
}
