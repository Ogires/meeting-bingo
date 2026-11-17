using System.Text.Json;
using MeetingBingo.Core.Phrases;

namespace MeetingBingo.Core.Tests.Phrases;

public class PhraseCatalogTests
{
    [Fact]
    public void The_embedded_catalog_has_between_60_and_80_valid_phrases()
    {
        var catalog = PhraseCatalog.LoadEmbedded();

        Assert.InRange(catalog.All.Count, 60, 80);
        Assert.Equal(catalog.All.Count, catalog.All.Select(p => p.Id).Distinct().Count());
        Assert.All(catalog.All, p => Assert.InRange(p.Text.Length, 1, PhraseCatalog.MaxLength));
        Assert.Equal(Enum.GetValues<PhraseCategory>().Length, catalog.All.Select(p => p.Category).Distinct().Count());
    }

    [Fact]
    public void Phrases_can_be_looked_up_by_id()
    {
        var catalog = TestCatalog.Create(30);
        Assert.Equal("Frase 7", catalog.Get(7).Text);
    }

    [Fact]
    public void Duplicate_ids_are_rejected() =>
        Assert.Throws<InvalidDataException>(() => PhraseCatalog.FromJson(Json((1, "A"), (1, "B")), minPhrases: 2));

    [Fact]
    public void Duplicate_texts_are_rejected_ignoring_case() =>
        Assert.Throws<InvalidDataException>(() => PhraseCatalog.FromJson(Json((1, "Hola"), (2, "HOLA")), minPhrases: 2));

    [Fact]
    public void Blank_texts_are_rejected() =>
        Assert.Throws<InvalidDataException>(() => PhraseCatalog.FromJson(Json((1, " "), (2, "B")), minPhrases: 2));

    [Fact]
    public void Texts_longer_than_the_maximum_are_rejected() =>
        Assert.Throws<InvalidDataException>(() =>
            PhraseCatalog.FromJson(Json((1, new string('a', PhraseCatalog.MaxLength + 1)), (2, "B")), minPhrases: 2));

    [Fact]
    public void A_catalog_too_small_to_fill_a_card_is_rejected() =>
        Assert.Throws<InvalidDataException>(() => PhraseCatalog.FromJson(Json((1, "A"), (2, "B"))));

    private static string Json(params (int Id, string Text)[] items) =>
        JsonSerializer.Serialize(items.Select(i => new { id = i.Id, text = i.Text, category = "jargon" }));
}
