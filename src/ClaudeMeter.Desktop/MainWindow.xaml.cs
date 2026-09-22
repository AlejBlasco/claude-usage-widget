using System.Windows;
using ClaudeMeter.Desktop.Configuration;
using ClaudeMeter.Desktop.Windowing;
using Microsoft.Extensions.DependencyInjection;

namespace ClaudeMeter.Desktop;

/// <summary>
/// Ventana única del widget: sin bordes/topmost/transparente (US-1 de F1),
/// aloja un único <c>BlazorWebView</c> cuyo contenido visible vive por
/// completo en Razor (<c>Pages/UsagePage.razor</c>). Desde F2/Ciclo B la
/// posición inicial se resuelve con <see cref="WindowPositionResolver"/> a
/// partir de <c>config.json</c> (US-1), cayendo al cálculo de esquina
/// inferior derecha si no hay posición configurada o si ya no cabe en
/// ningún monitor conectado; y esta ventana se "adjunta" a
/// <see cref="WindowDragService"/> para que el gesto de arrastre (US-2)
/// pueda invocar <see cref="Window.DragMove"/> sobre ella.
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
        var app = (App)System.Windows.Application.Current;
        BlazorWebViewHost.Services = app.Services;

        var config = app.Services.GetRequiredService<AppConfig>();
        var (left, top) = WindowPositionResolver.Resolve(
            config.Position,
            Width,
            Height,
            ScreenMargin,
            SystemParameters.WorkArea,
            Win32ScreenInfo.GetAllWorkAreas());
        Left = left;
        Top = top;

        // US-2: la misma instancia que UsagePage.razor usará para registrar
        // el listener de JS interop de arrastre.
        app.Services.GetRequiredService<WindowDragService>().AttachWindow(this);

        // F3/Ciclo A: idem para el ajuste automático de altura al contenido
        // real (ver WindowResizeService para el rationale completo).
        app.Services.GetRequiredService<WindowResizeService>().AttachWindow(this);
    }
}
