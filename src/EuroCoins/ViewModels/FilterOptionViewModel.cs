using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;

namespace EuroCoins.ViewModels;

/// <summary>
/// One selectable value of a multi-select filter (country, face value or year).
/// </summary>
public sealed partial class FilterOptionViewModel : ObservableObject
{
    private readonly Action changed;
    private bool suppress;

    /// <summary>Creates an option.</summary>
    /// <param name="key">The filter value as text (country code, cents or year).</param>
    /// <param name="label">The display text.</param>
    /// <param name="detail">Secondary text, e.g. the number of coins.</param>
    /// <param name="icon">An optional icon (country flag).</param>
    /// <param name="changed">Called when the user changes the selection.</param>
    public FilterOptionViewModel(string key, string label, string detail, ImageSource? icon, Action changed)
    {
        Key = key;
        Label = label;
        Detail = detail;
        Icon = icon;
        this.changed = changed;
    }

    /// <summary>Gets the filter value as text.</summary>
    public string Key { get; }

    /// <summary>Gets the display text.</summary>
    public string Label { get; }

    /// <summary>Gets secondary text, e.g. the number of coins.</summary>
    public string Detail { get; }

    /// <summary>Gets an optional icon (country flag).</summary>
    public ImageSource? Icon { get; }

    /// <summary>Gets a value indicating whether an icon is shown.</summary>
    public bool HasIcon => Icon is not null;

    /// <summary>Gets or sets whether the option is selected.</summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    /// <summary>Selects or clears the option without notifying the owner.</summary>
    internal void SetSilently(bool selected)
    {
        suppress = true;
        IsSelected = selected;
        suppress = false;
    }

    partial void OnIsSelectedChanged(bool value)
    {
        if (!suppress)
        {
            changed();
        }
    }
}
