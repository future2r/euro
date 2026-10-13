using System.Diagnostics;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EuroCoins.Core;
using EuroCoins.Core.Collections;
using EuroCoins.Core.Settings;
using EuroCoins.Services;
using Microsoft.Windows.AppLifecycle;

namespace EuroCoins.ViewModels;

/// <summary>
/// The settings page: theme, language, collection location and app information.
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private static readonly string?[] Languages = [null, "de", "en"];
    private readonly SettingsService settings;
    private readonly CollectionStore store;
    private readonly AppDataPaths paths;
    private readonly Localizer localizer;

    /// <summary>Creates the view model.</summary>
    public SettingsViewModel(SettingsService settings, CollectionStore store, AppDataPaths paths, Localizer localizer)
    {
        this.settings = settings;
        this.store = store;
        this.paths = paths;
        this.localizer = localizer;
        ThemeIndex = (int)settings.Current.Theme;
        LanguageIndex = Math.Max(0, Array.IndexOf(Languages, settings.Current.Language));
        CollectionPath = store.FilePath ?? paths.DefaultCollectionFile;
    }

    /// <summary>Gets or sets the theme: 0 = system, 1 = light, 2 = dark (order of <see cref="AppTheme"/>).</summary>
    [ObservableProperty]
    public partial int ThemeIndex { get; set; }

    /// <summary>Gets or sets the language: 0 = Windows, 1 = German, 2 = English.</summary>
    [ObservableProperty]
    public partial int LanguageIndex { get; set; }

    /// <summary>Gets a value indicating whether the app must restart to apply the language.</summary>
    [ObservableProperty]
    public partial bool IsRestartRequired { get; set; }

    /// <summary>Gets the path of the collection file.</summary>
    [ObservableProperty]
    public partial string CollectionPath { get; set; } = string.Empty;

    /// <summary>Gets an error message about the collection location, or an empty string.</summary>
    [ObservableProperty]
    public partial string LocationError { get; set; } = string.Empty;

    /// <summary>Gets the localized theme name shown in the header of the theme expander.</summary>
    public string ThemeLabel => localizer.Get(ThemeIndex switch
    {
        1 => "Settings_ThemeLight",
        2 => "Settings_ThemeDark",
        _ => "Settings_ThemeSystem",
    });

    /// <summary>Gets the version text.</summary>
    public string VersionText => localizer.Format("Settings_Version",
        Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "?");

    /// <summary>Gets the folder of the log files.</summary>
    public string LogDirectory => paths.LogDirectory;

    /// <summary>Uses the collection file in another folder (e.g. OneDrive). If the folder already contains a
    /// collection, it is opened; otherwise the current collection file is copied there.</summary>
    public void ChangeCollectionFolder(string folder)
    {
        var path = Path.Combine(folder, Path.GetFileName(paths.DefaultCollectionFile));
        try
        {
            if (!File.Exists(path))
            {
                store.Flush(); // The copy must contain a change that is still being written.
                if (File.Exists(CollectionPath))
                {
                    File.Copy(CollectionPath, path);
                }
            }

            store.Open(path);
            settings.Update(s => s with { CollectionPath = path == paths.DefaultCollectionFile ? null : path });
            CollectionPath = path;
            LocationError = string.Empty;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            LocationError = localizer.Format("Settings_LocationError", ex.Message);
        }
    }

    partial void OnThemeIndexChanged(int value)
    {
        settings.Update(s => s with { Theme = (AppTheme)Math.Clamp(value, 0, 2) });
        OnPropertyChanged(nameof(ThemeLabel));
    }

    partial void OnLanguageIndexChanged(int value)
    {
        var language = Languages[Math.Clamp(value, 0, Languages.Length - 1)];
        settings.Update(s => s with { Language = language });
        IsRestartRequired = language != settings.StartupLanguage;
    }

    [RelayCommand]
    private void Restart()
    {
        store.Flush(); // Restart ends the process immediately; write pending changes first.
        AppInstance.Restart(Program.RestartArgument);
    }

    [RelayCommand]
    private void OpenCollectionFolder() => OpenFolder(Path.GetDirectoryName(CollectionPath)!);

    [RelayCommand]
    private void OpenLogFolder()
    {
        Directory.CreateDirectory(paths.LogDirectory);
        OpenFolder(paths.LogDirectory);
    }

    private static void OpenFolder(string folder) =>
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{folder}\"") { UseShellExecute = true });
}
