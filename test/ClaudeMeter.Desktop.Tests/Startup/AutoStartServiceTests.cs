using System.IO;
using ClaudeMeter.Desktop.Configuration;
using ClaudeMeter.Desktop.Startup;
using ClaudeMeter.Desktop.Tests.TestDoubles;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Win32;

namespace ClaudeMeter.Desktop.Tests.Startup;

/// <summary>
/// Pruebas de <see cref="AutoStartService"/> (F6/US-3, issue #26).
///
/// Dos aislamientos deterministas, verificados explícitamente por estos
/// tests en vez de solo asumidos:
///
/// 1. <b>Registro real de Windows</b>: <c>AutoStartService.ResolveStableExePath</c>
///    resuelve la ruta estable vía <c>Velopack.Locators.VelopackLocator.Current</c>,
///    cuyo <c>IsCurrentSet</c> estático solo es <c>true</c> si el proceso
///    arrancó vía <c>Velopack.VelopackApp.Build().Run()</c> (ver
///    <c>ClaudeMeter.Desktop.Program.Main</c>). El proceso host de
///    `dotnet test` nunca invoca esa línea, así que <c>IsCurrentSet</c> es
///    siempre <c>false</c> aquí y <c>ApplyToRegistry</c> toma
///    deliberadamente la rama de guarda (log de <c>Warning</c> + return),
///    sin llegar nunca a <c>Registry.CurrentUser.CreateSubKey</c>. Esto se
///    confirma leyendo el valor real de <c>HKCU\...\Run\ClaudeMeter</c>
///    antes y después de <see cref="AutoStartService.SetEnabled"/> y
///    comprobando que no cambia -- no se usa ningún mock/abstracción sobre
///    <c>Microsoft.Win32.Registry</c> porque el propio diseño de producción
///    ya hace inalcanzable esa rama fuera de una instalación Velopack real.
///    La rama contraria (<c>IsCurrentSet == true</c>, escritura real en el
///    registro) requeriría mutar el estado estático global de Velopack
///    (<c>VelopackLocator.SetCurrentLocator</c>), que no tiene forma
///    pública de revertirse dentro del mismo proceso y contaminaría el
///    resto de la batería de tests de este ensamblado (incluida
///    <c>TrayIconServiceTests</c>, que también construye
///    <see cref="AutoStartService"/>) -- gap aceptado, mismo criterio que
///    <c>ClickThroughServiceTests</c> con la llamada real a user32.dll.
///
/// 2. <b>config.json real de la máquina</b>: a diferencia de
///    <see cref="AppConfigStore"/> (cuyo <c>Load</c>/<c>Save</c> ya aceptan
///    un <c>configPath</c> explícito), <see cref="AutoStartService.SetEnabled"/>
///    no recibía ningún parámetro de ruta -- se añadió
///    <see cref="AutoStartService.ConfigPathForTests"/> (cambio aditivo de
///    esta fase de testing, <c>null</c> por defecto preserva el
///    comportamiento de producción íntegro) para que estos tests nunca
///    escriban en el <c>%LOCALAPPDATA%\ClaudeMeter\config.json</c> real del
///    desarrollador/runner de CI.
/// </summary>
public sealed class AutoStartServiceTests
{
    private static AutoStartService CreateService(bool autoStartEnabled, out string tempConfigPath, ILogger<AutoStartService>? logger = null)
    {
        tempConfigPath = Path.Combine(Path.GetTempPath(), "ClaudeMeterTests", Guid.NewGuid().ToString("N"), "config.json");
        var initialConfig = AppConfig.Default with { AutoStartEnabled = autoStartEnabled };
        var service = new AutoStartService(
            initialConfig,
            new AppConfigStore(NullLogger<AppConfigStore>.Instance),
            logger ?? NullLogger<AutoStartService>.Instance);
        service.ConfigPathForTests = tempConfigPath;
        return service;
    }

