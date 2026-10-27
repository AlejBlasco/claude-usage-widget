using System.IO;
using ClaudeMeter.Desktop.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Velopack.Locators;

namespace ClaudeMeter.Desktop.Startup;

/// <summary>
/// Registra/retira ClaudeMeter de <c>HKCU\...\Run</c> (F6/US-3, issue #26).
/// La ruta escrita se resuelve vía el locator de Velopack
/// (<see cref="IVelopackLocator.RootAppDir"/> + <see cref="IVelopackLocator.AppId"/>),
/// nunca <c>Environment.ProcessPath</c>: en ejecución normal el proceso
/// corre desde <c>{RootAppDir}\current\...</c>, carpeta que Velopack
/// reemplaza en cada actualización; el stub
/// <c>{RootAppDir}\{AppId}.exe</c> es la única ruta estable entre versiones
/// (ver rationale completo en el documento de diseño de F6, Technology
/// Choices).
/// </summary>
public sealed class AutoStartService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "ClaudeMeter";

    private readonly AppConfigStore _configStore;
    private readonly ILogger<AutoStartService> _logger;

    public AutoStartService(AppConfig initialConfig, AppConfigStore configStore, ILogger<AutoStartService> logger)
    {
        _configStore = configStore;
        _logger = logger;
        IsEnabled = initialConfig.AutoStartEnabled;
    }

    public bool IsEnabled { get; private set; }

    /// <summary>
    /// Únicamente para tests (sdlc-testing, F6): redirige el
    /// <c>configPath</c> de <see cref="AppConfigStore.Load"/>/<see cref="AppConfigStore.Save"/>
    /// invocados desde <see cref="SetEnabled"/> a una ruta de fichero
    /// temporal en vez de <c>%LOCALAPPDATA%</c> real, para que las pruebas
    /// xUnit no muten el <c>config.json</c> real de la máquina que las
    /// ejecuta. <c>null</c> (valor por defecto) preserva exactamente el
    /// comportamiento de producción (ruta por defecto de
    /// <see cref="AppConfigStore"/>) — cambio aditivo, sin efecto en
    /// producción.
    /// </summary>
    internal string? ConfigPathForTests { get; set; }

    /// <summary>Se levanta tras un <see cref="SetEnabled"/> que sí llegó a aplicarse, para que <see cref="Tray.TrayIconService"/> sincronice el <c>Checked</c> del ítem de menú.</summary>
    public event Action? StateChanged;

    /// <summary>Llamado una única vez desde <c>App.OnStartup</c>: re-aplica (de forma idempotente) el estado ya persistido en <c>config.json</c> al registro, por si la ruta del stub necesitase reconfirmarse tras una actualización.</summary>
    public void Initialize()
    {
        if (IsEnabled)
        {
            ApplyToRegistry(enabled: true);
        }
    }

    public void Toggle() => SetEnabled(!IsEnabled);

    public void SetEnabled(bool enabled)
    {
        ApplyToRegistry(enabled);
        IsEnabled = enabled;
        _configStore.Save(_configStore.Load(ConfigPathForTests) with { AutoStartEnabled = enabled }, ConfigPathForTests);
        StateChanged?.Invoke();
    }

    private void ApplyToRegistry(bool enabled)
    {
        try
        {
            var exePath = ResolveStableExePath();
            if (exePath is null)
            {
                return; // ya se registró un Warning en ResolveStableExePath
            }

            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
            if (enabled)
            {
                key.SetValue(RunValueName, $"\"{exePath}\"");
            }
            else
            {
                key.DeleteValue(RunValueName, throwOnMissingValue: false);
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            _logger.LogError(ex, "No se pudo actualizar la entrada de autoarranque en el registro");
        }
    }

    /// <summary>
    /// <see cref="VelopackLocator.IsCurrentSet"/> es <c>false</c> cuando el
    /// proceso no arrancó vía una instalación real de Velopack (p. ej.
    /// <c>dotnet run</c>/depuración local en F5-F6 de desarrollo, sin
    /// <c>vpk pack</c> de por medio) -- en ese caso no existe un stub
    /// estable que resolver, así que se registra un aviso y se omite la
    /// escritura en el registro en vez de lanzar.
    /// </summary>
    private string? ResolveStableExePath()
    {
        if (!VelopackLocator.IsCurrentSet)
        {
            _logger.LogWarning("No se pudo resolver la ruta estable del ejecutable (la aplicación no se ejecuta desde una instalación de Velopack); se omite el autoarranque");
            return null;
        }

        var locator = VelopackLocator.Current;
        if (locator.RootAppDir is not { } rootAppDir)
        {
            _logger.LogWarning("El locator de Velopack no expone RootAppDir; se omite el autoarranque");
            return null;
        }

        return Path.Combine(rootAppDir, $"{locator.AppId}.exe");
    }
}
