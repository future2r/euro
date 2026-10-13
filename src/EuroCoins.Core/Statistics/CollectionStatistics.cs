using EuroCoins.Core.Collections;
using EuroCoins.Core.Models;

namespace EuroCoins.Core.Statistics;

/// <summary>
/// How many coins of a set are owned.
/// </summary>
/// <param name="Owned">The number of owned coins.</param>
/// <param name="Total">The number of coins in the set.</param>
public sealed record Progress(int Owned, int Total)
{
    /// <summary>Gets the owned share between 0 and 1.</summary>
    public double Fraction => Total == 0 ? 0 : (double)Owned / Total;
}

/// <summary>
/// The collection progress of one country.
/// </summary>
/// <param name="Country">The country.</param>
/// <param name="Progress">The progress.</param>
public sealed record CountryProgress(Country Country, Progress Progress);

/// <summary>
/// A coin that was added to the collection.
/// </summary>
/// <param name="Coin">The coin.</param>
/// <param name="Added">When it was added.</param>
public sealed record AddedCoin(Coin Coin, DateTimeOffset Added);

/// <summary>
/// Computes collection progress figures.
/// </summary>
public static class CollectionStatistics
{
    /// <summary>
    /// Returns the progress over the given coins.
    /// </summary>
    /// <param name="coins">The coins.</param>
    /// <param name="collection">The collection.</param>
    /// <returns>The progress.</returns>
    public static Progress For(IEnumerable<Coin> coins, CollectionState collection)
    {
        ArgumentNullException.ThrowIfNull(coins);
        ArgumentNullException.ThrowIfNull(collection);
        var total = 0;
        var owned = 0;
        foreach (var coin in coins)
        {
            total++;
            if (collection.Get(coin.Id).IsOwned)
            {
                owned++;
            }
        }

        return new Progress(owned, total);
    }

    /// <summary>
    /// Returns the progress of every country, best first.
    /// </summary>
    /// <param name="catalog">The catalog.</param>
    /// <param name="collection">The collection.</param>
    /// <returns>One entry per country.</returns>
    public static IReadOnlyList<CountryProgress> ByCountry(Catalog catalog, CollectionState collection)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var byCountry = catalog.Coins.ToLookup(c => c.CountryCode, StringComparer.OrdinalIgnoreCase);
        return catalog.Countries
            .Select(country => new CountryProgress(country, For(byCountry[country.Code], collection)))
            .OrderByDescending(p => p.Progress.Fraction)
            .ThenByDescending(p => p.Progress.Total)
            .ToList();
    }

    /// <summary>
    /// Returns the most recently added coins.
    /// </summary>
    /// <param name="catalog">The catalog.</param>
    /// <param name="collection">The collection.</param>
    /// <param name="count">The maximum number of coins.</param>
    /// <returns>The coins, newest first.</returns>
    public static IReadOnlyList<AddedCoin> RecentlyAdded(Catalog catalog, CollectionState collection, int count)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(collection);
        return collection.Entries
            .Where(e => e.Value.Added is not null) // Set only while the coin is owned.
            .Select(e => catalog.TryGetCoin(e.Key, out var coin) ? new AddedCoin(coin, e.Value.Added!.Value) : null)
            .OfType<AddedCoin>()
            .OrderByDescending(a => a.Added)
            .ThenBy(a => a.Coin.Id, StringComparer.Ordinal)
            .Take(count)
            .ToList();
    }
}
