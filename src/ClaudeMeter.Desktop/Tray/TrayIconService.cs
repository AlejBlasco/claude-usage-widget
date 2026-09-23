using System.Drawing;
using System.IO;
using System.Windows.Forms;
using ClaudeMeter.Desktop.Polling;
using ClaudeMeter.Desktop.Windowing;
using Microsoft.Extensions.Logging;

namespace ClaudeMeter.Desktop.Tray;

/// <summary>
/// Única clase de Desktop que referencia System.Windows.Forms (US-2, issue
/// #17). El constructor solo construye el menú y suscribe los eventos --
/// nunca toca NotifyIcon.Visible -- para que sea instanciable en xUnit sin
/// sesión de escritorio real. Initialize() (llamado una vez desde
/// App.OnStartup) resuelve el icono real y muestra el NotifyIcon.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private readonly PollingControlService _pollingControl;
    private readonly ClickThroughService _clickThrough;
    private readonly ILogger<TrayIconService> _logger;
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _pauseResumeItem;
    private readonly ToolStripMenuItem _reloadItem;
    private readonly ToolStripMenuItem _clickThroughItem;
    private readonly ToolStripMenuItem _exitItem;
    private bool _disposed;

    public TrayIconService(PollingControlService pollingControl, ClickThroughService clickThrough, ILogger<TrayIconService> logger)
    {
        _pollingControl = pollingControl;
        _clickThrough = clickThrough;
        _logger = logger;

        _pauseResumeItem = new ToolStripMenuItem("Pausar");
        _pauseResumeItem.Click += (_, _) => _pollingControl.TogglePause();

        _reloadItem = new ToolStripMenuItem("Recargar");
        _reloadItem.Click += (_, _) => _pollingControl.RequestReload();

        _clickThroughItem = new ToolStripMenuItem("Ignorar clics") { CheckOnClick = false };
        _clickThroughItem.Click += (_, _) => _clickThrough.Toggle();

        _exitItem = new ToolStripMenuItem("Salir");
        // Fully-qualified a propósito (norma defensiva ya usada en
        // MainWindow.xaml.cs): este fichero tiene `using System.Windows.Forms;`,
        // que también declara su propia clase Application distinta de
        // System.Windows.Application.
        _exitItem.Click += (_, _) => System.Windows.Application.Current.Shutdown();

        var menu = new ContextMenuStrip();
        menu.Items.Add(_pauseResumeItem);
        menu.Items.Add(_reloadItem);
        menu.Items.Add(_clickThroughItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_exitItem);

        _notifyIcon = new NotifyIcon { ContextMenuStrip = menu, Text = "ClaudeMeter" };

        _pollingControl.PauseStateChanged += OnPauseStateChanged;
        _clickThrough.StateChanged += OnClickThroughStateChanged;
    }

    /// <summary>Llamado una única vez al final de <c>App.OnStartup</c> (AC: "al completarse OnStartup, aparece el icono").</summary>
    public void Initialize()
    {
        try
        {
            _notifyIcon.Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException)
        {
            _logger.LogWarning(ex, "No se pudo extraer el icono del ejecutable; se usa el icono por defecto del sistema");
            _notifyIcon.Icon = SystemIcons.Application;
        }

        _notifyIcon.Visible = true;
    }

    private void OnPauseStateChanged() =>
        _pauseResumeItem.Text = _pollingControl.IsPaused ? "Reanudar" : "Pausar";

    private void OnClickThroughStateChanged() =>
        _clickThroughItem.Checked = _clickThrough.IsEnabled;

    /// <summary><c>internal</c> + <c>InternalsVisibleTo</c>: expone los ítems para que xUnit invoque <c>PerformClick()</c> directamente (Definition of Done: sin interacción de ratón real de Windows).</summary>
    internal ToolStripMenuItem PauseResumeItemForTests => _pauseResumeItem;
    internal ToolStripMenuItem ReloadItemForTests => _reloadItem;
    internal ToolStripMenuItem ClickThroughItemForTests => _clickThroughItem;
    internal ToolStripMenuItem ExitItemForTests => _exitItem;

    /// <summary>AC de "icono de bandeja huérfano": oculta y libera el NotifyIcon antes de que el proceso termine. Llamado desde <c>App.OnExit</c>, en las tres vías de cierre (converge en <c>Application.Shutdown()</c>).</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _pollingControl.PauseStateChanged -= OnPauseStateChanged;
        _clickThrough.StateChanged -= OnClickThroughStateChanged;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _disposed = true;
    }
}
