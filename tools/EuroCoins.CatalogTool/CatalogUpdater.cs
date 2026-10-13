using System.Globalization;
using EuroCoins.Core.Catalogs;
using EuroCoins.Core.Formatting;
using EuroCoins.Core.Models;

namespace EuroCoins.CatalogTool;

/// <summary>
/// Rebuilds the bundled catalog from the ECB's directory of euro coins: circulation coins (curated
/// series + ECB images) and all €2 commemoratives (fully from the ECB). Downloads new or changed
/// images (JPEG, at most 540 px) and missing flags, keeps the IDs of known coins and removes
/// images that are no longer used.
/// </summary>
internal sealed class CatalogUpdater(EcbClient client, string catalogFolder)
{
    private const string CirculationCredit = "© European Central Bank / European Union";
    private const string CommemorativeCredit = "© European Central Bank";
    private const int FirstCommemorativeYear = 2004;

    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");

    private static readonly Dictionary<int, string> DenominationPages = new()
    {
        [1] = "1cent", [2] = "2cents", [5] = "5cents", [10] = "10cents",
        [20] = "20cents", [50] = "50cents", [100] = "1euro", [200] = "2euro",
    };

    internal static readonly LocalizedText CommemorativeSeries = new("2-€-Gedenkmünze", "€2 commemorative coin");

    private string CatalogFile => Path.Combine(catalogFolder, "catalog.json");

    private string ImageFolder => Path.Combine(catalogFolder, "Images");

    private string FlagFolder => Path.Combine(catalogFolder, "Flags");

    /// <summary>Runs the update. Returns the process exit code.</summary>
    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        var previousDocument = LoadPrevious();
        var previous = previousDocument.Coins.ToList();

        Console.WriteLine("Reading circulation coins …");
        var circulation = await ReadCirculationAsync(cancellationToken);
        Console.WriteLine("Reading commemorative coins …");
        var commemoratives = await ReadCommemorativesAsync(cancellationToken);

        var coins = BuildCirculationCoins(circulation)
            .Concat(BuildCommemorativeCoins(commemoratives, new CommemorativeIdAssigner(previous, previousDocument.RetiredIds)))
            .ToList();
        var usedCountries = coins.Select(c => c.Country).ToHashSet(StringComparer.Ordinal);
        var ids = coins.Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
        var document = new CatalogDocument
        {
            Source = "European Central Bank – https://www.ecb.europa.eu/euro/coins/",
            GeneratedOn = DateOnly.FromDateTime(DateTime.Today),
            Countries = [.. CountryNames.All.Where(c => usedCountries.Contains(c.Code)).Select(c => new CountryDocument(c.Code, c.Name))],
            Coins = coins,

            // Removed coins keep their ID reserved: collections may still reference it.
            RetiredIds = [.. previousDocument.RetiredIds.Concat(previous.Select(c => c.Id))
                .Where(id => !ids.Contains(id)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)],
        };
        _ = CatalogSerializer.ToCatalog(document); // Throws on duplicate IDs or unknown countries.

        Console.WriteLine("Downloading images …");
        var failed = await DownloadImagesAsync(coins, previous, cancellationToken);
        failed += await DownloadMissingFlagsAsync(document.Countries.Select(c => c.Code), cancellationToken);
        if (failed > 0)
        {
            Console.Error.WriteLine($"{failed} downloads failed; the catalog was not changed. Run the update again.");
            return 1;
        }

        using (var stream = File.Create(CatalogFile))
        {
            CatalogSerializer.Write(stream, document);
        }

