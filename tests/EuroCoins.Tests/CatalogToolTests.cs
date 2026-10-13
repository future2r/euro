using EuroCoins.CatalogTool;
using EuroCoins.Core.Catalogs;
using EuroCoins.Core.Models;

namespace EuroCoins.Tests;

/// <summary>Tests of the catalog tool against HTML in the structure of the ECB pages (no network).</summary>
[TestClass]
public sealed class CatalogToolTests
{
    private static readonly Uri CirculationPage = new("https://www.ecb.europa.eu/euro/coins/1cent/html/index.de.html");
    private static readonly Uri CommemorativePage = new("https://www.ecb.europa.eu/euro/coins/comm/html/comm_2025.de.html");

    private const string CirculationHtml = """
        <div class="boxes">
          <div class="box"><div class="coins"><picture class="coin-cropper"><img src="/euro/coins/common/shared/img/ad/Andorra_1cent.jpg"></picture></div>
            <div class="content-box"><h3>Andorra</h3><p>Text</p></div></div>
          <div class="box"><div class="coins">
              <picture class="coin-cropper"><img src="/euro/coins/common/shared/img/be/Belgium_1cent_2000.jpg"></picture>
              <picture class="coin-cropper"><img src="/euro/coins/common/shared/img/be/Belgium_1cent_2013.jpg"></picture>
              <picture class="coin-cropper"><source srcset="/img/be/Belgium 1cent 2014.webp" type="image/webp"><source srcset="/img/be/Belgium 1cent 2014.jpg 2x" type="image/jpeg"><img src="/img/be/Belgium 1cent 2014.webp"></picture>
            </div><div class="content-box"><h3>Belgien</h3></div></div>
          <div class="box"><div class="coins"><picture><img src="/img/xx/placeholder.jpg"></picture></div><div class="content-box"><h3>Bulgarien</h3></div></div>
          <div class="box"><div class="content-box"><h3>Unbekanntland</h3><picture><img src="/img/x.jpg"></picture></div></div>
          <div class="box highlight"><h3>Deutschland</h3><picture><img src="/img/de.jpg"></picture></div>
        </div>
        """;

    private const string GermanYearHtml = """
        <div class="box"><div class="coins"><picture class="coin-cropper">
            <source srcset="comm_2025/Monaco.webp" type="image/webp"><source srcset="comm_2025/Monaco.jpg" type="image/jpeg"><img src="comm_2025/Monaco.jpg"></picture></div>
          <div class="content-box"><h3>Monaco</h3>
            <div><p><strong>Anlass:</strong>&nbsp;Marquisat des Baux</p><p><strong>Beschreibung:</strong>&nbsp;Die Münze erinnert an das <em>Marquisat</em>.</p>
            <p><strong>Ausgabetag:</strong> Juli 2025</p></div></div></div>
        <div class="box"><div class="coins"><picture><img src="comm_2025/Monaco_Carlades.jpg"></picture></div>
          <div class="content-box"><h3>Monaco</h3><p><strong>Anlass der Ausgabe</strong> Comté de Carladès <strong>Beschreibung:</strong> Schloss</p></div></div>
        """;

    private const string EnglishYearHtml = """
        <div class="box"><picture><img src="comm_2025/Monaco_Carlades.jpg"></picture><h3>Monaco</h3>
          <p><strong>Feature:&nbsp;</strong>Comté de Carladès</p><p><strong>Description: </strong>The castle.</p></div>
        <div class="box"><picture><img src="comm_2025/Monaco.jpg"></picture><h3>Monaco</h3>
          <p><strong>Feature</strong>Marquisate of Baux</p><p><strong>Description:</strong>The coin commemorates the marquisate.</p></div>
        """;

