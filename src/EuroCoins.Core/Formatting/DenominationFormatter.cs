using System.Globalization;
using EuroCoins.Core.Models;

namespace EuroCoins.Core.Formatting;

/// <summary>
/// Formats face values given in euro cents.
/// </summary>
public static class DenominationFormatter
{
    /// <summary>
    /// All face values of euro coins in cents, from the smallest to the largest.
    /// </summary>
    public static IReadOnlyList<int> All { get; } = [1, 2, 5, 10, 20, 50, 100, 200];

    /// <summary>
    /// Returns a short label, e.g. "2 €" / "50 ct" (German) or "€2" / "50c" (English).
    /// </summary>
    /// <param name="cents">The face value in cents.</param>
    /// <param name="culture">The UI culture.</param>
    /// <returns>The label.</returns>
    public static string ShortLabel(int cents, CultureInfo culture) =>
        (cents >= 100 ? new LocalizedText($"{cents / 100} €", $"€{cents / 100}") : new LocalizedText($"{cents} ct", $"{cents}c")).Get(culture);

    /// <summary>
    /// Returns a long label used as a group heading, e.g. "2 Euro" / "50 Cent".
    /// </summary>
    /// <param name="cents">The face value in cents.</param>
    /// <param name="culture">The UI culture.</param>
    /// <returns>The label.</returns>
    public static string LongLabel(int cents, CultureInfo culture) =>
        (cents >= 100
            ? new LocalizedText($"{cents / 100} Euro", $"{cents / 100} euro")
            : new LocalizedText($"{cents} Cent", cents == 1 ? "1 cent" : $"{cents} cents")).Get(culture);
}
