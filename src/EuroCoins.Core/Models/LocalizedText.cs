using System.Globalization;

namespace EuroCoins.Core.Models;

/// <summary>
/// A text that is available in German and English.
/// </summary>
/// <param name="De">The German text.</param>
/// <param name="En">The English text.</param>
public sealed record LocalizedText(string De, string En)
{
    /// <summary>
    /// Returns the text for the given culture. German cultures get the German text,
    /// every other culture the English one.
    /// </summary>
    /// <param name="culture">The culture to pick the text for.</param>
    /// <returns>The text in the matching language.</returns>
    public string Get(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);
        return culture.TwoLetterISOLanguageName == "de" ? De : En;
    }
}
