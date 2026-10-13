using EuroCoins.Core;
using EuroCoins.Core.Catalogs;
using EuroCoins.Core.Collections;
using EuroCoins.Core.Logging;
using EuroCoins.Core.Models;
using EuroCoins.Core.Settings;
using EuroCoins.Services;
using EuroCoins.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;

namespace EuroCoins;

/// <summary>
/// The application: composes the services and opens the main window.
/// </summary>
public partial class App : Application
{
    private ILogger<App>? logger;

    /// <summary>Creates the application.</summary>
    public App()
    {
        InitializeComponent();
        UnhandledException += OnUnhandledException;
    }

    /// <summary>Gets the running application.</summary>
    public static new App? Current => Application.Current as App;

    /// <summary>Gets the service provider.</summary>
    public IServiceProvider Services { get; private set; } = null!;

    /// <summary>Gets the main window.</summary>
    public MainWindow? MainWindow { get; private set; }

    /// <summary>Gets a problem that occurred while loading the collection, or <see langword="null"/>.</summary>
    public string? StartupProblem { get; private set; }

    /// <summary>Brings the main window to the front (a second instance was started).</summary>
    public void BringToFront() => MainWindow?.DispatcherQueue.TryEnqueue(() => MainWindow.BringToFront());

    /// <inheritdoc/>
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Services = ConfigureServices(AppDataPaths.Default);
        logger = Services.GetRequiredService<ILogger<App>>();

        _ = Services.GetRequiredService<Localizer>(); // Applies the UI language before any XAML loads.
        TryOpenCollection();

        MainWindow = Services.GetRequiredService<MainWindow>();
        MainWindow.Activate();
    }

    /// <summary>
    /// Opens the collection file from the settings. On failure the file is left untouched, nothing is
    /// saved (the store reports every change as not saved) and <see cref="StartupProblem"/> is set.
    /// </summary>
    /// <returns><see langword="true"/> if the collection was loaded.</returns>
    public bool TryOpenCollection()
    {
        var settings = Services.GetRequiredService<SettingsService>();
        var paths = Services.GetRequiredService<AppDataPaths>();
        try
        {
            Services.GetRequiredService<CollectionStore>().Open(settings.Current.CollectionPath ?? paths.DefaultCollectionFile);
            StartupProblem = null;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            LogCollectionLoadFailed(logger!, ex);
            StartupProblem = ex.Message;
            return false;
        }
    }

    private static ServiceProvider ConfigureServices(AppDataPaths paths)
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder
            .SetMinimumLevel(LogLevel.Warning)
            .AddProvider(new FileLoggerProvider(paths.LogDirectory)));
        services.AddSingleton(paths);
        services.AddSingleton(sp => new SettingsStore(paths.SettingsFile, sp.GetRequiredService<ILogger<SettingsStore>>()));
        services.AddSingleton<SettingsService>();
        services.AddSingleton(sp => new Localizer(AppLanguage.Apply(sp.GetRequiredService<SettingsService>().Current.Language)));
        services.AddSingleton<Catalog>(_ => CatalogSerializer.Load(Path.Combine(AppContext.BaseDirectory, "Assets", "Catalog", "catalog.json")));
        services.AddSingleton<CollectionState>();
        services.AddSingleton<CollectionStore>();
        services.AddSingleton<ImageService>();
        services.AddSingleton<NavigationService>();
        services.AddSingleton<ShellViewModel>();
        services.AddSingleton<MainWindow>();
        services.AddTransient<OverviewViewModel>();
        services.AddTransient<CoinListViewModel>();
        services.AddTransient<SettingsViewModel>();
        return services.BuildServiceProvider();
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        if (logger is not null)
        {
            LogUnhandled(logger, e.Exception);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "The collection could not be loaded.")]
    private static partial void LogCollectionLoadFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Critical, Message = "Unhandled exception.")]
    private static partial void LogUnhandled(ILogger logger, Exception exception);
}
