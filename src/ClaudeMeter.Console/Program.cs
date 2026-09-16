using ClaudeMeter.Application.Abstractions;
using ClaudeMeter.ConsoleApp.Polling;
using ClaudeMeter.ConsoleApp.Rendering;
using ClaudeMeter.Infrastructure.Authentication;
using ClaudeMeter.Infrastructure.Usage;

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
        IUsageDataSource usageDataSource = new AnthropicApiUsageDataSource(tokenProvider, httpClient);
        IUsagePollingRenderer renderer = new ConsoleUsagePollingRenderer();

        var loop = new UsagePollingLoop(usageDataSource, renderer, PollInterval);

        // F0 no requiere apagado controlado (decisión ya fijada por el
        // documento de requisitos): el proceso corre hasta que se mate
        // manualmente (Ctrl+C / cierre de la ventana de consola).
        await loop.RunAsync(CancellationToken.None);
    }
}
