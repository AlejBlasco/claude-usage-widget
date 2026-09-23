using System.Windows;
using System.Windows.Media;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace ClaudeMeter.Desktop.Windowing;

/// <summary>
/// Mantiene <see cref="Window.Height"/> ajustada a la altura real del
/// contenido Razor, reportada desde <c>wwwroot/js/resize.js</c> vía
/// <c>ResizeObserver</c>. Necesario porque WebView2 aplica el "Tamaño de
/// texto" de Accesibilidad de Windows (<c>TextScaleFactor</c>) como zoom
/// sobre el contenido incluso con <c>font-size</c> en píxeles fijos, y una
/// altura de ventana fija (como la de F1) recortaría ese contenido con
/// <c>overflow: hidden</c> en vez de mostrarlo completo.
/// </summary>
public sealed class WindowResizeService
{
    private readonly ILogger<WindowResizeService> _logger;
    private Window? _window;

    public WindowResizeService(ILogger<WindowResizeService> logger) => _logger = logger;

    /// <summary>Llamado una única vez desde el constructor de <c>MainWindow</c>.</summary>
    public void AttachWindow(Window window) => _window = window;

    /// <summary>
    /// Ancla el borde inferior de la ventana: un widget clavado en una
    /// esquina (o en la posición donde el usuario lo arrastró, F2/Ciclo B)
    /// debe "crecer hacia arriba" cuando el contenido necesita más alto,
    /// no desplazar ese borde inferior/superior fijo.
    /// </summary>
    [JSInvokable]
    public void SetContentHeight(double contentHeightDeviceUnits)
    {
        if (_window is null)
        {
            _logger.LogWarning("SetContentHeight invocado antes de que MainWindow estuviera adjunta; se ignora");
            return;
        }

        if (!_window.Dispatcher.CheckAccess())
        {
            _window.Dispatcher.Invoke(() => SetContentHeight(contentHeightDeviceUnits));
            return;
        }

        var heightDips = ToDeviceIndependentPixels(contentHeightDeviceUnits);
        var delta = heightDips - _window.Height;
        if (Math.Abs(delta) < 0.5)
        {
            return;
        }

        _window.Top -= delta;
        _window.Height = heightDips;
    }

    private double ToDeviceIndependentPixels(double heightDeviceUnits)
    {
        var transform = PresentationSource.FromVisual(_window!)?.CompositionTarget?.TransformFromDevice
            ?? Matrix.Identity;
        return transform.Transform(new Vector(0, heightDeviceUnits)).Y;
    }
}
