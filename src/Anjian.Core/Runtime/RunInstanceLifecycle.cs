namespace Anjian.Core.Runtime;

public enum RunInstanceState
{
    Created,
    Validating,
    Queued,
    Starting,
    Running,
    Paused,
    StopRequested,
    TimeoutRequested,
    Cancelling,
    ForceTerminating,
    CleaningUp,
    Cleaned
}

public enum RunInstanceOutcome
{
    None,
    Succeeded,
    Failed,
    Cancelled,
    TimedOut
}

public enum RunInstanceTerminationMode
{
    None,
    Graceful,
    Forced
}

public enum RunInstanceCleanupStatus
{
    NotStarted,
    InProgress,
    Succeeded,
    Failed
}

public sealed record RunInstanceId
{
    public string Value { get; }

    public RunInstanceId(string value)
    {
        if (!Guid.TryParseExact(value, "N", out var parsed) ||
            !string.Equals(value, parsed.ToString("N"), StringComparison.Ordinal))
            throw new ArgumentException("instanceId 必须是小写 32 位 GUID-N 字符串。", nameof(value));

        Value = value;
    }

    public static RunInstanceId Create() => new(Guid.NewGuid().ToString("N"));

    public override string ToString() => Value;
}

public sealed record RunInstanceSnapshot(
    RunInstanceId InstanceId,
    RunInstanceState State,
    RunInstanceOutcome Outcome,
    RunInstanceTerminationMode TerminationMode,
    RunInstanceCleanupStatus CleanupStatus);

public readonly record struct RunInstanceTransition(
    RunInstanceState From,
    RunInstanceState To);

public static class RunInstanceLimits
{
    // Each active instance owns a separate Worker process and Lua state.
    public const int MaximumActiveInstances = 4;
    public const int MaximumQueuedInstances = 16;
    public const int WorkerStartupTimeoutMilliseconds = 30_000;
    public const int WorkerCancellationGracePeriodMilliseconds = 2_000;
}

public static class RunInstanceLifecycle
{
    private static readonly IReadOnlyList<RunInstanceTransition> TransitionList =
        Array.AsReadOnly(new[]
        {
            new RunInstanceTransition(RunInstanceState.Created, RunInstanceState.Validating),
            new RunInstanceTransition(RunInstanceState.Created, RunInstanceState.StopRequested),

            new RunInstanceTransition(RunInstanceState.Validating, RunInstanceState.Queued),
            new RunInstanceTransition(RunInstanceState.Validating, RunInstanceState.Starting),
            new RunInstanceTransition(RunInstanceState.Validating, RunInstanceState.StopRequested),
            new RunInstanceTransition(RunInstanceState.Validating, RunInstanceState.CleaningUp),

            new RunInstanceTransition(RunInstanceState.Queued, RunInstanceState.Starting),
            new RunInstanceTransition(RunInstanceState.Queued, RunInstanceState.StopRequested),

            new RunInstanceTransition(RunInstanceState.Starting, RunInstanceState.Running),
            new RunInstanceTransition(RunInstanceState.Starting, RunInstanceState.StopRequested),
            new RunInstanceTransition(RunInstanceState.Starting, RunInstanceState.TimeoutRequested),
            new RunInstanceTransition(RunInstanceState.Starting, RunInstanceState.ForceTerminating),
            new RunInstanceTransition(RunInstanceState.Starting, RunInstanceState.CleaningUp),

            new RunInstanceTransition(RunInstanceState.Running, RunInstanceState.Paused),
            new RunInstanceTransition(RunInstanceState.Running, RunInstanceState.StopRequested),
            new RunInstanceTransition(RunInstanceState.Running, RunInstanceState.TimeoutRequested),
            new RunInstanceTransition(RunInstanceState.Running, RunInstanceState.ForceTerminating),
            new RunInstanceTransition(RunInstanceState.Running, RunInstanceState.CleaningUp),

            new RunInstanceTransition(RunInstanceState.Paused, RunInstanceState.Running),
            new RunInstanceTransition(RunInstanceState.Paused, RunInstanceState.StopRequested),
            new RunInstanceTransition(RunInstanceState.Paused, RunInstanceState.TimeoutRequested),
            new RunInstanceTransition(RunInstanceState.Paused, RunInstanceState.ForceTerminating),
            new RunInstanceTransition(RunInstanceState.Paused, RunInstanceState.CleaningUp),

            new RunInstanceTransition(RunInstanceState.StopRequested, RunInstanceState.Cancelling),
            new RunInstanceTransition(RunInstanceState.StopRequested, RunInstanceState.ForceTerminating),

            new RunInstanceTransition(RunInstanceState.TimeoutRequested, RunInstanceState.Cancelling),
            new RunInstanceTransition(RunInstanceState.TimeoutRequested, RunInstanceState.ForceTerminating),

            new RunInstanceTransition(RunInstanceState.Cancelling, RunInstanceState.ForceTerminating),
            new RunInstanceTransition(RunInstanceState.Cancelling, RunInstanceState.CleaningUp),

            new RunInstanceTransition(RunInstanceState.ForceTerminating, RunInstanceState.CleaningUp),

            new RunInstanceTransition(RunInstanceState.CleaningUp, RunInstanceState.Cleaned)
        });

    private static readonly HashSet<RunInstanceTransition> TransitionSet = TransitionList.ToHashSet();

    public static IReadOnlyList<RunInstanceTransition> AllowedTransitions => TransitionList;

    public static RunInstanceSnapshot Create(RunInstanceId? instanceId = null) =>
        new(
            instanceId ?? RunInstanceId.Create(),
            RunInstanceState.Created,
            RunInstanceOutcome.None,
            RunInstanceTerminationMode.None,
            RunInstanceCleanupStatus.NotStarted);

