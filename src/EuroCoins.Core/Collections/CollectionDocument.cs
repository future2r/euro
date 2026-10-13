using System.Text.Json;
using System.Text.Json.Serialization;

namespace EuroCoins.Core.Collections;

/// <summary>
/// The on-disk representation of the collection (<c>collection.json</c>).
/// </summary>
internal sealed record CollectionDocument
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public Dictionary<string, CollectionEntryDocument> Coins { get; init; } = [];

    public static CollectionDocument From(CollectionState collection) => new()
    {
        Coins = collection.Entries
            .OrderBy(e => e.Key, StringComparer.Ordinal)
            .ToDictionary(e => e.Key, e => new CollectionEntryDocument(e.Value.Quantity, e.Value.Note, e.Value.Added), StringComparer.Ordinal),
    };

    public IEnumerable<KeyValuePair<string, CollectionEntry>> ToEntries() =>
        Coins.Select(e => KeyValuePair.Create(e.Key, new CollectionEntry(Math.Max(0, e.Value.Quantity), e.Value.Note, e.Value.Quantity > 0 ? e.Value.Added : null)));

    public static string Serialize(CollectionState collection) =>
        JsonSerializer.Serialize(From(collection), CollectionJsonContext.Default.CollectionDocument);

    public static CollectionDocument Deserialize(string json)
    {
        CollectionDocument? document;
        try
        {
            document = JsonSerializer.Deserialize(json, CollectionJsonContext.Default.CollectionDocument);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("The collection file is not valid JSON.", ex);
        }

        document ??= new CollectionDocument();
        if (document.SchemaVersion > CurrentSchemaVersion)
        {
            throw new InvalidDataException($"The collection file was written by a newer version (schema {document.SchemaVersion}).");
        }

        return document;
    }
}

/// <summary>
/// The on-disk representation of one collection entry.
/// </summary>
internal sealed record CollectionEntryDocument(int Quantity, string? Note, DateTimeOffset? Added);

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(CollectionDocument))]
internal sealed partial class CollectionJsonContext : JsonSerializerContext;
