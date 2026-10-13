using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EuroCoins.Core.Collections;
using EuroCoins.Core.Formatting;
using EuroCoins.Core.Models;
using EuroCoins.Core.Querying;
using EuroCoins.Core.Settings;
using EuroCoins.Core.Statistics;
using EuroCoins.Services;

namespace EuroCoins.ViewModels;

/// <summary>
/// The coins page: a filtered, sorted and grouped coin list with a details pane.
/// </summary>
public sealed partial class CoinListViewModel : ObservableObject, ICoinItemHost, IDisposable
{
    private const double ZoomStep = 1.1;
    private readonly Catalog catalog;
    private readonly CollectionState collection;
    private readonly Localizer localizer;
    private readonly ImageService images;
    private readonly SettingsService settings;
    private readonly Dictionary<string, CoinItemViewModel> items = new(StringComparer.Ordinal);
    private readonly IReadOnlyList<FilterOptionViewModel> allOptions;
    private CoinListRequest request = new(CoinListKind.All);

    /// <summary>Creates the view model.</summary>
    public CoinListViewModel(Catalog catalog, CollectionState collection, Localizer localizer, ImageService images, SettingsService settings)
    {
        this.catalog = catalog;
        this.collection = collection;
        this.localizer = localizer;
        this.images = images;
        this.settings = settings;

        var culture = localizer.Culture;
        CountryOptions = [.. catalog.Countries
            .OrderBy(c => c.Name.Get(culture), StringComparer.Create(culture, true))
            .Select(c => new FilterOptionViewModel(
                c.Code,
                c.Name.Get(culture),
                catalog.Coins.Count(coin => coin.CountryCode == c.Code).ToString(CultureInfo.CurrentCulture),
                images.Flag(c.Code),
                Refresh))];
        DenominationOptions = [.. DenominationFormatter.All.Reverse()
            .Select(d => new FilterOptionViewModel(d.ToString(CultureInfo.InvariantCulture), DenominationFormatter.ShortLabel(d, culture), string.Empty, null, Refresh))];
        YearOptions = [.. catalog.Coins.Select(c => c.Year).Distinct().OrderDescending()
            .Select(y => new FilterOptionViewModel(y.ToString(CultureInfo.InvariantCulture), y.ToString(CultureInfo.InvariantCulture), string.Empty, null, Refresh))];
        allOptions = [.. CountryOptions, .. DenominationOptions, .. YearOptions];

        TileZoom = settings.Current.TileZoom;
        collection.EntryChanged += OnEntryChanged;
        collection.Reset += OnCollectionReset;
    }

    /// <summary>Gets the page title.</summary>
    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    /// <summary>Gets the number of listed coins as text.</summary>
    [ObservableProperty]
    public partial string CountText { get; set; } = string.Empty;

    /// <summary>Gets a value indicating whether the progress line is shown (not for search results).</summary>
    [ObservableProperty]
    public partial bool ShowProgress { get; set; } = true;

    /// <summary>Gets the progress text, e.g. "312 of 804 coins owned · 39%".</summary>
    [ObservableProperty]
    public partial string ProgressText { get; set; } = string.Empty;

    /// <summary>Gets the progress in percent.</summary>
    [ObservableProperty]
    public partial double ProgressValue { get; set; }

    /// <summary>Gets the coin groups.</summary>
    public ObservableCollection<CoinGroupViewModel> Groups { get; } = [];

    /// <summary>Gets a value indicating whether no coin matches.</summary>
    [ObservableProperty]
    public partial bool IsEmpty { get; set; }

    /// <summary>Gets or sets the selected list item (bound to the list control).</summary>
    [ObservableProperty]
    public partial object? SelectedItem { get; set; }

    /// <summary>Gets the coin shown in the details pane.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDetails))]
    public partial CoinItemViewModel? Details { get; set; }

    /// <summary>Gets a value indicating whether a coin is shown in the details pane.</summary>
    public bool HasDetails => Details is not null;

