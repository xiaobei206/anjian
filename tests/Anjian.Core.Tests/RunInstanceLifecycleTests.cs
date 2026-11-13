using Anjian.Core.Runtime;
using Xunit;

namespace Anjian.Core.Tests;

public sealed class RunInstanceLifecycleTests
{
    [Fact]
    public void CreateGeneratesStableFormatInstanceIdAndInitialSnapshot()
    {
        var first = RunInstanceLifecycle.Create();
        var second = RunInstanceLifecycle.Create();

        Assert.Matches("^[0-9a-f]{32}$", first.InstanceId.Value);
        Assert.NotEqual(first.InstanceId, second.InstanceId);
        Assert.Equal(RunInstanceState.Created, first.State);
        Assert.Equal(RunInstanceOutcome.None, first.Outcome);
        Assert.Equal(RunInstanceTerminationMode.None, first.TerminationMode);
        Assert.Equal(RunInstanceCleanupStatus.NotStarted, first.CleanupStatus);
        Assert.False(RunInstanceLifecycle.IsTerminal(first));
    }

    [Fact]
    public void InstanceIdRejectsNonCanonicalValues()
    {
        Assert.Throws<ArgumentException>(() => new RunInstanceId("not-an-id"));
        Assert.Throws<ArgumentException>(() => new RunInstanceId("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"));
        Assert.Throws<ArgumentException>(() => new RunInstanceId(Guid.NewGuid().ToString("D")));
    }

    [Fact]
    public void TransitionGraphContainsOnlyTheFrozenEdges()
    {
        var expected = new[]
        {
            (RunInstanceState.Created, RunInstanceState.Validating),
            (RunInstanceState.Created, RunInstanceState.StopRequested),
            (RunInstanceState.Validating, RunInstanceState.Queued),
            (RunInstanceState.Validating, RunInstanceState.Starting),
            (RunInstanceState.Validating, RunInstanceState.StopRequested),
            (RunInstanceState.Validating, RunInstanceState.CleaningUp),
            (RunInstanceState.Queued, RunInstanceState.Starting),
            (RunInstanceState.Queued, RunInstanceState.StopRequested),
            (RunInstanceState.Starting, RunInstanceState.Running),
            (RunInstanceState.Starting, RunInstanceState.StopRequested),
            (RunInstanceState.Starting, RunInstanceState.ForceTerminating),
            (RunInstanceState.Starting, RunInstanceState.CleaningUp),
            (RunInstanceState.Running, RunInstanceState.Paused),
            (RunInstanceState.Running, RunInstanceState.StopRequested),
            (RunInstanceState.Running, RunInstanceState.TimeoutRequested),
            (RunInstanceState.Running, RunInstanceState.ForceTerminating),
            (RunInstanceState.Running, RunInstanceState.CleaningUp),
            (RunInstanceState.Paused, RunInstanceState.Running),
            (RunInstanceState.Paused, RunInstanceState.StopRequested),
            (RunInstanceState.Paused, RunInstanceState.TimeoutRequested),
            (RunInstanceState.Paused, RunInstanceState.ForceTerminating),
            (RunInstanceState.Paused, RunInstanceState.CleaningUp),
            (RunInstanceState.StopRequested, RunInstanceState.Cancelling),
            (RunInstanceState.StopRequested, RunInstanceState.ForceTerminating),
            (RunInstanceState.TimeoutRequested, RunInstanceState.Cancelling),
            (RunInstanceState.TimeoutRequested, RunInstanceState.ForceTerminating),
            (RunInstanceState.Cancelling, RunInstanceState.ForceTerminating),
            (RunInstanceState.Cancelling, RunInstanceState.CleaningUp),
            (RunInstanceState.ForceTerminating, RunInstanceState.CleaningUp),
            (RunInstanceState.CleaningUp, RunInstanceState.Cleaned)
        };

        Assert.Equal(expected.OrderBy(x => x.Item1).ThenBy(x => x.Item2),
            RunInstanceLifecycle.AllowedTransitions
                .Select(x => (x.From, x.To))
                .OrderBy(x => x.From)
                .ThenBy(x => x.To));

        var states = Enum.GetValues<RunInstanceState>();
        foreach (var from in states)
        foreach (var to in states)
            Assert.Equal(expected.Contains((from, to)), RunInstanceLifecycle.CanTransition(from, to));
    }

