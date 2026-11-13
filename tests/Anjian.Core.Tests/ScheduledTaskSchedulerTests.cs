using System.Text.Json;
using Anjian.Core.Runtime;
using Xunit;

namespace Anjian.Core.Tests;

public sealed class ScheduledTaskSchedulerTests
{
    [Fact]
    public void DelayedAndOneTimeTriggersProduceExactlyOneUtcOccurrence()
    {
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 11, 14, 0, 0, 0, TimeSpan.Zero));
        var engine = new ScheduledTaskEngine(clock);
        var delayed = NewTask(clock, ScheduledTriggerKind.OnceAfterDelay) with
        {
            DelayFromCreation = TimeSpan.FromMinutes(5)
        };
        var once = NewTask(clock, ScheduledTriggerKind.OnceAtUtc) with
        {
            OnceAtUtc = clock.GetUtcNow().AddMinutes(10)
        };
        engine.AddOrUpdate(delayed);
        engine.AddOrUpdate(once);

        clock.Advance(TimeSpan.FromMinutes(5));
        var first = engine.Tick();
        Assert.Single(first.Occurrences);
        Assert.Equal(delayed.TaskId, first.Occurrences[0].Task.TaskId);
        Assert.True(engine.GetCursor(delayed.TaskId)!.Exhausted);

        clock.Advance(TimeSpan.FromMinutes(5));
        var second = engine.Tick();
        Assert.Single(second.Occurrences);
        Assert.Equal(once.TaskId, second.Occurrences[0].Task.TaskId);
        Assert.True(engine.GetCursor(once.TaskId)!.Exhausted);
        Assert.Empty(engine.Tick().Occurrences);
    }

    [Fact]
    public void DailyAndWeeklySchedulesUseConfiguredTimeZoneAndWeekdays()
    {
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 11, 14, 0, 0, 0, TimeSpan.Zero));
        var zone = TimeZoneInfo.FindSystemTimeZoneById("UTC");
        var daily = NewTask(clock, ScheduledTriggerKind.DailyLocalTime) with
        {
            TimeZoneId = zone.Id,
            LocalTime = new TimeOnly(9, 30)
        };
        var weekly = NewTask(clock, ScheduledTriggerKind.WeeklyLocalTime) with
        {
            TimeZoneId = zone.Id,
            LocalTime = new TimeOnly(10, 0),
            DaysOfWeek = [DayOfWeek.Monday, DayOfWeek.Friday]
        };

        var dailyNext = ScheduledTaskCalculator.GetNextOccurrence(daily, clock.GetUtcNow())!.Value;
        var weeklyNext = ScheduledTaskCalculator.GetNextOccurrence(weekly, clock.GetUtcNow())!.Value;

        Assert.Equal(new DateTimeOffset(2026, 11, 14, 9, 30, 0, TimeSpan.Zero), dailyNext);
        Assert.Equal(DayOfWeek.Monday, TimeZoneInfo.ConvertTime(weeklyNext, zone).DayOfWeek);
        Assert.True(weeklyNext > clock.GetUtcNow());
    }

    [Fact]
    public void InvalidDstTimeMovesForwardAndAmbiguousTimeChoosesEarlierUtcInstant()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");

        var invalid = ScheduledTaskCalculator.ResolveLocalTime(
            new DateTime(2026, 3, 8, 2, 30, 0), zone);
        var ambiguous = ScheduledTaskCalculator.ResolveLocalTime(
            new DateTime(2026, 11, 1, 1, 30, 0), zone);

        Assert.Equal(new DateTimeOffset(2026, 3, 8, 10, 0, 0, TimeSpan.Zero), invalid);
        Assert.Equal(new DateTimeOffset(2026, 11, 1, 8, 30, 0, TimeSpan.Zero), ambiguous);
    }

    [Fact]
    public void MisfireSkipAndRunOnceRespectGracePeriod()
    {
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 11, 14, 12, 0, 0, TimeSpan.Zero));
        var due = clock.GetUtcNow().AddMinutes(-10);
        var skip = NewTask(clock, ScheduledTriggerKind.OnceAtUtc) with
        {
            OnceAtUtc = due,
            MisfirePolicy = ScheduledMisfirePolicy.Skip,
            MisfireGracePeriod = TimeSpan.FromHours(1)
        };
        var run = NewTask(clock, ScheduledTriggerKind.OnceAtUtc) with
        {
            OnceAtUtc = due,
            MisfirePolicy = ScheduledMisfirePolicy.RunOnce,
            MisfireGracePeriod = TimeSpan.FromMinutes(15)
        };
        var expired = NewTask(clock, ScheduledTriggerKind.OnceAtUtc) with
        {
            OnceAtUtc = due,
            MisfirePolicy = ScheduledMisfirePolicy.RunOnce,
            MisfireGracePeriod = TimeSpan.FromMinutes(5)
        };
        var engine = new ScheduledTaskEngine(clock);
        engine.AddOrUpdate(skip, new ScheduledTaskCursor(null, due, false));
        engine.AddOrUpdate(run, new ScheduledTaskCursor(null, due, false));
        engine.AddOrUpdate(expired, new ScheduledTaskCursor(null, due, false));

        var result = engine.Tick();

        Assert.Single(result.Occurrences);
        Assert.Equal(run.TaskId, result.Occurrences[0].Task.TaskId);
        Assert.True(result.Occurrences[0].IsMisfire);
        Assert.All(result.ChangedTasks, task => Assert.True(task.Cursor.Exhausted));
    }

    [Fact]
    public void PersistedCursorPreventsReplayingAnOccurrenceAfterRestart()
    {
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 11, 14, 12, 0, 0, TimeSpan.Zero));
        var task = NewTask(clock, ScheduledTriggerKind.DailyLocalTime) with
        {
            TimeZoneId = "UTC",
            LocalTime = new TimeOnly(12, 0),
            MisfirePolicy = ScheduledMisfirePolicy.RunOnce
        };
        var firstEngine = new ScheduledTaskEngine(clock);
        firstEngine.AddOrUpdate(task);
        var fired = firstEngine.Tick();
        Assert.Single(fired.Occurrences);
        var savedSnapshot = fired.ChangedTasks.Single();
        var persistedJson = JsonSerializer.Serialize(savedSnapshot);
        var reloadedSnapshot = JsonSerializer.Deserialize<ScheduledTaskSnapshot>(persistedJson);
        Assert.NotNull(reloadedSnapshot);

        var restartedEngine = new ScheduledTaskEngine(clock);
        restartedEngine.AddOrUpdate(task, reloadedSnapshot.Cursor);
        Assert.Empty(restartedEngine.Tick().Occurrences);
    }

    [Fact]
    public void DisabledScheduleSkipsTimeWhileDisabledAndRecomputesNextOccurrence()
    {
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 11, 14, 8, 0, 0, TimeSpan.Zero));
        var task = NewTask(clock, ScheduledTriggerKind.DailyLocalTime) with
        {
            TimeZoneId = "UTC",
            LocalTime = new TimeOnly(9, 0)
        };
        var engine = new ScheduledTaskEngine(clock);
        engine.AddOrUpdate(task);
        Assert.Equal(new DateTimeOffset(2026, 11, 14, 9, 0, 0, TimeSpan.Zero),
            engine.GetCursor(task.TaskId)!.NextOccurrenceUtc);

        Assert.True(engine.SetEnabled(task.TaskId, false));
        clock.Advance(TimeSpan.FromDays(2));
        Assert.True(engine.SetEnabled(task.TaskId, true));
        Assert.Equal(new DateTimeOffset(2026, 11, 16, 9, 0, 0, TimeSpan.Zero),
            engine.GetCursor(task.TaskId)!.NextOccurrenceUtc);
        Assert.Empty(engine.Tick().Occurrences);
    }

    [Fact]
    public void DisabledCursorSurvivesReloadWithoutReactivatingWhileStillDisabled()
    {
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 11, 14, 8, 0, 0, TimeSpan.Zero));
        var task = NewTask(clock, ScheduledTriggerKind.DailyLocalTime) with
        {
            TimeZoneId = "UTC",
            LocalTime = new TimeOnly(9, 0),
            Enabled = false
        };
        var stored = new ScheduledTaskCursor(null, null, false);
        var engine = new ScheduledTaskEngine(clock);
        engine.AddOrUpdate(task, stored);

        Assert.Null(engine.GetCursor(task.TaskId)!.NextOccurrenceUtc);
        Assert.False(engine.IsEnabled(task.TaskId));
        Assert.True(engine.SetEnabled(task.TaskId, true));
        Assert.NotNull(engine.GetCursor(task.TaskId)!.NextOccurrenceUtc);
        Assert.True(engine.IsEnabled(task.TaskId));
    }

    [Fact]
    public void EditingAndReEnablingOneTimeTaskDoesNotReplayAnExpiredTrigger()
    {
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 11, 14, 12, 0, 0, TimeSpan.Zero));
        var task = NewTask(clock, ScheduledTriggerKind.OnceAtUtc) with
        {
            OnceAtUtc = clock.GetUtcNow().AddMinutes(1)
        };
        var engine = new ScheduledTaskEngine(clock);
        engine.AddOrUpdate(task);
        Assert.True(engine.SetEnabled(task.TaskId, false));
        clock.Advance(TimeSpan.FromMinutes(2));
        Assert.True(engine.SetEnabled(task.TaskId, true));
        Assert.True(engine.GetCursor(task.TaskId)!.Exhausted);
        Assert.Null(engine.GetCursor(task.TaskId)!.NextOccurrenceUtc);
        Assert.Empty(engine.Tick().Occurrences);
    }

    [Fact]
    public void OccurrenceRequestIdsAreStablePerAttemptAndDifferentAcrossRetries()
    {
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 11, 14, 12, 0, 0, TimeSpan.Zero));
        var task = NewTask(clock, ScheduledTriggerKind.OnceAtUtc) with { OnceAtUtc = clock.GetUtcNow() };
        var occurrence = new ScheduledTaskOccurrence(
            task, clock.GetUtcNow(), clock.GetUtcNow(), false, $"{task.TaskId}:001");

        Assert.Equal(occurrence.GetStartRequestId(0), occurrence.GetStartRequestId(0));
        Assert.NotEqual(occurrence.GetStartRequestId(0), occurrence.GetStartRequestId(1));
    }

    [Fact]
    public void EditingCompletedOneTimeTaskToANewTriggerResetsItsCursor()
    {
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 11, 14, 12, 0, 0, TimeSpan.Zero));
        var task = NewTask(clock, ScheduledTriggerKind.OnceAtUtc) with
        {
            OnceAtUtc = clock.GetUtcNow().AddMinutes(1)
        };
        var engine = new ScheduledTaskEngine(clock);
        engine.AddOrUpdate(task);
        clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Single(engine.Tick().Occurrences);
        Assert.True(engine.GetCursor(task.TaskId)!.Exhausted);

        var edited = task with { OnceAtUtc = clock.GetUtcNow().AddHours(1) };
        engine.AddOrUpdate(edited);
        Assert.False(engine.GetCursor(task.TaskId)!.Exhausted);
        Assert.Equal(edited.OnceAtUtc, engine.GetCursor(task.TaskId)!.NextOccurrenceUtc);
    }

    [Fact]
    public void OverlapPoliciesImplementSkipQueueOneCoalescingAndParallel()
    {
        Assert.Equal(ScheduledOverlapDecision.Start,
            ScheduledTaskPolicy.DecideOverlap(ScheduledOverlapPolicy.QueueOne, 0, false));
        Assert.Equal(ScheduledOverlapDecision.Skip,
            ScheduledTaskPolicy.DecideOverlap(ScheduledOverlapPolicy.Skip, 1, false));
        Assert.Equal(ScheduledOverlapDecision.QueueOne,
            ScheduledTaskPolicy.DecideOverlap(ScheduledOverlapPolicy.QueueOne, 1, false));
        Assert.Equal(ScheduledOverlapDecision.KeepExistingPending,
            ScheduledTaskPolicy.DecideOverlap(ScheduledOverlapPolicy.QueueOne, 2, true));
        Assert.Equal(ScheduledOverlapDecision.StartParallel,
            ScheduledTaskPolicy.DecideOverlap(ScheduledOverlapPolicy.Parallel, 4, false));
    }

    [Fact]
    public void FailurePoliciesStopContinueAndRetryWithCappedExponentialBackoff()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UtcNow);
        var stop = NewTask(clock, ScheduledTriggerKind.OnceAtUtc) with
        {
            OnceAtUtc = clock.GetUtcNow(),
            FailurePolicy = ScheduledFailurePolicy.Stop
        };
        var keepGoing = stop with { FailurePolicy = ScheduledFailurePolicy.Continue };
        var retry = stop with
        {
            FailurePolicy = ScheduledFailurePolicy.Retry,
            MaximumRetries = 2,
            RetryDelay = TimeSpan.FromSeconds(5),
            RetryBackoffMultiplier = 2
        };

        Assert.True(ScheduledTaskPolicy.EvaluateFailure(stop, 0, 1).StopSchedule);
        Assert.False(ScheduledTaskPolicy.EvaluateFailure(keepGoing, 0, 1).Retry);
        Assert.False(ScheduledTaskPolicy.EvaluateFailure(keepGoing, 0, 1).StopSchedule);

        var first = ScheduledTaskPolicy.EvaluateFailure(retry, 0, 1);
        var second = ScheduledTaskPolicy.EvaluateFailure(retry, 1, 2);
        var exhausted = ScheduledTaskPolicy.EvaluateFailure(retry, 2, 3);
        var consecutiveLimit = ScheduledTaskPolicy.EvaluateFailure(retry, 0, 5);

        Assert.True(first.Retry);
        Assert.Equal(TimeSpan.FromSeconds(5), first.RetryAfter);
        Assert.Equal(TimeSpan.FromSeconds(10), second.RetryAfter);
        Assert.False(exhausted.Retry);
        Assert.False(exhausted.StopSchedule);
        Assert.True(consecutiveLimit.StopSchedule);
    }

    [Fact]
    public void RuntimeTimeoutStartsOnlyAfterFirstRunningAndIncludesPausedWallClockTime()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UtcNow);
        var tracker = new RunInstanceTimeoutTracker(clock);
        var instance = RunInstanceId.Create();
        var timeout = TimeSpan.FromMinutes(2);

        Assert.False(tracker.IsExpired(instance, timeout));
        Assert.True(tracker.MarkRunning(instance));
        clock.Advance(TimeSpan.FromMinutes(1));
        Assert.False(tracker.IsExpired(instance, timeout));
        Assert.False(tracker.MarkRunning(instance));
        // Advancing the same wall clock while the instance is Paused must still count.
        clock.Advance(TimeSpan.FromMinutes(1));
        Assert.True(tracker.IsExpired(instance, timeout));
        tracker.Remove(instance);
        Assert.False(tracker.IsExpired(instance, timeout));
    }

    [Fact]
    public void StopAndTimeoutRaceUsesTheFirstCommittedLifecycleTransition()
    {
        var stopFirst = NewRunningCoordinator();
        var stopInstance = stopFirst.InstanceId;
        Assert.True(stopFirst.Coordinator.RequestStop(stopInstance).Changed);
        Assert.False(stopFirst.Coordinator.RequestTimeout(stopInstance).Changed);
        Assert.Equal(RunInstanceOutcome.Cancelled, stopFirst.Coordinator.GetSnapshot(stopInstance).Outcome);

        var timeoutFirst = NewRunningCoordinator();
        var timeoutId = timeoutFirst.InstanceId;
        Assert.True(timeoutFirst.Coordinator.RequestTimeout(timeoutId).Changed);
        Assert.False(timeoutFirst.Coordinator.RequestStop(timeoutId).Changed);
        Assert.Equal(RunInstanceOutcome.TimedOut, timeoutFirst.Coordinator.GetSnapshot(timeoutId).Outcome);
    }

    [Fact]
    public void QueueCancellationRemovesWaitingInstanceWithoutStartingIt()
    {
        var coordinator = new RunInstanceCoordinator();
        var admissions = Enumerable.Range(0, 5)
            .Select(_ => coordinator.Admit(Guid.NewGuid().ToString("N"), @"C:\scripts\job.lua"))
            .ToArray();

        var cancelled = coordinator.RequestStop(admissions[4].InstanceId);

        Assert.True(cancelled.Changed);
        Assert.True(cancelled.WasQueued);
        Assert.Equal(RunInstanceState.Cleaned, cancelled.Snapshot.State);
        Assert.Equal(0, coordinator.QueuedCount);
        Assert.Equal(4, coordinator.ActiveCount);
    }

    [Fact]
    public void InvalidScheduleDefinitionsAreRejected()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UtcNow);
        var valid = NewTask(clock, ScheduledTriggerKind.OnceAtUtc) with
        {
            OnceAtUtc = clock.GetUtcNow()
        };

        Assert.Throws<ArgumentException>(() => (valid with { TimeZoneId = "No/Such_Time_Zone" }).Validate());
        Assert.Throws<ArgumentException>(() => (valid with { ScriptPath = "relative.lua" }).Validate());
        Assert.Throws<ArgumentException>(() => (valid with { MaximumRetries = 0, FailurePolicy = ScheduledFailurePolicy.Retry }).Validate());
        Assert.Throws<ArgumentException>(() => (valid with { DaysOfWeek = [DayOfWeek.Monday, DayOfWeek.Monday], TriggerKind = ScheduledTriggerKind.WeeklyLocalTime }).Validate());
    }

    private static ScheduledTaskDefinition NewTask(
        ManualTimeProvider clock,
        ScheduledTriggerKind trigger) => new()
    {
        TaskId = Guid.NewGuid().ToString("N"),
        Name = "test",
        ScriptPath = Path.GetFullPath(@"C:\scripts\test.lua"),
        TriggerKind = trigger,
        CreatedAtUtc = clock.GetUtcNow(),
        TimeZoneId = "UTC"
    };

    private static (RunInstanceCoordinator Coordinator, RunInstanceId InstanceId) NewRunningCoordinator()
    {
        var coordinator = new RunInstanceCoordinator();
        var admission = coordinator.Admit(Guid.NewGuid().ToString("N"), @"C:\scripts\race.lua");
        Assert.True(coordinator.TryMarkRunning(admission.InstanceId, out _));
        return (coordinator, admission.InstanceId);
    }

    private sealed class ManualTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;
        private long _timestamp;

        public override DateTimeOffset GetUtcNow() => _utcNow;
        public override long GetTimestamp() => _timestamp;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public void Advance(TimeSpan value)
        {
            if (value < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(value));
            _utcNow = _utcNow.Add(value);
            _timestamp += value.Ticks;
        }
    }
}
