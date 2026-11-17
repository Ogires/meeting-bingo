using MeetingBingo.Core.Phrases;

namespace MeetingBingo.Core.Rooms;

public sealed class Card
{
    public const int Size = 5;
    public const int CellCount = Size * Size;
    public const int FreeIndex = CellCount / 2;

    private readonly int?[] _cells;

    private Card(int?[] cells) => _cells = cells;

    /// <summary>Phrase id per cell, row by row; null at the free centre.</summary>
    public IReadOnlyList<int?> Cells => _cells;

    public static Card Deal(PhraseCatalog catalog, Random random)
    {
        var ids = catalog.All.Select(p => p.Id).ToArray();
        random.Shuffle(ids);
        var cells = new int?[CellCount];
        var next = 0;
        for (var i = 0; i < CellCount; i++)
            cells[i] = i == FreeIndex ? null : ids[next++];
        return new Card(cells);
    }

    /// <summary>Lines whose cells are all marked; the free centre always counts as marked.</summary>
    public static IReadOnlyList<Line> CompletedLines(IReadOnlySet<int> marked) =>
        Line.All.Where(line => line.Cells.All(c => c == FreeIndex || marked.Contains(c))).ToList();
}
