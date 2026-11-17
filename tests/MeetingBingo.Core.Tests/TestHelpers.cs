using System.Text.Json;
using MeetingBingo.Core.Phrases;
using MeetingBingo.Core.Rooms;
using Microsoft.Extensions.Time.Testing;

namespace MeetingBingo.Core.Tests;

internal static class RoomFactory
{
    public static readonly DateTimeOffset Start = new(2026, 11, 17, 10, 0, 0, TimeSpan.Zero);

    public static (Room Room, FakeTimeProvider Time) Create(int seed = 1)
    {
        var time = new FakeTimeProvider(Start);
        return (new Room("K7QMP", TestCatalog.Create(30), new Random(seed), time), time);
    }

    // With 30 phrases and 24 cells per card, two cards always share some phrase
    public static (int A, int B) SharedPhrase(PlayerSnapshot a, PlayerSnapshot b)
    {
        var shared = a.Cells.First(c => c.PhraseId is { } id && b.Cells.Any(d => d.PhraseId == id));
        return (shared.Index, b.Cells.First(d => d.PhraseId == shared.PhraseId).Index);
    }
}

internal static class TestCatalog
{
    public static PhraseCatalog Create(int count = 30)
    {
        string[] categories = ["technical", "jargon", "situation"];
        var items = Enumerable.Range(1, count)
            .Select(i => new { id = i, text = $"Frase {i}", category = categories[i % 3] });
        return PhraseCatalog.FromJson(JsonSerializer.Serialize(items));
    }
}
