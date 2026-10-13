using EuroCoins.ViewModels;
using Microsoft.UI.Xaml;

namespace EuroCoins.Views;

/// <summary>
/// Small functions for <c>x:Bind</c> function bindings (instead of value converters).
/// </summary>
/// <remarks>
/// x:Bind does not call a function again when an argument becomes <see langword="null"/>; pass only
/// non-nullable values (bind a bool property such as <c>IsEnlarged</c> instead of an object).
/// </remarks>
public static class XamlFunctions
{
    /// <summary>Visible when the value is <see langword="false"/>.</summary>
    public static Visibility VisibleIfFalse(bool value) => value ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>Returns whether a text is not empty.</summary>
    public static bool HasText(string? text) => !string.IsNullOrEmpty(text);

    /// <summary>Visibility of the coin grid unless the list is empty.</summary>
    public static Visibility GridVisibility(CoinViewMode viewMode, bool isEmpty) =>
        viewMode == CoinViewMode.Grid && !isEmpty ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Visibility of the coin list unless the list is empty.</summary>
    public static Visibility ListVisibility(CoinViewMode viewMode, bool isEmpty) =>
        viewMode == CoinViewMode.List && !isEmpty ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Returns the Segoe Fluent Icons glyph of a view mode (shown on the view menu button).</summary>
    public static string ViewModeGlyph(CoinViewMode viewMode) => viewMode == CoinViewMode.List ? "" : "";
}
