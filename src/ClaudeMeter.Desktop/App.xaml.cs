using System.Net.Http;
using System.Windows;
using ClaudeMeter.Application.Abstractions;
using ClaudeMeter.Desktop.Audio;
using ClaudeMeter.Desktop.Configuration;
using ClaudeMeter.Desktop.Logging;
using ClaudeMeter.Desktop.Polling;
using ClaudeMeter.Desktop.Tray;
using ClaudeMeter.Desktop.Windowing;
using ClaudeMeter.Infrastructure.Authentication;
using ClaudeMeter.Infrastructure.Usage;
using Microsoft.AspNetCore.Components.WebView.Wpf;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace ClaudeMeter.Desktop;

/// <summary>
/// Composition root del widget: construye a mano (sin contenedor de
/// terceros, mismo estilo que <c>ClaudeMeter.ConsoleApp.Program</c> de F0)
/// el <see cref="HttpClient"/> compartido y los puertos de
/// Application/Infrastructure ya existentes, y los expone junto con
/// <c>AddWpfBlazorWebView()</c> a través de <see cref="Services"/> para que
/// <c>MainWindow</c> se los pase al <c>BlazorWebView</c>. Desde F2 también
/// inicializa Serilog (US-3) y compone la cadena real de
/// <see cref="IUsageDataSource"/>: <see cref="AnthropicApiUsageDataSource"/>
/// envuelta por <see cref="RetryingUsageDataSource"/> (US-2) — el único
/// decorator que se registra como <see cref="IUsageDataSource"/>. Desde
/// F2/Ciclo B también registra <see cref="AppConfigStore"/>/<see cref="AppConfig"/>,
/// <see cref="IChimePlayer"/> y <see cref="WindowDragService"/>.
/// </summary>
public partial class App : System.Windows.Application
{
    private HttpClient? _httpClient;

    /// <summary>Proveedor de servicios construido en <see cref="OnStartup"/>, leído por <c>MainWindow</c>.</summary>
    public IServiceProvider Services { get; private set; } = null!;

    /// <inheritdoc />
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        Log.Logger = SerilogConfigurator.CreateLogger(); // US-3
        Log.Information("ClaudeMeter iniciado");

        _httpClient = new HttpClient();

        var services = new ServiceCollection();
        services.AddWpfBlazorWebView();
        services.AddLogging(builder => builder.AddSerilog(Log.Logger, dispose: false)); // US-3
        services.AddSingleton(_httpClient);
        services.AddSingleton<ITokenProvider, CredentialsFileTokenProvider>();

        // AnthropicApiUsageDataSource se registra como tipo concreto (no
        // como IUsageDataSource) porque es RetryingUsageDataSource, no ella,
        // quien se expone como el IUsageDataSource real de la aplicación —
        // ver rationale del decorator en el documento de diseño de F2 (US-2).
        services.AddSingleton<AnthropicApiUsageDataSource>();
        services.AddSingleton<IUsageDataSource>(sp => new RetryingUsageDataSource(
            sp.GetRequiredService<AnthropicApiUsageDataSource>(),
            RetryPolicyOptions.Default,
            sp.GetRequiredService<ILogger<RetryingUsageDataSource>>()));

        // F2/Ciclo B: config.json (US-1) y arrastre (US-2) — ver rationale
        // de "AppConfigStore/AppConfig en Desktop, no como puerto de
        // Application" y de "WindowDragService sin Window por constructor"
        // en el documento de diseño.
        services.AddSingleton<AppConfigStore>();
        services.AddSingleton(sp => sp.GetRequiredService<AppConfigStore>().Load()); // AppConfig, resuelto una vez, perezosamente
        services.AddSingleton<IChimePlayer, SystemSoundChimePlayer>();
        services.AddSingleton<WindowDragService>();
        services.AddSingleton<WindowResizeService>();

        // F3/Ciclo B: click-through (US-1), cierre directo (US-2), puente
        // de pausa/recarga (US-2) e icono de bandeja (US-2) — ver rationale
        // completo en el documento de diseño.
        services.AddSingleton<ClickThroughService>();
        services.AddSingleton<WindowCloseService>();
        services.AddSingleton<PollingControlService>();
        services.AddSingleton<TrayIconService>();

        Services = services.BuildServiceProvider();

        // US-2: al completarse OnStartup, el icono ya debe estar visible (AC).
        Services.GetRequiredService<TrayIconService>().Initialize();
    }

    /// <inheritdoc />
    protected override void OnExit(ExitEventArgs e)
    {
        // US-2: evita el icono huérfano en las tres vías de cierre (todas
        // convergen aquí vía Application.Shutdown()) -- antes de cerrar
        // logs, por si Dispose() necesitase registrar algo.
        Services.GetRequiredService<TrayIconService>().Dispose();

        _httpClient?.Dispose();
        Log.CloseAndFlush(); // US-3: garantiza el volcado del sink de fichero
        base.OnExit(e);
    }
}
