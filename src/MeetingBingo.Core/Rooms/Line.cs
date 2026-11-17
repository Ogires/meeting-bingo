namespace MeetingBingo.Core.Rooms;

public sealed record Line(string Name, IReadOnlyList<int> Cells)
{
    /// <summary>5 rows, 5 columns and 2 diagonals, in that order.</summary>
    public static IReadOnlyList<Line> All { get; } = Build();

    private static List<Line> Build()
    {
        const int n = Card.Size;
        var lines = new List<Line>();
        for (var r = 0; r < n; r++)
            lines.Add(new($"row-{r + 1}", Enumerable.Range(r * n, n).ToArray()));
        for (var c = 0; c < n; c++)
            lines.Add(new($"col-{c + 1}", Enumerable.Range(0, n).Select(r => r * n + c).ToArray()));
        lines.Add(new("diagonal", Enumerable.Range(0, n).Select(i => i * (n + 1)).ToArray()));
        lines.Add(new("anti-diagonal", Enumerable.Range(1, n).Select(i => i * (n - 1)).ToArray()));
        return lines;
    }
}
