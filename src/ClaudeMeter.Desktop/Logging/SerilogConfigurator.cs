using System.IO;
using Serilog;

namespace ClaudeMeter.Desktop.Logging;

/// <summary>
/// Construye el <see cref="Serilog.ILogger"/> de la aplicación. Separada de
/// <c>App.xaml.cs</c> (que es quien la asigna a <c>Log.Logger</c>) para
/// poder testear la configuración con un directorio inyectado, sin escribir
/// en el <c>%LOCALAPPDATA%</c> real de la máquina de CI (AC de US-3, F2).
/// </summary>
public static class SerilogConfigurator
{
    private const string LogFileNameTemplate = "claudemeter-.log";
    private const int RetainedFileCountLimit = 7; // ~1 semana de logs, mitiga el riesgo de I/O sin límite (US-3)

    /// <summary>
    /// Crea un <see cref="Serilog.ILogger"/> configurado con un sink de
    /// fichero (rotación diaria) en <paramref name="logDirectory"/>, o en
    /// <see cref="DefaultLogDirectory"/> si no se especifica ninguno.
    /// </summary>
    public static Serilog.ILogger CreateLogger(string? logDirectory = null)
    {
        var directory = logDirectory ?? DefaultLogDirectory();
        Directory.CreateDirectory(directory);

        return new LoggerConfiguration()
            .MinimumLevel.Information()
            .Enrich.FromLogContext()
            .WriteTo.File(
                Path.Combine(directory, LogFileNameTemplate),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: RetainedFileCountLimit,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }

    /// <summary><c>%LOCALAPPDATA%\ClaudeMeter\logs</c> — ruta sugerida por el issue #12, adoptada tal cual.</summary>
    public static string DefaultLogDirectory() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClaudeMeter", "logs");
}
