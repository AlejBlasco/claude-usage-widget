using System.Windows;
using ClaudeMeter.Desktop.Configuration;
using ClaudeMeter.Desktop.Windowing;

namespace ClaudeMeter.Desktop.Tests.Windowing;

/// <summary>
/// Pruebas puras de <see cref="WindowPositionResolver.Resolve"/> (US-1, F2
/// Ciclo B) con monitores simulados (<see cref="Rect"/> construidos a mano,
/// sin <see cref="Win32ScreenInfo"/> real -- ver el gap ya aceptado por
/// Development de "sin monitor real en CI"). Cubre: posición configurada
/// que cabe (monitor principal y uno secundario), posición configurada
/// fuera de todo monitor conectado, y ausencia de posición configurada --
/// las tres caen exactamente donde el AC de US-1 exige.
/// </summary>
public sealed class WindowPositionResolverTests
{
    private const double WindowWidth = 320;
    private const double WindowHeight = 180;
    private const double ScreenMargin = 16;

    // Monitor principal: 1920x1080 en el origen.
    private static readonly Rect PrimaryWorkArea = new(0, 0, 1920, 1080);

    [Fact]
    public void Resolve_ConPosicionConfiguradaQueCabeEnElMonitorPrincipal_RespetaLaPosicionConfigurada()
    {
        var configuredPosition = new WindowPosition(Left: 100, Top: 200);

        var (left, top) = WindowPositionResolver.Resolve(
            configuredPosition, WindowWidth, WindowHeight, ScreenMargin,
            PrimaryWorkArea, allScreenWorkAreas: [PrimaryWorkArea]);

        Assert.Equal(100, left);
        Assert.Equal(200, top);
    }

    [Fact]
    public void Resolve_ConPosicionConfiguradaQueCabeSoloEnUnMonitorSecundario_RespetaLaPosicionConfigurada()
    {
        // Segundo monitor a la derecha del principal: 1920..3840 en X.
        var secondaryWorkArea = new Rect(1920, 0, 1920, 1080);
        var configuredPosition = new WindowPosition(Left: 2500, Top: 300);

        var (left, top) = WindowPositionResolver.Resolve(
            configuredPosition, WindowWidth, WindowHeight, ScreenMargin,
            PrimaryWorkArea, allScreenWorkAreas: [PrimaryWorkArea, secondaryWorkArea]);

        Assert.Equal(2500, left);
        Assert.Equal(300, top);
    }

    [Fact]
    public void Resolve_ConPosicionConfiguradaFueraDeTodosLosMonitoresConectados_CaeAEsquinaInferiorDerechaDelMonitorPrincipal()
    {
        // AC explícito: fallback al monitor PRINCIPAL, no a cualquier otro
        // monitor disponible, incluso si hubiera varios conectados.
        var secondaryWorkArea = new Rect(1920, 0, 1920, 1080);
        var configuredPosition = new WindowPosition(Left: 5000, Top: 5000); // fuera de ambos monitores

        var (left, top) = WindowPositionResolver.Resolve(
            configuredPosition, WindowWidth, WindowHeight, ScreenMargin,
            PrimaryWorkArea, allScreenWorkAreas: [PrimaryWorkArea, secondaryWorkArea]);

        Assert.Equal(PrimaryWorkArea.Right - WindowWidth - ScreenMargin, left);
        Assert.Equal(PrimaryWorkArea.Bottom - WindowHeight - ScreenMargin, top);
    }

    [Fact]
    public void Resolve_SinPosicionConfigurada_CaeAEsquinaInferiorDerechaDelMonitorPrincipal()
    {
        // Mismo fallback que F1 usaba siempre (sin config.json todavía).
        var (left, top) = WindowPositionResolver.Resolve(
            configuredPosition: null, WindowWidth, WindowHeight, ScreenMargin,
            PrimaryWorkArea, allScreenWorkAreas: [PrimaryWorkArea]);

        Assert.Equal(PrimaryWorkArea.Right - WindowWidth - ScreenMargin, left);
        Assert.Equal(PrimaryWorkArea.Bottom - WindowHeight - ScreenMargin, top);
    }

    [Fact]
    public void Resolve_SinNingunMonitorConectado_CaeAEsquinaInferiorDerechaDelAreaPrincipalRecibida()
    {
        // Caso límite defensivo: lista de monitores vacía (p. ej.
        // EnumDisplayMonitors real no devolvió nada) -- FitsWithinAnyScreen
        // nunca puede ser true, así que siempre cae al fallback, tanto con
        // como sin posición configurada.
        var configuredPosition = new WindowPosition(Left: 10, Top: 10);

        var (left, top) = WindowPositionResolver.Resolve(
            configuredPosition, WindowWidth, WindowHeight, ScreenMargin,
            PrimaryWorkArea, allScreenWorkAreas: []);

        Assert.Equal(PrimaryWorkArea.Right - WindowWidth - ScreenMargin, left);
        Assert.Equal(PrimaryWorkArea.Bottom - WindowHeight - ScreenMargin, top);
    }

    [Fact]
    public void Resolve_ConPosicionExactamenteEnElBordeInferiorDerechoDelMonitor_Cabe()
    {
        // Frontera inclusive: left + width == screen.Right y
        // top + height == screen.Bottom deben seguir considerándose "cabe".
        var configuredPosition = new WindowPosition(
            Left: PrimaryWorkArea.Right - WindowWidth,
            Top: PrimaryWorkArea.Bottom - WindowHeight);

        var (left, top) = WindowPositionResolver.Resolve(
            configuredPosition, WindowWidth, WindowHeight, ScreenMargin,
            PrimaryWorkArea, allScreenWorkAreas: [PrimaryWorkArea]);

        Assert.Equal(configuredPosition.Left, left);
        Assert.Equal(configuredPosition.Top, top);
    }

    [Fact]
    public void Resolve_ConPosicionQueSeSaleUnPixelDelBordeDelMonitor_NoCabeYCaeAlFallback()
    {
        // Un solo píxel más allá del borde ya no "cabe entera" -- frontera
        // exclusive justo pasado el límite anterior.
        var configuredPosition = new WindowPosition(
            Left: PrimaryWorkArea.Right - WindowWidth + 1,
            Top: 0);

        var (left, top) = WindowPositionResolver.Resolve(
            configuredPosition, WindowWidth, WindowHeight, ScreenMargin,
            PrimaryWorkArea, allScreenWorkAreas: [PrimaryWorkArea]);

        Assert.Equal(PrimaryWorkArea.Right - WindowWidth - ScreenMargin, left);
        Assert.Equal(PrimaryWorkArea.Bottom - WindowHeight - ScreenMargin, top);
    }

    [Fact]
    public void Resolve_ConPosicionConCoordenadasNegativas_FueraDelMonitor_CaeAlFallback()
    {
        var configuredPosition = new WindowPosition(Left: -500, Top: -500);

        var (left, top) = WindowPositionResolver.Resolve(
            configuredPosition, WindowWidth, WindowHeight, ScreenMargin,
            PrimaryWorkArea, allScreenWorkAreas: [PrimaryWorkArea]);

        Assert.Equal(PrimaryWorkArea.Right - WindowWidth - ScreenMargin, left);
        Assert.Equal(PrimaryWorkArea.Bottom - WindowHeight - ScreenMargin, top);
    }
}
