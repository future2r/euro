using EuroCoins.Core.Models;

namespace EuroCoins.CatalogTool;

/// <summary>
/// A series of circulation coins of one country (e.g. a new monarch or a redesign).
/// </summary>
/// <param name="Id">The series ID, part of the coin ID (<c>s1</c>, <c>s2</c>, …).</param>
/// <param name="Name">The localized series name.</param>
/// <param name="Year">The first year of issue.</param>
internal sealed record CirculationSeries(string Id, LocalizedText Name, int Year)
{
    /// <summary>
    /// The curated circulation series per country, oldest first. The ECB pages show the images
    /// per face value in the same order, usually one per series (see <see cref="AssignImages{T}"/>).
    /// Add a series here when a country issues a new design.
    /// </summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<CirculationSeries>> ByCountry { get; } =
        new Dictionary<string, IReadOnlyList<CirculationSeries>>
        {
            ["AD"] = [new("s1", new("Serie 2014", "2014 series"), 2014)],
            ["AT"] = [new("s1", new("Serie 2002", "2002 series"), 2002)],
            ["BE"] =
            [
                new("s1", new("Serie 2002 (König Albert II.)", "2002 series (King Albert II)"), 2002),
                new("s2", new("Serie 2008 (überarbeitet)", "2008 series (redesign)"), 2008),
                new("s3", new("Serie 2014 (König Philippe)", "2014 series (King Philippe)"), 2014),
            ],
            ["BG"] = [new("s1", new("Serie 2026", "2026 series"), 2026)],
            ["HR"] = [new("s1", new("Serie 2023", "2023 series"), 2023)],
            ["CY"] = [new("s1", new("Serie 2008", "2008 series"), 2008)],
            ["EE"] = [new("s1", new("Serie 2011", "2011 series"), 2011)],
            ["FI"] =
            [
                new("s1", new("Serie 1999 (1. Serie)", "1999 series (1st series)"), 1999),
                new("s2", new("Serie 2007 (2. Serie)", "2007 series (2nd series)"), 2007),
            ],
            ["FR"] =
            [
                new("s1", new("Serie 1999", "1999 series"), 1999),
                new("s2", new("Serie 2022 (Neugestaltung)", "2022 series (redesign)"), 2022),
            ],
            ["DE"] = [new("s1", new("Serie 2002", "2002 series"), 2002)],
            ["GR"] = [new("s1", new("Serie 2002", "2002 series"), 2002)],
            ["IE"] = [new("s1", new("Serie 2002", "2002 series"), 2002)],
            ["IT"] = [new("s1", new("Serie 2002", "2002 series"), 2002)],
            ["LV"] = [new("s1", new("Serie 2014", "2014 series"), 2014)],
            ["LT"] = [new("s1", new("Serie 2015", "2015 series"), 2015)],
            ["LU"] = [new("s1", new("Serie 2002", "2002 series"), 2002)],
            ["MT"] = [new("s1", new("Serie 2008", "2008 series"), 2008)],
            ["MC"] =
            [
                new("s1", new("Serie 2001 (Fürst Rainier III.)", "2001 series (Prince Rainier III)"), 2001),
                new("s2", new("Serie 2006 (Fürst Albert II.)", "2006 series (Prince Albert II)"), 2006),
                new("s3", new("Serie 2025 (Fürst Albert II., Frontalporträt)", "2025 series (Prince Albert II, frontal portrait)"), 2025),
            ],
            ["NL"] =
            [
                new("s1", new("Serie 1999 (Königin Beatrix)", "1999 series (Queen Beatrix)"), 1999),
                new("s2", new("Serie 2014 (König Willem-Alexander)", "2014 series (King Willem-Alexander)"), 2014),
            ],
            ["PT"] = [new("s1", new("Serie 2002", "2002 series"), 2002)],
            ["SM"] =
            [
                new("s1", new("Serie 2002 (1. Serie)", "2002 series (1st series)"), 2002),
                new("s2", new("Serie 2017 (2. Serie)", "2017 series (2nd series)"), 2017),
            ],
            ["SK"] = [new("s1", new("Serie 2009", "2009 series"), 2009)],
            ["SI"] = [new("s1", new("Serie 2007", "2007 series"), 2007)],
            ["ES"] =
            [
                new("s1", new("Serie 1999 (König Juan Carlos I.)", "1999 series (King Juan Carlos I)"), 1999),
                new("s2", new("Serie 2010 (überarbeitet)", "2010 series (redesign)"), 2010),
                new("s3", new("Serie 2015 (König Felipe VI.)", "2015 series (King Felipe VI)"), 2015),
            ],
            ["VA"] =
            [
                new("s1", new("Serie 2002 (Johannes Paul II.)", "2002 series (John Paul II)"), 2002),
                new("s2", new("Sedisvakanz 2005", "Sede Vacante 2005"), 2005),
                new("s3", new("Serie 2006 (Benedikt XVI.)", "2006 series (Benedict XVI)"), 2006),
                new("s4", new("Sedisvakanz 2013", "Sede Vacante 2013"), 2013),
                new("s5", new("Serie 2014 (Franziskus)", "2014 series (Francis)"), 2014),
                new("s6", new("Serie 2017 (Wappen)", "2017 series (coat of arms)"), 2017),
            ],
        };

    /// <summary>
    /// Assigns one of the ECB images to each series: proportionally, so that later series get
    /// later (newer) images. The ECB sometimes shows fewer images than there are series.
    /// </summary>
    public static IReadOnlyList<T?> AssignImages<T>(int seriesCount, IReadOnlyList<T> images)
        where T : class
    {
        if (images.Count == 0)
        {
            return [.. Enumerable.Repeat<T?>(null, seriesCount)];
        }

        if (seriesCount == 1)
        {
            return [images[^1]];
        }

        return [.. Enumerable.Range(0, seriesCount)
            .Select(i => images[(int)Math.Round(i * (images.Count - 1) / (double)(seriesCount - 1))])];
    }
}
