using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Extensions.Logging;

namespace ClaudeMeter.Desktop.Windowing;

/// <summary>
/// Alterna el estilo extendido WS_EX_TRANSPARENT del HWND real de
/// MainWindow (US-1, issue #16). Por defecto IsEnabled=false y no toca la
/// ventana hasta el primer SetEnabled(true) -- arranque idéntico al
/// comportamiento actual (AC de US-1). No contradice el workaround
/// Rectangle Fill="#01000000" de MainWindow.xaml (F1/F2): ese workaround
/// resuelve el hit-test basado en alfa de UpdateLayeredWindow;
/// WS_EX_TRANSPARENT actúa en una capa anterior y hace que el SO no
/// entregue ningún mensaje de ratón a la ventana en absoluto, con
/// independencia del alfa de cada píxel.
/// </summary>
public sealed class ClickThroughService
{
    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x00000020;

    private readonly ILogger<ClickThroughService> _logger;
    private Window? _window;

    public ClickThroughService(ILogger<ClickThroughService> logger) => _logger = logger;

    public bool IsEnabled { get; private set; }

    /// <summary>Se levanta tras un SetEnabled que sí llegó a aplicarse, para que TrayIconService sincronice el Checked del ítem de menú (AC de US-1).</summary>
    public event Action? StateChanged;

    /// <summary>Llamado una única vez desde el constructor de <c>MainWindow</c>.</summary>
    public void AttachWindow(Window window) => _window = window;

    public void Toggle() => SetEnabled(!IsEnabled);

    public void SetEnabled(bool enabled)
    {
        if (_window is null)
        {
            _logger.LogWarning("SetEnabled invocado antes de que MainWindow estuviera adjunta; se ignora");
            return;
        }

        if (enabled == IsEnabled)
        {
            return;
        }

        var hwnd = new WindowInteropHelper(_window).Handle;
        var currentStyle = GetWindowLongPtr(hwnd, GwlExStyle).ToInt64();
        var newStyle = enabled ? currentStyle | WsExTransparent : currentStyle & ~WsExTransparent;
        SetWindowLongPtr(hwnd, GwlExStyle, new IntPtr(newStyle));

        IsEnabled = enabled;
        StateChanged?.Invoke();
    }

    // Shim de 32/64 bits: GetWindowLongPtr/SetWindowLongPtr no existen como
    // tales en Windows de 32 bits (ver Technology Choices).
    private static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex) =>
        IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, nIndex) : new IntPtr(GetWindowLong32(hWnd, nIndex));

    private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr newValue) =>
        IntPtr.Size == 8 ? SetWindowLongPtr64(hWnd, nIndex, newValue) : new IntPtr(SetWindowLong32(hWnd, nIndex, newValue.ToInt32()));

    [DllImport("user32.dll", EntryPoint = "GetWindowLong", SetLastError = true)]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
    private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    /// <summary><c>internal</c> + <c>InternalsVisibleTo</c>: permite a xUnit simular una transición de estado sin un HWND real, para probar que TrayIconService reacciona a StateChanged (ver TrayIconServiceTests).</summary>
    internal void SetEnabledForTests(bool enabled)
    {
        IsEnabled = enabled;
        StateChanged?.Invoke();
    }
}
