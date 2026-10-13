namespace EuroCoins.Core.Collections;

/// <summary>
/// The user's state for one coin.
/// </summary>
/// <param name="Quantity">The number of copies owned. The coin counts as owned from 1.</param>
/// <param name="Note">A personal note, or <see langword="null"/>.</param>
/// <param name="Added">When the coin was added to the collection (quantity went from 0 to at
/// least 1), or <see langword="null"/> if it is not owned.</param>
public sealed record CollectionEntry(int Quantity, string? Note, DateTimeOffset? Added)
{
    /// <summary>The state of a coin that is neither owned nor annotated.</summary>
    public static CollectionEntry Empty { get; } = new(0, null, null);

    /// <summary>Gets a value indicating whether the coin is owned.</summary>
    public bool IsOwned => Quantity > 0;

    /// <summary>Gets a value indicating whether the entry carries no information.</summary>
    public bool IsEmpty => Quantity == 0 && string.IsNullOrEmpty(Note);
}
