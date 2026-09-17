using System.Net.Http;
using System.Windows;
using ClaudeMeter.Application.Abstractions;
using ClaudeMeter.Infrastructure.Authentication;
using ClaudeMeter.Infrastructure.Usage;
using Microsoft.AspNetCore.Components.WebView.Wpf;
using Microsoft.Extensions.DependencyInjection;

namespace ClaudeMeter.Desktop;

/// <summary>
/// Composition root del widget: construye a mano (sin contenedor de
/// terceros, mismo estilo que <c>ClaudeMeter.ConsoleApp.Program</c> de F0)
/// el <see cref="HttpClient"/> compartido y los puertos de
/// Application/Infrastructure ya existentes, y los expone junto con
/// <c>AddWpfBlazorWebView()</c> a través de <see cref="Services"/> para que
/// <c>MainWindow</c> se los pase al <c>BlazorWebView</c>.
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

        _httpClient = new HttpClient();

        var services = new ServiceCollection();
        services.AddWpfBlazorWebView();
        services.AddSingleton(_httpClient);
        services.AddSingleton<ITokenProvider, CredentialsFileTokenProvider>();
        services.AddSingleton<IUsageDataSource, AnthropicApiUsageDataSource>();

        Services = services.BuildServiceProvider();
    }

    /// <inheritdoc />
    protected override void OnExit(ExitEventArgs e)
    {
        _httpClient?.Dispose();
        base.OnExit(e);
    }
}
