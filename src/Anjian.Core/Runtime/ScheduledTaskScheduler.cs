using System.Security.Cryptography;
using System.Text;

namespace Anjian.Core.Runtime;

public enum ScheduledTriggerKind
{
    OnceAfterDelay,
    OnceAtUtc,
    DailyLocalTime,
    WeeklyLocalTime
}

public enum ScheduledMisfirePolicy
{
    Skip,
    RunOnce
}

public enum ScheduledOverlapPolicy
{
    Skip,
    QueueOne,
    Parallel
}

public enum ScheduledFailurePolicy
{
    Stop,
    Continue,
    Retry
}

public enum ScheduledOverlapDecision
{
    Start,
    QueueOne,
    Skip,
    StartParallel,
    KeepExistingPending
}

public sealed record ScheduledTaskDefinition
{
    public string TaskId { get; init; } = Guid.NewGuid().ToString("N");
    public string Name { get; init; } = "新建任务";
    public string ScriptPath { get; init; } = "";
    public bool Enabled { get; init; } = true;
    public ScheduledTriggerKind TriggerKind { get; init; } = ScheduledTriggerKind.OnceAtUtc;
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public TimeSpan DelayFromCreation { get; init; } = TimeSpan.FromMinutes(1);
    public DateTimeOffset? OnceAtUtc { get; init; }
    public TimeOnly LocalTime { get; init; } = new(9, 0);
    public string TimeZoneId { get; init; } = TimeZoneInfo.Local.Id;
    public IReadOnlyList<DayOfWeek> DaysOfWeek { get; init; } = Array.Empty<DayOfWeek>();
    public ScheduledMisfirePolicy MisfirePolicy { get; init; } = ScheduledMisfirePolicy.Skip;
    public TimeSpan MisfireGracePeriod { get; init; } = TimeSpan.FromHours(24);
    public ScheduledOverlapPolicy OverlapPolicy { get; init; } = ScheduledOverlapPolicy.Skip;
    public TimeSpan? RuntimeTimeout { get; init; }
    public ScheduledFailurePolicy FailurePolicy { get; init; } = ScheduledFailurePolicy.Stop;
    public int MaximumRetries { get; init; }
    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromSeconds(10);
    public double RetryBackoffMultiplier { get; init; } = 2.0;
    public int MaximumConsecutiveFailures { get; init; } = 5;

