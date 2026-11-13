namespace Anjian.Core.Runtime;

public sealed record RunInstanceAdmission(
    RunInstanceId InstanceId,
    bool IsDuplicate,
    bool ShouldStartImmediately,
    RunInstanceSnapshot Snapshot);

public sealed record RunInstanceStopResult(
    bool Changed,
    bool WasQueued,
    RunInstanceSnapshot Snapshot);

public sealed record RunInstanceCompletion(
    RunInstanceSnapshot Completed,
    RunInstanceSnapshot? Promoted);

public sealed record RunInstanceDescriptor(
    RunInstanceId InstanceId,
    string StartRequestId,
    string ScriptPath,
    DateTimeOffset CreatedAtUtc,
    RunInstanceSnapshot Snapshot);

public sealed class RunInstanceCapacityException(string message) : InvalidOperationException(message);

/// <summary>
/// Thread-safe admission, idempotency, FIFO queue and lifecycle coordinator.
/// Worker/process execution remains in the App layer.
/// </summary>
public sealed class RunInstanceCoordinator
{
    private sealed class Entry(
        RunInstanceId instanceId,
        string startRequestId,
        string scriptPath,
        DateTimeOffset createdAtUtc,
        RunInstanceSnapshot snapshot)
    {
        public RunInstanceId InstanceId { get; } = instanceId;
        public string StartRequestId { get; } = startRequestId;
        public string ScriptPath { get; } = scriptPath;
        public DateTimeOffset CreatedAtUtc { get; } = createdAtUtc;
        public RunInstanceSnapshot Snapshot { get; set; } = snapshot;
    }

    private readonly object _sync = new();
    private readonly Dictionary<RunInstanceId, Entry> _instances = [];
    private readonly Dictionary<string, RunInstanceId> _requestIds = new(StringComparer.Ordinal);
    private readonly LinkedList<RunInstanceId> _queue = [];

    public int ActiveCount
    {
        get { lock (_sync) return _instances.Values.Count(x => OccupiesWorkerSlot(x.Snapshot.State)); }
    }

    public int QueuedCount
    {
        get { lock (_sync) return _queue.Count; }
    }

    public bool CanAccept
    {
        get
        {
            lock (_sync)
            {
                return _instances.Values.Count(x => OccupiesWorkerSlot(x.Snapshot.State)) <
                           RunInstanceLimits.MaximumActiveInstances ||
                       _queue.Count < RunInstanceLimits.MaximumQueuedInstances;
            }
        }
    }

    public RunInstanceAdmission Admit(
        string startRequestId,
        string scriptPath,
        DateTimeOffset? createdAtUtc = null)
    {
        ValidateRequestId(startRequestId);
        if (string.IsNullOrWhiteSpace(scriptPath))
            throw new ArgumentException("脚本路径不能为空。", nameof(scriptPath));

        lock (_sync)
        {
            if (_requestIds.TryGetValue(startRequestId, out var existingId))
            {
                var existing = _instances[existingId];
                var pathComparison = OperatingSystem.IsWindows()
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal;
                if (!string.Equals(existing.ScriptPath, scriptPath, pathComparison))
                    throw new InvalidOperationException(
                        "相同 startRequestId 不能用于不同脚本路径；请为新的 Start 意图生成新的请求 ID。");
                return new RunInstanceAdmission(existingId, true, false, existing.Snapshot);
            }

            var activeCount = _instances.Values.Count(x => OccupiesWorkerSlot(x.Snapshot.State));
            if (activeCount >= RunInstanceLimits.MaximumActiveInstances &&
                _queue.Count >= RunInstanceLimits.MaximumQueuedInstances)
            {
                throw new RunInstanceCapacityException(
                    $"运行实例容量已满：最多 {RunInstanceLimits.MaximumActiveInstances} 个活动实例，" +
                    $"另有 {RunInstanceLimits.MaximumQueuedInstances} 个排队名额。");
            }

            var snapshot = RunInstanceLifecycle.Create();
            snapshot = RunInstanceLifecycle.Transition(snapshot, RunInstanceState.Validating);
            var shouldStart = activeCount < RunInstanceLimits.MaximumActiveInstances;
            snapshot = RunInstanceLifecycle.Transition(
                snapshot,
                shouldStart ? RunInstanceState.Starting : RunInstanceState.Queued);

            var entry = new Entry(
                snapshot.InstanceId,
                startRequestId,
                scriptPath,
                createdAtUtc ?? DateTimeOffset.UtcNow,
                snapshot);
            _instances.Add(entry.InstanceId, entry);
            _requestIds.Add(startRequestId, entry.InstanceId);
            if (!shouldStart)
                _queue.AddLast(entry.InstanceId);

            return new RunInstanceAdmission(entry.InstanceId, false, shouldStart, snapshot);
        }
    }