    [Fact]
    public void QueuedSuccessFlowPreservesOutcomeAndRequiresCompletedCleanup()
    {
        var snapshot = RunInstanceLifecycle.Create();
        snapshot = RunInstanceLifecycle.Transition(snapshot, RunInstanceState.Validating);
        snapshot = RunInstanceLifecycle.Transition(snapshot, RunInstanceState.Queued);
        snapshot = RunInstanceLifecycle.Transition(snapshot, RunInstanceState.Starting);
        snapshot = RunInstanceLifecycle.Transition(snapshot, RunInstanceState.Running);
        snapshot = RunInstanceLifecycle.Transition(snapshot, RunInstanceState.Paused);
        snapshot = RunInstanceLifecycle.Transition(snapshot, RunInstanceState.Running);
        snapshot = RunInstanceLifecycle.Transition(snapshot, RunInstanceState.CleaningUp,
            outcome: RunInstanceOutcome.Succeeded);

        Assert.Equal(RunInstanceCleanupStatus.InProgress, snapshot.CleanupStatus);
        Assert.Equal(RunInstanceTerminationMode.Graceful, snapshot.TerminationMode);
        Assert.False(RunInstanceLifecycle.IsTerminal(snapshot));

        snapshot = RunInstanceLifecycle.Transition(snapshot, RunInstanceState.Cleaned,
            cleanupStatus: RunInstanceCleanupStatus.Succeeded);

        Assert.True(RunInstanceLifecycle.IsTerminal(snapshot));
        Assert.Equal(RunInstanceOutcome.Succeeded, snapshot.Outcome);
        Assert.Equal(RunInstanceCleanupStatus.Succeeded, snapshot.CleanupStatus);
    }

    [Fact]
    public void StopAndForcedTerminationRemainBoundToOriginalCancellationOutcome()
    {
        var snapshot = RunInstanceLifecycle.Create();
        snapshot = RunInstanceLifecycle.Transition(snapshot, RunInstanceState.Validating);
        snapshot = RunInstanceLifecycle.Transition(snapshot, RunInstanceState.Starting);
        snapshot = RunInstanceLifecycle.Transition(snapshot, RunInstanceState.Running);
        snapshot = RunInstanceLifecycle.Transition(snapshot, RunInstanceState.StopRequested);
        snapshot = RunInstanceLifecycle.Transition(snapshot, RunInstanceState.Cancelling);
        snapshot = RunInstanceLifecycle.Transition(snapshot, RunInstanceState.ForceTerminating);
        snapshot = RunInstanceLifecycle.Transition(snapshot, RunInstanceState.CleaningUp);
        snapshot = RunInstanceLifecycle.Transition(snapshot, RunInstanceState.Cleaned,
            cleanupStatus: RunInstanceCleanupStatus.Succeeded);

        Assert.Equal(RunInstanceOutcome.Cancelled, snapshot.Outcome);
        Assert.Equal(RunInstanceTerminationMode.Forced, snapshot.TerminationMode);
        Assert.Equal(RunInstanceCleanupStatus.Succeeded, snapshot.CleanupStatus);
    }

    [Fact]
    public void TimeoutFlowIsDistinctFromUserStop()
    {
        var snapshot = RunInstanceLifecycle.Create();
        snapshot = RunInstanceLifecycle.Transition(snapshot, RunInstanceState.Validating);
        snapshot = RunInstanceLifecycle.Transition(snapshot, RunInstanceState.Starting);
        snapshot = RunInstanceLifecycle.Transition(snapshot, RunInstanceState.Running);
        snapshot = RunInstanceLifecycle.Transition(snapshot, RunInstanceState.TimeoutRequested);
        snapshot = RunInstanceLifecycle.Transition(snapshot, RunInstanceState.Cancelling);
        snapshot = RunInstanceLifecycle.Transition(snapshot, RunInstanceState.CleaningUp);
        snapshot = RunInstanceLifecycle.Transition(snapshot, RunInstanceState.Cleaned,
            cleanupStatus: RunInstanceCleanupStatus.Succeeded);

        Assert.Equal(RunInstanceOutcome.TimedOut, snapshot.Outcome);
        Assert.Equal(RunInstanceTerminationMode.Graceful, snapshot.TerminationMode);
    }

