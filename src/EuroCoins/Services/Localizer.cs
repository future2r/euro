using System.Globalization;
using Microsoft.Windows.ApplicationModel.Resources;

namespace EuroCoins.Services;

/// <summary>
/// Provides localized UI strings from <c>Strings/*/Resources.resw</c> for code; XAML uses <c>x:Uid</c>.
/// </summary>
public sealed class Localizer(CultureInfo culture)
{
    private readonly ResourceLoader loader = new();

    /// <summary>Gets the UI culture (German or English).</summary>
    public CultureInfo Culture { get; } = culture;

    /// <summary>Returns the string with the given key, or the key itself if it is missing.</summary>
    public string Get(string key)
    {
        var value = loader.GetString(key);
        return string.IsNullOrEmpty(value) ? key : value;
    }

    /// <summary>Returns a formatted string; the resource contains <c>{0}</c>-style placeholders.</summary>
    public string Format(string key, params object?[] args) => string.Format(CultureInfo.CurrentCulture, Get(key), args);
}