    public void Validate()
    {
        if (!Guid.TryParseExact(TaskId, "N", out var taskGuid) ||
            !string.Equals(TaskId, taskGuid.ToString("N"), StringComparison.Ordinal))
            throw new ArgumentException("taskId 必须是小写 32 位 GUID-N 字符串。", nameof(TaskId));
        if (string.IsNullOrWhiteSpace(Name) || Name.Length > 128)
            throw new ArgumentException("任务名称不能为空且最多 128 个字符。", nameof(Name));
        if (string.IsNullOrWhiteSpace(ScriptPath) || !Path.IsPathFullyQualified(ScriptPath))
            throw new ArgumentException("脚本路径必须是非空绝对路径。", nameof(ScriptPath));
        if (DaysOfWeek is null)
            throw new ArgumentException("星期列表不能为空。", nameof(DaysOfWeek));
        if (!Enum.IsDefined(TriggerKind) || !Enum.IsDefined(MisfirePolicy) ||
            !Enum.IsDefined(OverlapPolicy) || !Enum.IsDefined(FailurePolicy))
            throw new ArgumentOutOfRangeException(nameof(TriggerKind), "任务策略包含未知枚举值。");

        try { _ = TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId); }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            throw new ArgumentException($"找不到任务时区 ID：{TimeZoneId}", nameof(TimeZoneId), ex);
        }

        switch (TriggerKind)
        {
            case ScheduledTriggerKind.OnceAfterDelay:
                if (DelayFromCreation < TimeSpan.FromSeconds(1) || DelayFromCreation > TimeSpan.FromDays(30))
                    throw new ArgumentOutOfRangeException(nameof(DelayFromCreation), "延迟启动必须在 1 秒至 30 天之间。");
                break;
            case ScheduledTriggerKind.OnceAtUtc when OnceAtUtc is null:
                throw new ArgumentException("指定时刻任务必须设置 OnceAtUtc。", nameof(OnceAtUtc));
            case ScheduledTriggerKind.DailyLocalTime:
                if (DaysOfWeek.Count != 0)
                    throw new ArgumentException("每日任务不应指定星期列表。", nameof(DaysOfWeek));
                break;
            case ScheduledTriggerKind.WeeklyLocalTime:
                if (DaysOfWeek.Count is < 1 or > 7 || DaysOfWeek.Distinct().Count() != DaysOfWeek.Count ||
                    DaysOfWeek.Any(x => !Enum.IsDefined(x)))
                    throw new ArgumentException("每周任务必须指定 1 至 7 个互不重复的星期。", nameof(DaysOfWeek));
                break;
        }

        if (MisfireGracePeriod < TimeSpan.Zero || MisfireGracePeriod > TimeSpan.FromDays(30))
            throw new ArgumentOutOfRangeException(nameof(MisfireGracePeriod), "错过触发宽限期必须在 0 至 30 天之间。");
        if (RuntimeTimeout is { } timeout && (timeout < TimeSpan.FromSeconds(1) || timeout > TimeSpan.FromDays(7)))
            throw new ArgumentOutOfRangeException(nameof(RuntimeTimeout), "运行超时必须在 1 秒至 7 天之间。");
        if (MaximumRetries is < 0 or > 10)
            throw new ArgumentOutOfRangeException(nameof(MaximumRetries), "单次任务最多允许配置 0 至 10 次重试。");
        if (RetryDelay < TimeSpan.FromSeconds(1) || RetryDelay > TimeSpan.FromHours(1))
            throw new ArgumentOutOfRangeException(nameof(RetryDelay), "重试基础延迟必须在 1 秒至 1 小时之间。");
        if (!double.IsFinite(RetryBackoffMultiplier) || RetryBackoffMultiplier is < 1 or > 10)
            throw new ArgumentOutOfRangeException(nameof(RetryBackoffMultiplier), "重试退避倍数必须在 1 至 10 之间。");
        if (MaximumConsecutiveFailures is < 1 or > 1000)
            throw new ArgumentOutOfRangeException(nameof(MaximumConsecutiveFailures), "连续失败上限必须在 1 至 1000 之间。");
        if (FailurePolicy == ScheduledFailurePolicy.Retry && MaximumRetries == 0)
            throw new ArgumentException("Retry 策略必须设置至少 1 次重试。", nameof(MaximumRetries));
    }
}

public sealed record ScheduledTaskCursor(
    DateTimeOffset? LastHandledOccurrenceUtc,
    DateTimeOffset? NextOccurrenceUtc,
    bool Exhausted,
    int ConsecutiveFailures = 0);

public sealed record ScheduledTaskOccurrence(
    ScheduledTaskDefinition Task,
    DateTimeOffset ScheduledForUtc,
    DateTimeOffset ObservedAtUtc,
    bool IsMisfire,
    string OccurrenceKey)
{
    public string GetStartRequestId(int attemptNumber)
    {
        if (attemptNumber is < 0 or > 10)
            throw new ArgumentOutOfRangeException(nameof(attemptNumber));
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{Task.TaskId}|{OccurrenceKey}|attempt:{attemptNumber}"));
        return new Guid(bytes.AsSpan(0, 16)).ToString("N");
    }
}

public sealed record ScheduledTaskSnapshot(
    ScheduledTaskDefinition Definition,
    ScheduledTaskCursor Cursor);

public sealed record ScheduledTaskTickResult(
    IReadOnlyList<ScheduledTaskOccurrence> Occurrences,
    IReadOnlyList<ScheduledTaskSnapshot> ChangedTasks);

public sealed record ScheduledFailureDecision(
    bool Retry,
    bool StopSchedule,
    TimeSpan? RetryAfter,
    int ConsecutiveFailures);

