using MeetingBingo.Core.Games;
using MeetingBingo.Core.Phrases;

namespace MeetingBingo.Core.Rooms;

/// <summary>
/// One live game. Every public method takes the room's lock: operations are short and synchronous,
/// and the caller sends messages to the room after they return, outside the lock.
/// </summary>
public sealed class Room
{
    public const int MaxPlayers = 50;
    public static readonly TimeSpan ConfirmationWindow = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan EmptyTimeout = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan IdleTimeout = TimeSpan.FromHours(3);

    private readonly Lock _gate = new();
    private readonly PhraseCatalog _catalog;
    private readonly Random _random;
    private readonly TimeProvider _time;
    private readonly Dictionary<string, Player> _players = new(StringComparer.Ordinal);
    private readonly Dictionary<int, PhraseState> _phrases = [];
    private int _bingos;
    private bool _closed;

    public Room(string code, PhraseCatalog catalog, Random random, TimeProvider time)
    {
        Code = code;
        _catalog = catalog;
        _random = random;
        _time = time;
        CreatedAt = LastActivityAt = time.GetUtcNow();
        EmptySince = CreatedAt; // nobody has joined yet
    }

    public string Code { get; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset LastActivityAt { get; private set; }
    public DateTimeOffset? EmptySince { get; private set; }

    public JoinOutcome Join(string? token)
    {
        lock (_gate)
        {
            if (_closed) return new(JoinStatus.Closed);
            var now = _time.GetUtcNow();

            if (token is not null && _players.TryGetValue(token, out var existing))
            {
                existing.Connections++;
                Touch(now);
                return new(JoinStatus.Rejoined, Snapshot(existing));
            }

            if (_players.Count >= MaxPlayers) return new(JoinStatus.Full);

            var player = new Player(PlayerToken.New(), PickAlias(), Card.Deal(_catalog, _random)) { Connections = 1 };
            _players.Add(player.Token, player);
            Touch(now);
            return new(JoinStatus.Joined, Snapshot(player));
        }
    }

    /// <summary>Returns the alias when the player's last connection drops; null otherwise.</summary>
    public string? Leave(string token)
    {
        lock (_gate)
        {
            if (!_players.TryGetValue(token, out var player) || player.Connections == 0) return null;
            player.Connections--;
            if (player.Connections > 0) return null;
            if (!_players.Values.Any(p => p.Connections > 0)) EmptySince = _time.GetUtcNow();
            return player.Alias;
        }
    }

    public ToggleOutcome Toggle(string token, int index)
    {
        lock (_gate)
        {
            if (_closed) return ToggleOutcome.Rejected(ToggleStatus.Closed);
            if (!_players.TryGetValue(token, out var player)) return ToggleOutcome.Rejected(ToggleStatus.NotInRoom);
            if (index is < 0 or >= Card.CellCount || index == Card.FreeIndex)
                return ToggleOutcome.Rejected(ToggleStatus.InvalidCell);

            var now = _time.GetUtcNow();
            Touch(now);
            var phraseId = player.Card.Cells[index]!.Value;
            if (!_phrases.TryGetValue(phraseId, out var state)) _phrases[phraseId] = state = new PhraseState();

            if (player.Marked.Remove(index))
            {
                state.CurrentMarks.Remove(player.Token);
                return new(ToggleStatus.Unmarked, ToUpdate(phraseId, state), [], player.Alias);
            }

            player.Marked.Add(index);
            state.CurrentMarks[player.Token] = now;
            state.EverMarkedBy.Add(player.Token);
            // Confirmed when at least two players have it marked within the window; once confirmed, it stays
            if (!state.Confirmed && state.CurrentMarks.Values.Count(at => now - at <= ConfirmationWindow) >= 2)
                state.Confirmed = true;

            var newLines = new List<string>();
            foreach (var line in Card.CompletedLines(player.Marked))
                if (player.AnnouncedLines.Add(line.Name)) newLines.Add(line.Name);
            _bingos += newLines.Count;

            return new(ToggleStatus.Marked, ToUpdate(phraseId, state), newLines, player.Alias);
        }
    }

    /// <summary>Closes the room for good. Returns its summary, or null if nobody ever joined or it was already closed.</summary>
    public GameSummary? Close()
    {
        lock (_gate)
        {
            if (_closed) return null;
            _closed = true;
            if (_players.Count == 0) return null;

            var endedAt = _time.GetUtcNow();
            var tallies = _phrases
                .Where(kv => kv.Value.EverMarkedBy.Count > 0)
                .Select(kv => new PhraseTally(kv.Key, kv.Value.EverMarkedBy.Count, kv.Value.Confirmed))
                .OrderBy(t => t.PhraseId)
                .ToList();
            // The id is the Recorder's idempotency key: a retried write cannot duplicate the game
            return new GameSummary(Guid.CreateVersion7(endedAt), CreatedAt, endedAt, _players.Count, _bingos, tallies);
        }
    }

    public bool IsExpired(DateTimeOffset now)
    {
        lock (_gate)
        {
            return (EmptySince is { } since && now - since >= EmptyTimeout)
                || now - LastActivityAt >= IdleTimeout;
        }
    }

    private void Touch(DateTimeOffset now)
    {
        LastActivityAt = now;
        EmptySince = _players.Values.Any(p => p.Connections > 0) ? null : EmptySince ?? now;
    }

    private string PickAlias()
    {
        var used = _players.Values.Select(p => p.Alias).ToHashSet();
        var free = Aliases.All.Where(a => !used.Contains(a)).ToList();
        return free.Count > 0
            ? free[_random.Next(free.Count)]
            : $"{Aliases.All[_random.Next(Aliases.All.Count)]} {_players.Count + 1}";
    }

    private PlayerSnapshot Snapshot(Player player)
    {
        var cells = player.Card.Cells
            .Select((id, i) => new CellView(i, id, id is { } p ? _catalog.Get(p).Text : null, player.Marked.Contains(i)))
            .ToList();
        var players = _players.Values.Where(p => p.Connections > 0).Select(p => p.Alias).ToList();
        var phrases = player.Card.Cells.OfType<int>()
            .Where(_phrases.ContainsKey)
            .Select(id => ToUpdate(id, _phrases[id]))
            .ToList();
        return new(player.Token, player.Alias, cells, players, phrases, player.AnnouncedLines.ToList());
    }

    private static PhraseUpdate ToUpdate(int phraseId, PhraseState state) =>
        new(phraseId, state.CurrentMarks.Count, state.Confirmed);
}