    [TestMethod]
    public void CirculationPageYieldsOneImagePerSeriesWithoutWebPOrPlaceholders()
    {
        var countries = EcbPageParser.ParseCirculationPage(CirculationHtml, CirculationPage);

        CollectionAssert.AreEqual(new[] { "AD", "BE" }, countries.Select(c => c.CountryCode).ToList());
        CollectionAssert.AreEqual(
            new[]
            {
                "https://www.ecb.europa.eu/euro/coins/common/shared/img/be/Belgium_1cent_2000.jpg",
                "https://www.ecb.europa.eu/euro/coins/common/shared/img/be/Belgium_1cent_2013.jpg",
                "https://www.ecb.europa.eu/img/be/Belgium%201cent%202014.jpg",
            },
            countries[1].Images.Select(u => u.AbsoluteUri).ToList());
    }

    [TestMethod]
    public void CommemorativePageReadsLabelsAndMatchesEnglishTextsByImage()
    {
        var entries = EcbPageParser.ParseCommemorativePage(GermanYearHtml, EnglishYearHtml, CommemorativePage);

        Assert.HasCount(2, entries);
        var baux = entries[0];
        Assert.AreEqual("MC", baux.CountryCode);
        Assert.AreEqual("https://www.ecb.europa.eu/euro/coins/comm/html/comm_2025/Monaco.jpg", baux.Image.AbsoluteUri);
        Assert.AreEqual("Marquisat des Baux", baux.TitleDe);
        Assert.AreEqual("Die Münze erinnert an das Marquisat.", baux.DescriptionDe);
        Assert.AreEqual("Marquisate of Baux", baux.TitleEn);
        Assert.AreEqual("The coin commemorates the marquisate.", baux.DescriptionEn);

        var carlades = entries[1];
        Assert.AreEqual("Comté de Carladès", carlades.TitleDe);
        Assert.AreEqual("Schloss", carlades.DescriptionDe);
        Assert.AreEqual("Comté de Carladès", carlades.TitleEn);
    }

    [TestMethod]
    public void MissingEnglishPageLeavesEnglishTextsEmpty()
    {
        var entries = EcbPageParser.ParseCommemorativePage(GermanYearHtml, null, CommemorativePage);

        Assert.IsNull(entries[0].TitleEn);
    }

    [TestMethod]
    public void ImageFileNameIsSafeAndAlwaysJpeg()
    {
        Assert.AreEqual("Belgium_1cent_2014.jpg", EcbPageParser.ImageFileName(new Uri("https://x/img/Belgium%201cent%202014.png")));
        Assert.AreEqual("Malta.jpg", EcbPageParser.ImageFileName(new Uri("https://x/comm_2025/Malta.webp")));
    }

    [TestMethod]
    public void SeriesGetImagesProportionallyNewestLast()
    {
        string[] images = ["a", "b", "c"];

        CollectionAssert.AreEqual(new[] { "c" }, CirculationSeries.AssignImages(1, images).ToList());
        CollectionAssert.AreEqual(new[] { "a", "b", "c" }, CirculationSeries.AssignImages(3, images).ToList());
        CollectionAssert.AreEqual(new[] { "a", "a", "b", "b", "c", "c" }, CirculationSeries.AssignImages(6, images).ToList());
        CollectionAssert.AreEqual(new string?[] { null, null }, CirculationSeries.AssignImages<string>(2, []).ToList());
    }

    [TestMethod]
    public void KnownCoinKeepsIdWhenEcbReusesItsImageNameForANewCoin()
    {
        // Before: the Comté coin used the image Monaco.jpg. Now Monaco.jpg shows a new coin.
        var assigner = new CommemorativeIdAssigner([Previous("MC-CC-2025-01", "Comté de Carladès", "MC-CC-2025-01.jpg", Url("comm_2025/Monaco.jpg"))], []);

        var ids = assigner.Assign("MC", 2025,
        [
            Entry("Marquisat des Baux", "comm_2025/Monaco.jpg"),
            Entry("Comté de Carladès", "comm_2025/Monaco_Carlades.jpg"),
        ]);

        Assert.AreEqual("MC-CC-2025-01", ids[1]);
        Assert.AreEqual("MC-CC-2025-02", ids[0]);
    }

