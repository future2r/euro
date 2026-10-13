using Microsoft.UI.Xaml.Media;

namespace EuroCoins.ViewModels;

/// <summary>
/// A group of coins with a heading (country, year or face value).
/// </summary>
/// <param name="title">The heading.</param>
/// <param name="flag">The country flag when grouped by country.</param>
/// <param name="countText">The number of coins as text.</param>
/// <param name="items">The coins of the group.</param>
public sealed class CoinGroupViewModel(string title, ImageSource? flag, string countText, IReadOnlyList<CoinItemViewModel> items)
{
    /// <summary>Gets the heading.</summary>
    public string Title { get; } = title;

    /// <summary>Gets the country flag, or <see langword="null"/>.</summary>
    public ImageSource? Flag { get; } = flag;

    /// <summary>Gets a value indicating whether a flag is shown.</summary>
    public bool HasFlag => Flag is not null;

    /// <summary>Gets the number of coins as text.</summary>
    public string CountText { get; } = countText;

    /// <summary>Gets the coins (the items path of the grouped collection view).</summary>
    public IReadOnlyList<CoinItemViewModel> Items { get; } = items;

    /// <summary>Returns the name screen readers announce for the group.</summary>
    /// <returns>The heading and the number of coins.</returns>
    public override string ToString() => $"{Title}, {CountText}";
}
