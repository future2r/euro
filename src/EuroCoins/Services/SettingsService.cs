using EuroCoins.Core.Settings;

namespace EuroCoins.Services;

/// <summary>
/// Holds the current settings and saves every change.
/// </summary>
public sealed class SettingsService
{
    private readonly SettingsStore store;

    /// <summary>Creates the service and loads the stored settings.</summary>
    public SettingsService(SettingsStore store)
    {
        this.store = store;
        Current = store.Load();
        StartupLanguage = Current.Language;
    }

    /// <summary>Occurs after the settings changed.</summary>
    public event EventHandler? Changed;

    /// <summary>Gets the current settings.</summary>
    public AppSettings Current { get; private set; }

    /// <summary>Gets the language that was applied at startup.</summary>
    public string? StartupLanguage { get; }

    /// <summary>Changes and saves the settings.</summary>
    public void Update(Func<AppSettings, AppSettings> change)
    {
        ArgumentNullException.ThrowIfNull(change);
        var updated = change(Current);
        if (updated == Current)
        {
            return;
        }

        Current = updated;
        store.Save(Current);
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
