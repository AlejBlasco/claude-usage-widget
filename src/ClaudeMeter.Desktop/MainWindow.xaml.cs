using System.Windows;

namespace ClaudeMeter.Desktop;

/// <summary>
/// Ventana única del widget: sin bordes/topmost/transparente (US-1), aloja
/// un único <c>BlazorWebView</c> cuyo contenido visible vive por completo
/// en Razor (<c>Pages/UsagePage.razor</c>). Se posiciona en la esquina
/// inferior derecha del área de trabajo de la pantalla principal al
/// arrancar; la posición configurable queda fuera de alcance de F1 (ver F2
/// en <c>CLAUDE.md</c>).
/// </summary>
public partial class MainWindow : Window
{
    private const double ScreenMargin = 16;

    public MainWindow()
    {
        InitializeComponent();

        // System.Windows.Application.Current se escribe siempre totalmente
        // cualificado en el composition-root code de Desktop (norma
        // defensiva contra la colisión conocida con
        // ClaudeMeter.Application, ver comprobación de colisiones del
        // documento de diseño), aunque este fichero no tenga hoy ningún
        // `using ClaudeMeter.Application;` que la haga ambigua.
        BlazorWebViewHost.Services = ((App)System.Windows.Application.Current).Services;

        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - Width - ScreenMargin;
        Top = workArea.Bottom - Height - ScreenMargin;
    }
}
