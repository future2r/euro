namespace EuroCoins.Core.Models;

/// <summary>
/// A coin from the catalog (master data, immutable).
/// </summary>
/// <param name="Id">The unique catalog ID.</param>
/// <param name="CountryCode">The code of the issuing country.</param>
/// <param name="Type">The kind of coin.</param>
/// <param name="Denomination">The face value in euro cents (1 to 200).</param>
/// <param name="Year">The year of issue (first year of the series for circulation coins).</param>
/// <param name="Series">The localized series name.</param>
/// <param name="Title">The localized title.</param>
/// <param name="Description">The localized description of the design.</param>
/// <param name="ImageFile">The image file name relative to the catalog image folder.</param>
/// <param name="ImageCredit">The copyright notice of the image.</param>
/// <param name="ImageSource">The web page the image was taken from, if known.</param>
public sealed record Coin(
    string Id,
    string CountryCode,
    CoinType Type,
    int Denomination,
    int Year,
    LocalizedText Series,
    LocalizedText Title,
    LocalizedText Description,
    string ImageFile,
    string ImageCredit,
    Uri? ImageSource);
