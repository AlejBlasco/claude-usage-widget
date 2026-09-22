using Microsoft.Extensions.Logging;

namespace ClaudeMeter.Desktop.Polling;

/// <summary>
/// Puente entre el icono de bandeja (fuera de Blazor, US-2) y la instancia
/// real de <see cref="UsagePollingCoordinator"/> que posee
/// <c>UsagePage.razor</c> (no un singleton de DI -- ver rationale en
/// <see cref="UsagePollingCoordinator"/> y Technology Choices). Registrado
/// como singleton en <c>App.xaml.cs</c>; <c>UsagePage.OnInitialized()</c> lo
/// "adjunta" tras crear su coordinador, mismo verbo que
/// <see cref="Windowing.WindowDragService.AttachWindow"/>.
/// </summary>
public sealed class PollingControlService
{
    private UsagePollingCoordinator? _coordinator;

    public bool IsPaused { get; private set; }

    /// <summary>Se levanta tras Pausar/Reanudar, para que TrayIconService actualice el texto del ítem de menú (AC de US-2).</summary>
    public event Action? PauseStateChanged;

    public void AttachCoordinator(UsagePollingCoordinator coordinator)
    {
        _coordinator = coordinator;
        IsPaused = false; // AC: arranca siempre con el polling activo (no persistido)
    }

    /// <summary>Llamado desde <c>UsagePage.Dispose()</c> para no retener una instancia ya liberada.</summary>
    public void DetachCoordinator(UsagePollingCoordinator coordinator)
    {
        if (ReferenceEquals(_coordinator, coordinator))
        {
            _coordinator = null;
        }
    }

    public void TogglePause()
    {
        if (IsPaused)
        {
            Resume();
        }
        else
        {
            Pause();
        }
    }

    public void Pause()
    {
        if (IsPaused || _coordinator is null)
        {
            return;
        }

        _coordinator.Pause();
        IsPaused = true;
        PauseStateChanged?.Invoke();
    }

    public void Resume()
    {
        if (!IsPaused || _coordinator is null)
        {
            return;
        }

        _coordinator.Start(); // AC: "mismo comportamiento que el Start() inicial"
        IsPaused = false;
        PauseStateChanged?.Invoke();
    }

    /// <summary>AC de "Recargar": ciclo de poll adicional, sin tocar el estado de pausa ni el timer.</summary>
    public void RequestReload() => _coordinator?.PollNow();
}
