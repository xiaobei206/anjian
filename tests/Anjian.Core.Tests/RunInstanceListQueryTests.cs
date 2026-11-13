using Anjian.Core.Runtime;
using Xunit;

namespace Anjian.Core.Tests;

public sealed class RunInstanceListQueryTests
{
    [Fact]
    public void FilterMatchesScriptNameOrFullPathWithoutDeduplicatingSameScriptInstances()
    {
        var now = DateTimeOffset.UtcNow;
        var first = Descriptor(@"C:\scripts\repeat.lua", now, RunInstanceState.Running);
        var second = Descriptor(@"C:\scripts\repeat.lua", now.AddSeconds(1), RunInstanceState.Paused);
        var other = Descriptor(@"C:\scripts\other.lua", now.AddSeconds(2), RunInstanceState.Running);

        var result = RunInstanceListQuery.Filter([first, second, other], scriptQuery: "REPEAT");

        Assert.Equal(2, result.Count);
        Assert.Contains(result, item => item.InstanceId == first.InstanceId);
        Assert.Contains(result, item => item.InstanceId == second.InstanceId);
    }

    [Fact]
    public void FilterOrdersNewestFirstAndRetainsAllLiveInstancesPlusRecentCompletedHistory()
    {
        var now = DateTimeOffset.UtcNow;
        var oldCompleted = Descriptor(@"C:\scripts\old.lua", now, RunInstanceState.Cleaned, RunInstanceOutcome.Succeeded);
        var running = Descriptor(@"C:\scripts\active.lua", now.AddMinutes(-4), RunInstanceState.Running);
        var queued = Descriptor(@"C:\scripts\queued.lua", now.AddMinutes(-3), RunInstanceState.Queued);
        var newestCompleted = Descriptor(@"C:\scripts\new.lua", now.AddMinutes(-1), RunInstanceState.Cleaned, RunInstanceOutcome.Failed);

        var result = RunInstanceListQuery.Filter(
            [oldCompleted, running, queued, newestCompleted],
            recentCompletedLimit: 1);

        Assert.Equal(3, result.Count);
        Assert.Equal(newestCompleted.InstanceId, result[0].InstanceId);
        Assert.Equal(queued.InstanceId, result[1].InstanceId);
        Assert.Equal(running.InstanceId, result[2].InstanceId);
        Assert.DoesNotContain(result, item => item.InstanceId == oldCompleted.InstanceId);
    }

    [Fact]
    public void FilterAppliesStatePredicateAndPathQueryTogether()
    {
        var now = DateTimeOffset.UtcNow;
        var running = Descriptor(@"D:\work\alpha.lua", now, RunInstanceState.Running);
        var paused = Descriptor(@"D:\work\alpha.lua", now.AddSeconds(1), RunInstanceState.Paused);
        var other = Descriptor(@"D:\work\beta.lua", now.AddSeconds(2), RunInstanceState.Running);

        var result = RunInstanceListQuery.Filter(
            [running, paused, other],
            scriptQuery: "alpha.lua",
            snapshotPredicate: snapshot => snapshot.State == RunInstanceState.Running);

        Assert.Single(result);
        Assert.Equal(running.InstanceId, result[0].InstanceId);
    }

    [Fact]
    public void FilterRejectsNegativeHistoryLimit()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RunInstanceListQuery.Filter([], recentCompletedLimit: -1));
    }

    private static RunInstanceDescriptor Descriptor(
        string path,
        DateTimeOffset created,
        RunInstanceState state,
        RunInstanceOutcome outcome = RunInstanceOutcome.None)
    {
        var id = RunInstanceId.Create();
        var snapshot = new RunInstanceSnapshot(
            id, state, outcome, RunInstanceTerminationMode.None,
            state == RunInstanceState.Cleaned ? RunInstanceCleanupStatus.Succeeded : RunInstanceCleanupStatus.NotStarted);
        return new RunInstanceDescriptor(id, Guid.NewGuid().ToString("N"), path, created, snapshot);
    }
}
