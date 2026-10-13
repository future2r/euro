using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EuroCoins.Core.Collections;
using EuroCoins.Core.Models;
using EuroCoins.Core.Statistics;
using EuroCoins.Services;
using Microsoft.UI.Xaml.Media;

namespace EuroCoins.ViewModels;

/// <summary>
/// A progress card on the overview page.
/// </summary>
public sealed partial class StatCardViewModel(string label, Progress progress, Localizer localizer, Action open)
{
    /// <summary>Gets the card title.</summary>
    public string Label { get; } = label;

    /// <summary>Gets the progress in percent (0-100).</summary>
    public double Value { get; } = progress.Fraction * 100;

    /// <summary>Gets the percentage as text.</summary>
    public string PercentText { get; } = progress.Fraction.ToString("P0", CultureInfo.CurrentCulture);

    /// <summary>Gets the detail line, e.g. "312 / 804 coins".</summary>
    public string Detail { get; } = localizer.Format("Overview_OwnedOfTotal", progress.Owned, progress.Total);

    /// <summary>Gets the name announced by screen readers.</summary>
    public string AccessibleName => $"{Label}: {PercentText}, {Detail}";

    [RelayCommand]
    private void Open() => open();
}

/// <summary>
/// The progress of one country on the overview page.
/// </summary>
public sealed partial class CountryProgressViewModel(string name, ImageSource flag, Progress progress, Action open)
{
    /// <summary>Gets the country name.</summary>
    public string Name { get; } = name;

    /// <summary>Gets the country flag.</summary>
    public ImageSource Flag { get; } = flag;

    /// <summary>Gets the progress in percent.</summary>
    public double Value { get; } = progress.Fraction * 100;

    /// <summary>Gets the progress as "owned / total".</summary>
    public string Label { get; } = string.Create(CultureInfo.CurrentCulture, $"{progress.Owned} / {progress.Total}");

    /// <summary>Gets the name announced by screen readers.</summary>
    public string AccessibleName => $"{Name}: {Label}";

    [RelayCommand]
    private void Open() => open();
}

/// <summary>
/// A recently added coin on the overview page.
/// </summary>
public sealed partial class RecentCoinViewModel(string title, string meta, ImageSource? image, Action open)
{
    /// <summary>Gets the coin title.</summary>
    public string Title { get; } = title;

    /// <summary>Gets the country and date.</summary>
    public string Meta { get; } = meta;

    /// <summary>Gets the name announced by screen readers.</summary>
    public string AccessibleName => $"{Title}, {Meta}";

    /// <summary>Gets the coin image.</summary>
    public ImageSource? Image { get; } = image;

    [RelayCommand]
    private void Open() => open();
}

/// <summary>
/// The overview (start) page: overall progress, progress by country and recently added coins.
/// </summary>
public sealed partial class OverviewViewModel : ObservableObject, IDisposable
{
    private const int RecentCount = 5;
    private readonly Catalog catalog;
    private readonly CollectionState collection;
    private readonly Localizer localizer;
    private readonly ImageService images;
    private readonly NavigationService navigation;

    /// <summary>Creates the view model.</summary>
    public OverviewViewModel(Catalog catalog, CollectionState collection, Localizer localizer, ImageService images, NavigationService navigation)
    {
        this.catalog = catalog;
        this.collection = collection;
        this.localizer = localizer;
        this.images = images;
        this.navigation = navigation;
        collection.EntryChanged += OnCollectionChanged;
        collection.Reset += OnCollectionChanged;
        Refresh();
    }

    /// <summary>Gets the progress cards.</summary>
    public ObservableCollection<StatCardViewModel> Stats { get; } = [];

    /// <summary>Gets the progress of all countries, best first.</summary>
    public ObservableCollection<CountryProgressViewModel> Countries { get; } = [];

    /// <summary>Gets the most recently added coins.</summary>
    public ObservableCollection<RecentCoinViewModel> Recent { get; } = [];

    /// <summary>Gets a value indicating whether no coin was added yet.</summary>
    [ObservableProperty]
    public partial bool HasNoRecent { get; set; }

    /// <inheritdoc/>
    public void Dispose()
    {
        collection.EntryChanged -= OnCollectionChanged;
        collection.Reset -= OnCollectionChanged;
    }

    private void OnCollectionChanged(object? sender, object e) => Refresh();

    private void Refresh()
    {
        var culture = localizer.Culture;

        Stats.Clear();
        Stats.Add(new StatCardViewModel(localizer.Get("Overview_Total"), CollectionStatistics.For(catalog.Coins, collection), localizer,
            () => navigation.NavigateToCoins(new CoinListRequest(CoinListKind.All))));
        Stats.Add(new StatCardViewModel(localizer.Get("Nav_Circulation"), CollectionStatistics.For(catalog.Coins.Where(c => c.Type == CoinType.Circulation), collection), localizer,
            () => navigation.NavigateToCoins(new CoinListRequest(CoinListKind.Circulation))));
        Stats.Add(new StatCardViewModel(localizer.Get("Nav_Commemorative"), CollectionStatistics.For(catalog.Coins.Where(c => c.Type == CoinType.Commemorative), collection), localizer,
            () => navigation.NavigateToCoins(new CoinListRequest(CoinListKind.Commemorative))));

        Countries.Clear();
        foreach (var entry in CollectionStatistics.ByCountry(catalog, collection))
        {
            var code = entry.Country.Code;
            Countries.Add(new CountryProgressViewModel(entry.Country.Name.Get(culture), images.Flag(code), entry.Progress,
                () => navigation.NavigateToCoins(new CoinListRequest(CoinListKind.All, Countries: [code]))));
        }

        Recent.Clear();
        foreach (var added in CollectionStatistics.RecentlyAdded(catalog, collection, RecentCount))
        {
            var coin = added.Coin;
            var meta = $"{catalog.GetCountry(coin.CountryCode).Name.Get(culture)} · {added.Added.ToLocalTime().ToString("d", CultureInfo.CurrentCulture)}";
            Recent.Add(new RecentCoinViewModel(coin.Title.Get(culture), meta, ImageService.CoinImage(coin, 80),
                () => navigation.NavigateToCoins(new CoinListRequest(CoinListKind.All, SelectCoinId: coin.Id))));
        }

        HasNoRecent = Recent.Count == 0;
    }
}