    [TestMethod]
    public void RenamedCoinKeepsIdWhenImageAndASignificantWordMatch()
    {
        var image = Url("comm_2025/Spain.jpg");
        var assigner = new CommemorativeIdAssigner([Previous("ES-CC-2025-01", "Salamanca als UNESCO-Welterbe", "ES-CC-2025-01.jpg", image)], []);

        var ids = assigner.Assign("ES", 2025, [Entry("Anerkennung von Salamanca als UNESCO-Welterbestätte", "comm_2025/Spain.jpg")]);

        Assert.AreEqual("ES-CC-2025-01", ids[0]);
    }

    [TestMethod]
    public void CoinWithPlaceholderTitleIsRecognizedByItsImage()
    {
        var assigner = new CommemorativeIdAssigner(
        [
            Previous("SK-CC-2023-01", "Pferde-Expresspost", "SK-CC-2023-01.jpg", Url("comm_2023/IMG_0049.jpg")),
            Previous("SK-CC-2023-02", "2-€-Gedenkmünze", "SK-CC-2023-02.jpg", Url("comm_2023/0443-23r.jpg")),
        ], []);

        var ids = assigner.Assign("SK", 2023,
        [
            Entry("Pferde-Expresspost", "comm_2023/IMG_0049.jpg"),
            Entry("100. Jahrestag der ersten Bluttransfusion", "comm_2023/0443-23r.jpg"),
        ]);

        CollectionAssert.AreEqual(new[] { "SK-CC-2023-01", "SK-CC-2023-02" }, ids.ToList());
    }

    [TestMethod]
    public void AlternativeTitleLabelsAreRecognized()
    {
        const string html = """
            <div class="box"><picture><img src="comm_2004/comm_2004_sm.jpg"></picture><h3>San Marino</h3>
              <p><strong>Abbildung:</strong> Bartolomeo Borghesi</p><p><strong>Beschreibung:</strong> Büste</p></div>
            """;

        var entries = EcbPageParser.ParseCommemorativePage(html, null, CommemorativePage);

        Assert.AreEqual("Bartolomeo Borghesi", entries[0].TitleDe);
    }

    [TestMethod]
    public void SplitLabelsAreRecognized()
    {
        const string html = """
            <div class="box"><picture><img src="comm_2024/2024_comm_Luxembourg_3.JPG"></picture><h3>Luxemburg</h3>
              <div><div><p><strong>Anlass</strong><strong>:&nbsp;</strong>175.&nbsp;Todestag von Großherzog Wilhelm&nbsp;II.</p>
              <p><strong>Beschreibung</strong><strong>:</strong> Reiterstatue</p></div></div></div>
            """;

        var entries = EcbPageParser.ParseCommemorativePage(html, null, CommemorativePage);

        Assert.AreEqual("175. Todestag von Großherzog Wilhelm II.", entries[0].TitleDe);
        Assert.AreEqual("Reiterstatue", entries[0].DescriptionDe);
    }

    [TestMethod]
    public void BoxesSharingAnImageGetTheirOwnEnglishTexts()
    {
        const string german = """
            <div class="box"><picture><img src="comm_2025/Portugal.jpg"></picture><h3>Portugal</h3><p><strong>Anlass:</strong> Pfadfinder</p></div>
            <div class="box"><picture><img src="comm_2025/Portugal.jpg"></picture><h3>Portugal</h3><p><strong>Anlass:</strong> Nachhaltigkeit</p></div>
            """;
        const string english = """
            <div class="box"><picture><img src="comm_2025/Portugal.jpg"></picture><h3>Portugal</h3><p><strong>Feature:</strong> Scouts</p></div>
            <div class="box"><picture><img src="comm_2025/Portugal.jpg"></picture><h3>Portugal</h3><p><strong>Feature:</strong> Sustainability</p></div>
            """;

        var entries = EcbPageParser.ParseCommemorativePage(german, english, CommemorativePage);

        CollectionAssert.AreEqual(new[] { "Scouts", "Sustainability" }, entries.Select(e => e.TitleEn).ToList());
    }

