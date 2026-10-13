using System.Globalization;
using System.Text.RegularExpressions;
using EuroCoins.Core.Catalogs;
using EuroCoins.Core.Models;

namespace EuroCoins.CatalogTool;

/// <summary>
/// Gives commemorative coins stable IDs across catalog updates, e.g. <c>LU-CC-2022-01</c> (country,
/// year, number). Users' collections reference coins by ID, so a known coin must keep its ID and an
/// ID must never move to another coin.
/// </summary>
/// <remarks>
/// The ECB reuses image file names within a year (e.g. <c>comm_2025/Monaco.jpg</c> first showed one
/// coin and later another), so a coin is recognized by country, year and title first. Only if the
/// title changed, the image address decides — and then only if the titles still share a significant
/// word or one of them is only the placeholder. A new coin gets the number after the highest one ever used for its country and year,
/// including the IDs of removed coins.
/// </remarks>
internal sealed partial class CommemorativeIdAssigner
{
    private readonly ILookup<(string Country, int Year), CoinDocument> previousByGroup;
    private readonly HashSet<string> takenIds;

    public CommemorativeIdAssigner(IEnumerable<CoinDocument> previousCoins, IEnumerable<string> retiredIds)
    {
        var previous = previousCoins.ToList();
        previousByGroup = previous.Where(c => c.Type == CoinType.Commemorative).ToLookup(c => (c.Country, c.Year));
        takenIds = previous.Select(c => c.Id).Concat(retiredIds).ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>Returns one ID per entry (same order) for the coins of one country and year.</summary>
    public IReadOnlyList<string> Assign(string country, int year, IReadOnlyList<CommemorativeEntry> entries)
    {
        var ids = new string?[entries.Count];
        var unclaimed = previousByGroup[(country, year)].ToList();

        void Claim(int index, CoinDocument coin)
        {
            ids[index] = coin.Id;
            unclaimed.Remove(coin);
        }

        // 1. Same title (placeholder titles of coins whose title was not found do not count).
        for (var i = 0; i < entries.Count; i++)
        {
            var title = entries[i].TitleDe;
            if (!IsPlaceholder(title) && unclaimed.FirstOrDefault(c => Key(c.Title.De) == Key(title)) is { } match)
            {
                Claim(i, match);
            }
        }

        // 2. Title changed or unknown: same image address and a common significant word (unless one
        //    of the titles is only the placeholder).
        for (var i = 0; i < entries.Count; i++)
        {
            if (ids[i] is not null)
            {
                continue;
            }

            var entry = entries[i];
            var match = unclaimed.FirstOrDefault(c =>
                c.ImageUrl == entry.Image
                && (IsPlaceholder(c.Title.De) || IsPlaceholder(entry.TitleDe) || ShareSignificantWord(c.Title.De, entry.TitleDe)));
            if (match is not null)
            {
                Claim(i, match);
            }
        }

        // 3. New coins get the next number that was never used for this country and year.
        var prefix = string.Create(CultureInfo.InvariantCulture, $"{country}-CC-{year}-");
        var number = takenIds
            .Where(id => id.StartsWith(prefix, StringComparison.Ordinal))
            .Select(id => int.TryParse(id.AsSpan(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : 0)
            .DefaultIfEmpty(0)
            .Max();
        for (var i = 0; i < entries.Count; i++)
        {
            if (ids[i] is null)
            {
                var id = prefix + (++number).ToString("00", CultureInfo.InvariantCulture);
                takenIds.Add(id);
                ids[i] = id;
            }
        }

        return ids!;
    }

    private static string Key(string? title) => EcbPageParser.Normalize(title ?? string.Empty).ToUpperInvariant();

    /// <summary>Whether a title is missing or only the generic name used when the ECB page had none.</summary>
    private static bool IsPlaceholder(string? title) =>
        string.IsNullOrWhiteSpace(title) || Key(title) == Key(CatalogUpdater.CommemorativeSeries.De);

    private static bool ShareSignificantWord(string? a, string? b)
    {
        static HashSet<string> Words(string? text) => [.. Word().Matches(text ?? string.Empty).Select(m => m.Value.ToUpperInvariant())];
        return Words(a).Overlaps(Words(b));
    }

    [GeneratedRegex(@"\p{L}{4,}")]
    private static partial Regex Word();
}
