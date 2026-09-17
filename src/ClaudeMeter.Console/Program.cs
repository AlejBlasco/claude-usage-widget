using ClaudeMeter.Application.Abstractions;
using ClaudeMeter.ConsoleApp.Polling;
using ClaudeMeter.ConsoleApp.Rendering;
using ClaudeMeter.Infrastructure.Authentication;
using ClaudeMeter.Infrastructure.Usage;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClaudeMeter.ConsoleApp;

/// <summary>
/// Composition root del arnés de validación de F0: construye a mano (sin
/// contenedor de DI) el <see cref="HttpClient"/> compartido, el
/// <see cref="ITokenProvider"/>, el <see cref="IUsageDataSource"/> y el
/// <see cref="IUsagePollingRenderer"/>, y arranca el bucle de polling. No
/// contiene lógica propia más allá de esta construcción.
/// </summary>
internal static class Program
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(60);

    private static async Task Main()
    {
        using var httpClient = new HttpClient();

        ITokenProvider tokenProvider = new CredentialsFileTokenProvider();

        // F0 no tiene infraestructura de logging propia (Serilog se
        // introdujo en F2 solo para ClaudeMeter.Desktop, ver CLAUDE.md):
        // NullLogger es la opción mínima y correcta para este arnés de
        // consola, que sigue sin loguear nada por diseño.
        IUsageDataSource usageDataSource = new AnthropicApiUsageDataSource(
            tokenProvider, httpClient, NullLogger<AnthropicApiUsageDataSource>.Instance);
        IUsagePollingRenderer renderer = new ConsoleUsagePollingRenderer();

        var loop = new UsagePollingLoop(usageDataSource, renderer, PollInterval);

        // F0 no requiere apagado controlado (decisión ya fijada por el
        // documento de requisitos): el proceso corre hasta que se mate
        // manualmente (Ctrl+C / cierre de la ventana de consola).
        await loop.RunAsync(CancellationToken.None);
    }
}
