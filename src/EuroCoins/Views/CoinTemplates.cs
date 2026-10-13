using Microsoft.UI.Xaml;

namespace EuroCoins.Views;

/// <summary>
/// Helpers for <c>x:Bind</c> functions in the coin item templates (purely presentational).
/// </summary>
public static class CoinTemplates
{
    /// <summary>Returns the style of the ownership badge: filled for owned, outlined for missing coins.</summary>
    public static Style BadgeStyle(bool isOwned) =>
        (Style)Application.Current.Resources[isOwned ? "OwnedBadgeButtonStyle" : "MissingBadgeButtonStyle"];

    /// <summary>Returns the glyph of the ownership badge: a check mark or a plus.</summary>
    public static string BadgeGlyph(bool isOwned) => isOwned ? "" : "";
}
