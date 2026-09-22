namespace ClaudeMeter.Desktop.Audio;

/// <summary>Abstrae "reproducir el chime" para que bUnit pueda sustituirlo por un doble de test — ninguna prueba automatizada reproduce audio real.</summary>
public interface IChimePlayer
{
    /// <summary>Reproduce el sonido de aviso de umbral Crítico.</summary>
    void Play();
}
