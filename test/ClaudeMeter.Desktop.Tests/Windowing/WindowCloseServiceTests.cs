using ClaudeMeter.Desktop.Tests.TestDoubles;
using ClaudeMeter.Desktop.Windowing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClaudeMeter.Desktop.Tests.Windowing;

/// <summary>
/// Pruebas de <see cref="WindowCloseService"/> (US-2, cierre directo, F3/Ciclo
/// B) limitadas al guard de "sin ventana adjunta" de
/// <see cref="WindowCloseService.RequestClose"/> -- mismo patrón exacto que
/// <see cref="WindowDragServiceTests"/>/<see cref="WindowResizeServiceTests"/>.
/// La llamada real a <see cref="System.Windows.Application.Shutdown()"/> con
/// una <c>Window</c>/<c>Application</c> reales queda fuera de cobertura
/// automática a propósito: terminaría el propio proceso de test -- gap
/// aceptado, cubierto por la validación manual de la Definition of Done
/// (mismo criterio ya usado para <c>MainWindow</c>/<c>Win32ScreenInfo</c>).
/// </summary>
public sealed class WindowCloseServiceTests
{
    [Fact]
    public void RequestClose_SinVentanaAdjunta_NoLanza()
    {
        var service = new WindowCloseService(NullLogger<WindowCloseService>.Instance);

        var exception = Record.Exception(() => service.RequestClose());

        Assert.Null(exception);
    }

    [Fact]
    public void RequestClose_SinVentanaAdjunta_RegistraWarningYSeIgnoraLaLlamada()
    {
        var logger = new CapturingLogger<WindowCloseService>();
        var service = new WindowCloseService(logger);

        service.RequestClose();

        Assert.Contains(LogLevel.Warning, logger.LoggedLevels);
    }

    [Fact]
    public void RequestClose_LlamadoVariasVecesSinVentanaAdjunta_EsIdempotenteYSigueSinLanzar()
    {
        // close.js podría, en teoría, reenviar más de un clic antes de que
        // MainWindow termine de adjuntarse -- el guard debe tolerarlo sin
        // acumular estado ni lanzar (mismo razonamiento ya documentado para
        // WindowDragService.BeginDrag).
        var service = new WindowCloseService(NullLogger<WindowCloseService>.Instance);

        var exception = Record.Exception(() =>
        {
            service.RequestClose();
            service.RequestClose();
            service.RequestClose();
        });

        Assert.Null(exception);
    }
}