    public RunInstanceId GetInstanceIdForRequestId(string startRequestId)
    {
        ValidateRequestId(startRequestId);
        lock (_sync)
            return _requestIds.TryGetValue(startRequestId, out var instanceId)
                ? instanceId
                : throw new KeyNotFoundException($"启动请求不存在：{startRequestId}");
    }

    public RunInstanceSnapshot GetSnapshot(RunInstanceId instanceId)
    {
        ArgumentNullException.ThrowIfNull(instanceId);
        lock (_sync)
            return GetEntry(instanceId).Snapshot;
    }

    public IReadOnlyList<RunInstanceDescriptor> GetInstances()
    {
        lock (_sync)
        {
            return _instances.Values
                .OrderBy(x => x.CreatedAtUtc)
                .Select(x => new RunInstanceDescriptor(
                    x.InstanceId, x.StartRequestId, x.ScriptPath, x.CreatedAtUtc, x.Snapshot))
                .ToArray();
        }
    }

    public bool TryMarkRunning(RunInstanceId instanceId, out RunInstanceSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(instanceId);
        lock (_sync)
        {
            var entry = GetEntry(instanceId);
            if (entry.Snapshot.State != RunInstanceState.Starting)
            {
                snapshot = entry.Snapshot;
                return false;
            }

            entry.Snapshot = RunInstanceLifecycle.Transition(entry.Snapshot, RunInstanceState.Running);
            snapshot = entry.Snapshot;
            return true;
        }
    }

    public bool TryPause(RunInstanceId instanceId, out RunInstanceSnapshot snapshot) =>
        TryTransition(instanceId, RunInstanceState.Running, RunInstanceState.Paused, out snapshot);

    public bool TryResume(RunInstanceId instanceId, out RunInstanceSnapshot snapshot) =>
        TryTransition(instanceId, RunInstanceState.Paused, RunInstanceState.Running, out snapshot);

    public RunInstanceStopResult RequestStop(RunInstanceId instanceId)
    {
        ArgumentNullException.ThrowIfNull(instanceId);
        lock (_sync)
        {
            var entry = GetEntry(instanceId);
            var current = entry.Snapshot;
            if (RunInstanceLifecycle.IsTerminal(current) || current.Outcome != RunInstanceOutcome.None)
                return new RunInstanceStopResult(false, current.State == RunInstanceState.Queued, current);

            if (current.State is not (RunInstanceState.Created or RunInstanceState.Validating or
                RunInstanceState.Queued or RunInstanceState.Starting or RunInstanceState.Running or
                RunInstanceState.Paused))
                return new RunInstanceStopResult(false, false, current);

            var wasQueued = current.State == RunInstanceState.Queued;
            if (wasQueued)
                RemoveQueued(instanceId);

            current = RunInstanceLifecycle.Transition(current, RunInstanceState.StopRequested);
            current = RunInstanceLifecycle.Transition(current, RunInstanceState.Cancelling);
            if (wasQueued)
            {
                current = RunInstanceLifecycle.Transition(current, RunInstanceState.CleaningUp);
                current = RunInstanceLifecycle.Transition(
                    current, RunInstanceState.Cleaned,
                    cleanupStatus: RunInstanceCleanupStatus.Succeeded);
            }

            entry.Snapshot = current;
            return new RunInstanceStopResult(true, wasQueued, current);
        }
    }

    public RunInstanceStopResult RequestTimeout(RunInstanceId instanceId)
    {
        ArgumentNullException.ThrowIfNull(instanceId);
        lock (_sync)
        {
            var entry = GetEntry(instanceId);
            var current = entry.Snapshot;
            if (RunInstanceLifecycle.IsTerminal(current) || current.Outcome != RunInstanceOutcome.None)
                return new RunInstanceStopResult(false, false, current);

            if (current.State is not (RunInstanceState.Starting or RunInstanceState.Running or RunInstanceState.Paused))
                return new RunInstanceStopResult(false, false, current);

            current = RunInstanceLifecycle.Transition(current, RunInstanceState.TimeoutRequested);
            current = RunInstanceLifecycle.Transition(current, RunInstanceState.Cancelling);
            entry.Snapshot = current;
            return new RunInstanceStopResult(true, false, current);
        }
    }

