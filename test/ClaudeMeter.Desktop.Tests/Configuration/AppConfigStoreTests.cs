using System.IO;
using ClaudeMeter.Desktop.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClaudeMeter.Desktop.Tests.Configuration;

/// <summary>
/// Pruebas de <see cref="AppConfigStore"/> (US-1/US-2, F2 Ciclo B): confirma
/// que <see cref="AppConfigStore.Load"/> nunca lanza (fichero inexistente,
/// JSON corrupto, campos concretos fuera de rango) y que
/// <see cref="AppConfigStore.Save"/>/<see cref="AppConfigStore.SavePosition"/>
/// escriben de forma atómica y tampoco lanzan ante un fallo de E/S. Cada
/// test usa una ruta de fichero bajo un directorio temporal propio
/// (<c>Path.GetTempPath()</c>), nunca <c>%LOCALAPPDATA%</c> real -- limpiado
/// en <see cref="Dispose"/>.
/// </summary>
public sealed class AppConfigStoreTests : IDisposable
{
    private readonly string _tempDirectory =
        Path.Combine(Path.GetTempPath(), "ClaudeMeterTests", Guid.NewGuid().ToString("N"));
    private readonly string _configPath;
    private readonly AppConfigStore _store = new(NullLogger<AppConfigStore>.Instance);

    public AppConfigStoreTests()
    {
        _configPath = Path.Combine(_tempDirectory, "config.json");
    }

    [Fact]
    public void Load_ConFicheroInexistente_DevuelveDefaultSinCrearNada()
    {
        var config = _store.Load(_configPath);

        Assert.Equal(AppConfig.Default, config);
        Assert.False(File.Exists(_configPath)); // AC: sin fichero -> defaults, sin efecto secundario de creación
    }

    [Fact]
    public void Load_ConJsonSintacticamenteInvalido_DevuelveDefaultCompletoSinLanzar()
    {
        Directory.CreateDirectory(_tempDirectory);
        File.WriteAllText(_configPath, "{\"pollingIntervalSeconds\":");

        var exception = Record.Exception(() => _store.Load(_configPath));
        var config = _store.Load(_configPath);

        Assert.Null(exception);
        Assert.Equal(AppConfig.Default, config);
    }

