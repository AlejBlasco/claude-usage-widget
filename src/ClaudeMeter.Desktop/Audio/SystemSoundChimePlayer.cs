namespace ClaudeMeter.Desktop.Audio;

/// <summary>Implementación real de <see cref="IChimePlayer"/>: sonido del sistema, sin ningún asset propio (ver rationale en el documento de diseño).</summary>
public sealed class SystemSoundChimePlayer : IChimePlayer
{
    /// <inheritdoc />
    public void Play() => System.Media.SystemSounds.Exclamation.Play();
}