    public RunInstanceSnapshot MarkForceTerminating(RunInstanceId instanceId)
    {
        ArgumentNullException.ThrowIfNull(instanceId);
        lock (_sync)
        {
            var entry = GetEntry(instanceId);
            if (entry.Snapshot.State == RunInstanceState.ForceTerminating)
                return entry.Snapshot;

            if (entry.Snapshot.State is not (RunInstanceState.Cancelling or RunInstanceState.StopRequested or
                RunInstanceState.TimeoutRequested or RunInstanceState.Starting or RunInstanceState.Running or
                RunInstanceState.Paused))
                throw new InvalidOperationException($"实例当前状态 {entry.Snapshot.State} 不能强制终止。");

            if (entry.Snapshot.State is RunInstanceState.StopRequested or RunInstanceState.TimeoutRequested)
                entry.Snapshot = RunInstanceLifecycle.Transition(entry.Snapshot, RunInstanceState.Cancelling);

            var forcedOutcome = entry.Snapshot.Outcome == RunInstanceOutcome.None
                ? RunInstanceOutcome.Failed
                : RunInstanceOutcome.None;
            entry.Snapshot = RunInstanceLifecycle.Transition(
                entry.Snapshot, RunInstanceState.ForceTerminating, outcome: forcedOutcome);
            return entry.Snapshot;
        }
    }

    public RunInstanceCompletion Complete(
        RunInstanceId instanceId,
        RunInstanceOutcome outcome,
        RunInstanceCleanupStatus cleanupStatus)
    {
        ArgumentNullException.ThrowIfNull(instanceId);
        if (outcome == RunInstanceOutcome.None)
            throw new ArgumentOutOfRangeException(nameof(outcome));
        if (cleanupStatus is not (RunInstanceCleanupStatus.Succeeded or RunInstanceCleanupStatus.Failed))
            throw new ArgumentOutOfRangeException(nameof(cleanupStatus));

        lock (_sync)
        {
            var entry = GetEntry(instanceId);
            var current = entry.Snapshot;
            if (RunInstanceLifecycle.IsTerminal(current))
                return new RunInstanceCompletion(current, null);
            if (current.State == RunInstanceState.Queued)
                throw new InvalidOperationException("排队实例必须先取消或晋升，不能直接完成。");

            if (current.State is RunInstanceState.StopRequested or RunInstanceState.TimeoutRequested)
                current = RunInstanceLifecycle.Transition(current, RunInstanceState.Cancelling);

            if (current.State != RunInstanceState.CleaningUp)
            {
                var finalOutcome = current.Outcome == RunInstanceOutcome.None ? outcome : RunInstanceOutcome.None;
                current = RunInstanceLifecycle.Transition(
                    current, RunInstanceState.CleaningUp, outcome: finalOutcome);
            }

            current = RunInstanceLifecycle.Transition(
                current, RunInstanceState.Cleaned, cleanupStatus: cleanupStatus);
            entry.Snapshot = current;

            RunInstanceSnapshot? promoted = null;
            if (_instances.Values.Count(x => OccupiesWorkerSlot(x.Snapshot.State)) <
                RunInstanceLimits.MaximumActiveInstances &&
                _queue.First is { } node)
            {
                _queue.RemoveFirst();
                var next = GetEntry(node.Value);
                next.Snapshot = RunInstanceLifecycle.Transition(
                    next.Snapshot, RunInstanceState.Starting);
                promoted = next.Snapshot;
            }

            return new RunInstanceCompletion(current, promoted);
        }
    }

    private bool TryTransition(
        RunInstanceId instanceId,
        RunInstanceState from,
        RunInstanceState to,
        out RunInstanceSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(instanceId);
        lock (_sync)
        {
            var entry = GetEntry(instanceId);
            if (entry.Snapshot.State != from)
            {
                snapshot = entry.Snapshot;
                return false;
            }

            entry.Snapshot = RunInstanceLifecycle.Transition(entry.Snapshot, to);
            snapshot = entry.Snapshot;
            return true;
        }
    }

    private Entry GetEntry(RunInstanceId instanceId) =>
        _instances.TryGetValue(instanceId, out var entry)
            ? entry
            : throw new KeyNotFoundException($"运行实例不存在：{instanceId.Value}");

    private void RemoveQueued(RunInstanceId instanceId)
    {
        var node = _queue.Find(instanceId);
        if (node is not null)
            _queue.Remove(node);
    }

    private static bool OccupiesWorkerSlot(RunInstanceState state) =>
        state is RunInstanceState.Starting or RunInstanceState.Running or RunInstanceState.Paused or
            RunInstanceState.StopRequested or RunInstanceState.TimeoutRequested or
            RunInstanceState.Cancelling or RunInstanceState.ForceTerminating or RunInstanceState.CleaningUp;

    private static void ValidateRequestId(string value)
    {
        if (!Guid.TryParseExact(value, "N", out var parsed) ||
            !string.Equals(value, parsed.ToString("N"), StringComparison.Ordinal))
            throw new ArgumentException("startRequestId 必须是小写 32 位 GUID-N 字符串。", nameof(value));
    }
}