    [Fact]
    public void Load_ConContenidoLiteralmenteNull_DevuelveDefaultCompletoSinLanzar()
    {
        Directory.CreateDirectory(_tempDirectory);
        File.WriteAllText(_configPath, "null");

        var config = _store.Load(_configPath);

        Assert.Equal(AppConfig.Default, config);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-5.0)]
    public void Load_ConPollingIntervalSecondsFueraDeRango_SoloEseCampoCaeADefault_RestoSeRespeta(double invalidSeconds)
    {
        // AC clave: un campo concreto fuera de rango no debe tirar el resto
        // del fichero -- chimeEnabled/windowPosition, válidos en este JSON,
        // deben respetarse tal cual.
        Directory.CreateDirectory(_tempDirectory);
        var json = "{\"pollingIntervalSeconds\": " + invalidSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                    ", \"chimeEnabled\": true, \"windowPosition\": {\"left\": 123.0, \"top\": 45.0}}";
        File.WriteAllText(_configPath, json);

        var config = _store.Load(_configPath);

        Assert.Equal(AppConfig.Default.PollingInterval, config.PollingInterval);
        Assert.True(config.ChimeEnabled);
        Assert.Equal(new WindowPosition(123.0, 45.0), config.Position);
    }

    [Fact]
    public void Load_ConPollingIntervalSecondsAusente_UsaElDefaultDe60sSinAfectarAlRestoDelFichero()
    {
        Directory.CreateDirectory(_tempDirectory);
        File.WriteAllText(_configPath, """{"chimeEnabled": true}""");

        var config = _store.Load(_configPath);

        Assert.Equal(TimeSpan.FromSeconds(60), config.PollingInterval);
        Assert.True(config.ChimeEnabled);
        Assert.Null(config.Position);
    }

    [Fact]
    public void Load_ConPollingIntervalSecondsValido_LoRespeta()
    {
        Directory.CreateDirectory(_tempDirectory);
        File.WriteAllText(_configPath, """{"pollingIntervalSeconds": 10}""");

        var config = _store.Load(_configPath);

        Assert.Equal(TimeSpan.FromSeconds(10), config.PollingInterval);
    }

    [Fact]
    public void Load_ConWindowPositionConSoloUnCampoPresente_CaeANuloSinLanzar()
    {
        // "top" ausente: la posición completa se descarta (no hay forma de
        // construir una WindowPosition válida con un solo eje), cayendo al
        // cálculo de posición por defecto de WindowPositionResolver.
        Directory.CreateDirectory(_tempDirectory);
        File.WriteAllText(_configPath, """{"windowPosition": {"left": 100.0}}""");

        var config = _store.Load(_configPath);

        Assert.Null(config.Position);
    }

    [Fact]
    public void Load_ConWindowPositionValida_LaRespeta()
    {
        Directory.CreateDirectory(_tempDirectory);
        File.WriteAllText(_configPath, """{"windowPosition": {"left": 250.5, "top": 80.25}}""");

        var config = _store.Load(_configPath);

        Assert.Equal(new WindowPosition(250.5, 80.25), config.Position);
    }

    [Fact]
    public void Load_ConChimeEnabledAusente_UsaFalsePorDefecto()
    {
        Directory.CreateDirectory(_tempDirectory);
        File.WriteAllText(_configPath, """{"pollingIntervalSeconds": 30}""");

        var config = _store.Load(_configPath);

        Assert.False(config.ChimeEnabled);
    }

    [Fact]
    public void Save_EscribeUnFicheroQueLoadPuedeLeerDeVuelta()
    {
        var config = new AppConfig(TimeSpan.FromSeconds(15), new WindowPosition(10, 20), ChimeEnabled: true);

        _store.Save(config, _configPath);
        var reloaded = _store.Load(_configPath);

        Assert.Equal(config, reloaded);
    }

    [Fact]
    public void Save_ConDirectorioDestinoInexistente_LoCreaYNoLanza()
    {
        Assert.False(Directory.Exists(_tempDirectory));

        var exception = Record.Exception(() => _store.Save(AppConfig.Default, _configPath));

        Assert.Null(exception);
        Assert.True(File.Exists(_configPath));
    }

    [Fact]
    public void Save_EscrituraAtomica_NoDejaNingunFicheroTemporalTrasCompletar()
    {
        // Confirma el mecanismo de escritura atómica descrito por
        // Development (.tmp + File.Move): tras un Save() exitoso, el
        // .tmp intermedio no debe sobrevivir -- sólo config.json final,
        // nunca ambos ni un config.json a medio escribir.
        _store.Save(AppConfig.Default, _configPath);

        Assert.True(File.Exists(_configPath));
        Assert.False(File.Exists(_configPath + ".tmp"));
        Assert.Single(Directory.GetFiles(_tempDirectory));
    }

    [Fact]
    public void Save_LlamadoDosVecesSeguidas_ElFicheroFinalRespetaElUltimoValorSinQuedarACorruptoAMedias()
    {
        // Simula 2 escrituras consecutivas (equivalente a 2 arrastres
        // rápidos, AC de US-2): el resultado final debe ser JSON válido
        // reflejando siempre el último Save(), nunca una mezcla.
        _store.Save(AppConfig.Default with { Position = new WindowPosition(1, 1) }, _configPath);
        _store.Save(AppConfig.Default with { Position = new WindowPosition(99, 88) }, _configPath);

        var config = _store.Load(_configPath);

        Assert.Equal(new WindowPosition(99, 88), config.Position);
    }

    [Fact]
    public void Save_ConRutaCuyoDirectorioEsUnFicheroExistente_NoLanzaYNoEscribeNada()
    {
        // Simula un fallo de E/S real (Directory.CreateDirectory no puede
        // crear un directorio donde ya existe un fichero con ese nombre) sin
        // depender de trucos de permisos/timing -- confirma que Save()
        // atrapa el IOException y nunca deja la app en un estado roto.
        // Usa una ruta bloqueada propia (distinta de _tempDirectory) para no
        // interferir con la limpieza de Dispose().
        var blockedPath = _tempDirectory + "_blocked";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(blockedPath)!);
            File.WriteAllText(blockedPath, "esto es un fichero, no un directorio"); // blockedPath ocupa ya el nombre que Save() necesita como carpeta
            var configPathUnderBlockedDirectory = Path.Combine(blockedPath, "config.json");

            var exception = Record.Exception(() => _store.Save(AppConfig.Default, configPathUnderBlockedDirectory));

            Assert.Null(exception);
            Assert.False(File.Exists(configPathUnderBlockedDirectory));
        }
        finally
        {
            if (File.Exists(blockedPath))
            {
                File.Delete(blockedPath);
            }
        }
    }

    [Fact]
    public void SavePosition_ActualizaSoloLaPosicionYConservaIntervaloYChimeYaPersistidos()
    {
        _store.Save(new AppConfig(TimeSpan.FromSeconds(25), Position: null, ChimeEnabled: true), _configPath);

        _store.SavePosition(left: 300, top: 400, _configPath);

        var config = _store.Load(_configPath);
        Assert.Equal(TimeSpan.FromSeconds(25), config.PollingInterval);
        Assert.True(config.ChimeEnabled);
        Assert.Equal(new WindowPosition(300, 400), config.Position);
    }

    [Fact]
    public void SavePosition_SinConfigPrevio_PartedeLosDefaultsYSoloFijaLaPosicion()
    {
        _store.SavePosition(left: 5, top: 6, _configPath);

        var config = _store.Load(_configPath);
        Assert.Equal(AppConfig.Default.PollingInterval, config.PollingInterval);
        Assert.Equal(AppConfig.Default.ChimeEnabled, config.ChimeEnabled);
        Assert.Equal(new WindowPosition(5, 6), config.Position);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }
}