public static class ScheduledTaskPolicy
{
    public static ScheduledOverlapDecision DecideOverlap(
        ScheduledOverlapPolicy policy,
        int inFlightCount,
        bool hasPendingOne)
    {
        if (inFlightCount < 0)
            throw new ArgumentOutOfRangeException(nameof(inFlightCount));
        if (inFlightCount == 0)
            return ScheduledOverlapDecision.Start;

        return policy switch
        {
            ScheduledOverlapPolicy.Skip => ScheduledOverlapDecision.Skip,
            ScheduledOverlapPolicy.QueueOne when !hasPendingOne => ScheduledOverlapDecision.QueueOne,
            ScheduledOverlapPolicy.QueueOne => ScheduledOverlapDecision.KeepExistingPending,
            ScheduledOverlapPolicy.Parallel => ScheduledOverlapDecision.StartParallel,
            _ => throw new ArgumentOutOfRangeException(nameof(policy))
        };
    }

    public static ScheduledFailureDecision EvaluateFailure(
        ScheduledTaskDefinition task,
        int retriesUsed,
        int consecutiveFailures)
    {
        ArgumentNullException.ThrowIfNull(task);
        task.Validate();
        if (retriesUsed < 0 || retriesUsed > task.MaximumRetries)
            throw new ArgumentOutOfRangeException(nameof(retriesUsed));
        if (consecutiveFailures < 1)
            throw new ArgumentOutOfRangeException(nameof(consecutiveFailures));

        if (task.FailurePolicy == ScheduledFailurePolicy.Stop ||
            consecutiveFailures >= task.MaximumConsecutiveFailures)
            return new ScheduledFailureDecision(false, true, null, consecutiveFailures);

        if (task.FailurePolicy != ScheduledFailurePolicy.Retry || retriesUsed >= task.MaximumRetries)
            return new ScheduledFailureDecision(false, false, null, consecutiveFailures);

        var multiplier = Math.Pow(task.RetryBackoffMultiplier, retriesUsed);
        var delayMilliseconds = Math.Min(
            TimeSpan.FromHours(1).TotalMilliseconds,
            task.RetryDelay.TotalMilliseconds * multiplier);
        return new ScheduledFailureDecision(
            true,
            false,
            TimeSpan.FromMilliseconds(delayMilliseconds),
            consecutiveFailures);
    }
}

/// <summary>
/// Deterministic schedule calculator. Invalid DST local times move forward to the first
/// valid local minute; ambiguous local times select the offset that produces the earlier UTC instant.
/// </summary>
public static class ScheduledTaskCalculator
{
    public static DateTimeOffset? GetNextOccurrence(
        ScheduledTaskDefinition task,
        DateTimeOffset afterUtc)
    {
        ArgumentNullException.ThrowIfNull(task);
        task.Validate();
        afterUtc = afterUtc.ToUniversalTime();

        switch (task.TriggerKind)
        {
            case ScheduledTriggerKind.OnceAfterDelay:
            {
                var due = task.CreatedAtUtc.ToUniversalTime() + task.DelayFromCreation;
                return due > afterUtc ? due : null;
            }
            case ScheduledTriggerKind.OnceAtUtc:
            {
                var due = task.OnceAtUtc!.Value.ToUniversalTime();
                return due > afterUtc ? due : null;
            }
        }

        var zone = TimeZoneInfo.FindSystemTimeZoneById(task.TimeZoneId);
        var localAfter = TimeZoneInfo.ConvertTime(afterUtc, zone).DateTime;
        var firstDate = DateOnly.FromDateTime(localAfter);
        for (var dayOffset = 0; dayOffset <= 14; dayOffset++)
        {
            var date = firstDate.AddDays(dayOffset);
            if (task.TriggerKind == ScheduledTriggerKind.WeeklyLocalTime &&
                !task.DaysOfWeek.Contains(date.DayOfWeek))
                continue;

            var local = date.ToDateTime(task.LocalTime, DateTimeKind.Unspecified);
            var candidate = ResolveLocalTime(local, zone);
            if (candidate > afterUtc)
                return candidate;
        }

        throw new InvalidOperationException("无法在 14 天范围内计算任务下一次触发时间。");
    }

