namespace EuroCoins.Core.Collections;

/// <summary>
/// The state of the user's collection: quantity, note and "added" date of every coin, keyed by coin ID.
/// </summary>
/// <remarks>The quantity is authoritative: a coin is owned when its quantity is at least 1.
/// Not thread-safe; use it from one thread (the UI thread).</remarks>
/// <param name="timeProvider">The clock used for the "added" date.</param>
public sealed class CollectionState(TimeProvider timeProvider)
{
    private readonly Dictionary<string, CollectionEntry> entries = new(StringComparer.Ordinal);

    /// <summary>
    /// Creates an empty collection that uses the system clock.
    /// </summary>
    public CollectionState()
        : this(TimeProvider.System)
    {
    }

    /// <summary>
    /// Occurs when the state of one coin changed. The argument is the coin ID.
    /// </summary>
    public event EventHandler<string>? EntryChanged;

    /// <summary>
    /// Occurs when the whole collection was replaced, e.g. after a reload.
    /// </summary>
    public event EventHandler? Reset;

    /// <summary>
    /// Gets all non-empty entries.
    /// </summary>
    public IReadOnlyDictionary<string, CollectionEntry> Entries => entries;

    /// <summary>
    /// Returns the state of a coin.
    /// </summary>
    /// <param name="coinId">The coin ID.</param>
    /// <returns>The entry; <see cref="CollectionEntry.Empty"/> for unknown coins.</returns>
    public CollectionEntry Get(string coinId) =>
        entries.TryGetValue(coinId, out var entry) ? entry : CollectionEntry.Empty;

    /// <summary>
    /// Sets the owned quantity of a coin and maintains the "added" date: it is set when the
    /// quantity goes from 0 to at least 1 and cleared when it drops to 0.
    /// </summary>
    /// <param name="coinId">The coin ID.</param>
    /// <param name="quantity">The new quantity; negative values count as 0.</param>
    public void SetQuantity(string coinId, int quantity)
    {
        var current = Get(coinId);
        quantity = Math.Max(0, quantity);
        if (quantity == current.Quantity)
        {
            return;
        }

        DateTimeOffset? added = quantity == 0 ? null : current.Added ?? timeProvider.GetUtcNow();
        Store(coinId, current with { Quantity = quantity, Added = added });
    }

    /// <summary>
    /// Toggles ownership: an owned coin gets quantity 0, a missing coin quantity 1.
    /// </summary>
    /// <param name="coinId">The coin ID.</param>
    public void ToggleOwned(string coinId) => SetQuantity(coinId, Get(coinId).IsOwned ? 0 : 1);

    /// <summary>
    /// Sets the personal note of a coin.
    /// </summary>
    /// <param name="coinId">The coin ID.</param>
    /// <param name="note">The note; empty or white-space text removes it.</param>
    public void SetNote(string coinId, string? note)
    {
        var current = Get(coinId);
        note = string.IsNullOrWhiteSpace(note) ? null : note;
        if (note == current.Note)
        {
            return;
        }

        Store(coinId, current with { Note = note });
    }

    /// <summary>
    /// Replaces the whole collection, e.g. after the file was changed by another device.
    /// </summary>
    /// <param name="newEntries">The new entries.</param>
    public void ReplaceAll(IEnumerable<KeyValuePair<string, CollectionEntry>> newEntries)
    {
        ArgumentNullException.ThrowIfNull(newEntries);
        entries.Clear();
        foreach (var (id, entry) in newEntries)
        {
            if (!entry.IsEmpty)
            {
                entries[id] = entry;
            }
        }

        Reset?.Invoke(this, EventArgs.Empty);
    }

    private void Store(string coinId, CollectionEntry entry)
    {
        if (entry.IsEmpty)
        {
            entries.Remove(coinId);
        }
        else
        {
            entries[coinId] = entry;
        }

        EntryChanged?.Invoke(this, coinId);
    }
}
