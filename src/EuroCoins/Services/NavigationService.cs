using EuroCoins.Views;
using Microsoft.UI.Xaml.Controls;

namespace EuroCoins.Services;

/// <summary>
/// The kind of coin list shown on the coins page.
/// </summary>
public enum CoinListKind
{
    /// <summary>All coins.</summary>
    All,

    /// <summary>Circulation coins only.</summary>
    Circulation,

    /// <summary>€2 commemorative coins only.</summary>
    Commemorative,

    /// <summary>Search results.</summary>
    Search,
}

/// <summary>
/// The navigation parameter of the coins page.
/// </summary>
/// <param name="Kind">Which coins to list.</param>
/// <param name="SearchText">The search text for <see cref="CoinListKind.Search"/>.</param>
/// <param name="Countries">Country codes to preselect in the country filter.</param>
/// <param name="SelectCoinId">The coin to select and show in the details pane.</param>
public sealed record CoinListRequest(
    CoinListKind Kind,
    string? SearchText = null,
    IReadOnlyList<string>? Countries = null,
    string? SelectCoinId = null);

/// <summary>
/// Navigates the main frame between the pages of the app.
/// </summary>
public sealed class NavigationService
{
    private Frame? frame;

    /// <summary>Connects the service to the main frame.</summary>
    public void Attach(Frame contentFrame) => frame = contentFrame;

    /// <summary>Shows the overview page.</summary>
    public void NavigateToOverview() => frame?.Navigate(typeof(OverviewPage));

    /// <summary>Shows a coin list.</summary>
    public void NavigateToCoins(CoinListRequest request) => frame?.Navigate(typeof(CoinsPage), request);

    /// <summary>Shows the settings page.</summary>
    public void NavigateToSettings() => frame?.Navigate(typeof(SettingsPage));

    /// <summary>Goes back to the previous page.</summary>
    public void GoBack()
    {
        if (frame?.CanGoBack == true)
        {
            frame.GoBack();
        }
    }
}
