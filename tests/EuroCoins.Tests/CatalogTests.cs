using EuroCoins.Core.Catalogs;
using EuroCoins.Core.Formatting;
using EuroCoins.Core.Models;

namespace EuroCoins.Tests;

[TestClass]
public sealed class CatalogTests
{
    [TestMethod]
    public void BundledCatalogLoadsWithAllCoinsAndCountries()
    {
        var catalog = TestData.RealCatalog;

        Assert.HasCount(25, catalog.Countries);
        Assert.IsGreaterThanOrEqualTo(800, catalog.Coins.Count);
        Assert.IsTrue(catalog.Coins.Any(c => c.Type == CoinType.Circulation));
        Assert.IsTrue(catalog.Coins.Any(c => c.Type == CoinType.Commemorative));
    }

    [TestMethod]
    public void BundledCatalogHasTextsAndImagesForEveryCoin()
    {
        foreach (var coin in TestData.RealCatalog.Coins)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(coin.Title.De), coin.Id);
            Assert.IsFalse(string.IsNullOrWhiteSpace(coin.Title.En), coin.Id);
            Assert.EndsWith(".jpg", coin.ImageFile, coin.Id);
            Assert.Contains(coin.Denomination, DenominationFormatter.All, coin.Id);
        }
    }

    [TestMethod]
    public void UnknownCountryIsRejected()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new Catalog(
            [new Country("DE", new LocalizedText("Deutschland", "Germany"))],
            [new Coin("X", "FR", CoinType.Circulation, 1, 2002, TestData.NoText, TestData.NoText, TestData.NoText, "x.jpg", string.Empty, null)]));
    }

    [TestMethod]
    public void DuplicateCoinIdIsRejected()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new Catalog(
            [new Country("DE", new LocalizedText("Deutschland", "Germany"))],
            [
                new Coin("DE-1", "DE", CoinType.Circulation, 1, 2002, TestData.NoText, TestData.NoText, TestData.NoText, "a.jpg", string.Empty, null),
                new Coin("DE-1", "DE", CoinType.Circulation, 2, 2002, TestData.NoText, TestData.NoText, TestData.NoText, "b.jpg", string.Empty, null),
            ]));
    }

    [TestMethod]
    public void DocumentRoundTripKeepsAllData()
    {
        var document = new CatalogDocument
        {
            Source = "Test",
            Countries = [new CountryDocument("MT", new LocalizedText("Malta", "Malta"))],
            Coins = [new CoinDocument("MT-1", "MT", CoinType.Commemorative, 200, 2025,
                new LocalizedText("S", "S"), new LocalizedText("Titel", "Title"), new LocalizedText("B", "D"),
                "mt.jpg", "© ECB", new Uri("https://www.ecb.europa.eu/"), new Uri("https://www.ecb.europa.eu/mt.jpg"))],
            RetiredIds = ["MT-2"],
        };

        using var stream = new MemoryStream();
        CatalogSerializer.Write(stream, document);
        stream.Position = 0;
        var read = CatalogSerializer.Read(stream);

        Assert.AreEqual(document.Source, read.Source);
        Assert.AreEqual(document.Countries[0], read.Countries[0]);
        Assert.AreEqual(document.Coins[0], read.Coins[0]);
        CollectionAssert.AreEqual(document.RetiredIds.ToList(), read.RetiredIds.ToList());
    }

    [TestMethod]
    public void LocalizedTextPicksLanguage()
    {
        var text = new LocalizedText("Hallo", "Hello");

        Assert.AreEqual("Hallo", text.Get(TestData.German));
        Assert.AreEqual("Hello", text.Get(TestData.English));
        Assert.AreEqual("Hello", text.Get(System.Globalization.CultureInfo.GetCultureInfo("fr-FR")));
    }
}
