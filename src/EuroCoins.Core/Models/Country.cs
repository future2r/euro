namespace EuroCoins.Core.Models;

/// <summary>
/// A country that issues euro coins.
/// </summary>
/// <param name="Code">The ISO 3166-1 alpha-2 code in upper case, e.g. <c>DE</c>.</param>
/// <param name="Name">The localized country name.</param>
public sealed record Country(string Code, LocalizedText Name);
