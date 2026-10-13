using System.ComponentModel;
using EuroCoins.Services;
using EuroCoins.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.System;

namespace EuroCoins.Views;

/// <summary>
/// The coin list: filter bar, grouped grid or list, details pane and enlarged image.
/// </summary>
public sealed partial class CoinsPage : Page
{
    private const double TileWidth = 150;
    private const double TileHeight = 196;
    private readonly Localizer localizer;
    private ScrollViewer? gridScrollViewer;

    /// <summary>Creates the page.</summary>
    public CoinsPage()
    {
        ViewModel = App.Current!.Services.GetRequiredService<CoinListViewModel>();
        localizer = App.Current.Services.GetRequiredService<Localizer>();
        InitializeComponent();
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;

        var escape = new KeyboardAccelerator { Key = VirtualKey.Escape };
        escape.Invoked += (_, args) =>
        {
            if (ViewModel.IsEnlarged)
            {
                ViewModel.CloseEnlargedCommand.Execute(null);
                args.Handled = true;
            }
        };
        KeyboardAccelerators.Add(escape);

        // View menu shortcuts, also while the menu is closed (like Ctrl+Shift+1… in File Explorer).
        var shiftControl = VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift;
        AddAccelerator(VirtualKey.Number1, shiftControl, ViewModel.SetViewModeCommand, nameof(CoinViewMode.Grid));
        AddAccelerator(VirtualKey.Number2, shiftControl, ViewModel.SetViewModeCommand, nameof(CoinViewMode.List));
        foreach (var key in new[] { VirtualKey.Add, (VirtualKey)187 })
        {
            AddAccelerator(key, VirtualKeyModifiers.Control, ViewModel.ZoomInCommand);
        }

        foreach (var key in new[] { VirtualKey.Subtract, (VirtualKey)189 })
        {
            AddAccelerator(key, VirtualKeyModifiers.Control, ViewModel.ZoomOutCommand);
        }

        // Otherwise WinUI shows the page's accelerators (Esc, Ctrl+Shift+1, …) as a tooltip when hovering anywhere on the page.
        KeyboardAcceleratorPlacementMode = KeyboardAcceleratorPlacementMode.Hidden;
    }

    /// <summary>Gets the view model.</summary>
    public CoinListViewModel ViewModel { get; }

    private void AddAccelerator(VirtualKey key, VirtualKeyModifiers modifiers, System.Windows.Input.ICommand command, string? parameter = null)
    {
        var accelerator = new KeyboardAccelerator { Key = key, Modifiers = modifiers };
        accelerator.Invoked += (_, args) =>
        {
            if (command.CanExecute(parameter))
            {
                command.Execute(parameter);
            }

            args.Handled = true;
        };
        KeyboardAccelerators.Add(accelerator);
    }

