using System.Text.Json;
using MeetingBingo.Core.Phrases;

namespace MeetingBingo.Core.Tests;

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