    /// <summary>Gets or sets whether the details pane is open.</summary>
    [ObservableProperty]
    public partial bool IsDetailsPaneOpen { get; set; } = true;

    /// <summary>Gets or sets the view mode (grid or list).</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ZoomInCommand), nameof(ZoomOutCommand))]
    public partial CoinViewMode ViewMode { get; set; }

    /// <summary>Gets or sets the sort order (and grouping).</summary>
    [ObservableProperty]
    public partial CoinSortOrder SortOrder { get; set; } = CoinSortOrder.Country;

    /// <summary>Gets or sets the ownership filter.</summary>
    [ObservableProperty]
    public partial OwnershipFilter Ownership { get; set; } = OwnershipFilter.All;

    /// <summary>Gets the tile zoom factor (1 = default size).</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ZoomInCommand), nameof(ZoomOutCommand))]
    public partial double TileZoom { get; set; } = 1.0;

    /// <summary>Gets the coin shown enlarged, or <see langword="null"/>.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEnlarged))]
    public partial CoinItemViewModel? EnlargedItem { get; set; }

    /// <summary>Gets a value indicating whether a coin is shown enlarged.</summary>
    /// <remarks>Bound instead of <see cref="EnlargedItem"/>: x:Bind does not re-evaluate a function
    /// binding when its argument becomes <see langword="null"/>, so the overlay would never hide.</remarks>
    public bool IsEnlarged => EnlargedItem is not null;

    /// <summary>Gets the label of the country filter button.</summary>
    [ObservableProperty]
    public partial string CountryFilterLabel { get; set; } = string.Empty;

    /// <summary>Gets the label of the face value filter button.</summary>
    [ObservableProperty]
    public partial string DenominationFilterLabel { get; set; } = string.Empty;

    /// <summary>Gets the label of the year filter button.</summary>
    [ObservableProperty]
    public partial string YearFilterLabel { get; set; } = string.Empty;

    /// <summary>Gets the label of the ownership filter button.</summary>
    [ObservableProperty]
    public partial string OwnershipFilterLabel { get; set; } = string.Empty;

    /// <summary>Gets the label of the sort button.</summary>
    [ObservableProperty]
    public partial string SortLabel { get; set; } = string.Empty;

    /// <summary>Gets the number of selected countries as text.</summary>
    [ObservableProperty]
    public partial string SelectedCountriesText { get; set; } = string.Empty;

    /// <summary>Gets a value indicating whether any filter of the filter bar is active.</summary>
    [ObservableProperty]
    public partial bool HasActiveFilters { get; set; }

    /// <summary>Gets the country options.</summary>
    public IReadOnlyList<FilterOptionViewModel> CountryOptions { get; }

    /// <summary>Gets the face value options.</summary>
    public IReadOnlyList<FilterOptionViewModel> DenominationOptions { get; }

    /// <summary>Gets the year options.</summary>
    public IReadOnlyList<FilterOptionViewModel> YearOptions { get; }

    /// <summary>Sets up the list for a navigation request.</summary>
    public void Initialize(CoinListRequest listRequest, bool isWide)
    {
        request = listRequest;
        Title = localizer.Get(request.Kind switch
        {
            CoinListKind.Circulation => "Nav_Circulation",
            CoinListKind.Commemorative => "Nav_Commemorative",
            CoinListKind.Search => "Coins_SearchResults",
            _ => "Nav_All",
        });
        ShowProgress = request.Kind != CoinListKind.Search;
        IsDetailsPaneOpen = isWide || request.SelectCoinId is not null;
        foreach (var option in CountryOptions)
        {
            option.SetSilently(request.Countries?.Contains(option.Key, StringComparer.OrdinalIgnoreCase) == true);
        }

        Refresh();
        if (request.SelectCoinId is { } id && items.TryGetValue(id, out var item))
        {
            SelectedItem = item;
        }
        else
        {
            SelectedItem = Groups is [{ Items: [var first, ..] }, ..] ? first : null;
        }
    }

