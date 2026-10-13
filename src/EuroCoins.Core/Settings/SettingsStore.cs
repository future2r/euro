using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace EuroCoins.Core.Settings;

/// <summary>
/// Loads and saves <see cref="AppSettings"/> as JSON. Fail-safe: unreadable settings fall back
/// to defaults, and failed writes are logged but never thrown.
/// </summary>
/// <param name="path">The path of <c>settings.json</c>.</param>
/// <param name="logger">The logger.</param>
public sealed partial class SettingsStore(string path, ILogger<SettingsStore> logger)
{
    /// <summary>
    /// Loads the settings.
    /// </summary>
    /// <returns>The stored settings, or defaults if there are none or they cannot be read.</returns>
    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(path))
            {
                return new AppSettings();
            }

            var settings = JsonSerializer.Deserialize(File.ReadAllText(path, Encoding.UTF8), SettingsJsonContext.Default.AppSettings)
                ?? new AppSettings();
            return Sanitize(settings);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            LogLoadFailed(ex, path);
            return new AppSettings();
        }
    }

    /// <summary>
    /// Saves the settings (best effort).
    /// </summary>
    /// <param name="settings">The settings.</param>
    public void Save(AppSettings settings)
    {
        try
        {
            AtomicFile.WriteAllText(path, JsonSerializer.Serialize(settings, SettingsJsonContext.Default.AppSettings));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogSaveFailed(ex, path);
        }
    }

    private static AppSettings Sanitize(AppSettings settings) => settings with
    {
        Theme = Enum.IsDefined(settings.Theme) ? settings.Theme : AppTheme.System,
        Language = settings.Language is "de" or "en" ? settings.Language : null,
        TileZoom = double.IsFinite(settings.TileZoom)
            ? Math.Clamp(settings.TileZoom, AppSettings.MinTileZoom, AppSettings.MaxTileZoom)
            : 1.0,
        Window = settings.Window is { Width: > 0, Height: > 0 } ? settings.Window : null,
    };

    [LoggerMessage(Level = LogLevel.Warning, Message = "Reading the settings from {Path} failed; using defaults.")]
    private partial void LogLoadFailed(Exception exception, string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Saving the settings to {Path} failed.")]
    private partial void LogSaveFailed(Exception exception, string path);
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true,
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(AppSettings))]
internal sealed partial class SettingsJsonContext : JsonSerializerContext;
