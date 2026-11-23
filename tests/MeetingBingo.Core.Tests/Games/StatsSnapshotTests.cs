using MeetingBingo.Core.Games;

namespace MeetingBingo.Core.Tests.Games;

public class StatsSnapshotTests
{
    [Fact]
    public void A_new_snapshot_is_empty()
    {
        var snapshot = new StatsSnapshot();

        Assert.Same(WeeklyStats.Empty, snapshot.Current);
        Assert.Null(snapshot.Current.ComputedAt);
        Assert.Empty(snapshot.Current.Top);
    }

    [Fact]
    public void An_update_replaces_the_current_stats()
    {
        var snapshot = new StatsSnapshot();
        var stats = new WeeklyStats(
            new DateTimeOffset(2026, 11, 17, 16, 0, 0, TimeSpan.Zero),
            [new PhraseCount(1, "¿Se me oye?", Marks: 12, Confirmations: 3)]);

        snapshot.Update(stats);

        Assert.Same(stats, snapshot.Current);
    }
}