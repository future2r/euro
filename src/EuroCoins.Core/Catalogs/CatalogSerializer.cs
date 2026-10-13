using System.Text.Json;
using EuroCoins.Core.Models;

namespace EuroCoins.Core.Catalogs;

/// <summary>
/// Reads and writes the catalog file.
/// </summary>
public static class CatalogSerializer
{
    /// <summary>
    /// Loads the catalog from a file.
    /// </summary>
    /// <param name="path">The path of <c>catalog.json</c>.</param>
    /// <returns>The catalog.</returns>
    public static Catalog Load(string path)
    {
        using var stream = File.OpenRead(path);
        return ToCatalog(Read(stream));
    }

    /// <summary>
    /// Reads a catalog document from a stream.
    /// </summary>
    /// <param name="stream">The stream with UTF-8 JSON.</param>
    /// <returns>The document.</returns>
    /// <exception cref="InvalidDataException">The content is not a valid catalog.</exception>
    public static CatalogDocument Read(Stream stream)
    {
        CatalogDocument document;
        try
        {
            document = JsonSerializer.Deserialize(stream, CatalogJsonContext.Default.CatalogDocument)
                ?? throw new InvalidDataException("The catalog file is empty.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("The catalog file is not valid JSON.", ex);
        }

        if (document.SchemaVersion != CatalogDocument.CurrentSchemaVersion)
        {
            throw new InvalidDataException($"Unsupported catalog schema version {document.SchemaVersion}.");
        }

        return document;
    }

    /// <summary>
    /// Writes a catalog document to a stream.
    /// </summary>
    /// <param name="stream">The target stream.</param>
    /// <param name="document">The document.</param>
    public static void Write(Stream stream, CatalogDocument document) =>
        JsonSerializer.Serialize(stream, document, CatalogJsonContext.Default.CatalogDocument);

    /// <summary>
    /// Converts a document into the domain model.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <returns>The catalog.</returns>
    public static Catalog ToCatalog(CatalogDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return new Catalog(
            document.Countries.Select(c => new Country(c.Code, c.Name)),
            document.Coins.Select(c => new Coin(
                c.Id, c.Country, c.Type, c.Denomination, c.Year, c.Series, c.Title, c.Description,
                c.Image, c.ImageCredit, c.ImageSource)));
    }
}
