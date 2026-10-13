using EuroCoins.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.Storage.Pickers;

namespace EuroCoins.Views;

/// <summary>
/// The settings page (theme, language, collection location, about).
/// </summary>
public sealed partial class SettingsPage : Page
{
    /// <summary>Creates the page.</summary>
    public SettingsPage()
    {
        ViewModel = App.Current!.Services.GetRequiredService<SettingsViewModel>();
        InitializeComponent();
    }

    /// <summary>Gets the view model.</summary>
    public SettingsViewModel ViewModel { get; }

    private async void OnChangeLocationClick(object sender, RoutedEventArgs e)
    {
        if (App.Current?.MainWindow is not { } window)
        {
            return;
        }

        var picker = new FolderPicker(window.AppWindow.Id);
        var folder = await picker.PickSingleFolderAsync();
        if (folder is not null)
        {
            ViewModel.ChangeCollectionFolder(folder.Path);
        }
    }
}
