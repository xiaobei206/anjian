using Anjian.Core.Runtime;
using Xunit;

namespace Anjian.Core.Tests;

public sealed class RunInstanceCoordinatorTests
{
    [Fact]
    public void DuplicateStartRequestIsIdempotentButSameScriptCanCreateIndependentInstances()
    {
        var coordinator = new RunInstanceCoordinator();
        var requestId = Guid.NewGuid().ToString("N");

        var first = coordinator.Admit(requestId, @"C:\Scripts\same.lua");
        var retry = coordinator.Admit(requestId, @"C:\Scripts\same.lua");
        var secondStart = coordinator.Admit(Guid.NewGuid().ToString("N"), @"C:\Scripts\same.lua");

        Assert.False(first.IsDuplicate);
        Assert.True(first.ShouldStartImmediately);
        Assert.True(retry.IsDuplicate);
        Assert.Equal(first.InstanceId, retry.InstanceId);
        Assert.NotEqual(first.InstanceId, secondStart.InstanceId);
        Assert.Equal(2, coordinator.ActiveCount);
        Assert.Equal(2, coordinator.GetInstances().Count);
    }

    [Fact]
    public void EnforcesFourActiveAndSixteenFifoQueuedEntries()
    {
        var coordinator = new RunInstanceCoordinator();
        var admissions = Enumerable.Range(0, 20)
            .Select(_ => coordinator.Admit(Guid.NewGuid().ToString("N"), @"C:\Scripts\same.lua"))
            .ToArray();

        Assert.Equal(4, admissions.Count(x => x.ShouldStartImmediately));
        Assert.Equal(16, admissions.Count(x => !x.ShouldStartImmediately));
        Assert.Equal(4, coordinator.ActiveCount);
        Assert.Equal(16, coordinator.QueuedCount);
        Assert.False(coordinator.CanAccept);
        Assert.Throws<RunInstanceCapacityException>(() =>
            coordinator.Admit(Guid.NewGuid().ToString("N"), @"C:\Scripts\next.lua"));
    }

    [Fact]
    public void CompletionPromotesExactlyTheOldestQueuedInstance()
    {
        var coordinator = new RunInstanceCoordinator();
        var admissions = Enumerable.Range(0, 6)
            .Select(_ => coordinator.Admit(Guid.NewGuid().ToString("N"), @"C:\Scripts\same.lua"))
            .ToArray();
        var first = admissions[0];

        Assert.True(coordinator.TryMarkRunning(first.InstanceId, out _));
        var completed = coordinator.Complete(
            first.InstanceId, RunInstanceOutcome.Succeeded, RunInstanceCleanupStatus.Succeeded);

        Assert.Equal(RunInstanceState.Cleaned, completed.Completed.State);
        Assert.NotNull(completed.Promoted);
        Assert.Equal(admissions[4].InstanceId, completed.Promoted!.InstanceId);
        Assert.Equal(RunInstanceState.Starting, completed.Promoted.State);
        Assert.Equal(4, coordinator.ActiveCount);
        Assert.Equal(1, coordinator.QueuedCount);
    }

    [Fact]
    public void CancellingQueuedEntryDoesNotConsumeWorkerSlotOrReorderQueue()
    {
        var coordinator = new RunInstanceCoordinator();
        var admissions = Enumerable.Range(0, 7)
            .Select(_ => coordinator.Admit(Guid.NewGuid().ToString("N"), @"C:\Scripts\same.lua"))
            .ToArray();

        var stopped = coordinator.RequestStop(admissions[5].InstanceId);
        Assert.True(stopped.Changed);
        Assert.True(stopped.WasQueued);
        Assert.Equal(RunInstanceState.Cleaned, stopped.Snapshot.State);
        Assert.Equal(RunInstanceOutcome.Cancelled, stopped.Snapshot.Outcome);
        Assert.Equal(4, coordinator.ActiveCount);
        Assert.Equal(2, coordinator.QueuedCount);

        Assert.True(coordinator.TryMarkRunning(admissions[0].InstanceId, out _));
        var completed = coordinator.Complete(
            admissions[0].InstanceId, RunInstanceOutcome.Succeeded, RunInstanceCleanupStatus.Succeeded);
        Assert.Equal(admissions[4].InstanceId, completed.Promoted!.InstanceId);
    }

    [Fact]
    public void StopPauseAndWorkerFailureAreIsolatedPerInstance()
    {
        var coordinator = new RunInstanceCoordinator();
        var first = coordinator.Admit(Guid.NewGuid().ToString("N"), @"C:\Scripts\one.lua");
        var second = coordinator.Admit(Guid.NewGuid().ToString("N"), @"C:\Scripts\two.lua");
        Assert.True(coordinator.TryMarkRunning(first.InstanceId, out _));
        Assert.True(coordinator.TryMarkRunning(second.InstanceId, out _));
        Assert.True(coordinator.TryPause(first.InstanceId, out var paused));
        Assert.Equal(RunInstanceState.Paused, paused.State);

        var stop = coordinator.RequestStop(first.InstanceId);
        Assert.True(stop.Changed);
        Assert.Equal(RunInstanceOutcome.Cancelled, stop.Snapshot.Outcome);

        var failed = coordinator.Complete(
            second.InstanceId, RunInstanceOutcome.Failed, RunInstanceCleanupStatus.Succeeded);
        Assert.Equal(RunInstanceOutcome.Failed, failed.Completed.Outcome);
        Assert.Equal(RunInstanceState.Cancelling, coordinator.GetSnapshot(first.InstanceId).State);
        Assert.Equal(RunInstanceOutcome.Cancelled, coordinator.GetSnapshot(first.InstanceId).Outcome);
    }

    [Fact]
    public void RejectsMalformedRequestIdentityAndKeepsRequestIdStableAfterCompletion()
    {
        var coordinator = new RunInstanceCoordinator();
        Assert.Throws<ArgumentException>(() => coordinator.Admit("not-guid", "script.lua"));
        var requestId = Guid.NewGuid().ToString("N");
        var admission = coordinator.Admit(requestId, "script.lua");
        Assert.True(coordinator.TryMarkRunning(admission.InstanceId, out _));
        coordinator.Complete(admission.InstanceId, RunInstanceOutcome.Succeeded, RunInstanceCleanupStatus.Succeeded);

        var retry = coordinator.Admit(requestId, "script.lua");
        Assert.True(retry.IsDuplicate);
        Assert.Equal(admission.InstanceId, retry.InstanceId);
        Assert.Equal(RunInstanceState.Cleaned, retry.Snapshot.State);
    }
}
