using MusicVault.Services.Search;

namespace MusicVault.Tests.Search;

public sealed class SearchSourceHealthTests
{
    [Fact]
    public void Opens_after_threshold_failures_and_closes_after_cooldown()
    {
        var now = DateTimeOffset.Parse("2026-07-17T05:00:00Z");
        var health = new SearchSourceHealth(
            failureThreshold: 3,
            cooldown: TimeSpan.FromSeconds(30),
            utcNow: () => now);

        Assert.False(health.IsOpen("src.a"));
        health.RecordFailure("src.a");
        health.RecordFailure("src.a");
        Assert.False(health.IsOpen("src.a"));
        health.RecordFailure("src.a");
        Assert.True(health.IsOpen("src.a"));

        now = now.AddSeconds(31);
        Assert.False(health.IsOpen("src.a"));
    }

    [Fact]
    public void Success_resets_failures()
    {
        var health = new SearchSourceHealth(failureThreshold: 2, cooldown: TimeSpan.FromMinutes(1));
        health.RecordFailure("src.b");
        health.RecordSuccess("src.b");
        health.RecordFailure("src.b");
        Assert.False(health.IsOpen("src.b"));
        health.RecordFailure("src.b");
        Assert.True(health.IsOpen("src.b"));
    }

    [Fact]
    public void ApplyOutcome_records_failed_and_timed_out()
    {
        var health = new SearchSourceHealth(failureThreshold: 2);
        var bucket = new SearchSourceExecution.OutcomeBucket();
        bucket.Failed.Add("x");
        health.ApplyOutcome(bucket);
        bucket = new SearchSourceExecution.OutcomeBucket();
        bucket.TimedOut.Add("x");
        health.ApplyOutcome(bucket);
        Assert.True(health.IsOpen("x"));
    }
}
