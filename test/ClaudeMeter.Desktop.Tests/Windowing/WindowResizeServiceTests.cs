using ClaudeMeter.Desktop.Tests.TestDoubles;
using ClaudeMeter.Desktop.Windowing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClaudeMeter.Desktop.Tests.Windowing;

/// <summary>
/// Pruebas de <see cref="WindowResizeService"/> (F3/Ciclo A, fix de bug de
/// validación manual: "Tamaño de texto" de Accesibilidad de Windows subido
/// recortaba el contenido con una altura de ventana fija) limitadas al guard
/// de "sin ventana adjunta" de <see cref="WindowResizeService.SetContentHeight(double)"/>
/// -- la única rama alcanzable de forma determinista sin una
/// <c>System.Windows.Window</c> real en un hilo STA con sesión de escritorio
/// real. La conversión de píxeles de dispositivo a DIPs
/// (<c>PresentationSource.CompositionTarget.TransformFromDevice</c>) y la
/// escritura real sobre <see cref="System.Windows.Window.Top"/>/
/// <see cref="System.Windows.Window.Height"/> requieren esa ventana real y
/// quedan como gap aceptado, mismo criterio ya usado para
/// <c>WindowDragService.DragDelta</c>/<c>EndDrag</c> en
/// <see cref="WindowDragServiceTests"/>.
/// </summary>
public sealed class WindowResizeServiceTests
{
    [Fact]
    public void SetContentHeight_SinVentanaAdjunta_NoLanza()
    {
        var service = new WindowResizeService(NullLogger<WindowResizeService>.Instance);

        var exception = Record.Exception(() => service.SetContentHeight(480));

        Assert.Null(exception);
    }

    [Fact]
    public void SetContentHeight_SinVentanaAdjunta_RegistraWarningYSeIgnoraLaLlamada()
    {
        // Verifica la rama de guard real del código fuente (LogWarning) --
        // ResizeObserver puede disparar antes de que MainWindow termine de
        // adjuntarse (mismo razonamiento ya documentado para BeginDrag en
        // WindowDragServiceTests).
        var logger = new CapturingLogger<WindowResizeService>();
        var service = new WindowResizeService(logger);

        service.SetContentHeight(480);

        Assert.Contains(LogLevel.Warning, logger.LoggedLevels);
    }

    [Fact]
    public void SetContentHeight_LlamadoVariasVecesSinVentanaAdjunta_EsIdempotenteYSigueSinLanzar()
    {
        // ResizeObserver dispara en cada cambio de tamaño observado -- el
        // guard debe tolerar múltiples llamadas repetidas antes de que
        // AttachWindow() se invoque, sin acumular estado ni lanzar.
        var service = new WindowResizeService(NullLogger<WindowResizeService>.Instance);

        var exception = Record.Exception(() =>
        {
            service.SetContentHeight(480);
            service.SetContentHeight(520);
            service.SetContentHeight(0);
        });

        Assert.Null(exception);
    }

    [Fact]
    public void SetContentHeight_ConValorNegativoYSinVentanaAdjunta_NoLanza()
    {
        // El guard de "sin ventana" debe cortar antes de cualquier cálculo
        // sobre el valor recibido, incluso si el valor en sí no tendría
        // sentido físico (altura negativa) -- JSInvokable no controla lo que
        // envía el JS de origen.
        var service = new WindowResizeService(NullLogger<WindowResizeService>.Instance);

        var exception = Record.Exception(() => service.SetContentHeight(-10));

        Assert.Null(exception);
    }
}
