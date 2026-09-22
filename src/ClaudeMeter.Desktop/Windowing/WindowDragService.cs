using System.Windows;
using System.Windows.Media;
using ClaudeMeter.Desktop.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace ClaudeMeter.Desktop.Windowing;

/// <summary>
/// Punto de entrada del gesto de arrastre (US-2), reenviado desde
/// <c>wwwroot/js/drag.js</c>. No usa <see cref="Window.DragMove"/>: WebView2
/// aloja su propio proceso de renderizado y retiene la captura de ratón a
/// nivel de SO durante el gesto, así que el bucle nativo que <c>DragMove</c>
/// intenta iniciar nunca recibe los <c>WM_MOUSEMOVE</c> posteriores (el
/// <c>ReleaseCapture()</c> interno de <c>DragMove</c> solo libera la captura
/// del hilo de WPF, no la del proceso de WebView2). En su lugar, JS seguía el
/// gesto completo y aquí solo se aplican los deltas discretos que reenvía a
/// <see cref="Window.Left"/>/<see cref="Window.Top"/>.
/// </summary>
public sealed class WindowDragService
{
    private readonly AppConfigStore _configStore;
    private readonly ILogger<WindowDragService> _logger;
    private Window? _window;

    public WindowDragService(AppConfigStore configStore, ILogger<WindowDragService> logger)
    {
        _configStore = configStore;
        _logger = logger;
    }

    /// <summary>Llamado una única vez desde el constructor de <c>MainWindow</c>.</summary>
    public void AttachWindow(Window window) => _window = window;

    [JSInvokable]
    public void BeginDrag()
    {
        if (_window is null)
        {
            _logger.LogWarning("BeginDrag invocado antes de que MainWindow estuviera adjunta; se ignora el gesto");
        }
    }

    /// <summary>
    /// Reposiciona la ventana en caliente durante el arrastre.
    /// <paramref name="deltaXDeviceUnits"/>/<paramref name="deltaYDeviceUnits"/>
    /// llegan en píxeles de dispositivo (ver <c>drag.js</c>) y se convierten a
    /// las unidades independientes del dispositivo que usan
    /// <see cref="Window.Left"/>/<see cref="Window.Top"/>. Sin log en el guard
    /// de "sin ventana": se dispara en cada mousemove, y <see cref="BeginDrag"/>
    /// ya registró la advertencia una vez para este mismo gesto.
    /// </summary>
    [JSInvokable]
    public void DragDelta(double deltaXDeviceUnits, double deltaYDeviceUnits)
    {
        if (_window is null)
        {
            return;
        }

        if (!_window.Dispatcher.CheckAccess())
        {
            _window.Dispatcher.Invoke(() => DragDelta(deltaXDeviceUnits, deltaYDeviceUnits));
            return;
        }

        var deltaDips = ToDeviceIndependentPixels(deltaXDeviceUnits, deltaYDeviceUnits);
        _window.Left += deltaDips.X;
        _window.Top += deltaDips.Y;
    }

    /// <summary>Persiste la posición final al soltar el botón (AC de US-2).</summary>
    [JSInvokable]
    public void EndDrag()
    {
        if (_window is null)
        {
            return;
        }

        if (!_window.Dispatcher.CheckAccess())
        {
            _window.Dispatcher.Invoke(EndDrag);
            return;
        }

        _configStore.SavePosition(_window.Left, _window.Top);
    }

    private Vector ToDeviceIndependentPixels(double deltaX, double deltaY)
    {
        var transform = PresentationSource.FromVisual(_window!)?.CompositionTarget?.TransformFromDevice
            ?? Matrix.Identity;
        return transform.Transform(new Vector(deltaX, deltaY));
    }
}
