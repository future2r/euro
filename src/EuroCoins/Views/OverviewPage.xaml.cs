using EuroCoins.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace EuroCoins.Views;

/// <summary>
/// The start page: collection progress and recently added coins.
/// </summary>
public sealed partial class OverviewPage : Page
{
    /// <summary>Creates the page.</summary>
    public OverviewPage()
    {
        ViewModel = App.Current!.Services.GetRequiredService<OverviewViewModel>();
        InitializeComponent();
    }

    /// <summary>Gets the view model.</summary>
    public OverviewViewModel ViewModel { get; }

    /// <inheritdoc/>
    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        ViewModel.Dispose();
    }
}
