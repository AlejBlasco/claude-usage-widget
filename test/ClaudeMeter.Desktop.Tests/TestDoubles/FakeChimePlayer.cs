using ClaudeMeter.Desktop.Audio;

namespace ClaudeMeter.Desktop.Tests.TestDoubles;

/// <summary>
/// Test double de <see cref="IChimePlayer"/>: nunca reproduce sonido real,
/// solo cuenta cuántas veces se invocó <see cref="Play"/> -- usado por
/// <c>UsagePageTests</c> (F2 Ciclo B, US-1) para verificar el AC más
/// importante de esta pieza: el chime se dispara exactamente una vez por
/// transición a Crítico y no se repite mientras el estado se mantenga en
/// Crítico.
/// </summary>
internal sealed class FakeChimePlayer : IChimePlayer
{
    public int PlayCount { get; private set; }

    public void Play() => PlayCount++;
}
