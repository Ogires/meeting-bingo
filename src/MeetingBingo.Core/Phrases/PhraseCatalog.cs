using System.Text.Json;
using System.Text.Json.Serialization;

namespace MeetingBingo.Core.Phrases;

public sealed class PhraseCatalog
{
    /// <summary>The phrases a card needs: 24 cells around the free centre.</summary>
    public const int MinPhrases = 24;
    public const int MaxLength = 60;

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly Dictionary<int, Phrase> _byId;

    private PhraseCatalog(List<Phrase> phrases)
    {
        All = phrases;
        _byId = phrases.ToDictionary(p => p.Id);
    }

    public IReadOnlyList<Phrase> All { get; }

    public Phrase Get(int id) => _byId[id];

    public static PhraseCatalog LoadEmbedded()
    {
        using var stream = typeof(PhraseCatalog).Assembly.GetManifestResourceStream("phrases.es.json")
            ?? throw new InvalidOperationException("phrases.es.json is not embedded");
        using var reader = new StreamReader(stream);
        return FromJson(reader.ReadToEnd());
    }

    public static PhraseCatalog FromJson(string json, int minPhrases = MinPhrases)
    {
        var phrases = JsonSerializer.Deserialize<List<Phrase>>(json, Options)
            ?? throw new InvalidDataException("The phrase catalog is empty");
        if (phrases.Count < minPhrases)
            throw new InvalidDataException($"The catalog needs at least {minPhrases} phrases");

        var ids = new HashSet<int>();
        var texts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var phrase in phrases)
        {
            if (string.IsNullOrWhiteSpace(phrase.Text) || phrase.Text.Length > MaxLength)
                throw new InvalidDataException($"Phrase {phrase.Id}: text must have 1 to {MaxLength} characters");
            if (!ids.Add(phrase.Id))
                throw new InvalidDataException($"Duplicate phrase id {phrase.Id}");
            if (!texts.Add(phrase.Text.Trim()))
                throw new InvalidDataException($"Duplicate phrase text '{phrase.Text}'");
        }
        return new PhraseCatalog(phrases);
    }
}
