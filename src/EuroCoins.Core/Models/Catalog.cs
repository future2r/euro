using System.Collections.Frozen;

namespace EuroCoins.Core.Models;

/// <summary>
/// The complete coin catalog: all countries and all coins.
/// </summary>
public sealed class Catalog
{
    private readonly FrozenDictionary<string, Country> countriesByCode;
    private readonly FrozenDictionary<string, Coin> coinsById;

    /// <summary>
    /// Creates a catalog.
    /// </summary>
    /// <param name="countries">The issuing countries.</param>
    /// <param name="coins">The coins.</param>
    /// <exception cref="ArgumentException">A coin ID or country code is not unique, or a coin
    /// references an unknown country.</exception>
    public Catalog(IEnumerable<Country> countries, IEnumerable<Coin> coins)
    {
        ArgumentNullException.ThrowIfNull(countries);
        ArgumentNullException.ThrowIfNull(coins);

        Countries = [.. countries];
        Coins = [.. coins];
        // ToDictionary throws on duplicate keys; ToFrozenDictionary alone would keep the last one.
        countriesByCode = Countries.ToDictionary(c => c.Code, StringComparer.OrdinalIgnoreCase).ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
        coinsById = Coins.ToDictionary(c => c.Id, StringComparer.Ordinal).ToFrozenDictionary(StringComparer.Ordinal);

        var unknown = Coins.FirstOrDefault(c => !countriesByCode.ContainsKey(c.CountryCode));
        if (unknown is not null)
        {
            throw new ArgumentException($"Coin '{unknown.Id}' references unknown country '{unknown.CountryCode}'.", nameof(coins));
        }
    }

    /// <summary>
    /// Gets all issuing countries.
    /// </summary>
    public IReadOnlyList<Country> Countries { get; }

    /// <summary>
    /// Gets all coins.
    /// </summary>
    public IReadOnlyList<Coin> Coins { get; }

    /// <summary>
    /// Returns the country with the given code.
    /// </summary>
    /// <param name="code">The country code (case-insensitive).</param>
    /// <returns>The country.</returns>
    /// <exception cref="KeyNotFoundException">The country does not exist.</exception>
    public Country GetCountry(string code) => countriesByCode[code];

    /// <summary>
    /// Looks up a coin by its ID.
    /// </summary>
    /// <param name="id">The coin ID.</param>
    /// <param name="coin">The coin, if found.</param>
    /// <returns><see langword="true"/> if the coin exists.</returns>
    public bool TryGetCoin(string id, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out Coin? coin) =>
        coinsById.TryGetValue(id, out coin);
}
