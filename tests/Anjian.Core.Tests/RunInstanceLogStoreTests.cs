using Anjian.Core.Runtime;
using Xunit;

namespace Anjian.Core.Tests;

public sealed class RunInstanceLogStoreTests
{
    [Fact]
    public void AddNormalizesTimestampsToUtcAndEnforcesCapacity()
    {
        var store = new RunInstanceLogStore(capacity: 2);
        var instance = RunInstanceId.Create();
        var timestamp = new DateTimeOffset(2026, 11, 14, 12, 0, 0, TimeSpan.FromHours(9));

        store.Add(new RunInstanceLogEntry(timestamp, RunInstanceLogLevel.Information, "RUN.ONE", instance, null, 1, "one"));
        store.Add(new RunInstanceLogEntry(timestamp.AddSeconds(1), RunInstanceLogLevel.Information, "RUN.TWO", instance, null, 2, "two"));
        store.Add(new RunInstanceLogEntry(timestamp.AddSeconds(2), RunInstanceLogLevel.Warning, "RUN.THREE", instance, null, 3, "three"));

        var entries = store.Query(limit: 2);
        Assert.Equal(2, store.Count);
        Assert.Equal(new[] { "two", "three" }, entries.Select(entry => entry.Message));
        Assert.All(entries, entry => Assert.Equal(TimeSpan.Zero, entry.TimestampUtc.Offset));
    }

    [Fact]
    public void QueryNeverLeaksAnotherInstancesLogsAndSupportsLevelAndTextFilters()
    {
        var store = new RunInstanceLogStore();
        var first = RunInstanceId.Create();
        var second = RunInstanceId.Create();
        store.Add(new RunInstanceLogEntry(DateTimeOffset.UtcNow, RunInstanceLogLevel.Information, "SCRIPT.MESSAGE", first, "attempt-a", 1, "first marker"));
        store.Add(new RunInstanceLogEntry(DateTimeOffset.UtcNow.AddMilliseconds(1), RunInstanceLogLevel.Error, "RUN.ERROR", second, "attempt-b", 1, "second marker"));
        store.Add(new RunInstanceLogEntry(DateTimeOffset.UtcNow.AddMilliseconds(2), RunInstanceLogLevel.Warning, "INPUT.FAULT", null, null, null, "global marker"));

        var firstLogs = store.Query(first, includeGlobal: false);
        Assert.Single(firstLogs);
        Assert.Equal("first marker", firstLogs[0].Message);

        var secondErrors = store.Query(second, RunInstanceLogLevel.Error, "marker", includeGlobal: false);
        Assert.Single(secondErrors);
        Assert.Equal(second, secondErrors[0].InstanceId);

        var selectedWithGlobal = store.Query(first, includeGlobal: true);
        Assert.Equal(2, selectedWithGlobal.Count);
        Assert.Contains(selectedWithGlobal, entry => entry.InstanceId is null);
    }

    [Fact]
    public void SanitizerRedactsLongAndStandaloneShortSensitiveValues()
    {
        var message = RunInstanceLogSanitizer.Sanitize(
            "credential=long-secret; mode pw; keep pwned",
            ["long-secret", "pw"]);

        Assert.DoesNotContain("long-secret", message, StringComparison.Ordinal);
        Assert.Contains("mode " + RunInstanceLogSanitizer.RedactionMarker, message, StringComparison.Ordinal);
        Assert.Contains("keep pwned", message, StringComparison.Ordinal);
    }

    [Fact]
    public void SanitizerRemovesControlCharactersAndTruncatesMessages()
    {
        var message = RunInstanceLogSanitizer.Sanitize("hello\u0000world\nnext", maximumLength: 8);

        Assert.DoesNotContain('\0', message);
        Assert.True(message.Length <= 9);
        Assert.StartsWith("hellowor", message, StringComparison.Ordinal);
    }

    [Fact]
    public void QueryRejectsUnboundedLimits()
    {
        var store = new RunInstanceLogStore();
        Assert.Throws<ArgumentOutOfRangeException>(() => store.Query(limit: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => store.Query(limit: 10_001));
    }
}
