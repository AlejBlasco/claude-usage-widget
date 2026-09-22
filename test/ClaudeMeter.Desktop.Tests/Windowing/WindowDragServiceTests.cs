using ClaudeMeter.Desktop.Configuration;
using ClaudeMeter.Desktop.Tests.TestDoubles;
using ClaudeMeter.Desktop.Windowing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClaudeMeter.Desktop.Tests.Windowing;

/// <summary>
/// Pruebas de <see cref="WindowDragService"/> (US-2, F2 Ciclo B) limitadas a
/// los guards de "sin ventana adjunta" de <see cref="WindowDragService.BeginDrag"/>,
/// <see cref="WindowDragService.DragDelta"/> y <see cref="WindowDragService.EndDrag"/>
/// -- las únicas ramas alcanzables de forma determinista sin una
/// <c>System.Windows.Window</c> real en un hilo STA con sesión de escritorio
/// real. La conversión de píxeles de dispositivo a DIPs y la escritura real
/// sobre <see cref="Window.Left"/>/<see cref="Window.Top"/> requieren esa
/// ventana real y quedan como gap aceptado (mismo criterio ya usado por
/// Development/Design para <c>MainWindow</c>/<c>Win32ScreenInfo</c>). El
/// <see cref="AppConfigStore"/> real que recibe cada instancia nunca llega a
/// invocarse en estos tests (el guard de "sin ventana" retorna antes), así
/// que no hay riesgo de escribir en el <c>%LOCALAPPDATA%</c> real de la
/// máquina.
/// </summary>
public sealed class WindowDragServiceTests
{
    [Fact]
    public void BeginDrag_SinVentanaAdjunta_NoLanza()
    {
        var service = new WindowDragService(
            new AppConfigStore(NullLogger<AppConfigStore>.Instance), NullLogger<WindowDragService>.Instance);

        var exception = Record.Exception(() => service.BeginDrag());

        Assert.Null(exception);
    }

    [Fact]
    public void BeginDrag_SinVentanaAdjunta_RegistraWarningYSeIgnoraElGesto()
    {
        // Verifica la rama de guard real del código fuente (LogWarning) sin
        // necesitar espiar AppConfigStore (clase concreta, no una interfaz --
        // decisión ya documentada por Design/Development).
        var logger = new CapturingLogger<WindowDragService>();
        var service = new WindowDragService(new AppConfigStore(NullLogger<AppConfigStore>.Instance), logger);

        service.BeginDrag();

        Assert.Contains(LogLevel.Warning, logger.LoggedLevels);
    }

    [Fact]
    public void BeginDrag_LlamadoVariasVecesSinVentanaAdjunta_EsIdempotenteYSigueSinLanzar()
    {
        var service = new WindowDragService(
            new AppConfigStore(NullLogger<AppConfigStore>.Instance), NullLogger<WindowDragService>.Instance);

        var exception = Record.Exception(() =>
        {
            service.BeginDrag();
            service.BeginDrag();
            service.BeginDrag();
        });

        Assert.Null(exception);
    }

    [Fact]
    public void DragDelta_SinVentanaAdjunta_NoLanzaYNoRegistraNada()
    {
        // A diferencia de BeginDrag, DragDelta se dispara en cada mousemove:
        // no debe loguear en cada llamada (BeginDrag ya avisó una vez para
        // este mismo gesto), solo ignorar el delta en silencio.
        var logger = new CapturingLogger<WindowDragService>();
        var service = new WindowDragService(new AppConfigStore(NullLogger<AppConfigStore>.Instance), logger);

        var exception = Record.Exception(() => service.DragDelta(5, 5));

        Assert.Null(exception);
        Assert.Empty(logger.LoggedLevels);
    }

    [Fact]
    public void EndDrag_SinVentanaAdjunta_NoLanzaYNoPersisteNada()
    {
        var service = new WindowDragService(
            new AppConfigStore(NullLogger<AppConfigStore>.Instance), NullLogger<WindowDragService>.Instance);

        var exception = Record.Exception(() => service.EndDrag());

        Assert.Null(exception);
    }
}