    private static void CleanUp(string tempConfigPath)
    {
        var dir = Path.GetDirectoryName(tempConfigPath);
        if (dir is not null && Directory.Exists(dir))
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Constructor_IsEnabled_ReflejaElValorDeAutoStartEnabledDeAppConfig(bool autoStartEnabled)
    {
        var service = CreateService(autoStartEnabled, out var tempConfigPath);

        try
        {
            Assert.Equal(autoStartEnabled, service.IsEnabled);
        }
        finally
        {
            CleanUp(tempConfigPath);
        }
    }

    [Fact]
    public void Initialize_ConIsEnabledFalse_NoLanzaYNoRegistraNingunWarning()
    {
        // AC: autoarranque desactivado -> Initialize() no debe intentar
        // aplicar nada al registro (ni siquiera la rama de guarda).
        var logger = new CapturingLogger<AutoStartService>();
        var service = CreateService(autoStartEnabled: false, out var tempConfigPath, logger);

        try
        {
            var exception = Record.Exception(service.Initialize);

            Assert.Null(exception);
            Assert.DoesNotContain(LogLevel.Warning, logger.LoggedLevels);
        }
        finally
        {
            CleanUp(tempConfigPath);
        }
    }

    [Fact]
    public void Initialize_ConIsEnabledTrue_NoLanzaYRegistraWarningPorFaltaDeContextoVelopack()
    {
        // AC + guarda de nulabilidad documentada en el XMLDoc de producción:
        // sin una instalación Velopack real (caso de `dotnet test`),
        // ResolveStableExePath no puede resolver una ruta estable -- debe
        // registrar un Warning y omitir la escritura en el registro sin
        // lanzar, no fallar de forma silenciosa ni ruidosa (excepción).
        var logger = new CapturingLogger<AutoStartService>();
        var service = CreateService(autoStartEnabled: true, out var tempConfigPath, logger);

        try
        {
            var exception = Record.Exception(service.Initialize);

            Assert.Null(exception);
            Assert.Contains(LogLevel.Warning, logger.LoggedLevels);
            Assert.True(service.IsEnabled); // el guard no debe alterar el estado ya resuelto por el constructor
        }
        finally
        {
            CleanUp(tempConfigPath);
        }
    }

    [Fact]
    public void SetEnabled_True_ActualizaIsEnabled()
    {
        var service = CreateService(autoStartEnabled: false, out var tempConfigPath);

        try
        {
            service.SetEnabled(true);

            Assert.True(service.IsEnabled);
        }
        finally
        {
            CleanUp(tempConfigPath);
        }
    }

    [Fact]
    public void SetEnabled_True_PersisteElValorEnConfigJson()
    {
        var service = CreateService(autoStartEnabled: false, out var tempConfigPath);

        try
        {
            service.SetEnabled(true);

            var persisted = new AppConfigStore(NullLogger<AppConfigStore>.Instance).Load(tempConfigPath);
            Assert.True(persisted.AutoStartEnabled);
        }
        finally
        {
            CleanUp(tempConfigPath);
        }
    }

    [Fact]
    public void SetEnabled_False_PersisteElValorEnConfigJson()
    {
        var service = CreateService(autoStartEnabled: true, out var tempConfigPath);

        try
        {
            service.SetEnabled(false);

            var persisted = new AppConfigStore(NullLogger<AppConfigStore>.Instance).Load(tempConfigPath);
            Assert.False(persisted.AutoStartEnabled);
        }
        finally
        {
            CleanUp(tempConfigPath);
        }
    }

    [Fact]
    public void SetEnabled_DisparaStateChangedExactamenteUnaVez()
    {
        var service = CreateService(autoStartEnabled: false, out var tempConfigPath);

        try
        {
            var stateChangedCount = 0;
            service.StateChanged += () => stateChangedCount++;

            service.SetEnabled(true);

            Assert.Equal(1, stateChangedCount);
        }
        finally
        {
            CleanUp(tempConfigPath);
        }
    }

    [Fact]
    public void Toggle_LlamadoDosVeces_VuelveAlEstadoOriginal()
    {
        var service = CreateService(autoStartEnabled: false, out var tempConfigPath);

        try
        {
            service.Toggle(); // false -> true
            service.Toggle(); // true -> false

            Assert.False(service.IsEnabled);
        }
        finally
        {
            CleanUp(tempConfigPath);
        }
    }

    [Fact]
    public void SetEnabled_NoTocaElRegistroRealDeAutoarranqueDeLaMaquina()
    {
        // Prueba de aislamiento explícita (ver XMLDoc de la clase): confirma
        // -leyendo el valor real- que SetEnabled(true) no escribe en
        // HKCU\Software\Microsoft\Windows\CurrentVersion\Run\ClaudeMeter en
        // la máquina que ejecuta el test, gracias a la rama de guarda de
        // ResolveStableExePath (VelopackLocator.IsCurrentSet == false fuera
        // de una instalación Velopack real).
        const string runKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string valueName = "ClaudeMeter";

        object? before;
        using (var key = Registry.CurrentUser.OpenSubKey(runKeyPath, writable: false))
        {
            before = key?.GetValue(valueName);
        }

        var service = CreateService(autoStartEnabled: false, out var tempConfigPath);

        try
        {
            service.SetEnabled(true);

            object? after;
            using (var key = Registry.CurrentUser.OpenSubKey(runKeyPath, writable: false))
            {
                after = key?.GetValue(valueName);
            }

            Assert.Equal(before, after);
        }
        finally
        {
            CleanUp(tempConfigPath);
        }
    }

    [Fact]
    public void SetEnabled_LlamadoVariasVecesSeguidas_EsIdempotenteYNoLanza()
    {
        var service = CreateService(autoStartEnabled: false, out var tempConfigPath);

        try
        {
            var exception = Record.Exception(() =>
            {
                service.SetEnabled(true);
                service.SetEnabled(true);
                service.SetEnabled(false);
            });

            Assert.Null(exception);
            Assert.False(service.IsEnabled);
        }
        finally
        {
            CleanUp(tempConfigPath);
        }
    }
}
