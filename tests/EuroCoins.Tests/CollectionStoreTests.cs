using EuroCoins.Core.Collections;
using Microsoft.Extensions.Logging.Abstractions;

namespace EuroCoins.Tests;

[TestClass]
public sealed class CollectionStoreTests
{
    private readonly string folder = TestData.NewTempFolder();

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void CreateFolder() => Directory.CreateDirectory(folder);

    [TestCleanup]
    public void DeleteFolder() => Directory.Delete(folder, recursive: true);

    [TestMethod]
    public void MissingFileMeansEmptyCollection()
    {
        using var store = NewStore(out var collection);
        store.Open(Path.Combine(folder, "collection.json"));

        Assert.IsEmpty(collection.Entries);
    }

    [TestMethod]
    public void OpenCreatesAMissingFolderSoTheFileIsWatched()
    {
        var subfolder = Path.Combine(folder, "OneDrive");
        using var store = NewStore(out _);
        store.Open(Path.Combine(subfolder, "collection.json"));

        Assert.IsTrue(Directory.Exists(subfolder));
    }

    [TestMethod]
    public void ChangesAreSavedAndCanBeReloaded()
    {
        var path = Path.Combine(folder, "collection.json");
        using (var store = NewStore(out var collection))
        {
            store.Open(path);
            collection.SetQuantity("DE-1", 2);
            collection.SetNote("DE-1", "aus Berlin");
            store.Flush();
        }

        using var reopened = NewStore(out var loaded);
        reopened.Open(path);

        Assert.AreEqual(2, loaded.Get("DE-1").Quantity);
        Assert.AreEqual("aus Berlin", loaded.Get("DE-1").Note);
        Assert.IsNotNull(loaded.Get("DE-1").Added);
    }

    [TestMethod]
    public void RapidChangesEndWithTheNewestStateOnDisk()
    {
        var path = Path.Combine(folder, "collection.json");
        using (var store = NewStore(out var collection))
        {
            store.Open(path);
            for (var i = 1; i <= 50; i++)
            {
                collection.SetQuantity("DE-1", i); // Each change starts a save without waiting for it.
            }

            store.Flush();
        }

        using var reopened = NewStore(out var reloaded);
        reopened.Open(path);

        Assert.AreEqual(50, reloaded.Get("DE-1").Quantity);
    }

    [TestMethod]
    public void ChangesWithoutAnOpenFileAreReportedAsNotSaved()
    {
        using var store = NewStore(out var collection);
        Exception? reported = null;
        store.SaveFailed += (_, ex) => reported = ex;

        collection.SetQuantity("DE-1", 1);

        Assert.IsInstanceOfType<InvalidOperationException>(reported);
    }

    [TestMethod]
    public void CorruptFileIsNotOverwritten()
    {
        var path = Path.Combine(folder, "collection.json");
        File.WriteAllText(path, "{ not json");
        using var store = NewStore(out var collection);

        Assert.ThrowsExactly<InvalidDataException>(() => store.Open(path));
        collection.SetQuantity("DE-1", 1);

        Assert.AreEqual("{ not json", File.ReadAllText(path));
    }

    [TestMethod]
    public async Task ExternalChangeIsPickedUp()
    {
        var path = Path.Combine(folder, "collection.json");
        using var store = NewStore(out var collection);
        store.Open(path);
        var changed = new TaskCompletionSource();
        collection.Reset += (_, _) => changed.TrySetResult();

        // Another device writes the file (e.g. synchronized by OneDrive).
        await File.WriteAllTextAsync(path, """{ "schemaVersion": 1, "coins": { "MT-C1": { "quantity": 1 } } }""", TestContext.CancellationToken);
        await changed.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.CancellationToken);

        Assert.IsTrue(collection.Get("MT-C1").IsOwned);
    }

    [TestMethod]
    public async Task OwnWritesDoNotTriggerReload()
    {
        var path = Path.Combine(folder, "collection.json");
        using var store = NewStore(out var collection);
        store.Open(path);
        var reloads = 0;
        collection.Reset += (_, _) => reloads++;

        collection.SetQuantity("DE-1", 1);
        store.Flush();
        await Task.Delay(1500, TestContext.CancellationToken);

        Assert.AreEqual(0, reloads);
    }

    private static CollectionStore NewStore(out CollectionState collection)
    {
        collection = new CollectionState();
        return new CollectionStore(collection, NullLogger<CollectionStore>.Instance);
    }
}
