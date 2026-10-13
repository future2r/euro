using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace EuroCoins.CatalogTool;

/// <summary>The images of one country on an ECB circulation coin page, oldest first (usually one per series).</summary>
internal sealed record CirculationImages(string CountryCode, IReadOnlyList<Uri> Images);

/// <summary>A €2 commemorative coin found on an ECB year page.</summary>
internal sealed record CommemorativeEntry(
    string CountryCode,
    Uri Image,
    string? TitleDe,
    string? DescriptionDe,
    string? TitleEn,
    string? DescriptionEn);

/// <summary>
/// Parses the ECB euro coin pages. Every country is a <c>div.box</c> with a heading (the country
/// name), one <c>picture</c> per coin image and, on commemorative pages, labeled paragraphs
/// ("Anlass:", "Beschreibung:" / "Feature:", "Description:").
/// </summary>
internal static partial class EcbPageParser
{
    private static readonly HtmlParser Parser = new();
    private static readonly string[] ImageExtensions = [".jpg", ".jpeg", ".png", ".gif"];
    private static readonly string[] ImageTypes = ["image/jpeg", "image/png", "image/gif"];

    // The label in front of a commemorative's title varies over the years.
    private static readonly string[] TitleLabels = ["Anlass der Ausgabe", "Anlass", "Thema", "Abbildung", "Gedenkobjekt"];

    /// <summary>Parses a circulation coin page (one page per face value).</summary>
    public static IReadOnlyList<CirculationImages> ParseCirculationPage(string html, Uri pageUri)
    {
        using var document = Parser.ParseDocument(html);
        return [.. Boxes(document)
            .Select(box => (Code: CountryNames.CodeFromName(Heading(box)), Images: ImageSources(box)))
            .Where(b => b.Code is not null && b.Images.Count > 0)
            .Select(b => new CirculationImages(b.Code!, [.. b.Images.Select(src => new Uri(pageUri, src))]))];
    }

    /// <summary>
    /// Parses a commemorative coin year page in German and, if available, English. The English
    /// texts are matched by the (language-independent) image file name; boxes that share an image
    /// are matched in page order, and a coin whose image name differs between the languages gets
    /// the remaining English box of the same country.
    /// </summary>
    public static IReadOnlyList<CommemorativeEntry> ParseCommemorativePage(string germanHtml, string? englishHtml, Uri pageUri)
    {
        using var german = Parser.ParseDocument(germanHtml);
        using var english = Parser.ParseDocument(englishHtml ?? string.Empty); // No page: no English boxes.

        var germanBoxes = Boxes(german)
            .Select(box => (Box: box, Code: CountryNames.CodeFromName(Heading(box)), Sources: ImageSources(box)))
            .Where(b => b.Code is not null && b.Sources.Count > 0)
            .Select(b => (b.Box, Code: b.Code!, Source: b.Sources[0], Stem: Path.GetFileNameWithoutExtension(b.Sources[0])))
            .ToList();
        var remaining = Boxes(english)
            .Select(box => (Box: box, Code: CountryNames.CodeFromName(Heading(box)), Sources: ImageSources(box)))
            .Where(b => b.Sources.Count > 0)
            .Select(b => (b.Box, b.Code, Stem: Path.GetFileNameWithoutExtension(b.Sources[0])))
            .ToList();

        // Each English box is used once: first by image name, then by country among the boxes whose
        // image name does not occur on the German page.
        var matches = new IElement?[germanBoxes.Count];
        void Match(int index, Func<(IElement Box, string? Code, string Stem), bool> predicate)
        {
            var match = remaining.FirstOrDefault(predicate);
            if (match.Box is not null)
            {
                matches[index] = match.Box;
                remaining.Remove(match);
            }
        }

        for (var i = 0; i < germanBoxes.Count; i++)
        {
            var stem = germanBoxes[i].Stem;
            Match(i, e => e.Stem == stem);
        }

        var germanStems = germanBoxes.Select(g => g.Stem).ToHashSet(StringComparer.Ordinal);
        for (var i = 0; i < germanBoxes.Count; i++)
        {
            var code = germanBoxes[i].Code;
            if (matches[i] is null)
            {
                Match(i, e => e.Code == code && !germanStems.Contains(e.Stem));
            }
        }

        return [.. germanBoxes.Select((g, i) => new CommemorativeEntry(
            g.Code,
            new Uri(pageUri, g.Source),
            LabeledValue(g.Box, TitleLabels),
            LabeledValue(g.Box, "Beschreibung"),
            matches[i] is { } en ? LabeledValue(en, "Feature", "Occasion", "Subject") : null,
            matches[i] is { } en2 ? LabeledValue(en2, "Description") : null))];
    }

