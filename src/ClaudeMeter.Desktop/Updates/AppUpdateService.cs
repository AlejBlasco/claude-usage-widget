using Microsoft.Extensions.Logging;
using Velopack;
using Velopack.Sources;

namespace ClaudeMeter.Desktop.Updates;

/// <summary>
/// Envuelve <see cref="Velopack.UpdateManager"/> (F6/US-3, issue #26).
/// Nombrada "AppUpdateService", no "UpdateManager", para no colisionar por
/// lectura con <see cref="Velopack.UpdateManager"/> (namespaces distintos
/// evitan la ambigüedad en compilación, pero se evita igualmente para no
/// confundir al leer el código). Comprobación al arrancar + timer periódico
/// de baja frecuencia; degradación silenciosa ante cualquier fallo, mismo
/// criterio que <c>RetryingUsageDataSource</c> (F2) y la propia AC de US-3.
/// </summary>
public sealed class AppUpdateService : IDisposable
{
    private static readonly TimeSpan PeriodicCheckInterval = TimeSpan.FromHours(4);

    private readonly UpdateManager _updateManager;
    private readonly ILogger<AppUpdateService> _logger;
    private System.Timers.Timer? _timer;

    public AppUpdateService(ILogger<AppUpdateService> logger)
        : this(logger, new UpdateManager(
            new GithubSource("https://github.com/AlejBlasco/claude-usage-widget", accessToken: null, prerelease: false)))
    {
    }

    /// <summary>
    /// Únicamente para tests (sdlc-testing, F6): permite inyectar un
    /// <see cref="UpdateManager"/> (p. ej. una subclase que sobrescribe
    /// <see cref="UpdateManager.IsInstalled"/>/<see cref="UpdateManager.CheckForUpdatesAsync"/>/
    /// <see cref="UpdateManager.DownloadUpdatesAsync"/>, todos ellos
    /// miembros <c>virtual</c> de la API pública de Velopack) en vez de
    /// construir uno real contra <see cref="GithubSource"/>, para poder
    /// simular el flujo de comprobación/descarga sin red real ni feed de
    /// GitHub real — cambio aditivo, el constructor público de un solo
    /// argumento no cambia de comportamiento.
    /// </summary>
    internal AppUpdateService(ILogger<AppUpdateService> logger, UpdateManager updateManager)
    {
        _logger = logger;
        _updateManager = updateManager;
    }

    /// <summary>Llamado una vez desde <c>App.OnStartup</c>, tras <c>VelopackApp.Build().Run()</c>.</summary>
    public void Start()
    {
        _ = CheckAndApplyAsync();

        _timer = new System.Timers.Timer(PeriodicCheckInterval.TotalMilliseconds) { AutoReset = true };
        _timer.Elapsed += async (_, _) => await CheckAndApplyAsync();
        _timer.Start();
    }

    /// <summary>Únicamente para tests: invoca directamente el flujo asíncrono privado de comprobación/descarga/aplicación, sin pasar por el temporizador ni por el disparo "fire-and-forget" de <see cref="Start"/>, para poder esperarlo (<c>await</c>) de forma determinista.</summary>
    internal Task CheckAndApplyForTestsAsync() => CheckAndApplyAsync();

    private async Task CheckAndApplyAsync()
    {
        try
        {
            // IsInstalled evita comprobar actualizaciones cuando se ejecuta
            // sin instalar vía Velopack (p. ej. dotnet run en desarrollo
            // local, sin vpk pack de por medio).
            if (!_updateManager.IsInstalled)
            {
                return;
            }

            var info = await _updateManager.CheckForUpdatesAsync();
            if (info is null)
            {
                return; // sin actualización disponible
            }

            await _updateManager.DownloadUpdatesAsync(info);
            _updateManager.ApplyUpdatesAndRestart(toApply: null); // null: aplica la última descargada; Velopack gestiona el reinicio del proceso
        }
        catch (Exception ex)
        {
            // Catch-all deliberado (desviación justificada del criterio de
            // excepciones específicas usado en el resto del repo, p. ej.
            // AppConfigStore/RetryingUsageDataSource): la AC de US-3 exige
            // degradación silenciosa ante *cualquier* fallo de red/feed
            // inaccesible, y la superficie de excepciones de un pipeline de
            // descarga+aplicación de Velopack no es enumerable de forma
            // estable entre versiones del paquete.
            _logger.LogWarning(ex, "No se pudo comprobar o aplicar actualizaciones; se continúa con la versión actual");
        }
    }

    public void Dispose() => _timer?.Dispose();
}
