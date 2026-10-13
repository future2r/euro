using EuroCoins.Core.Collections;
using EuroCoins.Core.Settings;
using EuroCoins.Core.Statistics;
using Microsoft.Extensions.Logging.Abstractions;

namespace EuroCoins.Tests;

[TestClass]
public sealed class StatisticsAndSettingsTests
{
    private readonly string folder = TestData.NewTempFolder();

    [TestCleanup]
    public void DeleteFolder()
    {
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [TestMethod]
    public void ProgressCountsOwnedCoins()
    {
        var collection = new CollectionState();
        collection.SetQuantity("MT-C1", 1);
        collection.SetQuantity("MT-C2", 3);

        var progress = CollectionStatistics.For(TestData.SmallCatalog.Coins, collection);

        Assert.AreEqual(new Progress(2, 6), progress);
        Assert.AreEqual(2.0 / 6, progress.Fraction, 1e-9);
    }

    [TestMethod]
    public void CountriesAreOrderedByProgress()
    {
        var collection = new CollectionState();
        collection.SetQuantity("MT-C1", 1);
        collection.SetQuantity("MT-C2", 1);
        collection.SetQuantity("DE-1", 1);

        var order = CollectionStatistics.ByCountry(TestData.SmallCatalog, collection).Select(p => p.Country.Code).ToList();

        CollectionAssert.AreEqual(new[] { "MT", "DE", "AT" }, order);
    }

    [TestMethod]
    public void RecentlyAddedIsNewestFirstAndIgnoresMissingCoins()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero));
        var collection = new CollectionState(clock);
        collection.SetQuantity("DE-1", 1);
        clock.Now = clock.Now.AddDays(1);
        collection.SetQuantity("MT-C1", 1);
        clock.Now = clock.Now.AddDays(1);
        collection.SetQuantity("AT-1", 1);
        collection.SetQuantity("AT-1", 0);

        var recent = CollectionStatistics.RecentlyAdded(TestData.SmallCatalog, collection, 5).Select(a => a.Coin.Id).ToList();

        CollectionAssert.AreEqual(new[] { "MT-C1", "DE-1" }, recent);
    }

    [TestMethod]
    public void SettingsRoundTripAndFallBackToDefaults()
    {
        var path = Path.Combine(folder, "settings.json");
        var store = new SettingsStore(path, NullLogger<SettingsStore>.Instance);
        Assert.AreEqual(new AppSettings(), store.Load());

        var settings = new AppSettings { Theme = AppTheme.Dark, Language = "en", TileZoom = 1.5, Window = new WindowPlacement(10, 20, 800, 600, true) };
        store.Save(settings);
        Assert.AreEqual(settings, store.Load());

        File.WriteAllText(path, "garbage");
        Assert.AreEqual(new AppSettings(), store.Load());
    }

    [TestMethod]
    public void InvalidSettingValuesAreSanitized()
    {
        var path = Path.Combine(folder, "settings.json");
        Directory.CreateDirectory(folder);
        File.WriteAllText(path, """{ "language": "fr", "tileZoom": 99, "window": { "x": 0, "y": 0, "width": 0, "height": 0 } }""");

        var settings = new SettingsStore(path, NullLogger<SettingsStore>.Instance).Load();

        Assert.IsNull(settings.Language);
        Assert.AreEqual(AppSettings.MaxTileZoom, settings.TileZoom);
        Assert.IsNull(settings.Window);
    }
}
