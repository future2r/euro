using System.Globalization;
using EuroCoins.Core.Formatting;
using EuroCoins.Core.Models;

namespace EuroCoins.Core.Querying;

/// <summary>
/// The sort order of a coin list; the list is grouped by the same key.
/// </summary>
public enum CoinSortOrder
{
    /// <summary>By country name (default).</summary>
    Country,

    /// <summary>By year of issue, newest first.</summary>
    Year,

    /// <summary>By face value, largest first.</summary>
    Denomination,
}

/// <summary>
/// A group of coins in a list.
/// </summary>
/// <param name="Title">The localized heading.</param>
/// <param name="CountryCode">The country code when grouped by country, otherwise <see langword="null"/>.</param>
/// <param name="Coins">The coins of the group, sorted.</param>
public sealed record CoinGroup(string Title, string? CountryCode, IReadOnlyList<Coin> Coins);

/// <summary>
/// Sorts and groups coins.
/// </summary>
public static class CoinGrouping
{
    /// <summary>
    /// Sorts the coins and groups them by the sort key.
    /// </summary>
    /// <param name="coins">The coins.</param>
    /// <param name="catalog">The catalog (for country names).</param>
    /// <param name="order">The sort order.</param>
    /// <param name="culture">The UI culture.</param>
    /// <returns>The groups in display order.</returns>
    public static IReadOnlyList<CoinGroup> Group(IEnumerable<Coin> coins, Catalog catalog, CoinSortOrder order, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(coins);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(culture);

        var names = StringComparer.Create(culture, ignoreCase: true);
        string CountryName(string code) => catalog.GetCountry(code).Name.Get(culture);

        return order switch
        {
            CoinSortOrder.Year => coins
                .GroupBy(c => c.Year)
                .OrderByDescending(g => g.Key)
                .Select(g => new CoinGroup(
                    g.Key.ToString(CultureInfo.InvariantCulture),
                    null,
                    [.. g.OrderBy(c => CountryName(c.CountryCode), names).ThenBy(c => c.Type).ThenBy(c => c.Denomination).ThenBy(c => c.Id, StringComparer.Ordinal)]))
                .ToList(),

            CoinSortOrder.Denomination => coins
                .GroupBy(c => c.Denomination)
                .OrderByDescending(g => g.Key)
                .Select(g => new CoinGroup(
                    DenominationFormatter.LongLabel(g.Key, culture),
                    null,
                    [.. g.OrderBy(c => CountryName(c.CountryCode), names).ThenByDescending(c => c.Year).ThenBy(c => c.Id, StringComparer.Ordinal)]))
                .ToList(),

            // Within a country: circulation coins by series and face value, then commemoratives newest first.
            _ => coins
                .GroupBy(c => c.CountryCode)
                .Select(g => new CoinGroup(
                    CountryName(g.Key),
                    g.Key,
                    [.. g.OrderBy(c => c.Type)
                        .ThenBy(c => c.Type == CoinType.Circulation ? c.Year : -c.Year)
                        .ThenBy(c => c.Denomination)
                        .ThenBy(c => c.Id, StringComparer.Ordinal)]))
                .OrderBy(g => g.Title, names)
                .ToList(),
        };
    }
}