    [Fact]
    public void CleanupFailureDoesNotEraseScriptOutcome()
    {
        var snapshot = RunInstanceLifecycle.Create();
        snapshot = RunInstanceLifecycle.Transition(snapshot, RunInstanceState.Validating);
        snapshot = RunInstanceLifecycle.Transition(snapshot, RunInstanceState.Starting);
        snapshot = RunInstanceLifecycle.Transition(snapshot, RunInstanceState.Running);
        snapshot = RunInstanceLifecycle.Transition(snapshot, RunInstanceState.CleaningUp,
            outcome: RunInstanceOutcome.Succeeded);
        snapshot = RunInstanceLifecycle.Transition(snapshot, RunInstanceState.Cleaned,
            cleanupStatus: RunInstanceCleanupStatus.Failed);

        Assert.Equal(RunInstanceOutcome.Succeeded, snapshot.Outcome);
        Assert.Equal(RunInstanceCleanupStatus.Failed, snapshot.CleanupStatus);
        Assert.True(RunInstanceLifecycle.IsTerminal(snapshot));
    }

    [Fact]
    public void RejectsIllegalTransitionsAndAttemptToOverwriteFirstOutcome()
    {
        var snapshot = RunInstanceLifecycle.Create();
        Assert.Throws<InvalidOperationException>(() =>
            RunInstanceLifecycle.Transition(snapshot, RunInstanceState.Running));

        snapshot = RunInstanceLifecycle.Transition(snapshot, RunInstanceState.Validating);
        snapshot = RunInstanceLifecycle.Transition(snapshot, RunInstanceState.Starting);
        snapshot = RunInstanceLifecycle.Transition(snapshot, RunInstanceState.Running);
        snapshot = RunInstanceLifecycle.Transition(snapshot, RunInstanceState.TimeoutRequested);

        Assert.Throws<InvalidOperationException>(() =>
            RunInstanceLifecycle.Transition(snapshot, RunInstanceState.Cancelling,
                outcome: RunInstanceOutcome.Cancelled));
        Assert.Throws<InvalidOperationException>(() =>
            RunInstanceLifecycle.Transition(snapshot, RunInstanceState.ForceTerminating,
                outcome: RunInstanceOutcome.Cancelled));
        Assert.Throws<InvalidOperationException>(() =>
            RunInstanceLifecycle.Transition(snapshot, RunInstanceState.CleaningUp,
                outcome: RunInstanceOutcome.Cancelled));
    }

    [Fact]
    public void RequiresAnOutcomeBeforeCleanupAndAResultAfterCleanup()
    {
        var snapshot = RunInstanceLifecycle.Create();
        snapshot = RunInstanceLifecycle.Transition(snapshot, RunInstanceState.Validating);
        snapshot = RunInstanceLifecycle.Transition(snapshot, RunInstanceState.Starting);
        snapshot = RunInstanceLifecycle.Transition(snapshot, RunInstanceState.Running);

        Assert.Throws<InvalidOperationException>(() =>
            RunInstanceLifecycle.Transition(snapshot, RunInstanceState.CleaningUp));
        snapshot = RunInstanceLifecycle.Transition(snapshot, RunInstanceState.CleaningUp,
            outcome: RunInstanceOutcome.Failed);

        Assert.Throws<InvalidOperationException>(() =>
            RunInstanceLifecycle.Transition(snapshot, RunInstanceState.Cleaned));
        Assert.Throws<InvalidOperationException>(() =>
            RunInstanceLifecycle.Transition(snapshot, RunInstanceState.Cleaned,
                cleanupStatus: RunInstanceCleanupStatus.InProgress));

        snapshot = RunInstanceLifecycle.Transition(snapshot, RunInstanceState.Cleaned,
            cleanupStatus: RunInstanceCleanupStatus.Failed);
        Assert.True(RunInstanceLifecycle.IsTerminal(snapshot));
    }

    [Fact]
    public void ContractFreezesBoundedConcurrencyAndWorkerStopGracePeriod()
    {
        Assert.Equal(4, RunInstanceLimits.MaximumActiveInstances);
        Assert.Equal(16, RunInstanceLimits.MaximumQueuedInstances);
        Assert.Equal(2_000, RunInstanceLimits.WorkerCancellationGracePeriodMilliseconds);
    }
}
