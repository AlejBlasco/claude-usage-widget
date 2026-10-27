using System;

namespace ClaudeMeter.Desktop;

/// <summary>
/// Punto de entrada real del proceso (US-2/US-3, F6, issues #25/#26).
/// Sustituye al <c>Main()</c> autogenerado por WPF a partir de
/// <c>App.xaml</c> (que ya no es <c>ApplicationDefinition</c>, ver
/// <c>ClaudeMeter.Desktop.csproj</c>) porque esa generación automática no
/// permite ejecutar código antes de <c>InitializeComponent()</c>.
/// </summary>
internal static class Program
{
    /// <summary>
    /// <see cref="Velopack.VelopackApp.Build"/>().<c>Run()</c> debe ser la
    /// primera línea, antes de cualquier inicialización de WPF/Serilog/DI:
    /// es la técnica oficial documentada por Velopack para aplicaciones WPF
    /// (ver "Technology Choices" del documento de diseño de F6) -- si
    /// <c>Run()</c> va a terminar el proceso para aplicar una actualización
    /// o gestionar un evento de instalación, evita inicializar WPF/
    /// BlazorWebView de más. Con <c>SetAutoApplyOnStartup</c> activado por
    /// defecto, aplica automáticamente cualquier actualización ya
    /// descargada por <see cref="Updates.AppUpdateService"/> en una sesión
    /// anterior.
    /// </summary>
    [STAThread]
    private static void Main()
    {
        Velopack.VelopackApp.Build().Run();

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