    public static bool CanTransition(RunInstanceState from, RunInstanceState to) =>
        Enum.IsDefined(from) && Enum.IsDefined(to) &&
        TransitionSet.Contains(new RunInstanceTransition(from, to));

    public static bool IsTerminal(RunInstanceSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return snapshot.State == RunInstanceState.Cleaned;
    }

    public static RunInstanceSnapshot Transition(
        RunInstanceSnapshot current,
        RunInstanceState nextState,
        RunInstanceOutcome outcome = RunInstanceOutcome.None,
        RunInstanceCleanupStatus cleanupStatus = RunInstanceCleanupStatus.NotStarted)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (!CanTransition(current.State, nextState))
            throw new InvalidOperationException($"不允许运行实例状态转换：{current.State} -> {nextState}。");

        var nextOutcome = current.Outcome;
        var nextTerminationMode = current.TerminationMode;
        var nextCleanupStatus = current.CleanupStatus;

        switch (nextState)
        {
            case RunInstanceState.StopRequested:
                RequireNoExistingOutcome(current, outcome);
                if (outcome is not (RunInstanceOutcome.None or RunInstanceOutcome.Cancelled))
                    throw new InvalidOperationException("用户停止请求的结果必须是 Cancelled。");
                nextOutcome = RunInstanceOutcome.Cancelled;
                break;

            case RunInstanceState.TimeoutRequested:
                RequireNoExistingOutcome(current, outcome);
                if (outcome is not (RunInstanceOutcome.None or RunInstanceOutcome.TimedOut))
                    throw new InvalidOperationException("超时请求的结果必须是 TimedOut。");
                nextOutcome = RunInstanceOutcome.TimedOut;
                break;

            case RunInstanceState.ForceTerminating:
                SetOutcome(current, outcome, ref nextOutcome);
                if (nextOutcome == RunInstanceOutcome.None)
                    throw new InvalidOperationException("强制终止必须保留或指定一个终止结果。");
                nextTerminationMode = RunInstanceTerminationMode.Forced;
                break;

            case RunInstanceState.CleaningUp:
                SetOutcome(current, outcome, ref nextOutcome);
                if (nextOutcome == RunInstanceOutcome.None)
                    throw new InvalidOperationException("开始清理前必须确定运行结果。");
                ValidateOutcomeForCleanup(current.State, nextOutcome);
                if (cleanupStatus != RunInstanceCleanupStatus.NotStarted)
                    throw new InvalidOperationException("清理开始时不能提前指定最终清理状态。");
                nextCleanupStatus = RunInstanceCleanupStatus.InProgress;
                if (current.State == RunInstanceState.Cancelling &&
                    nextTerminationMode == RunInstanceTerminationMode.None)
                    nextTerminationMode = RunInstanceTerminationMode.Graceful;
                break;

            case RunInstanceState.Cleaned:
                if (outcome != RunInstanceOutcome.None)
                    throw new InvalidOperationException("清理完成不能更改运行结果。");
                if (current.CleanupStatus != RunInstanceCleanupStatus.InProgress ||
                    cleanupStatus is not (RunInstanceCleanupStatus.Succeeded or RunInstanceCleanupStatus.Failed))
                    throw new InvalidOperationException("清理完成必须报告 Succeeded 或 Failed。");
                nextCleanupStatus = cleanupStatus;
                break;

            default:
                if (outcome != RunInstanceOutcome.None ||
                    cleanupStatus != RunInstanceCleanupStatus.NotStarted)
                    throw new InvalidOperationException("该状态转换不接受运行结果或清理结果参数。");
                break;
        }

        return current with
        {
            State = nextState,
            Outcome = nextOutcome,
            TerminationMode = nextTerminationMode,
            CleanupStatus = nextCleanupStatus
        };
    }

    private static void RequireNoExistingOutcome(
        RunInstanceSnapshot current,
        RunInstanceOutcome requestedOutcome)
    {
        if (current.Outcome != RunInstanceOutcome.None ||
            requestedOutcome is not RunInstanceOutcome.None and not RunInstanceOutcome.Cancelled and not RunInstanceOutcome.TimedOut)
            throw new InvalidOperationException("运行实例的首个终止原因一经确定便不可覆盖。");
    }

    private static void SetOutcome(
        RunInstanceSnapshot current,
        RunInstanceOutcome requestedOutcome,
        ref RunInstanceOutcome nextOutcome)
    {
        if (requestedOutcome != RunInstanceOutcome.None)
        {
            if (current.Outcome != RunInstanceOutcome.None && current.Outcome != requestedOutcome)
                throw new InvalidOperationException("运行实例的首个终止原因一经确定便不可覆盖。");
            nextOutcome = requestedOutcome;
        }
    }

    private static void ValidateOutcomeForCleanup(
        RunInstanceState from,
        RunInstanceOutcome outcome)
    {
        var valid = from switch
        {
            RunInstanceState.Validating or RunInstanceState.Starting =>
                outcome == RunInstanceOutcome.Failed,
            RunInstanceState.Running =>
                outcome is RunInstanceOutcome.Succeeded or RunInstanceOutcome.Failed,
            RunInstanceState.Paused =>
                outcome == RunInstanceOutcome.Failed,
            RunInstanceState.Cancelling or RunInstanceState.ForceTerminating =>
                outcome is RunInstanceOutcome.Cancelled or RunInstanceOutcome.TimedOut or RunInstanceOutcome.Failed,
            _ => false
        };

        if (!valid)
            throw new InvalidOperationException($"状态 {from} 不能以结果 {outcome} 直接进入清理。");
    }
}
