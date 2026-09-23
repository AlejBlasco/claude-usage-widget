using System.Windows;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace ClaudeMeter.Desktop.Windowing;

/// <summary>
/// Punto de entrada del gesto de cierre directo (US-2), reenviado desde
/// <c>wwwroot/js/close.js</c>. Converge en el mismo punto de salida que
/// "Salir" desde la bandeja (<see cref="System.Windows.Application.Shutdown"/>),
/// que a su vez dispara <c>App.OnExit</c> -- así ambas vías producen
/// exactamente el mismo efecto observable (AC de US-2).
/// </summary>
public sealed class WindowCloseService
{
    private readonly ILogger<WindowCloseService> _logger;
    private Window? _window;

    public WindowCloseService(ILogger<WindowCloseService> logger) => _logger = logger;

    /// <summary>Llamado una única vez desde el constructor de <c>MainWindow</c>.</summary>
    public void AttachWindow(Window window) => _window = window;

    [JSInvokable]
    public void RequestClose()
    {
        if (_window is null)
        {
            _logger.LogWarning("RequestClose invocado antes de que MainWindow estuviera adjunta; se ignora");
            return;
        }

        if (!_window.Dispatcher.CheckAccess())
        {
            _window.Dispatcher.Invoke(RequestClose);
            return;
        }

        System.Windows.Application.Current.Shutdown();
    }
}
