using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace ClaudeMeter.Desktop.Configuration;

/// <summary>
/// Única clase que lee/escribe <c>config.json</c>. <see cref="Load"/> nunca
/// lanza: JSON sintácticamente inválido o ilegible cae a
/// <see cref="AppConfig.Default"/> completo; un campo concreto fuera de
/// rango (p. ej. intervalo &lt;= 0) cae solo ese campo a su valor por
/// defecto, con un <c>Warning</c> en el log por cada caso (AC de US-1).
/// <see cref="Save"/>/<see cref="SavePosition"/> nunca lanzan: un fallo de
/// E/S se registra y se traga, sin abortar la aplicación (AC de US-2).
/// </summary>
public sealed class AppConfigStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private readonly ILogger<AppConfigStore> _logger;

    public AppConfigStore(ILogger<AppConfigStore> logger) => _logger = logger;

    /// <summary><c>%LOCALAPPDATA%\ClaudeMeter\config.json</c> — misma carpeta base que los logs de F2/Ciclo A (<c>%LOCALAPPDATA%\ClaudeMeter\logs</c>).</summary>
    public static string DefaultConfigPath() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClaudeMeter", "config.json");

    /// <summary>
    /// Lee y resuelve <c>config.json</c>. Nunca lanza: cualquier problema de
    /// lectura/parseo cae a <see cref="AppConfig.Default"/> (completo o solo
    /// en el campo afectado, según el caso — ver comentarios de cada rama).
    /// </summary>
    public AppConfig Load(string? configPath = null)
    {
        var path = configPath ?? DefaultConfigPath();
        if (!File.Exists(path))
        {
            return AppConfig.Default; // AC: sin fichero -> valores por defecto, sin crearlo como efecto secundario
        }

        AppConfigDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<AppConfigDto>(File.ReadAllText(path), SerializerOptions);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "config.json no se pudo leer o es JSON inválido; se usan los valores por defecto");
            return AppConfig.Default;
        }

        if (dto is null)
        {
            _logger.LogWarning("config.json está vacío o es literalmente 'null'; se usan los valores por defecto");
            return AppConfig.Default;
        }

        return new AppConfig(
            PollingInterval: ResolveInterval(dto.PollingIntervalSeconds),
            Position: ResolvePosition(dto.WindowPosition),
            ChimeEnabled: dto.ChimeEnabled ?? AppConfig.Default.ChimeEnabled);
    }

    /// <summary>
    /// Escribe <c>config.json</c> de forma atómica (fichero temporal +
    /// <see cref="File.Move(string, string, bool)"/> con reemplazo). Nunca
    /// lanza: un fallo de E/S se registra como <c>Error</c> y el cambio
    /// queda solo en memoria para esta sesión.
    /// </summary>
    public void Save(AppConfig config, string? configPath = null)
    {
        var path = configPath ?? DefaultConfigPath();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            var dto = new AppConfigDto
            {
                PollingIntervalSeconds = config.PollingInterval.TotalSeconds,
                ChimeEnabled = config.ChimeEnabled,
                WindowPosition = config.Position is { } p ? new WindowPositionDto { Left = p.Left, Top = p.Top } : null,
            };

            var tempPath = path + ".tmp";
            File.WriteAllText(tempPath, JsonSerializer.Serialize(dto, SerializerOptions));
            File.Move(tempPath, path, overwrite: true); // escritura atómica: nunca deja config.json a medias (AC de US-2)
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "No se pudo escribir config.json; el cambio permanece solo en memoria para esta sesión");
        }
    }

    /// <summary>Usado por <see cref="Windowing.WindowDragService"/> tras soltar un arrastre (AC de US-2). Conserva intervalo/chime ya persistidos, solo actualiza la posición.</summary>
    public void SavePosition(double left, double top, string? configPath = null)
    {
        var current = Load(configPath);
        Save(current with { Position = new WindowPosition(left, top) }, configPath);
    }

    private TimeSpan ResolveInterval(double? seconds)
    {
        if (seconds is not { } value || value <= 0 || double.IsNaN(value) || double.IsInfinity(value))
        {
            if (seconds is not null)
            {
                _logger.LogWarning("pollingIntervalSeconds inválido en config.json ({Value}); se usa el valor por defecto de 60s", seconds);
            }
            return AppConfig.Default.PollingInterval;
        }
        return TimeSpan.FromSeconds(value);
    }

    private WindowPosition? ResolvePosition(WindowPositionDto? dto)
    {
        if (dto is null)
        {
            return null;
        }

        if (dto.Left is not { } left || dto.Top is not { } top ||
            double.IsNaN(left) || double.IsInfinity(left) || double.IsNaN(top) || double.IsInfinity(top))
        {
            _logger.LogWarning("windowPosition inválida en config.json; se usa el cálculo de posición por defecto");
            return null;
        }

        return new WindowPosition(left, top);
    }

    /// <summary>DTO de (de)serialización, deliberadamente con campos anulables: distingue "campo ausente" (usar valor por defecto en silencio) de "campo presente pero fuera de rango" (usar valor por defecto y loguear Warning).</summary>
    private sealed class AppConfigDto
    {
        [JsonPropertyName("pollingIntervalSeconds")]
        public double? PollingIntervalSeconds { get; set; }

        [JsonPropertyName("chimeEnabled")]
        public bool? ChimeEnabled { get; set; }

        [JsonPropertyName("windowPosition")]
        public WindowPositionDto? WindowPosition { get; set; }
    }

    private sealed class WindowPositionDto
    {
        [JsonPropertyName("left")]
        public double? Left { get; set; }

        [JsonPropertyName("top")]
        public double? Top { get; set; }
    }
}
