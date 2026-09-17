using System.IO;
using ClaudeMeter.Desktop.Logging;

namespace ClaudeMeter.Desktop.Tests.Logging;

/// <summary>
/// Pruebas de <see cref="SerilogConfigurator"/> (US-3, F2): confirma que
/// <see cref="SerilogConfigurator.CreateLogger"/> escribe en el directorio
/// inyectado por parámetro (nunca en el <c>%LOCALAPPDATA%</c> real de la
/// máquina de CI) y que <see cref="SerilogConfigurator.DefaultLogDirectory"/>
/// calcula la ruta sugerida por el issue #12 sin escribir ningún fichero.
/// Cada test usa un directorio temporal propio, limpiado en
/// <see cref="Dispose"/>.
/// </summary>
public sealed class SerilogConfiguratorTests : IDisposable
{
    private readonly string _tempDirectory =
        Path.Combine(Path.GetTempPath(), "ClaudeMeterTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void CreateLogger_ConDirectorioInyectado_CreaEseDirectorioYEscribeElLogAhiSinTocarLocalAppData()
    {
        // Arrange
        Assert.False(Directory.Exists(_tempDirectory));

        // Act
        var logger = SerilogConfigurator.CreateLogger(_tempDirectory);
        try
        {
            logger.Information("mensaje de prueba {Value}", 42);
        }
        finally
        {
            // Serilog.Core.Logger implementa IDisposable; forzar el
            // flush/cierre del sink de fichero antes de leerlo, igual que
            // App.xaml.cs hace con Log.CloseAndFlush() al cerrar la app.
            (logger as IDisposable)?.Dispose();
        }

        // Assert
        Assert.True(Directory.Exists(_tempDirectory));
        var logFiles = Directory.GetFiles(_tempDirectory, "claudemeter-*.log");
        Assert.Single(logFiles);

        var content = File.ReadAllText(logFiles[0]);
        Assert.Contains("mensaje de prueba 42", content);
        Assert.Contains("[INF]", content);
    }

    [Fact]
    public void CreateLogger_ConDirectorioInyectadoYaExistente_NoLanzaYSigueEscribiendoAhi()
    {
        // Caso límite: Directory.CreateDirectory es idempotente, pero se
        // confirma explícitamente que CreateLogger no lanza si el
        // directorio ya existe de una ejecución anterior.
        Directory.CreateDirectory(_tempDirectory);

        var logger = SerilogConfigurator.CreateLogger(_tempDirectory);
        try
        {
            logger.Information("segunda ejecución");
        }
        finally
        {
            (logger as IDisposable)?.Dispose();
        }

        var logFiles = Directory.GetFiles(_tempDirectory, "claudemeter-*.log");
        Assert.Single(logFiles);
        Assert.Contains("segunda ejecución", File.ReadAllText(logFiles[0]));
    }

    [Fact]
    public void DefaultLogDirectory_DevuelveLocalAppDataClaudeMeterLogs_SinEscribirNingunFichero()
    {
        // Solo se lee Environment.GetFolderPath (API pura, sin E/S de
        // escritura) -- nunca se llama a CreateLogger con este valor, así
        // que este test no debe crear ni escribir nada en el
        // %LOCALAPPDATA% real de la máquina que lo ejecute.
        var expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClaudeMeter", "logs");

        var actual = SerilogConfigurator.DefaultLogDirectory();

        Assert.Equal(expected, actual);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }
}
