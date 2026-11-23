using System.Net;

using MeetingBingo.Core.Phrases;
using MeetingBingo.Core.Rooms;

namespace MeetingBingo.Core.Tests.Rooms;

public class CardTests
{
    private static readonly PhraseCatalog Catalog = TestCatalog.Create(30);

    [Fact]
    public void A_card_has_24_distinct_phrases_around_a_free_centre()
    {
        var card = Card.Deal(Catalog, new Random(1));

        Assert.Equal(Card.CellCount, card.Cells.Count);
        Assert.Null(card.Cells[Card.FreeIndex]);
        var ids = card.Cells.OfType<int>().ToList();
        Assert.Equal(24, ids.Count);
        Assert.Equal(24, ids.Distinct().Count());
    }

    [Fact]
    public void Dealing_is_reproducible_with_the_same_seed() =>
        Assert.Equal(Card.Deal(Catalog, new Random(7)).Cells, Card.Deal(Catalog, new Random(7)).Cells);

    [Fact]
    public void Different_seeds_deal_different_cards() =>
        Assert.NotEqual(Card.Deal(Catalog, new Random(1)).Cells, Card.Deal(Catalog, new Random(2)).Cells);

    [Fact]
    public void There_are_twelve_lines() => Assert.Equal(12, Line.All.Count);

    [Theory]
    [InlineData(new[] { 0, 1, 2, 3, 4 }, "row-1")]
    [InlineData(new[] { 10, 11, 13, 14 }, "row-3")]      // the centre row, with the free cell
    [InlineData(new[] { 0, 5, 10, 15, 20 }, "col-1")]
    [InlineData(new[] { 0, 6, 18, 24 }, "diagonal")]
    [InlineData(new[] { 4, 8, 16, 20 }, "anti-diagonal")]
    public void A_full_line_is_completed(int[] marked, string line) =>
        Assert.Equal(new[] { line }, Card.CompletedLines(marked.ToHashSet()).Select(l => l.Name));

    [Fact]
    public void Four_cells_of_a_row_are_not_a_line() =>
        Assert.Empty(Card.CompletedLines(new HashSet<int> { 0, 1, 2, 3 }));

    public static TheoryData<int[], string[]> Combinations => new()
    {
        // Four cells of a row: no line
        { [0, 1, 2, 3], [] },
        // One row
        { [0, 1, 2, 3, 4], ["row-1"] },
        // The first row and the first column, which share cell 0
        { [0, 1, 2, 3, 4, 5, 10, 15, 20], ["row-1", "col-1"] },
        // The whole card: every line, in the order of Line.All
        { Enumerable.Range(0, Card.CellCount).ToArray(), Line.All.Select(l => l.Name).ToArray() },
    };

    [Theory]
    [MemberData(nameof(Combinations))]
    public void Completed_lines_are_detected_correctly(int[] marked, string[] expected) =>
        Assert.Equal(expected, Card.CompletedLines(marked.ToHashSet()).Select(l => l.Name));
}