using ClaudeMeter.Desktop.Tests.TestDoubles;
using ClaudeMeter.Desktop.Windowing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClaudeMeter.Desktop.Tests.Windowing;

/// <summary>
/// Pruebas de <see cref="ClickThroughService"/> (US-1, F3/Ciclo B) limitadas
/// al estado inicial y al guard de "sin ventana adjunta" de
/// <see cref="ClickThroughService.SetEnabled"/>/<see cref="ClickThroughService.Toggle"/>
/// -- mismo patrón y mismo alcance que <see cref="WindowDragServiceTests"/>/
/// <see cref="WindowResizeServiceTests"/> (leídos como referencia antes de
/// escribir nada) -- y a <see cref="ClickThroughService.SetEnabledForTests"/>,
/// el único gancho <c>internal</c> capaz de simular una transición de estado
/// real sin un <c>HWND</c> real. La llamada real a <c>user32.dll</c>
/// (<c>GetWindowLongPtr</c>/<c>SetWindowLongPtr</c>) con un <c>HWND</c> real
/// requiere una <c>System.Windows.Window</c> real en un hilo STA con sesión
/// de escritorio real y queda como gap aceptado, mismo criterio ya usado
/// para <c>WindowDragService.DragDelta</c>/<c>EndDrag</c> y
/// <c>WindowResizeService.SetContentHeight</c> (ver Gaps del informe de
/// testing).
/// </summary>
public sealed class ClickThroughServiceTests
{
    [Fact]
    public void IsEnabled_AlConstruir_EsFalsePorDefecto()
    {
        // AC de US-1: la ventana arranca en modo interactivo (click-through
        // desactivado), idéntico al comportamiento actual -- sin tocar la
        // ventana en absoluto hasta el primer SetEnabled(true).
        var service = new ClickThroughService(NullLogger<ClickThroughService>.Instance);

        Assert.False(service.IsEnabled);
    }

    [Fact]
    public void SetEnabled_SinVentanaAdjunta_NoLanza()
    {
        var service = new ClickThroughService(NullLogger<ClickThroughService>.Instance);

        var exception = Record.Exception(() => service.SetEnabled(true));

        Assert.Null(exception);
    }

    [Fact]
    public void SetEnabled_SinVentanaAdjunta_RegistraWarningYNoCambiaIsEnabled()
    {
        var logger = new CapturingLogger<ClickThroughService>();
        var service = new ClickThroughService(logger);

        service.SetEnabled(true);

        Assert.Contains(LogLevel.Warning, logger.LoggedLevels);
        Assert.False(service.IsEnabled);
    }

    [Fact]
    public void SetEnabled_SinVentanaAdjunta_NoDisparaStateChanged()
    {
        // El guard debe cortar antes de tocar user32.dll y antes de levantar
        // el evento que sincroniza el Checked del ítem de menú de
        // TrayIconService -- de lo contrario TrayIconService reflejaría un
        // estado que nunca llegó a aplicarse de verdad.
        var service = new ClickThroughService(NullLogger<ClickThroughService>.Instance);
        var stateChangedCount = 0;
        service.StateChanged += () => stateChangedCount++;

        service.SetEnabled(true);

        Assert.Equal(0, stateChangedCount);
    }

    [Fact]
    public void Toggle_SinVentanaAdjunta_NoLanzaYSeComportaComoSetEnabled()
    {
        // Toggle() delega en SetEnabled(!IsEnabled) -- con IsEnabled == false
        // por defecto, equivale a SetEnabled(true), mismo guard.
        var logger = new CapturingLogger<ClickThroughService>();
        var service = new ClickThroughService(logger);

        var exception = Record.Exception(() => service.Toggle());

        Assert.Null(exception);
        Assert.Contains(LogLevel.Warning, logger.LoggedLevels);
        Assert.False(service.IsEnabled);
    }

    [Fact]
    public void SetEnabled_LlamadoVariasVecesSinVentanaAdjunta_EsIdempotenteYSigueSinLanzar()
    {
        var service = new ClickThroughService(NullLogger<ClickThroughService>.Instance);

        var exception = Record.Exception(() =>
        {
            service.SetEnabled(true);
            service.SetEnabled(false);
            service.Toggle();
        });

        Assert.Null(exception);
        Assert.False(service.IsEnabled);
    }

    [Fact]
    public void SetEnabledForTests_ActualizaIsEnabledYDisparaStateChangedUnaVez()
    {
        // Gancho internal explícitamente diseñado para que TrayIconServiceTests
        // pueda verificar la suscripción a StateChanged sin pasar por
        // user32.dll real (ver TrayIconServiceTests).
        var service = new ClickThroughService(NullLogger<ClickThroughService>.Instance);
        var stateChangedCount = 0;
        service.StateChanged += () => stateChangedCount++;

        service.SetEnabledForTests(true);

        Assert.True(service.IsEnabled);
        Assert.Equal(1, stateChangedCount);
    }

    [Fact]
    public void SetEnabledForTests_ConFalse_ActualizaIsEnabledYDisparaStateChangedUnaVez()
    {
        var service = new ClickThroughService(NullLogger<ClickThroughService>.Instance);
        service.SetEnabledForTests(true);
        var stateChangedCount = 0;
        service.StateChanged += () => stateChangedCount++;

        service.SetEnabledForTests(false);

        Assert.False(service.IsEnabled);
        Assert.Equal(1, stateChangedCount);
    }
}
