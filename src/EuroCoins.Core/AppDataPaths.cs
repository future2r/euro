namespace EuroCoins.Core;

/// <summary>
/// The locations of the app's per-user data.
/// </summary>
/// <param name="root">The data folder, normally <c>%LOCALAPPDATA%\EuroCoins</c>.</param>
public sealed class AppDataPaths(string root)
{
    /// <summary>
    /// Gets the default locations under <c>%LOCALAPPDATA%\EuroCoins</c>.
    /// </summary>
    public static AppDataPaths Default { get; } = new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EuroCoins"));

    /// <summary>Gets the path of the settings file.</summary>
    public string SettingsFile => Path.Combine(root, "settings.json");

    /// <summary>Gets the default path of the collection file.</summary>
    public string DefaultCollectionFile => Path.Combine(root, "collection.json");

    /// <summary>Gets the folder of the log files.</summary>
    public string LogDirectory => Path.Combine(root, "Logs");
}