        RemoveUnusedImages(coins);
        PrintSummary(previous, coins, ids);
        return 0;
    }

    // ----- Reading the ECB pages ---------------------------------------------------------

    private static Uri CirculationPage(string page) => new(EcbClient.BaseUri, $"euro/coins/{page}/html/index.de.html");

    private static Uri CommemorativePage(int year, string language) => new(EcbClient.BaseUri, $"euro/coins/comm/html/comm_{year}.{language}.html");

    private async Task<Dictionary<string, Dictionary<int, IReadOnlyList<Uri>>>> ReadCirculationAsync(CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, Dictionary<int, IReadOnlyList<Uri>>>(StringComparer.Ordinal);
        foreach (var (cents, page) in DenominationPages)
        {
            var uri = CirculationPage(page);
            var html = await client.GetPageAsync(uri, cancellationToken)
                ?? throw new InvalidOperationException($"The ECB page {uri} does not exist.");
            foreach (var country in EcbPageParser.ParseCirculationPage(html, uri))
            {
                if (!result.TryGetValue(country.CountryCode, out var byDenomination))
                {
                    result[country.CountryCode] = byDenomination = [];
                }

                byDenomination[cents] = country.Images;
            }

            Console.WriteLine($"  {DenominationFormatter.LongLabel(cents, CultureInfo.InvariantCulture),-9} {page}");
        }

        return result;
    }

    private async Task<List<(int Year, CommemorativeEntry Entry)>> ReadCommemorativesAsync(CancellationToken cancellationToken)
    {
        var result = new List<(int, CommemorativeEntry)>();

        // Up to next year: new years are picked up automatically; pages that do not exist yet are skipped.
        for (var year = FirstCommemorativeYear; year <= DateTime.Today.Year + 1; year++)
        {
            var germanUri = CommemorativePage(year, "de");
            var german = await client.GetPageAsync(germanUri, cancellationToken);
            if (german is null)
            {
                Console.WriteLine($"  {year}: no page yet");
                continue;
            }

            var english = await client.GetPageAsync(CommemorativePage(year, "en"), cancellationToken);
            var entries = EcbPageParser.ParseCommemorativePage(german, english, germanUri);
            result.AddRange(entries.Select(e => (year, e)));
            Console.WriteLine($"  {year}: {entries.Count} coins");
        }

        return result;
    }

    // ----- Building the catalog --------------------------------------------------------------

    private static IEnumerable<CoinDocument> BuildCirculationCoins(Dictionary<string, Dictionary<int, IReadOnlyList<Uri>>> circulation)
    {
        foreach (var (code, seriesList) in CirculationSeries.ByCountry)
        {
            var byDenomination = circulation.GetValueOrDefault(code) ?? [];
            foreach (var cents in DenominationFormatter.All)
            {
                var images = CirculationSeries.AssignImages(seriesList.Count, byDenomination.GetValueOrDefault(cents) ?? []);
                for (var i = 0; i < seriesList.Count; i++)
                {
                    var series = seriesList[i];
                    var title = new LocalizedText(DenominationFormatter.LongLabel(cents, German), DenominationFormatter.LongLabel(cents, English));

                    // Circulation images are shared by coins of several series; their ECB file names are stable.
                    yield return new CoinDocument(
                        $"{code}-{series.Id}-{cents}",
                        code,
                        CoinType.Circulation,
                        cents,
                        series.Year,
                        series.Name,
                        title,
                        new LocalizedText($"{title.De} Umlaufmünze – {series.Name.De}", $"{title.En} circulation coin – {series.Name.En}"),
                        images[i] is { } image ? EcbPageParser.ImageFileName(image) : string.Empty,
                        CirculationCredit,
                        CirculationPage(DenominationPages[cents]),
                        images[i]);
                }
            }
        }
    }

    private static IEnumerable<CoinDocument> BuildCommemorativeCoins(List<(int Year, CommemorativeEntry Entry)> commemoratives, CommemorativeIdAssigner idAssigner)
    {
        foreach (var group in commemoratives.GroupBy(c => (c.Year, c.Entry.CountryCode)))
        {
            var entries = group.Select(g => g.Entry).ToList();
            var ids = idAssigner.Assign(group.Key.CountryCode, group.Key.Year, entries);
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];

                // One image file per coin: the ECB reuses image file names for different coins.
                yield return new CoinDocument(
                    ids[i],
                    entry.CountryCode,
                    CoinType.Commemorative,
                    200,
                    group.Key.Year,
                    CommemorativeSeries,
                    new LocalizedText(entry.TitleDe ?? CommemorativeSeries.De, entry.TitleEn ?? entry.TitleDe ?? CommemorativeSeries.En),
                    new LocalizedText(entry.DescriptionDe ?? string.Empty, entry.DescriptionEn ?? entry.DescriptionDe ?? string.Empty),
                    ids[i] + ".jpg",
                    CommemorativeCredit,
                    CommemorativePage(group.Key.Year, "de"),
                    entry.Image);
            }
        }
    }

    // ----- Images and flags ------------------------------------------------------------------

    /// <summary>Downloads images that are missing or whose source address changed since the last update.</summary>
    private async Task<int> DownloadImagesAsync(List<CoinDocument> coins, List<CoinDocument> previous, CancellationToken cancellationToken)
    {
        var previousImages = previous
            .Where(c => c.ImageUrl is not null)
            .GroupBy(c => c.Image, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().ImageUrl, StringComparer.OrdinalIgnoreCase);
        var downloads = coins
            .Where(c => c.ImageUrl is not null)
            .DistinctBy(c => c.Image, StringComparer.OrdinalIgnoreCase)
            .Where(c => !File.Exists(Path.Combine(ImageFolder, c.Image)) || previousImages.GetValueOrDefault(c.Image) != c.ImageUrl)
            .ToList();

        var failed = 0;
        foreach (var coin in downloads)
        {
            try
            {
                var data = await client.GetFileAsync(coin.ImageUrl!, cancellationToken)
                    ?? throw new HttpRequestException("Not found.");
                ImageProcessing.SaveNormalized(data, Path.Combine(ImageFolder, coin.Image));
                Console.WriteLine($"  + {coin.Image}");
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or ArgumentException or System.Runtime.InteropServices.ExternalException)
            {
                failed++;
                Console.Error.WriteLine($"  ! {coin.ImageUrl}: {ex.Message}");
            }

            await Task.Delay(150, cancellationToken); // Be gentle with the ECB servers.
        }

        Console.WriteLine($"  {downloads.Count - failed} images downloaded, {failed} failed.");
        return failed;
    }

    private async Task<int> DownloadMissingFlagsAsync(IEnumerable<string> countryCodes, CancellationToken cancellationToken)
    {
        var failed = 0;
        foreach (var code in countryCodes.Select(c => c.ToLowerInvariant()))
        {
            var target = Path.Combine(FlagFolder, code + ".png");
            if (File.Exists(target))
            {
                continue;
            }

            var data = await client.GetFileAsync(new Uri($"https://flagcdn.com/w40/{code}.png"), cancellationToken);
            if (data is null)
            {
                failed++;
                Console.Error.WriteLine($"  ! flag {code} not found");
                continue;
            }

            Directory.CreateDirectory(FlagFolder);
            await File.WriteAllBytesAsync(target, data, cancellationToken);
            Console.WriteLine($"  + flag {code}");
        }

        return failed;
    }

    private void RemoveUnusedImages(List<CoinDocument> coins)
    {
        var used = coins.Select(c => c.Image).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unused = Directory.EnumerateFiles(ImageFolder).Where(f => !used.Contains(Path.GetFileName(f))).ToList();
        foreach (var file in unused)
        {
            File.Delete(file);
        }

        if (unused.Count > 0)
        {
            Console.WriteLine($"  {unused.Count} unused images removed.");
        }
    }

    // ----- Previous catalog ------------------------------------------------------------------

    private CatalogDocument LoadPrevious()
    {
        if (!File.Exists(CatalogFile))
        {
            return new CatalogDocument();
        }

        using var stream = File.OpenRead(CatalogFile);
        return CatalogSerializer.Read(stream);
    }

    private static void PrintSummary(List<CoinDocument> previous, List<CoinDocument> coins, HashSet<string> ids)
    {
        var previousIds = previous.Select(c => c.Id).ToHashSet(StringComparer.Ordinal);

        Console.WriteLine();
        Console.WriteLine($"Catalog written: {coins.Count(c => c.Type == CoinType.Circulation)} circulation coins, "
            + $"{coins.Count(c => c.Type == CoinType.Commemorative)} commemoratives, {coins.Count} in total.");
        foreach (var coin in coins.Where(c => !previousIds.Contains(c.Id)))
        {
            Console.WriteLine($"  new:     {coin.Id}  {coin.Title.De}");
        }

        foreach (var coin in previous.Where(c => !ids.Contains(c.Id)))
        {
            Console.WriteLine($"  removed: {coin.Id}  {coin.Title.De}  (collections keep the entry, but the coin is no longer shown)");
        }
    }
}
