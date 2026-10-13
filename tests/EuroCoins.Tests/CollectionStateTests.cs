using EuroCoins.Core.Collections;

namespace EuroCoins.Tests;

[TestClass]
public sealed class CollectionStateTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 12, 10, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void QuantityDecidesOwnership()
    {
        var collection = new CollectionState();

        collection.SetQuantity("A", 2);
        Assert.IsTrue(collection.Get("A").IsOwned);

        collection.SetQuantity("A", 0);
        Assert.IsFalse(collection.Get("A").IsOwned);
    }

    [TestMethod]
    public void AddedDateIsSetOnFirstCopyAndKeptWhileOwned()
    {
        var clock = new FakeTimeProvider(Start);
        var collection = new CollectionState(clock);

        collection.SetQuantity("A", 1);
        clock.Now = Start.AddDays(3);
        collection.SetQuantity("A", 3);

        Assert.AreEqual(Start, collection.Get("A").Added);
    }

    [TestMethod]
    public void AddedDateIsClearedWhenQuantityDropsToZero()
    {
        var clock = new FakeTimeProvider(Start);
        var collection = new CollectionState(clock);
        collection.SetQuantity("A", 1);
        collection.SetNote("A", "Tausch");

        collection.SetQuantity("A", 0);
        clock.Now = Start.AddDays(1);
        collection.SetQuantity("A", 1);

        Assert.AreEqual(Start.AddDays(1), collection.Get("A").Added);
        Assert.AreEqual("Tausch", collection.Get("A").Note);
    }

    [TestMethod]
    public void ToggleOwnedSwitchesBetweenZeroAndOne()
    {
        var collection = new CollectionState();

        collection.ToggleOwned("A");
        Assert.AreEqual(1, collection.Get("A").Quantity);

        collection.SetQuantity("A", 4);
        collection.ToggleOwned("A");
        Assert.AreEqual(0, collection.Get("A").Quantity);
    }

    [TestMethod]
    public void NegativeQuantityCountsAsZero()
    {
        var collection = new CollectionState();
        collection.SetQuantity("A", -3);

        Assert.AreEqual(0, collection.Get("A").Quantity);
        Assert.IsEmpty(collection.Entries);
    }

    [TestMethod]
    public void EmptyEntriesAreRemoved()
    {
        var collection = new CollectionState();
        collection.SetNote("A", "x");
        collection.SetNote("A", "  ");

        Assert.IsEmpty(collection.Entries);
    }

    [TestMethod]
    public void ChangesRaiseEntryChangedOnlyWhenSomethingChanged()
    {
        var collection = new CollectionState();
        var changed = new List<string>();
        collection.EntryChanged += (_, id) => changed.Add(id);

        collection.SetQuantity("A", 1);
        collection.SetQuantity("A", 1);
        collection.SetNote("B", "n");

        CollectionAssert.AreEqual(new[] { "A", "B" }, changed);
    }
}
