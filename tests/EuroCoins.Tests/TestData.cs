using System.Globalization;
using EuroCoins.Core.Catalogs;
using EuroCoins.Core.Models;

namespace EuroCoins.Tests;

/// <summary>Shared test fixtures.</summary>
internal static class TestData
{
    private static readonly Lazy<Catalog> RealCatalogInstance = new(() =>
        CatalogSerializer.Load(Path.Combine(AppContext.BaseDirectory, "TestData", "catalog.json")));

    public static CultureInfo German { get; } = CultureInfo.GetCultureInfo("de-DE");

    public static CultureInfo English { get; } = CultureInfo.GetCultureInfo("en-US");

    /// <summary>An empty text in both languages.</summary>
    public static LocalizedText NoText { get; } = new(string.Empty, string.Empty);

    /// <summary>The catalog that ships with the app.</summary>
    public static Catalog RealCatalog => RealCatalogInstance.Value;

    /// <summary>Returns a new, not yet created folder for test files.</summary>
    public static string NewTempFolder() => Path.Combine(Path.GetTempPath(), "EuroCoins.Tests", Guid.NewGuid().ToString("N"));

    /// <summary>A small hand-made catalog.</summary>
    public static Catalog SmallCatalog { get; } = new(
        [
            new Country("DE", new LocalizedText("Deutschland", "Germany")),
            new Country("AT", new LocalizedText("Österreich", "Austria")),
            new Country("MT", new LocalizedText("Malta", "Malta")),
        ],
        [
            Coin("DE-1", "DE", CoinType.Circulation, 100, 2002, "1 Euro", "1 euro"),
            Coin("DE-2", "DE", CoinType.Circulation, 200, 2002, "2 Euro", "2 euro"),
            Coin("DE-C1", "DE", CoinType.Commemorative, 200, 2025, "Saarland", "Saarland"),
            Coin("AT-1", "AT", CoinType.Circulation, 50, 2002, "50 Cent", "50 cent"),
            Coin("MT-C1", "MT", CoinType.Commemorative, 200, 2025, "Die Stadt Mdina", "City of Mdina"),
            Coin("MT-C2", "MT", CoinType.Commemorative, 200, 2024, "Die Zitadelle", "The Citadel"),
        ]);

    private static Coin Coin(string id, string country, CoinType type, int denomination, int year, string titleDe, string titleEn) =>
        new(id, country, type, denomination, year,
            new LocalizedText(type == CoinType.Commemorative ? "2-€-Gedenkmünze" : "Serie 2002", type == CoinType.Commemorative ? "€2 commemorative coin" : "2002 series"),
            new LocalizedText(titleDe, titleEn), TestData.NoText, id + ".jpg", "© ECB", null);
}

/// <summary>A clock that can be set by tests.</summary>
internal sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}
