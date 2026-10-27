using Velopack;
using Velopack.Locators;
using Velopack.Sources;

namespace ClaudeMeter.Desktop.Tests.Updates;

/// <summary>
/// Test double de <see cref="UpdateManager"/> (F6/US-3, issue #26) para
/// <c>AppUpdateServiceTests</c>. <c>UpdateManager</c> no está sellada y
/// expone <see cref="UpdateManager.IsInstalled"/>, <see cref="UpdateManager.CheckForUpdatesAsync"/>
/// y <see cref="UpdateManager.DownloadUpdatesAsync"/> como miembros
/// <c>virtual</c> de su propia API pública (confirmado por reflexión contra
/// el paquete NuGet <c>Velopack</c> 1.2.158 antes de escribir este test
/// double) -- se sobrescriben aquí para simular cada escenario sin red
/// real ni feed de GitHub real, nunca con <c>HttpMessageHandler</c> (esta
/// clase no usa <see cref="System.Net.Http.HttpClient"/> directamente).
///
/// El constructor base exige un <see cref="Velopack.Locators.IVelopackLocator"/>
/// no nulo (o lanza <c>InvalidOperationException</c> si no hay uno
/// establecido globalmente) -- se le pasa una instancia de
/// <see cref="TestVelopackLocator"/>, la clase de test oficial que el
/// propio paquete Velopack publica para este caso, pasada como argumento
/// de constructor (dependencia de instancia), nunca vía el setter estático
/// global <c>VelopackLocator.SetCurrentLocator</c> -- ese setter no tiene
/// forma pública de revertirse dentro del mismo proceso y contaminaría el
/// resto de la batería de tests del ensamblado.
///
/// Deliberadamente NO se sobrescribe <see cref="UpdateManager.ApplyUpdatesAndRestart"/>
/// porque no es <c>virtual</c> en la API de Velopack -- ningún escenario de
/// estos tests llega a invocarlo de verdad (ver comentarios de cada
/// escenario en <c>AppUpdateServiceTests</c>), evitando el riesgo real de
/// que se intente reiniciar el propio proceso de test.
/// </summary>
internal class FakeUpdateManager : UpdateManager
{
    public FakeUpdateManager()
        : base(
            new GithubSource("https://github.com/AlejBlasco/claude-usage-widget", accessToken: null, prerelease: false),
            new UpdateOptions(),
            new TestVelopackLocator("ClaudeMeter", "1.0.0", Path.Combine(Path.GetTempPath(), "ClaudeMeterTests", "fake-velopack-root"), null!))
    {
    }

    public bool ThrowOnCheck { get; set; }
    public UpdateInfo? UpdateToReturn { get; set; }
    public bool ThrowOnDownload { get; set; }
    public int CheckForUpdatesCallCount { get; private set; }
    public int DownloadUpdatesCallCount { get; private set; }

    private bool _isInstalled = true;
    public override bool IsInstalled => _isInstalled;

    public void SetIsInstalledForTests(bool value) => _isInstalled = value;

    public override Task<UpdateInfo?> CheckForUpdatesAsync()
    {
        CheckForUpdatesCallCount++;
        if (ThrowOnCheck)
        {
            throw new HttpRequestException("Simulado: feed de releases inaccesible / sin conexión");
        }

        return Task.FromResult(UpdateToReturn);
    }

    public override Task DownloadUpdatesAsync(UpdateInfo updates, Action<int>? progress = null, CancellationToken cancelToken = default)
    {
        DownloadUpdatesCallCount++;
        if (ThrowOnDownload)
        {
            throw new HttpRequestException("Simulado: fallo de red durante la descarga");
        }

        return Task.CompletedTask;
    }
}
