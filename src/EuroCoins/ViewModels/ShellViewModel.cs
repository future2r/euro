using System.Collections.ObjectModel;
using System.Globalization;
using EuroCoins.Core.Collections;
using EuroCoins.Core.Models;
using EuroCoins.Core.Querying;
using EuroCoins.Services;
using Microsoft.UI.Xaml.Media;

namespace EuroCoins.ViewModels;

/// <summary>
/// A suggestion of the title bar search.
/// </summary>
/// <param name="Title">The coin title, or the "show all results" text.</param>
/// <param name="Meta">Country, year and ownership, or an empty string.</param>
/// <param name="Image">The coin image, or <see langword="null"/> for "show all results".</param>
/// <param name="CoinId">The coin ID, or <see langword="null"/> for "show all results".</param>
public sealed record SearchSuggestion(string Title, string Meta, ImageSource? Image, string? CoinId)
{
    /// <summary>Gets a value indicating whether this is a coin (not "show all results").</summary>
    public bool IsCoin => CoinId is not null;

    /// <inheritdoc/>
    public override string ToString() => Title;
}

/// <summary>
/// The main window: title bar search and navigation.
/// </summary>
public sealed class ShellViewModel(Catalog catalog, CollectionState collection, Localizer localizer, NavigationService navigation)
{
    private const int MaxSuggestions = 6;

    /// <summary>Gets the current search suggestions.</summary>
    public ObservableCollection<SearchSuggestion> Suggestions { get; } = [];

    /// <summary>Gets the navigation service.</summary>
    public NavigationService Navigation { get; } = navigation;

    /// <summary>Updates the suggestions for the typed text.</summary>
    public void UpdateSuggestions(string text)
    {
        Suggestions.Clear();
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var culture = localizer.Culture;
        foreach (var coin in catalog.Coins.Where(c => CoinSearch.Matches(c, catalog, text, culture)).Take(MaxSuggestions))
        {
            var state = localizer.Get(collection.Get(coin.Id).IsOwned ? "Coin_Owned" : "Coin_Missing");
            var meta = $"{catalog.GetCountry(coin.CountryCode).Name.Get(culture)} · {coin.Year.ToString(CultureInfo.InvariantCulture)} · {state}";
            Suggestions.Add(new SearchSuggestion(coin.Title.Get(culture), meta, ImageService.CoinImage(coin, 64), coin.Id));
        }

        Suggestions.Add(new SearchSuggestion(localizer.Format("Search_ShowAll", text.Trim()), string.Empty, null, null));
    }

    /// <summary>Opens a chosen suggestion or the search results page.</summary>
    public void Submit(string text, SearchSuggestion? chosen)
    {
        if (chosen?.CoinId is { } id)
        {
            Navigation.NavigateToCoins(new CoinListRequest(CoinListKind.All, SelectCoinId: id));
        }
        else if (!string.IsNullOrWhiteSpace(text))
        {
            Navigation.NavigateToCoins(new CoinListRequest(CoinListKind.Search, SearchText: text.Trim()));
        }
    }
}
