using System.Globalization;
using EuroCoins.Core.Collections;
using EuroCoins.Core.Formatting;
using EuroCoins.Core.Models;

namespace EuroCoins.Core.Querying;

/// <summary>
/// Which coins to show regarding ownership.
/// </summary>
public enum OwnershipFilter
{
    /// <summary>All coins.</summary>
    All,

    /// <summary>Only owned coins.</summary>
    Owned,

    /// <summary>Only missing coins.</summary>
    Missing,
}

/// <summary>
/// The filter criteria of a coin list. Empty sets mean "no restriction".
/// </summary>
public sealed record CoinQuery
{
    /// <summary>Gets the coin type, or <see langword="null"/> for all types.</summary>
    public CoinType? Type { get; init; }

    /// <summary>Gets the country codes to include.</summary>
    public IReadOnlySet<string> Countries { get; init; } = new HashSet<string>();

    /// <summary>Gets the ownership filter.</summary>
    public OwnershipFilter Ownership { get; init; } = OwnershipFilter.All;

    /// <summary>Gets the face values (in cents) to include.</summary>
    public IReadOnlySet<int> Denominations { get; init; } = new HashSet<int>();

    /// <summary>Gets the years to include.</summary>
    public IReadOnlySet<int> Years { get; init; } = new HashSet<int>();

    /// <summary>Gets the full-text search, or <see langword="null"/>.</summary>
    public string? SearchText { get; init; }

    /// <summary>
    /// Returns the coins that match all criteria, in catalog order.
    /// </summary>
    /// <param name="catalog">The catalog.</param>
    /// <param name="collection">The user's collection.</param>
    /// <param name="culture">The UI culture (for the full-text search).</param>
    /// <returns>The matching coins.</returns>
    public IEnumerable<Coin> Apply(Catalog catalog, CollectionState collection, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(collection);
        return catalog.Coins.Where(coin =>
            (Type is null || coin.Type == Type)
            && (Countries.Count == 0 || Countries.Contains(coin.CountryCode))
            && (Denominations.Count == 0 || Denominations.Contains(coin.Denomination))
            && (Years.Count == 0 || Years.Contains(coin.Year))
            && Ownership switch
            {
                OwnershipFilter.Owned => collection.Get(coin.Id).IsOwned,
                OwnershipFilter.Missing => !collection.Get(coin.Id).IsOwned,
                _ => true,
            }
            && (string.IsNullOrWhiteSpace(SearchText) || CoinSearch.Matches(coin, catalog, SearchText, culture)));
    }
}

/// <summary>
/// Full-text search over title, series, country, year and face value.
/// </summary>
public static class CoinSearch
{
    /// <summary>
    /// Returns whether a coin matches the search text. Every word of the text must occur
    /// (case- and accent-insensitive) in the coin's searchable text.
    /// </summary>
    /// <param name="coin">The coin.</param>
    /// <param name="catalog">The catalog (for country names).</param>
    /// <param name="text">The search text.</param>
    /// <param name="culture">The UI culture.</param>
    /// <returns><see langword="true"/> if all words match.</returns>
    public static bool Matches(Coin coin, Catalog catalog, string text, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(coin);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(culture);

        var country = catalog.GetCountry(coin.CountryCode);
        var haystack = string.Join(' ',
            coin.Title.Get(culture),
            coin.Series.Get(culture),
            country.Name.Get(culture),
            coin.CountryCode,
            coin.Year.ToString(CultureInfo.InvariantCulture),
            DenominationFormatter.ShortLabel(coin.Denomination, culture),
            DenominationFormatter.LongLabel(coin.Denomination, culture));
        var compare = culture.CompareInfo;
        const CompareOptions options = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;
        return text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .All(word => compare.IndexOf(haystack, word, options) >= 0);
    }
}