    /// <inheritdoc/>
    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        // Same breakpoint as the AdaptiveTrigger of WideState in CoinsPage.xaml.
        var width = App.Current?.MainWindow?.Bounds.Width ?? 1280;
        ViewModel.Initialize(e.Parameter as CoinListRequest ?? new CoinListRequest(CoinListKind.All), width >= 1000);
    }

    /// <inheritdoc/>
    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        ViewModel.Dispose();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CoinListViewModel.TileZoom))
        {
            ApplyTileZoom();
        }
        else if (e.PropertyName is nameof(CoinListViewModel.SelectedItem) or nameof(CoinListViewModel.ViewMode))
        {
            ScrollToSelection();
        }
    }

    /// <summary>
    /// Scrolls the visible list to the selected coin. Deferred until the current layout pass is done,
    /// because right after navigation (e.g. a chosen search suggestion) or a view switch the list has
    /// not created its items yet and an immediate ScrollIntoView would do nothing.
    /// </summary>
    private void ScrollToSelection()
    {
        if (ViewModel.SelectedItem is not { } item)
        {
            return;
        }

        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            (ViewModel.ViewMode == CoinViewMode.Grid ? (ListViewBase)CoinGrid : CoinList).ScrollIntoView(item));
    }

    private void OnCoinListLoaded(object sender, RoutedEventArgs e) => ScrollToSelection();

    // ----- List columns: in a narrow list (small window or open details pane) the title would be cut
    // to a few characters. Then the type column and the country name (the flag stays) are hidden.
    // Column indexes match the item template and the header in CoinsPage.xaml.

    private const double CompactListWidth = 820;
    private const int CountryColumn = 1;
    private const int TypeColumn = 3;
    private bool isCompactList;

    private void OnCoinListSizeChanged(object sender, SizeChangedEventArgs e)
    {
        var compact = e.NewSize.Width < CompactListWidth;
        if (compact == isCompactList) // The XAML starts with the wide columns.
        {
            return;
        }

        isCompactList = compact;
        ApplyListColumns(ListHeader);
        foreach (var container in CoinList.ItemsPanelRoot?.Children.OfType<SelectorItem>() ?? [])
        {
            if (container.ContentTemplateRoot is Grid row)
            {
                ApplyListColumns(row);
            }
        }
    }

    private void OnCoinListContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.ItemContainer.ContentTemplateRoot is Grid row)
        {
            ApplyListColumns(row);
        }
    }

    private void ApplyListColumns(Grid row)
    {
        row.ColumnDefinitions[CountryColumn].Width = new GridLength(isCompactList ? 24 : 140);
        row.ColumnDefinitions[TypeColumn].Width = new GridLength(isCompactList ? 0 : 110);
        foreach (var child in row.Children.OfType<FrameworkElement>())
        {
            switch (Grid.GetColumn(child))
            {
                // The header shows the heading "Country"; a row shows flag and name.
                case CountryColumn when child is TextBlock:
                    child.Visibility = isCompactList ? Visibility.Collapsed : Visibility.Visible;
                    break;
                case CountryColumn when child is Panel { Children.Count: > 1 } panel:
                    panel.Children[1].Visibility = isCompactList ? Visibility.Collapsed : Visibility.Visible;
                    break;
                case TypeColumn:
                    child.Visibility = isCompactList ? Visibility.Collapsed : Visibility.Visible;
                    break;
            }
        }
    }

    // ----- Tile zoom: pinch and Ctrl+mouse wheel ----------------------------------------------
    // The grid's ScrollViewer recognizes both gestures. Instead of scaling the picture, its zoom
    // factor is turned into a new tile size and reset, so the tiles reflow into more or fewer columns.

    private void OnCoinGridLoaded(object sender, RoutedEventArgs e)
    {
        if (gridScrollViewer is null && FindDescendant<ScrollViewer>(CoinGrid) is { } viewer)
        {
            gridScrollViewer = viewer;
            viewer.MinZoomFactor = 0.5f;
            viewer.MaxZoomFactor = 2f;
            viewer.ViewChanged += OnGridViewChanged;
        }
    }

    // The items panel is created after the grid is loaded (once items arrive); apply the remembered zoom then.
    private void OnItemsPanelLoaded(object sender, RoutedEventArgs e)
    {
        ApplyTileZoom();
        ScrollToSelection();
    }

    private void OnGridViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
    {
        if (gridScrollViewer is not { } viewer || e.IsIntermediate || Math.Abs(viewer.ZoomFactor - 1f) < 0.01f)
        {
            return;
        }

        var factor = viewer.ZoomFactor;
        viewer.ChangeView(null, null, 1f, disableAnimation: true);
        ViewModel.ChangeTileZoom(factor);
    }

    private void ApplyTileZoom()
    {
        if (CoinGrid.ItemsPanelRoot is ItemsWrapGrid panel)
        {
            panel.ItemWidth = TileWidth * ViewModel.TileZoom;
            panel.ItemHeight = TileHeight * ViewModel.TileZoom;
        }
    }

    // ----- Ownership, sort and view menus: show the view model's current choice when opened -------

    private void OnViewMenuOpening(object sender, object e) =>
        CheckCurrent((MenuFlyout)sender, ViewModel.ViewMode.ToString());

    private void OnOwnershipMenuOpening(object sender, object e) =>
        CheckCurrent((MenuFlyout)sender, ViewModel.Ownership.ToString());

    private void OnSortMenuOpening(object sender, object e) =>
        CheckCurrent((MenuFlyout)sender, ViewModel.SortOrder.ToString());

    private static void CheckCurrent(MenuFlyout menu, string current)
    {
        foreach (var item in menu.Items.OfType<RadioMenuFlyoutItem>())
        {
            item.IsChecked = (string)item.CommandParameter == current;
        }
    }

    // ----- Keyboard: Space toggles ownership, + increases the quantity --------------------------
    // PreviewKeyDown: the list itself handles Space (selection) before KeyDown would bubble up.
    // Ctrl +/- (zoom) are page accelerators, so they are left alone here.

    private void OnListKeyDown(object sender, KeyRoutedEventArgs e)
    {
        var control = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        switch (e.Key)
        {
            // Only on a list item: a focused badge button handles Space itself (it toggles its own coin).
            case VirtualKey.Space when e.OriginalSource is SelectorItem:
                ViewModel.ToggleSelectedOwnedCommand.Execute(null);
                e.Handled = true;
                break;
            case VirtualKey.Add or (VirtualKey)187 when !control:
                ViewModel.Details?.IncreaseQuantityCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }

    // ----- Context menu of a coin (right click, press and hold, Shift+F10, menu key) ---------------
    // Handled on the list instead of the item template: keyboard requests go to the focused list item,
    // so a flyout inside the template would only open with the mouse or touch.

    private void OnListContextRequested(UIElement sender, ContextRequestedEventArgs e)
    {
        // The source is the focused list item (keyboard) or an element of its template (mouse, touch).
        var item = e.OriginalSource switch
        {
            SelectorItem { Content: CoinItemViewModel focused } => focused,
            FrameworkElement { DataContext: CoinItemViewModel pointed } => pointed,
            _ => null,
        };
        if (item is null || ((ListViewBase)sender).ContainerFromItem(item) is not SelectorItem container)
        {
            return;
        }

        var menu = CreateCoinMenu(item);
        if (e.TryGetPosition(container, out var point))
        {
            menu.ShowAt(container, new FlyoutShowOptions { Position = point });
        }
        else
        {
            menu.ShowAt(container);
        }

        e.Handled = true;
    }

    private MenuFlyout CreateCoinMenu(CoinItemViewModel item)
    {
        static MenuFlyoutItem Entry(string text, string glyph, System.Windows.Input.ICommand command, string? key = null) => new()
        {
            Text = text,
            Icon = new FontIcon { Glyph = glyph },
            Command = command,
            KeyboardAcceleratorTextOverride = key ?? string.Empty,
        };

        var menu = new MenuFlyout();
        menu.Items.Add(Entry(item.ToggleOwnedLabel, "\uE73E", item.ToggleOwnedCommand, localizer.Get("Key_Space")));
        menu.Items.Add(Entry(localizer.Get("Coin_IncreaseQuantity"), "\uE710", item.IncreaseQuantityCommand, "+"));
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(Entry(localizer.Get("Coin_ShowDetails"), "\uE89F", item.ShowDetailsCommand));
        menu.Items.Add(Entry(localizer.Get("Coin_Enlarge"), "\uE8A3", item.EnlargeCommand));
        return menu;
    }

    // ----- Enlarged image ------------------------------------------------------------------------

    private void OnLightboxTapped(object sender, TappedRoutedEventArgs e) => ViewModel.CloseEnlargedCommand.Execute(null);

    private void OnLightboxSizeChanged(object sender, SizeChangedEventArgs e)
    {
        var side = Math.Max(120, Math.Min(540, Math.Min(Lightbox.ActualWidth, Lightbox.ActualHeight - 120) * 0.8));
        LightboxImage.Width = side;
        LightboxImage.Height = side;
        LightboxImage.CornerRadius = new CornerRadius(side / 2);
    }

    private static T? FindDescendant<T>(DependencyObject root)
        where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match)
            {
                return match;
            }

            if (FindDescendant<T>(child) is { } found)
            {
                return found;
            }
        }

        return null;
    }
}
