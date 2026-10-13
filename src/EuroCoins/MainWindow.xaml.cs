using EuroCoins.Core.Collections;
using EuroCoins.Core.Settings;
using EuroCoins.Services;
using EuroCoins.ViewModels;
using EuroCoins.Views;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Windows.Graphics;
using WinRT.Interop;

namespace EuroCoins;

/// <summary>
/// The main window: title bar with search, navigation pane and content frame.
/// </summary>
public sealed partial class MainWindow : Window
{
    private const int DefaultWidth = 1280;
    private const int DefaultHeight = 800;
    private readonly SettingsService settings;
    private readonly CollectionStore store;
    private readonly Localizer localizer;
    private RectInt32? normalBounds;

    /// <summary>Creates the main window.</summary>
    public MainWindow(ShellViewModel viewModel, SettingsService settings, CollectionStore store, Localizer localizer)
    {
        ViewModel = viewModel;
        this.settings = settings;
        this.store = store;
        this.localizer = localizer;
        InitializeComponent();

        Title = localizer.Get("App_Title");
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "EuroCoins.ico"));
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.PreferredMinimumWidth = 640;
            presenter.PreferredMinimumHeight = 480;
        }

        RestorePlacement();
        AppWindow.Changed += OnAppWindowChanged;
        AppWindow.Closing += (_, _) =>
        {
            SavePlacement();
            store.Flush(); // Do not lose a change that is still being written.
        };

        ApplyTheme();
        settings.Changed += (_, _) => ApplyTheme();

        store.SaveFailed += (_, ex) => ShowCollectionProblem(store.FilePath is null ? App.Current?.StartupProblem ?? ex.Message : ex.Message);
        if (App.Current?.StartupProblem is { } problem)
        {
            ShowCollectionProblem(problem);
        }

        ViewModel.Navigation.Attach(ContentFrame);
        ViewModel.Navigation.NavigateToOverview();
    }

    /// <summary>Gets the view model.</summary>
    public ShellViewModel ViewModel { get; }

    /// <summary>Restores, shows and focuses the window.</summary>
    public void BringToFront()
    {
        if (AppWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Minimized } presenter)
        {
            presenter.Restore();
        }

        AppWindow.Show();
        Program.SetForegroundWindow(WindowNative.GetWindowHandle(this));
    }

    // ----- Collection problems -------------------------------------------------------------------

    private void ShowCollectionProblem(string details)
    {
        var notLoaded = store.FilePath is null;
        CollectionInfoBar.Title = localizer.Get(notLoaded ? "Collection_NotLoadedTitle" : "Collection_SaveFailedTitle");
        CollectionInfoBar.Message = notLoaded ? $"{localizer.Get("Collection_NotLoadedMessage")} {details}" : details;
        RetryButton.Visibility = notLoaded ? Visibility.Visible : Visibility.Collapsed;
        CollectionInfoBar.IsOpen = true;
    }

    private void OnRetryLoadClick(object sender, RoutedEventArgs e)
    {
        if (App.Current?.TryOpenCollection() == true)
        {
            CollectionInfoBar.IsOpen = false;
        }
        else if (App.Current?.StartupProblem is { } problem)
        {
            ShowCollectionProblem(problem);
        }
    }

    private void ApplyTheme()
    {
        var theme = settings.Current.Theme;
        RootGrid.RequestedTheme = theme switch
        {
            AppTheme.Light => ElementTheme.Light,
            AppTheme.Dark => ElementTheme.Dark,
            _ => ElementTheme.Default,
        };
        AppWindow.TitleBar.PreferredTheme = theme switch
        {
            AppTheme.Light => TitleBarTheme.Light,
            AppTheme.Dark => TitleBarTheme.Dark,
            _ => TitleBarTheme.UseDefaultAppMode,
        };
    }

    // ----- Window placement (remembered per device) ---------------------------------------

    private void RestorePlacement()
    {
        if (settings.Current.Window is { } saved)
        {
            var bounds = new RectInt32(saved.X, saved.Y, saved.Width, saved.Height);
            var area = DisplayArea.GetFromRect(bounds, DisplayAreaFallback.None);
            if (area is not null)
            {
                AppWindow.MoveAndResize(bounds);
                normalBounds = bounds;
                if (saved.IsMaximized && AppWindow.Presenter is OverlappedPresenter presenter)
                {
                    presenter.Maximize();
                }

                return;
            }
        }

        // First start (or the saved monitor is gone): default size, centered on the primary display.
        var work = DisplayArea.Primary.WorkArea;
        var scale = Program.GetDpiForWindow(WindowNative.GetWindowHandle(this)) / 96.0;
        var width = Math.Min((int)(DefaultWidth * scale), work.Width);
        var height = Math.Min((int)(DefaultHeight * scale), work.Height);
        var defaultBounds = new RectInt32(work.X + ((work.Width - width) / 2), work.Y + ((work.Height - height) / 2), width, height);
        AppWindow.MoveAndResize(defaultBounds);
        normalBounds = defaultBounds;
    }

    private void OnAppWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if ((args.DidPositionChange || args.DidSizeChange)
            && sender.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Restored })
        {
            normalBounds = new RectInt32(sender.Position.X, sender.Position.Y, sender.Size.Width, sender.Size.Height);
        }
    }

    private void SavePlacement()
    {
        if (normalBounds is not { } bounds)
        {
            return;
        }

        var maximized = AppWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Maximized };
        settings.Update(s => s with { Window = new WindowPlacement(bounds.X, bounds.Y, bounds.Width, bounds.Height, maximized) });
    }

    // ----- Navigation -----------------------------------------------------------------------

    private void OnNavigationItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        var navigation = ViewModel.Navigation;
        if (args.IsSettingsInvoked)
        {
            navigation.NavigateToSettings();
            return;
        }

        // The tags of the coin list items are the names of their CoinListKind.
        var tag = (args.InvokedItemContainer as NavigationViewItem)?.Tag as string;
        if (tag == "Overview")
        {
            navigation.NavigateToOverview();
        }
        else if (Enum.TryParse<CoinListKind>(tag, out var kind))
        {
            navigation.NavigateToCoins(new CoinListRequest(kind));
        }
    }

    private void OnFrameNavigated(object sender, NavigationEventArgs e)
    {
        AppTitleBar.IsBackButtonEnabled = ContentFrame.CanGoBack;
        var tag = e.SourcePageType == typeof(OverviewPage) ? "Overview"
            : e.Parameter is CoinListRequest { Kind: not CoinListKind.Search } request ? request.Kind.ToString()
            : null;

        NavView.SelectedItem = e.SourcePageType == typeof(SettingsPage)
            ? NavView.SettingsItem
            : NavView.MenuItems.OfType<NavigationViewItem>().FirstOrDefault(i => (string)i.Tag == tag);
    }

    private void OnBackRequested(TitleBar sender, object args) => ViewModel.Navigation.GoBack();

    private void OnPaneToggleRequested(TitleBar sender, object args) => NavView.IsPaneOpen = !NavView.IsPaneOpen;

    // ----- Search ---------------------------------------------------------------------------

    private void OnSearchTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            ViewModel.UpdateSuggestions(sender.Text);
        }
    }

    private void OnSearchQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        ViewModel.Submit(args.QueryText, args.ChosenSuggestion as SearchSuggestion);
        ViewModel.Suggestions.Clear();
    }

    private void OnSearchAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        SearchBox.Focus(FocusState.Keyboard);
        args.Handled = true;
    }
}