    /// <inheritdoc/>
    public void ShowDetails(CoinItemViewModel item)
    {
        SelectedItem = item;
        IsDetailsPaneOpen = true;
    }

    /// <inheritdoc/>
    public void Enlarge(CoinItemViewModel item)
    {
        SelectedItem = item;
        EnlargedItem = item;
    }

    /// <summary>Multiplies the tile zoom (pinch, Ctrl+mouse wheel, View menu, Ctrl+Plus/Minus), clamped to the allowed range, and remembers it.</summary>
    public void ChangeTileZoom(double factor)
    {
        var zoom = Math.Clamp(TileZoom * factor, AppSettings.MinTileZoom, AppSettings.MaxTileZoom);
        if (Math.Abs(zoom - TileZoom) < 0.001)
        {
            return;
        }

        TileZoom = zoom;
        settings.Update(s => s with { TileZoom = zoom });
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        collection.EntryChanged -= OnEntryChanged;
        collection.Reset -= OnCollectionReset;
    }

    [RelayCommand]
    private void ToggleSelectedOwned() => Details?.ToggleOwnedCommand.Execute(null);

    [RelayCommand]
    private void CloseEnlarged() => EnlargedItem = null;

    [RelayCommand]
    private void ToggleDetailsPane() => IsDetailsPaneOpen = !IsDetailsPaneOpen;

    [RelayCommand]
    private void SetSortOrder(string order)
    {
        SortOrder = Enum.Parse<CoinSortOrder>(order);
    }

    [RelayCommand]
    private void SetViewMode(string viewMode)
    {
        ViewMode = Enum.Parse<CoinViewMode>(viewMode);
    }

    [RelayCommand(CanExecute = nameof(CanZoomIn))]
    private void ZoomIn() => ChangeTileZoom(ZoomStep);

    private bool CanZoomIn() => ViewMode == CoinViewMode.Grid && TileZoom < AppSettings.MaxTileZoom - 0.001;

    [RelayCommand(CanExecute = nameof(CanZoomOut))]
    private void ZoomOut() => ChangeTileZoom(1 / ZoomStep);

    private bool CanZoomOut() => ViewMode == CoinViewMode.Grid && TileZoom > AppSettings.MinTileZoom + 0.001;

    [RelayCommand]
    private void SetOwnership(string ownership)
    {
        Ownership = Enum.Parse<OwnershipFilter>(ownership);
    }

    [RelayCommand]
    private void ClearCountries()
    {
        ClearSilently(CountryOptions);
        Refresh();
    }

    [RelayCommand]
    private void ResetFilters()
    {
        ClearSilently(allOptions);
        if (Ownership == OwnershipFilter.All)
        {
            Refresh();
        }
        else
        {
            Ownership = OwnershipFilter.All; // Refreshes.
        }
    }

    private static void ClearSilently(IEnumerable<FilterOptionViewModel> options)
    {
        foreach (var option in options)
        {
            option.SetSilently(false);
        }
    }

    partial void OnSelectedItemChanged(object? value)
    {
        if (value is CoinItemViewModel item)
        {
            Details = item;
        }
    }

    partial void OnSortOrderChanged(CoinSortOrder value) => Refresh();

    partial void OnOwnershipChanged(OwnershipFilter value) => Refresh();

    private void OnEntryChanged(object? sender, string coinId)
    {
        if (items.TryGetValue(coinId, out var item))
        {
            item.Refresh();
        }

        // Ownership filters depend on the changed state; otherwise only the figures change.
        if (Ownership != OwnershipFilter.All)
        {
            Refresh();
        }
        else
        {
            UpdateProgress();
        }
    }

    private void OnCollectionReset(object? sender, EventArgs e)
    {
        foreach (var item in items.Values)
        {
            item.Refresh();
        }

        Refresh();
    }

    // The coin type of the list (all types for "all coins" and search results).
    private CoinType? RequestedType => request.Kind switch
    {
        CoinListKind.Circulation => CoinType.Circulation,
        CoinListKind.Commemorative => CoinType.Commemorative,
        _ => null,
    };

