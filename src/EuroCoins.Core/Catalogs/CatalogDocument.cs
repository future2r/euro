using System.Text.Json.Serialization;
using EuroCoins.Core.Models;

namespace EuroCoins.Core.Catalogs;

/// <summary>
/// The on-disk representation of the catalog (<c>catalog.json</c>).
/// </summary>
public sealed record CatalogDocument
{
    internal const int CurrentSchemaVersion = 1;

    /// <summary>Gets the schema version of the document.</summary>
    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    /// <summary>Gets a description of the data source.</summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>Gets the date the catalog was generated.</summary>
    public DateOnly? GeneratedOn { get; init; }

    /// <summary>Gets the issuing countries.</summary>
    public IReadOnlyList<CountryDocument> Countries { get; init; } = [];

    /// <summary>Gets the coins.</summary>
    public IReadOnlyList<CoinDocument> Coins { get; init; } = [];

    /// <summary>
    /// Gets the IDs of coins that were removed from the catalog. Collections may still reference them,
    /// so they are never given to another coin.
    /// </summary>
    public IReadOnlyList<string> RetiredIds { get; init; } = [];
}

/// <summary>
/// The on-disk representation of a country.
/// </summary>
/// <param name="Code">The ISO 3166-1 alpha-2 code.</param>
/// <param name="Name">The localized name.</param>
public sealed record CountryDocument(string Code, LocalizedText Name);

/// <summary>
/// The on-disk representation of a coin.
/// </summary>
/// <param name="Id">The unique catalog ID.</param>
/// <param name="Country">The code of the issuing country.</param>
/// <param name="Type">The kind of coin.</param>
/// <param name="Denomination">The face value in euro cents.</param>
/// <param name="Year">The year of issue.</param>
/// <param name="Series">The localized series name.</param>
/// <param name="Title">The localized title.</param>
/// <param name="Description">The localized description.</param>
/// <param name="Image">The image file name.</param>
/// <param name="ImageCredit">The copyright notice of the image.</param>
/// <param name="ImageSource">The web page the image was taken from.</param>
/// <param name="ImageUrl">The address the image was downloaded from (used by the catalog tool to
/// detect changed images and to recognize renamed commemoratives; not used by the app).</param>
public sealed record CoinDocument(
    string Id,
    string Country,
    CoinType Type,
    int Denomination,
    int Year,
    LocalizedText Series,
    LocalizedText Title,
    LocalizedText Description,
    string Image,
    string ImageCredit,
    Uri? ImageSource,
    Uri? ImageUrl);

/// <summary>
/// Source-generated JSON metadata for the catalog document.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true,
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(CatalogDocument))]
internal sealed partial class CatalogJsonContext : JsonSerializerContext;