    public static DateTimeOffset ResolveLocalTime(DateTime localTime, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);
        localTime = DateTime.SpecifyKind(localTime, DateTimeKind.Unspecified);

        if (zone.IsInvalidTime(localTime))
        {
            // Move forward to the first valid local minute after a DST gap.
            var limit = localTime.AddHours(3);
            do
            {
                localTime = localTime.AddMinutes(1);
                if (localTime > limit)
                    throw new InvalidOperationException("DST 无效本地时间超过 3 小时，无法安全解析。");
            } while (zone.IsInvalidTime(localTime));
        }

        if (zone.IsAmbiguousTime(localTime))
        {
            var earlierUtcOffset = zone.GetAmbiguousTimeOffsets(localTime).Max();
            return new DateTimeOffset(localTime, earlierUtcOffset).ToUniversalTime();
        }

        var utc = TimeZoneInfo.ConvertTimeToUtc(localTime, zone);
        return new DateTimeOffset(utc, TimeSpan.Zero);
    }
}

/// <summary>
/// In-memory deterministic scheduler. Persist every returned cursor before dispatching
/// its occurrence. Rehydrating that cursor after restart prevents a handled occurrence
/// from being delivered again.
/// </summary>
public sealed class ScheduledTaskEngine
{
    private static readonly TimeSpan NormalDispatchTolerance = TimeSpan.FromMinutes(2);

    private sealed class Entry(ScheduledTaskDefinition definition, ScheduledTaskCursor cursor)
    {
        public ScheduledTaskDefinition Definition { get; set; } = definition;
        public ScheduledTaskCursor Cursor { get; set; } = cursor;
    }

    private readonly object _sync = new();
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly TimeProvider _timeProvider;

