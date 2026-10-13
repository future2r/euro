using EuroCoins.Core.Models;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace EuroCoins.Services;

/// <summary>
/// Creates image sources for coin pictures and country flags from the bundled catalog.
/// </summary>
public sealed class ImageService
{
    private static readonly string CatalogFolder = Path.Combine(AppContext.BaseDirectory, "Assets", "Catalog");
    private readonly Dictionary<string, ImageSource> flags = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Returns the image of a coin, decoded at the given width (in effective pixels), or
    /// <see langword="null"/> if the ECB has not published an image yet.</summary>
    public static ImageSource? CoinImage(Coin coin, int decodeWidth)
    {
        ArgumentNullException.ThrowIfNull(coin);
        if (string.IsNullOrEmpty(coin.ImageFile))
        {
            return null;
        }

        return new BitmapImage(new Uri(Path.Combine(CatalogFolder, "Images", coin.ImageFile)))
        {
            DecodePixelWidth = decodeWidth,
            DecodePixelType = DecodePixelType.Logical,
        };
    }

    /// <summary>Returns the flag of a country (shared instance, UI thread only).</summary>
    public ImageSource Flag(string countryCode)
    {
        if (!flags.TryGetValue(countryCode, out var flag))
        {
            flag = new BitmapImage(new Uri(Path.Combine(CatalogFolder, "Flags", countryCode.ToLowerInvariant() + ".png")));
            flags[countryCode] = flag;
        }

        return flag;
    }
}