    /// <summary>Returns the country boxes in document order (class exactly "box").</summary>
    private static IEnumerable<IElement> Boxes(IDocument document) =>
        document.QuerySelectorAll("div.box").Where(e => e.ClassName == "box");

    /// <summary>Returns the first heading of a box (the country name).</summary>
    private static string? Heading(IElement box) =>
        box.QuerySelector("h1, h2, h3, h4") is { } heading ? Normalize(heading.TextContent) : null;

    /// <summary>
    /// Returns one JPEG/PNG/GIF source per <c>picture</c> (never WebP), as written in the page
    /// (relative). Placeholders for not yet published designs are skipped.
    /// </summary>
    private static List<string> ImageSources(IElement box)
    {
        var sources = new List<string>();
        foreach (var picture in box.QuerySelectorAll("picture"))
        {
            var src = picture.QuerySelector("img")?.GetAttribute("src") is { } img && HasImageExtension(img)
                ? img
                : picture.QuerySelectorAll("source")
                    .Where(s => ImageTypes.Contains(s.GetAttribute("type"), StringComparer.OrdinalIgnoreCase))
                    .Select(s => s.GetAttribute("srcset"))
                    .FirstOrDefault(s => !string.IsNullOrWhiteSpace(s));
            if (src is null)
            {
                continue;
            }

            // First srcset candidate without its descriptor (" 2x", " 540w"); file names may contain spaces.
            src = SrcsetDescriptor().Replace(src.Split(',')[0].Trim(), string.Empty);
            if (!src.Contains("placeholder", StringComparison.OrdinalIgnoreCase))
            {
                sources.Add(src);
            }
        }

        return sources;
    }

    /// <summary>
    /// Returns the text after a bold label (e.g. <c>&lt;strong&gt;Anlass:&lt;/strong&gt;</c>) up to the
    /// end of its paragraph or the next label.
    /// </summary>
    private static string? LabeledValue(IElement box, params string[] labels)
    {
        foreach (var label in labels)
        {
            var strong = box.QuerySelectorAll("strong")
                .FirstOrDefault(s => string.Equals(Normalize(s.TextContent).TrimEnd(':').Trim(), label, StringComparison.OrdinalIgnoreCase));
            if (strong is null)
            {
                continue;
            }

            // Some labels are split: <strong>Anlass</strong><strong>:&nbsp;</strong>Text
            var text = string.Concat(FollowingSiblings(strong)
                .SkipWhile(n => n is IElement { LocalName: "strong" } s && Normalize(s.TextContent).Trim(':').Length == 0)
                .TakeWhile(n => n is not IElement { LocalName: "strong" })
                .Select(n => n.TextContent));
            var value = Normalize(text);
            if (value.Length > 0)
            {
                return value;
            }
        }

        return null;
    }

    /// <summary>Collapses white space (including no-break spaces) and trims.</summary>
    internal static string Normalize(string text) => Whitespace().Replace(text.Replace(' ', ' '), " ").Trim();

    /// <summary>Returns the file name a circulation image is stored under in the catalog: safe characters, always JPEG.</summary>
    internal static string ImageFileName(Uri image)
    {
        var name = Uri.UnescapeDataString(Path.GetFileName(image.AbsolutePath));
        return Path.ChangeExtension(UnsafeFileNameChars().Replace(name, "_"), ".jpg");
    }

    private static bool HasImageExtension(string src) =>
        ImageExtensions.Contains(Path.GetExtension(src), StringComparer.OrdinalIgnoreCase);

    private static IEnumerable<INode> FollowingSiblings(INode node)
    {
        for (var sibling = node.NextSibling; sibling is not null; sibling = sibling.NextSibling)
        {
            yield return sibling;
        }
    }

    [GeneratedRegex(@"\s+\d+[wx]$")]
    private static partial Regex SrcsetDescriptor();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    [GeneratedRegex("[^A-Za-z0-9._-]")]
    private static partial Regex UnsafeFileNameChars();
}
