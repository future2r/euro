using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EuroCoins.Core.Collections;
using EuroCoins.Core.Formatting;
using EuroCoins.Core.Models;
using EuroCoins.Services;
using Microsoft.UI.Xaml.Media;

namespace EuroCoins.ViewModels;

/// <summary>
/// Actions of a coin item that need the surrounding list.
/// </summary>
public interface ICoinItemHost
{
    /// <summary>Selects the coin and opens the details pane.</summary>
    void ShowDetails(CoinItemViewModel item);

    /// <summary>Shows the enlarged coin image.</summary>
    void Enlarge(CoinItemViewModel item);
}

/// <summary>
/// One coin in a list, combining catalog data with the user's collection state.
/// </summary>
public sealed partial class CoinItemViewModel : ObservableObject
{
    private readonly Coin coin;
    private readonly CollectionState collection;
    private readonly Localizer localizer;
    private readonly ICoinItemHost host;

    /// <summary>Creates the view model.</summary>
    public CoinItemViewModel(Coin coin, Catalog catalog, CollectionState collection, Localizer localizer, ImageService images, ICoinItemHost host)
    {
        ArgumentNullException.ThrowIfNull(coin);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(images);
        this.coin = coin;
        this.collection = collection;
        this.localizer = localizer;
        this.host = host;

        var culture = localizer.Culture;
        Title = coin.Title.Get(culture);
        CountryName = catalog.GetCountry(coin.CountryCode).Name.Get(culture);
        Flag = images.Flag(coin.CountryCode);
        DenominationLabel = DenominationFormatter.ShortLabel(coin.Denomination, culture);
        YearText = coin.Year.ToString(CultureInfo.InvariantCulture);
        Meta = $"{YearText} · {DenominationLabel}";
        TypeLabel = localizer.Get(coin.Type == CoinType.Commemorative ? "CoinType_Commemorative" : "CoinType_Circulation");
        Series = coin.Series.Get(culture);
        Description = coin.Description.Get(culture);
    }

    /// <summary>Gets the coin ID.</summary>
    public string Id => coin.Id;

    /// <summary>Gets the localized title.</summary>
    public string Title { get; }

    /// <summary>Gets the localized country name.</summary>
    public string CountryName { get; }

    /// <summary>Gets the country flag.</summary>
    public ImageSource Flag { get; }

    /// <summary>Gets the face value label, e.g. "2 €".</summary>
    public string DenominationLabel { get; }

    /// <summary>Gets the year as text.</summary>
    public string YearText { get; }

    /// <summary>Gets the secondary line of a tile, e.g. "2025 · 2 €".</summary>
    public string Meta { get; }

    /// <summary>Gets the localized coin type.</summary>
    public string TypeLabel { get; }

    /// <summary>Gets the localized series name.</summary>
    public string Series { get; }

    /// <summary>Gets the localized description.</summary>
    public string Description { get; }

    /// <summary>Gets the image credit.</summary>
    public string ImageCredit => coin.ImageCredit;

    /// <summary>Gets the source page of the image.</summary>
    public Uri? ImageSourcePage => coin.ImageSource;

    /// <summary>Gets the tile image. Not cached: a realized tile holds it, and it is released together
    /// with the tile when the list virtualizes it away.</summary>
    public ImageSource? Thumbnail => ImageService.CoinImage(coin, 224);

    /// <summary>Gets the image for the details pane and the enlarged view (not cached, see <see cref="Thumbnail"/>).</summary>
    public ImageSource? LargeImage => ImageService.CoinImage(coin, 540);

    /// <summary>Gets or sets the owned quantity; the coin is owned from 1.</summary>
    public double Quantity
    {
        get => collection.Get(Id).Quantity;
        set
        {
            if (double.IsFinite(value))
            {
                collection.SetQuantity(Id, (int)Math.Round(value));
            }
        }
    }

    /// <summary>Gets or sets whether the coin is owned (sets quantity 1 or 0).</summary>
    public bool IsOwned
    {
        get => collection.Get(Id).IsOwned;
        set
        {
            if (value != IsOwned)
            {
                collection.SetQuantity(Id, value ? 1 : 0);
            }
        }
    }

    /// <summary>Gets or sets the personal note.</summary>
    public string Note
    {
        get => collection.Get(Id).Note ?? string.Empty;
        set => collection.SetNote(Id, value);
    }

    /// <summary>Gets the "owned since" date as text, or an empty string.</summary>
    public string AddedText => collection.Get(Id).Added is { } added
        ? added.ToLocalTime().ToString("d", CultureInfo.CurrentCulture)
        : string.Empty;

    /// <summary>Gets the opacity of the coin image (missing coins are dimmed).</summary>
    public double ImageOpacity => IsOwned ? 1.0 : 0.4;

    /// <summary>Gets the label of the ownership action.</summary>
    public string ToggleOwnedLabel => localizer.Get(IsOwned ? "Coin_MarkMissing" : "Coin_MarkOwned");

    /// <summary>Returns the name screen readers announce for the list item.</summary>
    /// <returns>Title, country and year.</returns>
    public override string ToString() => $"{Title}, {CountryName} {YearText}";

    /// <summary>Notifies the bindings after the collection state of this coin changed.</summary>
    public void Refresh()
    {
        OnPropertyChanged(nameof(Quantity));
        OnPropertyChanged(nameof(IsOwned));
        OnPropertyChanged(nameof(Note));
        OnPropertyChanged(nameof(AddedText));
        OnPropertyChanged(nameof(ImageOpacity));
        OnPropertyChanged(nameof(ToggleOwnedLabel));
    }

    [RelayCommand]
    private void ToggleOwned() => collection.ToggleOwned(Id);

    [RelayCommand]
    private void IncreaseQuantity() => collection.SetQuantity(Id, collection.Get(Id).Quantity + 1);

    [RelayCommand]
    private void ShowDetails() => host.ShowDetails(this);

    [RelayCommand]
    private void Enlarge() => host.Enlarge(this);
}
