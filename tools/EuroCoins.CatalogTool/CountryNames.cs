using System.Collections.Frozen;
using EuroCoins.Core.Models;

namespace EuroCoins.CatalogTool;

/// <summary>
/// The euro-issuing countries with their names, in catalog order.
/// </summary>
internal static class CountryNames
{
    /// <summary>ISO code, German and English name.</summary>
    public static IReadOnlyList<(string Code, LocalizedText Name)> All { get; } =
    [
        ("AD", new("Andorra", "Andorra")),
        ("AT", new("Österreich", "Austria")),
        ("BE", new("Belgien", "Belgium")),
        ("BG", new("Bulgarien", "Bulgaria")),
        ("HR", new("Kroatien", "Croatia")),
        ("CY", new("Zypern", "Cyprus")),
        ("EE", new("Estland", "Estonia")),
        ("FI", new("Finnland", "Finland")),
        ("FR", new("Frankreich", "France")),
        ("DE", new("Deutschland", "Germany")),
        ("GR", new("Griechenland", "Greece")),
        ("IE", new("Irland", "Ireland")),
        ("IT", new("Italien", "Italy")),
        ("LV", new("Lettland", "Latvia")),
        ("LT", new("Litauen", "Lithuania")),
        ("LU", new("Luxemburg", "Luxembourg")),
        ("MT", new("Malta", "Malta")),
        ("MC", new("Monaco", "Monaco")),
        ("NL", new("Niederlande", "Netherlands")),
        ("PT", new("Portugal", "Portugal")),
        ("SM", new("San Marino", "San Marino")),
        ("SK", new("Slowakei", "Slovakia")),
        ("SI", new("Slowenien", "Slovenia")),
        ("ES", new("Spanien", "Spain")),
        ("VA", new("Vatikanstadt", "Vatican City")),
    ];

    // The ECB pages head every country box with its name (German or English page).
    private static readonly FrozenDictionary<string, string> CodesByAnyName = All
        .SelectMany(c => new[] { KeyValuePair.Create(c.Name.De, c.Code), KeyValuePair.Create(c.Name.En, c.Code) })
        .Concat([KeyValuePair.Create("Vatikan", "VA"), KeyValuePair.Create("Vatikanstaat", "VA"), KeyValuePair.Create("Vatican", "VA"), KeyValuePair.Create("Holy See", "VA")])
        .DistinctBy(p => p.Key, StringComparer.OrdinalIgnoreCase)
        .ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>Returns the ISO code for a German or English country name.</summary>
    public static string? CodeFromName(string? name) =>
        name is not null && CodesByAnyName.TryGetValue(name.Trim(), out var code) ? code : null;
}
