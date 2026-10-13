using EuroCoins.Core.Collections;
using EuroCoins.Core.Formatting;
using EuroCoins.Core.Models;
using EuroCoins.Core.Querying;

namespace EuroCoins.Tests;

[TestClass]
public sealed class QueryingTests
{
    private static readonly Catalog Catalog = TestData.SmallCatalog;

    [TestMethod]
    public void EmptyQueryReturnsAllCoins()
    {
        var result = new CoinQuery().Apply(Catalog, new CollectionState(), TestData.German);

        Assert.HasCount(Catalog.Coins.Count, result.ToList());
    }

    [TestMethod]
    public void FiltersAreCombined()
    {
        var query = new CoinQuery
        {
            Type = CoinType.Commemorative,
            Countries = new HashSet<string> { "MT" },
            Years = new HashSet<int> { 2025 },
        };

        var result = query.Apply(Catalog, new CollectionState(), TestData.German).Select(c => c.Id).ToList();

        CollectionAssert.AreEqual(new[] { "MT-C1" }, result);
    }

    [TestMethod]
    public void OwnershipFilterUsesQuantity()
    {
        var collection = new CollectionState();
        collection.SetQuantity("DE-1", 1);

        var owned = new CoinQuery { Ownership = OwnershipFilter.Owned }.Apply(Catalog, collection, TestData.German).ToList();
        var missing = new CoinQuery { Ownership = OwnershipFilter.Missing }.Apply(Catalog, collection, TestData.German).ToList();

        Assert.HasCount(1, owned);
        Assert.HasCount(Catalog.Coins.Count - 1, missing);
    }

    [TestMethod]
    [DataRow("mdina", "MT-C1")]
    [DataRow("MALTA 2024", "MT-C2")]
    [DataRow("osterreich", "AT-1")]
    [DataRow("50 ct", "AT-1")]
    public void SearchMatchesTitleCountryYearAndDenomination(string text, string expectedId)
    {
        var result = new CoinQuery { SearchText = text }.Apply(Catalog, new CollectionState(), TestData.German).Select(c => c.Id).ToList();

        CollectionAssert.AreEqual(new[] { expectedId }, result);
    }

    [TestMethod]
    public void GroupingByCountryIsAlphabeticalInUiLanguage()
    {
        var german = CoinGrouping.Group(Catalog.Coins, Catalog, CoinSortOrder.Country, TestData.German).Select(g => g.Title).ToList();
        var english = CoinGrouping.Group(Catalog.Coins, Catalog, CoinSortOrder.Country, TestData.English).Select(g => g.Title).ToList();

        CollectionAssert.AreEqual(new[] { "Deutschland", "Malta", "Österreich" }, german);
        CollectionAssert.AreEqual(new[] { "Austria", "Germany", "Malta" }, english);
    }

    [TestMethod]
    public void WithinCountryCirculationComesFirstThenNewestCommemoratives()
    {
        var groups = CoinGrouping.Group(Catalog.Coins, Catalog, CoinSortOrder.Country, TestData.German);
        var germany = groups.Single(g => g.CountryCode == "DE").Coins.Select(c => c.Id).ToList();
        var malta = groups.Single(g => g.CountryCode == "MT").Coins.Select(c => c.Id).ToList();

        CollectionAssert.AreEqual(new[] { "DE-1", "DE-2", "DE-C1" }, germany);
        CollectionAssert.AreEqual(new[] { "MT-C1", "MT-C2" }, malta);
    }

    [TestMethod]
    public void GroupingByYearIsNewestFirst()
    {
        var years = CoinGrouping.Group(Catalog.Coins, Catalog, CoinSortOrder.Year, TestData.German).Select(g => g.Title).ToList();

        CollectionAssert.AreEqual(new[] { "2025", "2024", "2002" }, years);
    }

    [TestMethod]
    public void GroupingByDenominationIsLargestFirst()
    {
        var groups = CoinGrouping.Group(Catalog.Coins, Catalog, CoinSortOrder.Denomination, TestData.German);

        CollectionAssert.AreEqual(new[] { "2 Euro", "1 Euro", "50 Cent" }, groups.Select(g => g.Title).ToList());
    }

    [TestMethod]
    [DataRow(200, "de-DE", "2 €", "2 Euro")]
    [DataRow(50, "de-DE", "50 ct", "50 Cent")]
    [DataRow(200, "en-US", "€2", "2 euro")]
    [DataRow(1, "en-US", "1c", "1 cent")]
    public void DenominationLabels(int cents, string culture, string shortLabel, string longLabel)
    {
        var info = System.Globalization.CultureInfo.GetCultureInfo(culture);

        Assert.AreEqual(shortLabel, DenominationFormatter.ShortLabel(cents, info));
        Assert.AreEqual(longLabel, DenominationFormatter.LongLabel(cents, info));
    }
}