    [TestMethod]
    public void EnglishBoxWithDifferentImageNameIsMatchedByCountry()
    {
        const string german = """
            <div class="box"><picture><img src="comm_2025/Portugal.jpg"></picture><h3>Portugal</h3><p><strong>Anlass:</strong> Pfadfinder</p></div>
            <div class="box"><picture><img src="comm_2025/Portugal.jpg"></picture><h3>Portugal</h3><p><strong>Anlass:</strong> Nachhaltigkeit</p></div>
            <div class="box"><picture><img src="comm_2025/Germany.jpg"></picture><h3>Deutschland</h3><p><strong>Anlass:</strong> Saarland</p></div>
            """;
        const string english = """
            <div class="box"><picture><img src="comm_2025/Germany_en.jpg"></picture><h3>Germany</h3><p><strong>Feature:</strong> Saarland</p></div>
            <div class="box"><picture><img src="comm_2025/Portugal.jpg"></picture><h3>Portugal</h3><p><strong>Feature:</strong> Scouts</p></div>
            <div class="box"><picture><img src="comm_2025/PT_sustainable.jpg"></picture><h3>Portugal</h3><p><strong>Feature:</strong> Sustainability</p></div>
            """;

        var entries = EcbPageParser.ParseCommemorativePage(german, english, CommemorativePage);

        CollectionAssert.AreEqual(new[] { "Scouts", "Sustainability", "Saarland" }, entries.Select(e => e.TitleEn).ToList());
    }

    [TestMethod]
    public void SeveralNewCoinsOfOneYearGetDistinctIds()
    {
        var assigner = new CommemorativeIdAssigner([Previous("PT-CC-2025-01", "Alt", "PT-CC-2025-01.jpg", null)], []);

        var ids = assigner.Assign("PT", 2025, [Entry("Neu eins", "comm_2025/Portugal.jpg"), Entry("Neu zwei", "comm_2025/Portugal.jpg")]);

        CollectionAssert.AreEqual(new[] { "PT-CC-2025-02", "PT-CC-2025-03" }, ids.ToList());
    }

    [TestMethod]
    public void NewCoinNeverReusesAnExistingId()
    {
        var assigner = new CommemorativeIdAssigner(
        [
            Previous("MT-CC-2025-01", "Die Stadt Mdina", "MT-CC-2025-01.jpg", null),
            Previous("MT-CC-2025-02", "Maltesischer Ochse", "MT-CC-2025-02.jpg", null),
        ], []);

        var ids = assigner.Assign("MT", 2025, [Entry("Etwas ganz Neues", "comm_2025/Malta.jpg")]);

        Assert.AreEqual("MT-CC-2025-03", ids[0]);
    }

    [TestMethod]
    public void NewCoinNeverReusesTheIdOfARemovedCoin()
    {
        // MT-CC-2025-02 was removed from the catalog; a collection may still reference it.
        var assigner = new CommemorativeIdAssigner([Previous("MT-CC-2025-01", "Die Stadt Mdina", "MT-CC-2025-01.jpg", null)], ["MT-CC-2025-02"]);

        var ids = assigner.Assign("MT", 2025, [Entry("Die Stadt Mdina", "comm_2025/Malta.jpg"), Entry("Etwas ganz Neues", "comm_2025/Malta_2.jpg")]);

        CollectionAssert.AreEqual(new[] { "MT-CC-2025-01", "MT-CC-2025-03" }, ids.ToList());
    }

    [TestMethod]
    public void FirstCoinsOfAYearAreNumberedFromOne()
    {
        var ids = new CommemorativeIdAssigner([], []).Assign("LU", 2022, [Entry("Eins", "comm_2022/a.jpg"), Entry("Zwei", "comm_2022/b.jpg")]);

        CollectionAssert.AreEqual(new[] { "LU-CC-2022-01", "LU-CC-2022-02" }, ids.ToList());
    }

    private static Uri Url(string image) => new(CommemorativePage, image);

    private static CommemorativeEntry Entry(string titleDe, string image) =>
        new("XX", Url(image), titleDe, null, null, null);

    private static CoinDocument Previous(string id, string titleDe, string image, Uri? imageUrl) =>
        new(id, id[..2], CoinType.Commemorative, 200, int.Parse(id[6..10], System.Globalization.CultureInfo.InvariantCulture), TestData.NoText, new LocalizedText(titleDe, titleDe),
            TestData.NoText, image, "© ECB", null, imageUrl);
}