    private CoinQuery BuildQuery() => new()
    {
        Type = RequestedType,
        SearchText = request.Kind == CoinListKind.Search ? request.SearchText : null,
        Countries = CountryOptions.Where(o => o.IsSelected).Select(o => o.Key).ToHashSet(StringComparer.OrdinalIgnoreCase),
        Denominations = DenominationOptions.Where(o => o.IsSelected).Select(o => int.Parse(o.Key, CultureInfo.InvariantCulture)).ToHashSet(),
        Years = YearOptions.Where(o => o.IsSelected).Select(o => int.Parse(o.Key, CultureInfo.InvariantCulture)).ToHashSet(),
        Ownership = Ownership,
    };

    private void Refresh()
    {
        var culture = localizer.Culture;
        var coins = BuildQuery().Apply(catalog, collection, culture).ToList();
        var groups = CoinGrouping.Group(coins, catalog, SortOrder, culture);

        var previous = SelectedItem;
        Groups.Clear();
        foreach (var group in groups)
        {
            Groups.Add(new CoinGroupViewModel(
                group.Title,
                group.CountryCode is { } code ? images.Flag(code) : null,
                CoinCount(group.Coins.Count),
                [.. group.Coins.Select(GetItem)]));
        }

        IsEmpty = coins.Count == 0;
        if (previous is CoinItemViewModel item && coins.Any(c => c.Id == item.Id))
        {
            SelectedItem = item;
        }

        UpdateTexts(coins.Count);
        UpdateProgress();
    }

    private CoinItemViewModel GetItem(Coin coin)
    {
        if (!items.TryGetValue(coin.Id, out var item))
        {
            item = new CoinItemViewModel(coin, catalog, collection, localizer, images, this);
            items[coin.Id] = item;
        }

        return item;
    }

    private string CoinCount(int count) => localizer.Format(count == 1 ? "Coins_CountOne" : "Coins_CountMany", count);

    private void UpdateTexts(int count)
    {
        CountText = request.Kind == CoinListKind.Search
            ? localizer.Format("Coins_SearchCount", request.SearchText, CoinCount(count))
            : CoinCount(count);

        CountryFilterLabel = Label("Filter_Country", CountryOptions);
        SelectedCountriesText = localizer.Format("Filter_SelectedCount", CountryOptions.Count(o => o.IsSelected));
        DenominationFilterLabel = Label("Filter_Denomination", DenominationOptions);
        YearFilterLabel = Label("Filter_Year", YearOptions);
        OwnershipFilterLabel = localizer.Get(Ownership switch
        {
            OwnershipFilter.Owned => "Filter_OwnedOnly",
            OwnershipFilter.Missing => "Filter_MissingOnly",
            _ => "Filter_Ownership",
        });
        SortLabel = localizer.Get(SortOrder switch
        {
            CoinSortOrder.Year => "Sort_ByYear",
            CoinSortOrder.Denomination => "Sort_ByDenomination",
            _ => "Sort_ByCountry",
        });
        HasActiveFilters = Ownership != OwnershipFilter.All || allOptions.Any(o => o.IsSelected);
    }

    private string Label(string key, IReadOnlyList<FilterOptionViewModel> options)
    {
        var selected = options.Where(o => o.IsSelected).ToList();
        return selected.Count switch
        {
            0 => localizer.Get(key),
            1 => $"{localizer.Get(key)}: {selected[0].Label}",
            _ => $"{localizer.Get(key)}: {selected[0].Label} +{selected.Count - 1}",
        };
    }

    private void UpdateProgress()
    {
        var type = RequestedType;
        var progress = CollectionStatistics.For(catalog.Coins.Where(c => type is null || c.Type == type), collection);
        ProgressValue = progress.Fraction * 100;
        ProgressText = localizer.Format("Coins_Progress", progress.Owned, progress.Total, progress.Fraction);
    }
}