    public ScheduledTaskEngine(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public void AddOrUpdate(ScheduledTaskDefinition definition, ScheduledTaskCursor? cursor = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        definition.Validate();
        var now = _timeProvider.GetUtcNow().ToUniversalTime();

        lock (_sync)
        {
            if (_entries.TryGetValue(definition.TaskId, out var existing))
            {
                cursor ??= existing.Cursor;
                var scheduleChanged = existing.Definition.TriggerKind != definition.TriggerKind ||
                    existing.Definition.OnceAtUtc != definition.OnceAtUtc ||
                    ((existing.Definition.TriggerKind == ScheduledTriggerKind.OnceAfterDelay ||
                      definition.TriggerKind == ScheduledTriggerKind.OnceAfterDelay) &&
                     existing.Definition.CreatedAtUtc != definition.CreatedAtUtc) ||
                    existing.Definition.DelayFromCreation != definition.DelayFromCreation ||
                    existing.Definition.LocalTime != definition.LocalTime ||
                    !string.Equals(existing.Definition.TimeZoneId, definition.TimeZoneId, StringComparison.Ordinal) ||
                    !existing.Definition.DaysOfWeek.Order().SequenceEqual(definition.DaysOfWeek.Order());

                if (cursor.Exhausted && scheduleChanged)
                {
                    cursor = cursor with
                    {
                        LastHandledOccurrenceUtc = null,
                        Exhausted = false,
                        ConsecutiveFailures = 0
                    };
                }

                var next = !definition.Enabled || cursor.Exhausted
                    ? null
                    : GetInitialOccurrence(definition, now);
                var exhausted = cursor.Exhausted ||
                    (definition.Enabled &&
                     (definition.TriggerKind is ScheduledTriggerKind.OnceAtUtc or ScheduledTriggerKind.OnceAfterDelay) &&
                     next is null);
                _entries[definition.TaskId] = new Entry(
                    definition,
                    cursor with { NextOccurrenceUtc = next, Exhausted = exhausted });
                return;
            }

            var initialNext = definition.Enabled ? GetInitialOccurrence(definition, now) : null;
            var initialCursor = cursor ?? new ScheduledTaskCursor(
                null,
                initialNext,
                (definition.TriggerKind is ScheduledTriggerKind.OnceAtUtc or ScheduledTriggerKind.OnceAfterDelay) &&
                    definition.Enabled && initialNext is null);
            if (!definition.Enabled)
            {
                initialCursor = initialCursor with { NextOccurrenceUtc = null };
            }
            else if (!initialCursor.Exhausted && initialCursor.NextOccurrenceUtc is null)
            {
                var next = initialCursor.LastHandledOccurrenceUtc is { } lastHandled
                    ? ScheduledTaskCalculator.GetNextOccurrence(definition, lastHandled)
                    : GetInitialOccurrence(definition, now);
                var exhausted = (definition.TriggerKind is
                    ScheduledTriggerKind.OnceAtUtc or ScheduledTriggerKind.OnceAfterDelay) &&
                    next is null;
                initialCursor = initialCursor with
                {
                    NextOccurrenceUtc = next,
                    Exhausted = exhausted
                };
            }
            _entries.Add(definition.TaskId, new Entry(definition, initialCursor));
        }
    }

    private static DateTimeOffset? GetInitialOccurrence(
        ScheduledTaskDefinition definition,
        DateTimeOffset now)
    {
        if (definition.TriggerKind == ScheduledTriggerKind.OnceAtUtc)
            return definition.OnceAtUtc?.ToUniversalTime();
        if (definition.TriggerKind == ScheduledTriggerKind.OnceAfterDelay)
            return definition.CreatedAtUtc.ToUniversalTime() + definition.DelayFromCreation;
        return ScheduledTaskCalculator.GetNextOccurrence(definition, now.AddTicks(-1));
    }

    public bool IsEnabled(string taskId)
    {
        lock (_sync)
            return _entries.TryGetValue(taskId, out var entry) &&
                entry.Definition.Enabled;
    }

    public bool SetEnabled(string taskId, bool enabled)
    {
        lock (_sync)
        {
            if (!_entries.TryGetValue(taskId, out var entry))
                return false;
            entry.Definition = entry.Definition with { Enabled = enabled };
            if (!enabled)
            {
                entry.Cursor = entry.Cursor with { NextOccurrenceUtc = null };
            }
            else if (!entry.Cursor.Exhausted)
            {
                var now = _timeProvider.GetUtcNow().ToUniversalTime();
                var next = ScheduledTaskCalculator.GetNextOccurrence(entry.Definition, now);
                var exhausted = (entry.Definition.TriggerKind is
                    ScheduledTriggerKind.OnceAtUtc or ScheduledTriggerKind.OnceAfterDelay) && next is null;
                entry.Cursor = entry.Cursor with
                {
                    NextOccurrenceUtc = next,
                    Exhausted = exhausted
                };
            }
            return true;
        }
    }

    public bool Remove(string taskId)
    {
        lock (_sync)
            return _entries.Remove(taskId);
    }

    public ScheduledTaskCursor? GetCursor(string taskId)
    {
        lock (_sync)
            return _entries.TryGetValue(taskId, out var entry) ? entry.Cursor : null;
    }

    public IReadOnlyList<ScheduledTaskSnapshot> GetSnapshots()
    {
        lock (_sync)
            return _entries.Values
                .OrderBy(x => x.Definition.Name, StringComparer.OrdinalIgnoreCase)
                .Select(x => new ScheduledTaskSnapshot(x.Definition, x.Cursor))
                .ToArray();
    }

    public void RecordSuccess(string taskId)
    {
        lock (_sync)
        {
            if (_entries.TryGetValue(taskId, out var entry))
                entry.Cursor = entry.Cursor with { ConsecutiveFailures = 0 };
        }
    }

    public ScheduledFailureDecision RecordFailure(string taskId, int retriesUsed)
    {
        lock (_sync)
        {
            var entry = _entries.TryGetValue(taskId, out var found)
                ? found
                : throw new KeyNotFoundException($"定时任务不存在：{taskId}");
            var failures = entry.Cursor.ConsecutiveFailures + 1;
            entry.Cursor = entry.Cursor with { ConsecutiveFailures = failures };
            return ScheduledTaskPolicy.EvaluateFailure(entry.Definition, retriesUsed, failures);
        }
    }

    public ScheduledTaskTickResult Tick()
    {
        var now = _timeProvider.GetUtcNow().ToUniversalTime();
        var occurrences = new List<ScheduledTaskOccurrence>();
        var changed = new List<ScheduledTaskSnapshot>();

        lock (_sync)
        {
            foreach (var entry in _entries.Values)
            {
                var definition = entry.Definition;
                var cursor = entry.Cursor;
                if (!definition.Enabled || cursor.Exhausted ||
                    cursor.NextOccurrenceUtc is not { } firstDue || firstDue > now)
                    continue;

                var latestDue = GetLatestMissedOccurrence(definition, firstDue, now);
                var lateness = now - latestDue;
                var isMisfire = lateness > NormalDispatchTolerance;
                var shouldDispatch = !isMisfire ||
                    (definition.MisfirePolicy == ScheduledMisfirePolicy.RunOnce &&
                     lateness <= definition.MisfireGracePeriod);

                var occurrenceKey =
                    $"{definition.TaskId}:{latestDue.UtcDateTime.Ticks.ToString("x16", System.Globalization.CultureInfo.InvariantCulture)}";
                if (shouldDispatch)
                {
                    occurrences.Add(new ScheduledTaskOccurrence(
                        definition, latestDue, now, isMisfire, occurrenceKey));
                }

                var exhausted = definition.TriggerKind is
                    ScheduledTriggerKind.OnceAtUtc or ScheduledTriggerKind.OnceAfterDelay;
                var next = exhausted
                    ? null
                    : ScheduledTaskCalculator.GetNextOccurrence(definition, now);
                entry.Cursor = cursor with
                {
                    LastHandledOccurrenceUtc = latestDue,
                    NextOccurrenceUtc = next,
                    Exhausted = exhausted
                };
                changed.Add(new ScheduledTaskSnapshot(definition, entry.Cursor));
            }
        }

        return new ScheduledTaskTickResult(occurrences, changed);
    }

    private static DateTimeOffset GetLatestMissedOccurrence(
        ScheduledTaskDefinition definition,
        DateTimeOffset firstDue,
        DateTimeOffset now)
    {
        var latest = firstDue;
        for (var index = 0; index < 10_000; index++)
        {
            var next = ScheduledTaskCalculator.GetNextOccurrence(definition, latest);
            if (next is null || next > now)
                return latest;
            latest = next.Value;
        }

        throw new InvalidOperationException("任务错过的周期数量异常，拒绝无限追赶。");
    }
}

/// <summary>Monotonic timeout tracking; Pause intentionally does not pause elapsed time.</summary>
public sealed class RunInstanceTimeoutTracker
{
    private readonly object _sync = new();
    private readonly Dictionary<RunInstanceId, long> _firstRunningTimestamps = [];
    private readonly TimeProvider _timeProvider;

    public RunInstanceTimeoutTracker(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public bool MarkRunning(RunInstanceId instanceId)
    {
        ArgumentNullException.ThrowIfNull(instanceId);
        lock (_sync)
            return _firstRunningTimestamps.TryAdd(instanceId, _timeProvider.GetTimestamp());
    }

    public bool IsExpired(RunInstanceId instanceId, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(instanceId);
        if (timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout));
        lock (_sync)
        {
            if (!_firstRunningTimestamps.TryGetValue(instanceId, out var started))
                return false;
            return _timeProvider.GetElapsedTime(started, _timeProvider.GetTimestamp()) >= timeout;
        }
    }

    public void Remove(RunInstanceId instanceId)
    {
        ArgumentNullException.ThrowIfNull(instanceId);
        lock (_sync)
            _firstRunningTimestamps.Remove(instanceId);
    }
}
