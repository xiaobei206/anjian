using Anjian.Core.Runtime;
using Xunit;

namespace Anjian.Core.Tests;

public sealed class InputArbiterTests
{
    [Fact]
    public async Task GrantsDifferentOwnersInFifoOrder()
    {
        using var arbiter = new InputArbiter();
        var firstOwner = RunInstanceId.Create();
        var secondOwner = RunInstanceId.Create();
        var thirdOwner = RunInstanceId.Create();

        var first = await arbiter.AcquireAsync(firstOwner);
        var secondTask = arbiter.AcquireAsync(secondOwner).AsTask();
        var thirdTask = arbiter.AcquireAsync(thirdOwner).AsTask();

        Assert.False(secondTask.IsCompleted);
        Assert.False(thirdTask.IsCompleted);
        first.Dispose();

        using var second = await secondTask.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(secondOwner.Value, arbiter.CurrentOwnerId);
        Assert.False(thirdTask.IsCompleted);
        second.Dispose();

        using var third = await thirdTask.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(thirdOwner.Value, arbiter.CurrentOwnerId);
    }

    [Fact]
    public async Task CurrentOwnerCanReenterToReleaseHeldInputAheadOfQueuedOwners()
    {
        using var arbiter = new InputArbiter();
        var owner = RunInstanceId.Create();
        var waitingOwner = RunInstanceId.Create();

        using var heldDownLease = await arbiter.AcquireAsync(owner);
        var waiting = arbiter.AcquireAsync(waitingOwner).AsTask();
        using var releaseLease = await arbiter.AcquireAsync(owner);

        Assert.Equal(owner.Value, releaseLease.OwnerId);
        Assert.False(waiting.IsCompleted);
        releaseLease.Dispose();
        heldDownLease.Dispose();
        using var next = await waiting.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(waitingOwner.Value, next.OwnerId);
    }

    [Fact]
    public async Task CancellingQueuedWaiterDoesNotBlockFollowingOwner()
    {
        using var arbiter = new InputArbiter();
        var firstOwner = RunInstanceId.Create();
        var cancelledOwner = RunInstanceId.Create();
        var nextOwner = RunInstanceId.Create();

        using var first = await arbiter.AcquireAsync(firstOwner);
        using var cancellation = new CancellationTokenSource();
        var cancelled = arbiter.AcquireAsync(cancelledOwner, cancellation.Token).AsTask();
        var nextTask = arbiter.AcquireAsync(nextOwner).AsTask();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await cancelled.WaitAsync(TimeSpan.FromSeconds(2)));
        first.Dispose();
        using var next = await nextTask.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(nextOwner.Value, next.OwnerId);
    }

    [Fact]
    public async Task FaultLocksNewOwnersUntilManualAcknowledgement()
    {
        using var arbiter = new InputArbiter();
        var owner = RunInstanceId.Create();
        var other = RunInstanceId.Create();
        var held = await arbiter.AcquireAsync(owner);

        arbiter.MarkFaulted(owner, "SendInput key-up failed");
        Assert.True(arbiter.IsFaulted);
        Assert.False(arbiter.TryRecoverAfterManualAcknowledgement());
        Assert.Throws<InputArbiterFaultedException>(() =>
            arbiter.AcquireAsync(other).AsTask().GetAwaiter().GetResult());

        held.Dispose();
        Assert.True(arbiter.TryRecoverAfterManualAcknowledgement());
        Assert.False(arbiter.IsFaulted);
        using var recovered = await arbiter.AcquireAsync(other);
        Assert.Equal(other.Value, recovered.OwnerId);
    }

    [Fact]
    public async Task FaultRejectsQueuedWaitersAndExposesReason()
    {
        using var arbiter = new InputArbiter();
        var owner = RunInstanceId.Create();
        var waitingOwner = RunInstanceId.Create();
        using var held = await arbiter.AcquireAsync(owner);
        var queued = arbiter.AcquireAsync(waitingOwner).AsTask();

        arbiter.MarkFaulted(owner, "failed to release mouse-left");
        Assert.Equal("failed to release mouse-left", arbiter.FaultReason);
        await Assert.ThrowsAsync<InputArbiterFaultedException>(async () => await queued);
    }
}
