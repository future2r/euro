using System.Globalization;
using Microsoft.Windows.Globalization;

namespace EuroCoins.Services;

/// <summary>
/// Applies the UI language at startup. A change in the settings takes effect after a restart.
/// </summary>
internal static class AppLanguage
{
    /// <summary>
    /// Sets the resource language override and the UI culture.
    /// </summary>
    /// <param name="language"><c>de</c>, <c>en</c> or <see langword="null"/> to follow Windows.</param>
    /// <returns>The effective UI culture.</returns>
    public static CultureInfo Apply(string? language)
    {
        // "Follow Windows" resolves to the Windows display language (German or English). The override
        // is always set explicitly, so an override from an earlier choice can never stay in effect
        // (an empty override is not accepted by MRT).
        var effective = language switch
        {
            "de" => "de-DE",
            "en" => "en-US",
            _ => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "de" ? "de-DE" : "en-US",
        };
        ApplicationLanguages.PrimaryLanguageOverride = effective;

        var culture = CultureInfo.GetCultureInfo(effective);
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentUICulture = culture;
        return culture;
    }
}
