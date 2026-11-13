using System.Text.Json;
using Anjian.Core;
using Anjian.Core.Editor;
using Anjian.Core.Imaging;
using Anjian.Core.Parameters;
using Anjian.Core.Permissions;
using Anjian.Core.Plugins;
using Anjian.Core.Runtime;

namespace Anjian.App.Services;

internal sealed record RunInstanceRuntimeStatus(
    RunInstanceId InstanceId,
    string ScriptPath,
    DateTimeOffset CreatedAtUtc,
    RunInstanceSnapshot Snapshot,
    int CurrentIteration,
    int TotalIterations,
    string? WorkerAttemptId,
    int? WorkerProcessId,
    int? WorkerExitCode,
    TimeSpan? RuntimeTimeout,
    string? LastDiagnostic);

internal sealed class RunInstanceRuntimeCoordinator : IDisposable
{
    private sealed class Entry(
        RunInstanceId instanceId,
        PlaybackRunner runner,
        TaskCompletionSource completion,
        PlaybackOptions options,
        string scriptPath,
        LuaDebugLaunchOptions? debugOptions,
        IReadOnlyDictionary<string, JsonElement> parameters,
        ScriptPermissionGrant permissions,
        SpriteWorkerRuntimeOptions? spriteRuntimeOptions,
        PluginWorkerRuntimeOptions? pluginRuntimeOptions,
        TimeSpan? runtimeTimeout)
    {
        public RunInstanceId InstanceId { get; } = instanceId;
        public TimeSpan? RuntimeTimeout { get; } = runtimeTimeout;
        public CancellationTokenSource TimeoutCancellation { get; } = new();
        public int TimeoutWatchStarted;
        public PlaybackRunner Runner { get; } = runner;
        public TaskCompletionSource Completion { get; } = completion;
        public PlaybackOptions Options { get; } = options;
        public string ScriptPath { get; } = scriptPath;
        public LuaDebugLaunchOptions? DebugOptions { get; } = debugOptions;
        public IReadOnlyDictionary<string, JsonElement> Parameters { get; } = parameters;
        public ScriptPermissionGrant Permissions { get; } = permissions;
        public SpriteWorkerRuntimeOptions? SpriteRuntimeOptions { get; } = spriteRuntimeOptions;
        public PluginWorkerRuntimeOptions? PluginRuntimeOptions { get; } = pluginRuntimeOptions;
        public bool Started { get; set; }
        public int CurrentIteration { get; set; }
        public int TotalIterations { get; set; }
        public string? LastDiagnostic { get; set; }
        public IReadOnlyList<string> SensitiveValues { get; set; } = [];
    }

    private readonly object _sync = new();
    private readonly Anjian.Core.Runtime.RunInstanceCoordinator _model = new();
    private readonly InputArbiter _inputArbiter = new();
    private readonly TimeProvider _timeProvider;
    private readonly RunInstanceTimeoutTracker _timeoutTracker;
    private readonly RunInstanceLogStore _logStore = new();
    private readonly Dictionary<RunInstanceId, Entry> _entries = [];
    private RunInstanceId? _selected;
    private bool _shuttingDown;
    private bool _disposed;

    public RunInstanceRuntimeCoordinator(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
        _timeoutTracker = new RunInstanceTimeoutTracker(_timeProvider);
    }

    public event Action<PlaybackRunner>? RunnerCreated;
    private Control? _uiOwner;

    public event Action<string>? Log;
    public event Action<RunInstanceId, string>? InstanceLog;
    public event Action<RunInstanceLogEntry>? StructuredLogAdded;
    public event Action<RunInstanceSnapshot>? StateChanged;
    public event Action<int, int>? IterationChanged;
    public event Action<LuaDebugSnapshot>? DebugPaused;
    public event Action? DebugSessionCompleted;
    public event Action<ImageVisualDebugEvent>? VisualDebugUpdated;
    public bool IsRunning => SelectedIsRunning;
    public bool IsPaused => SelectedIsPaused;
    public bool IsDebugging => SelectedIsDebugging;

    public void SetUiOwner(Control owner)
    {
        _uiOwner = owner ?? throw new ArgumentNullException(nameof(owner));
    }

    public Task RunAsync(
        string scriptPath,
        PlaybackOptions options,
        LuaDebugLaunchOptions? debugOptions = null,
        IReadOnlyDictionary<string, JsonElement>? parameters = null,
        SpriteWorkerRuntimeOptions? spriteRuntimeOptions = null,
        ScriptPermissionGrant? permissions = null,
        PluginWorkerRuntimeOptions? pluginRuntimeOptions = null,
        TimeSpan? runtimeTimeout = null) =>
        StartAsync(
            Guid.NewGuid().ToString("N"),
            scriptPath,
            options,
            debugOptions,
            parameters,
            permissions,
            spriteRuntimeOptions,
            pluginRuntimeOptions,
            runtimeTimeout);

    public void Pause() => PauseSelected();
    public void Resume() => ResumeSelected();
    public void Stop() => StopSelected();

    public int ActiveCount => _model.ActiveCount;
    public int QueuedCount => _model.QueuedCount;
    public bool InputFaulted => _inputArbiter.IsFaulted;
    public string? InputFaultReason => _inputArbiter.FaultReason;
    public bool CanAccept
    {
        get { lock (_sync) return !_shuttingDown && !_disposed && !_inputArbiter.IsFaulted && _model.CanAccept; }
    }
    public bool HasActiveInstances => ActiveCount > 0;
    public RunInstanceId? SelectedInstanceId
    {
        get
        {
            lock (_sync)
            {
                SelectLatestRunnableLocked();
                return _selected;
            }
        }
    }

    public PlaybackRunner? SelectedRunner
    {
        get
        {
            lock (_sync)
            {
                SelectLatestRunnableLocked();
                return _selected is not null && _entries.TryGetValue(_selected, out var entry)
                    ? entry.Runner : null;
            }
        }
    }

    public bool SelectedIsRunning => SelectedSnapshot()?.State is
        RunInstanceState.Queued or RunInstanceState.Starting or RunInstanceState.Running or RunInstanceState.Paused or
        RunInstanceState.StopRequested or RunInstanceState.TimeoutRequested or
        RunInstanceState.Cancelling or RunInstanceState.ForceTerminating or RunInstanceState.CleaningUp;

    public bool SelectedIsPaused => SelectedSnapshot()?.State == RunInstanceState.Paused;
    public bool SelectedCanPause => SelectedSnapshot()?.State is RunInstanceState.Running or RunInstanceState.Paused;
    public bool SelectedIsDebugging => SelectedRunner?.IsDebugging == true;
    public bool HasDebuggingInstances
    {
        get
        {
            lock (_sync)
                return _entries.Values.Any(entry => entry.Runner.IsDebugging &&
                    !RunInstanceLifecycle.IsTerminal(_model.GetSnapshot(entry.InstanceId)));
        }
    }
    public bool VisualDebugEnabled
    {
        get => SelectedRunner?.VisualDebugEnabled == true;
        set
        {
            var runner = SelectedRunner;
            if (runner is not null)
                runner.VisualDebugEnabled = value;
        }
    }

    public Task StartAsync(
        string startRequestId,
        string scriptPath,
        PlaybackOptions options,
        LuaDebugLaunchOptions? debugOptions = null,
        IReadOnlyDictionary<string, JsonElement>? parameters = null,
        ScriptPermissionGrant? permissions = null,
        SpriteWorkerRuntimeOptions? spriteRuntimeOptions = null,
        PluginWorkerRuntimeOptions? pluginRuntimeOptions = null,
        TimeSpan? runtimeTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        if (runtimeTimeout is { } timeout &&
            (timeout < TimeSpan.FromSeconds(1) || timeout > TimeSpan.FromDays(7)))
            throw new ArgumentOutOfRangeException(nameof(runtimeTimeout), "实例运行超时必须在 1 秒至 7 天之间。");
        if (debugOptions is not null && (options.RepeatCount != 1 || options.IntervalMilliseconds != 0))
            throw new ArgumentException("调试运行固定执行单轮，不支持循环次数和轮间隔。", nameof(options));
        if (spriteRuntimeOptions is not null && pluginRuntimeOptions is not null)
            throw new ArgumentException("小精灵运行参数与插件运行参数不能同时使用。");

        var fullPath = Path.GetFullPath(scriptPath);
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_shuttingDown)
                throw new InvalidOperationException("应用正在退出，拒绝新的运行实例。");

            var admission = _model.Admit(startRequestId, fullPath);
            if (admission.IsDuplicate)
            {
                if (_entries.TryGetValue(admission.InstanceId, out var existing))
                    return existing.Completion.Task;
                throw new InvalidOperationException("重复启动请求已完成但实例记录不可用。");
            }

            var runner = new PlaybackRunner(admission.InstanceId, _inputArbiter);
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var entry = new Entry(
                admission.InstanceId,
                runner,
                completion,
                options with { },
                fullPath,
                debugOptions,
                CloneParameters(parameters),
                CloneGrant(permissions ?? ScriptPermissionGrant.Empty),
                spriteRuntimeOptions,
                ClonePluginRuntimeOptions(pluginRuntimeOptions),
                runtimeTimeout);
            entry.SensitiveValues = GetSensitiveParameterValues(fullPath, entry.Parameters);
            entry.TotalIterations = options.RepeatCount;
            _entries.Add(entry.InstanceId, entry);
            _selected = entry.InstanceId;
            WireRunner(entry);
            if (_uiOwner is not null)
                runner.SetUiOwner(_uiOwner);
            RunnerCreated?.Invoke(runner);
            StateChanged?.Invoke(admission.Snapshot);

            if (admission.ShouldStartImmediately)
                _ = StartEntryAsync(entry);
            else
                PublishLog(entry.InstanceId, $"已进入 FIFO 等待队列（前方 {QueuedCount - 1} 个）。");

            return completion.Task;
        }
    }

    public async Task<RunInstanceSnapshot> StartAndWaitAsync(
        string startRequestId,
        string scriptPath,
        PlaybackOptions options,
        TimeSpan? runtimeTimeout,
        IReadOnlyDictionary<string, JsonElement>? parameters = null,
        ScriptPermissionGrant? permissions = null,
        CancellationToken cancellationToken = default)
    {
        var execution = StartAsync(
            startRequestId,
            scriptPath,
            options,
            parameters: parameters,
            permissions: permissions,
            runtimeTimeout: runtimeTimeout);
        RunInstanceId instanceId;
        lock (_sync)
            instanceId = _model.GetInstanceIdForRequestId(startRequestId);

        await execution.WaitAsync(cancellationToken).ConfigureAwait(false);
        return _model.GetSnapshot(instanceId);
    }

    public void PauseSelected()
    {
        RunInstanceSnapshot? snapshot = null;
        lock (_sync)
        {
            var entry = GetSelectedEntryLocked();
            if (entry is null || !_model.TryPause(entry.InstanceId, out snapshot))
                return;
            entry.Runner.Pause();
        }
        StateChanged?.Invoke(snapshot);
    }

    public void ResumeSelected()
    {
        RunInstanceSnapshot? snapshot = null;
        lock (_sync)
        {
            var entry = GetSelectedEntryLocked();
            if (entry is null || !_model.TryResume(entry.InstanceId, out snapshot))
                return;
            entry.Runner.Resume();
        }
        StateChanged?.Invoke(snapshot);
    }

    public void SubmitDebugCommand(LuaDebugCommand command) =>
        SelectedRunner?.SubmitDebugCommand(command);

    public void StopSelected()
    {
        RunInstanceStopResult? stop = null;
        lock (_sync)
        {
            var entry = GetSelectedEntryLocked();
            if (entry is null)
                return;

            stop = _model.RequestStop(entry.InstanceId);
            if (!stop.Changed)
                return;

            if (stop.WasQueued)
            {
                entry.TimeoutCancellation.Cancel();
                _timeoutTracker.Remove(entry.InstanceId);
                entry.Runner.Dispose();
                entry.Completion.TrySetResult();
            }
            else
            {
                entry.TimeoutCancellation.Cancel();
                _timeoutTracker.Remove(entry.InstanceId);
                entry.Runner.Stop();
            }
        }
        StateChanged?.Invoke(stop.Snapshot);
    }

    public async Task StopSelectedAsync()
    {
        Entry? entry;
        lock (_sync)
            entry = GetSelectedEntryLocked();
        if (entry is null)
            return;
        StopEntry(entry);
        await entry.Completion.Task.ConfigureAwait(true);
    }

    public async Task StopAllAsync()
    {
        var notifications = new List<RunInstanceSnapshot>();
        Task[] waits;
        lock (_sync)
        {
            _shuttingDown = true;
            foreach (var entry in _entries.Values)
            {
                var state = _model.GetSnapshot(entry.InstanceId);
                if (RunInstanceLifecycle.IsTerminal(state))
                    continue;

                var stop = _model.RequestStop(entry.InstanceId);
                if (stop.Changed)
                {
                    notifications.Add(stop.Snapshot);
                    if (stop.WasQueued)
                    {
                        entry.TimeoutCancellation.Cancel();
                        _timeoutTracker.Remove(entry.InstanceId);
                        entry.Runner.Dispose();
                        entry.Completion.TrySetResult();
                    }
                    else
                    {
                        entry.TimeoutCancellation.Cancel();
                        _timeoutTracker.Remove(entry.InstanceId);
                        entry.Runner.Stop();
                    }
                }
            }

            waits = _entries.Values.Select(entry => entry.Completion.Task).ToArray();
        }

        foreach (var snapshot in notifications)
            StateChanged?.Invoke(snapshot);

        await Task.WhenAll(waits).ConfigureAwait(true);
    }

    public IReadOnlyList<RunInstanceDescriptor> GetInstances() => _model.GetInstances();

    public IReadOnlyList<RunInstanceRuntimeStatus> GetInstanceStatuses()
    {
        lock (_sync)
        {
            var statuses = _model.GetInstances()
                .Select(descriptor =>
                {
                    if (!_entries.TryGetValue(descriptor.InstanceId, out var entry))
                        return new RunInstanceRuntimeStatus(
                            descriptor.InstanceId, descriptor.ScriptPath, descriptor.CreatedAtUtc,
                            descriptor.Snapshot, 0, 0, null, null, null, null, null);

                    var snapshot = descriptor.Snapshot;
                    var diagnostic = entry.LastDiagnostic;
                    if (snapshot.CleanupStatus == RunInstanceCleanupStatus.Failed)
                        diagnostic = "实例清理失败：全局键鼠状态仍需人工核验；该实例的 CleanupStatus 保持 Failed。";
                    else if (snapshot.TerminationMode == RunInstanceTerminationMode.Forced)
                        diagnostic ??= "Worker 被强制终止，建议检查目标应用与该实例的执行日志。";
                    else if (snapshot.Outcome == RunInstanceOutcome.TimedOut)
                        diagnostic ??= "实例达到配置的运行超时。";
                    else if (snapshot.Outcome == RunInstanceOutcome.Failed)
                        diagnostic ??= "实例失败；请查看该实例日志。";

                    return new RunInstanceRuntimeStatus(
                        entry.InstanceId,
                        entry.ScriptPath,
                        descriptor.CreatedAtUtc,
                        snapshot,
                        entry.CurrentIteration,
                        entry.TotalIterations,
                        entry.Runner.LastWorkerAttemptId,
                        entry.Runner.LastWorkerProcessId,
                        entry.Runner.LastWorkerExitCode,
                        entry.RuntimeTimeout,
                        diagnostic);
                })
                .ToArray();
            return statuses
                .Where(status => !RunInstanceLifecycle.IsTerminal(status.Snapshot))
                .Concat(statuses
                    .Where(status => RunInstanceLifecycle.IsTerminal(status.Snapshot))
                    .OrderByDescending(status => status.CreatedAtUtc)
                    .Take(500))
                .OrderByDescending(status => status.CreatedAtUtc)
                .ToArray();
        }
    }

    public IReadOnlyList<RunInstanceLogEntry> GetLogs(
        RunInstanceId? instanceId = null,
        RunInstanceLogLevel? level = null,
        string? text = null,
        int limit = 1_000,
        bool includeGlobal = true) =>
        _logStore.Query(instanceId, level, text, limit, includeGlobal);

    public void RecordGlobalLog(
        string message,
        RunInstanceLogLevel level = RunInstanceLogLevel.Information,
        string eventCode = "APP.EVENT")
    {
        var safeMessage = RunInstanceLogSanitizer.Sanitize(message);
        var entry = new RunInstanceLogEntry(
            _timeProvider.GetUtcNow(), level, eventCode, null, null, null, safeMessage);
        _logStore.Add(entry);
        StructuredLogAdded?.Invoke(entry);
        Log?.Invoke(safeMessage);
    }

    public bool PauseInstance(RunInstanceId instanceId)
    {
        ArgumentNullException.ThrowIfNull(instanceId);
        RunInstanceSnapshot snapshot;
        lock (_sync)
        {
            if (!_entries.TryGetValue(instanceId, out var entry) ||
                !_model.TryPause(instanceId, out snapshot))
                return false;
            entry.Runner.Pause();
        }
        StateChanged?.Invoke(snapshot);
        return true;
    }

    public bool ResumeInstance(RunInstanceId instanceId)
    {
        ArgumentNullException.ThrowIfNull(instanceId);
        RunInstanceSnapshot snapshot;
        lock (_sync)
        {
            if (!_entries.TryGetValue(instanceId, out var entry) ||
                !_model.TryResume(instanceId, out snapshot))
                return false;
            entry.Runner.Resume();
        }
        StateChanged?.Invoke(snapshot);
        return true;
    }

    public async Task StopInstanceAsync(RunInstanceId instanceId)
    {
        ArgumentNullException.ThrowIfNull(instanceId);
        Entry? entry;
        lock (_sync)
            _entries.TryGetValue(instanceId, out entry);
        if (entry is null)
            return;

        StopEntry(entry);
        await entry.Completion.Task.ConfigureAwait(true);
    }

    public bool TryRecoverInputAfterManualAcknowledgement()
    {
        var recovered = _inputArbiter.TryRecoverAfterManualAcknowledgement();
        if (recovered)
            RecordGlobalLog(
                "用户明确确认已人工核验物理键鼠状态；全局 InputArbiter 锁定已解除。此前 CleanupStatus=Failed 的实例记录保持不变。",
                RunInstanceLogLevel.Warning,
                "INPUT.RECOVERY_CONFIRMED");
        return recovered;
    }

    private void WireRunner(Entry entry)
    {
        var runner = entry.Runner;
        runner.Log += message => PublishLog(entry.InstanceId, message);
        runner.IterationChanged += (current, total) =>
        {
            RunInstanceSnapshot snapshot;
            lock (_sync)
            {
                entry.CurrentIteration = current;
                entry.TotalIterations = total;
                snapshot = _model.GetSnapshot(entry.InstanceId);
            }
            if (SelectedInstanceId == entry.InstanceId)
                IterationChanged?.Invoke(current, total);
        };
        runner.DebugPaused += snapshot =>
        {
            if (SelectedInstanceId == entry.InstanceId)
                DebugPaused?.Invoke(snapshot);
        };
        runner.DebugSessionCompleted += () =>
        {
            if (SelectedInstanceId == entry.InstanceId)
                DebugSessionCompleted?.Invoke();
        };
        runner.VisualDebugUpdated += value =>
        {
            if (SelectedInstanceId == entry.InstanceId)
                VisualDebugUpdated?.Invoke(value);
        };
        runner.WorkerReady += () =>
        {
            RunInstanceSnapshot? snapshot = null;
            var startTimeoutWatch = false;
            lock (_sync)
            {
                if (_model.TryMarkRunning(entry.InstanceId, out var current))
                {
                    snapshot = current;
                    _timeoutTracker.MarkRunning(entry.InstanceId);
                    startTimeoutWatch = entry.RuntimeTimeout is not null &&
                        Interlocked.CompareExchange(ref entry.TimeoutWatchStarted, 1, 0) == 0;
                }
            }
            if (snapshot is not null)
                StateChanged?.Invoke(snapshot);
            if (startTimeoutWatch)
                _ = WatchRuntimeTimeoutAsync(entry);
        };
        runner.WorkerForceTerminating += () =>
        {
            try
            {
                RunInstanceSnapshot snapshot;
                lock (_sync)
                    snapshot = _model.MarkForceTerminating(entry.InstanceId);
                PublishLog(entry.InstanceId, "Worker 超过正常退出宽限，已强制终止；请检查目标应用与该实例诊断。");
                StateChanged?.Invoke(snapshot);
            }
            catch (Exception ex)
            {
                PublishLog(entry.InstanceId, "更新强制终止状态失败：" + ex.Message);
            }
        };
    }

    private async Task WatchRuntimeTimeoutAsync(Entry entry)
    {
        if (entry.RuntimeTimeout is not { } timeout)
            return;

        try
        {
            await Task.Delay(timeout, _timeProvider, entry.TimeoutCancellation.Token).ConfigureAwait(false);
            if (!_timeoutTracker.IsExpired(entry.InstanceId, timeout))
                return;

            RunInstanceStopResult? requested = null;
            lock (_sync)
            {
                var current = _model.GetSnapshot(entry.InstanceId);
                if (current.State is RunInstanceState.Running or RunInstanceState.Paused)
                {
                    requested = _model.RequestTimeout(entry.InstanceId);
                    if (requested.Changed)
                        entry.Runner.Stop();
                }
            }

            if (requested is { Changed: true })
            {
                PublishLog(entry.InstanceId, $"达到运行超时 {timeout}，已请求停止并清理本实例。");
                StateChanged?.Invoke(requested.Snapshot);
            }
        }
        catch (OperationCanceledException) when (entry.TimeoutCancellation.IsCancellationRequested)
        {
            // 正常完成或用户 Stop 已赢得生命周期竞争。
        }
        catch (Exception ex)
        {
            PublishLog(entry.InstanceId, "运行超时监视器异常：" + ex);
        }
    }

    private async Task StartEntryAsync(Entry entry)
    {
        Task execution;
        lock (_sync)
        {
            if (entry.Started || _disposed)
                return;

            if (_shuttingDown)
            {
                var stop = _model.RequestStop(entry.InstanceId);
                StateChanged?.Invoke(stop.Snapshot);
                entry.TimeoutCancellation.Cancel();
                _timeoutTracker.Remove(entry.InstanceId);
                entry.Runner.Dispose();
                entry.Completion.TrySetResult();
                return;
            }

            entry.Started = true;
            // Start the runner while holding the admission lock. StopAllAsync therefore
            // either observes a started Worker and cancels it, or prevents a promoted entry
            // from spawning any process after shutdown has been accepted.
            execution = entry.Runner.RunAsync(
                entry.ScriptPath,
                entry.Options,
                entry.DebugOptions,
                entry.Parameters,
                entry.SpriteRuntimeOptions,
                entry.Permissions,
                entry.PluginRuntimeOptions);
        }

        Exception? failure = null;
        try
        {
            await execution.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            failure = ex;
            if (ex is TimeoutException)
            {
                RunInstanceStopResult? timeout = null;
                lock (_sync)
                {
                    var current = _model.GetSnapshot(entry.InstanceId);
                    if (current.State is RunInstanceState.Starting or RunInstanceState.Running or RunInstanceState.Paused)
                        timeout = _model.RequestTimeout(entry.InstanceId);
                }
                if (timeout is { Changed: true })
                    StateChanged?.Invoke(timeout.Snapshot);
            }
            PublishLog(entry.InstanceId, "实例运行失败：" + ex);
        }
        finally
        {
            entry.TimeoutCancellation.Cancel();
            _timeoutTracker.Remove(entry.InstanceId);
            try { entry.Runner.Dispose(); }
            catch (Exception ex) { PublishLog(entry.InstanceId, "实例释放资源失败：" + ex.Message); }

            Entry? promoted = null;
            RunInstanceCompletion? completed = null;
            try
            {
                lock (_sync)
                {
                    var current = _model.GetSnapshot(entry.InstanceId);
                    var outcome = current.Outcome != RunInstanceOutcome.None
                        ? current.Outcome
                        : failure is null ? RunInstanceOutcome.Succeeded : RunInstanceOutcome.Failed;
                    var cleanup = entry.Runner.LastCleanupSucceeded
                        ? RunInstanceCleanupStatus.Succeeded
                        : RunInstanceCleanupStatus.Failed;
                    completed = _model.Complete(entry.InstanceId, outcome, cleanup);
                    if (completed.Promoted is not null)
                        _entries.TryGetValue(completed.Promoted.InstanceId, out promoted);
                    entry.Completion.TrySetResult();
                }

                if (completed.Completed.CleanupStatus == RunInstanceCleanupStatus.Failed)
                    PublishLog(entry.InstanceId, "严重：实例输入清理失败；全局 InputArbiter 已锁定，必须人工检查物理键鼠状态。此前清理失败记录不会因人工恢复而改写。");
                StateChanged?.Invoke(completed.Completed);
                if (promoted is not null)
                {
                    PublishLog(promoted.InstanceId, "FIFO 队列轮到本实例，开始 Worker 启动。");
                    _ = StartEntryAsync(promoted);
                }
            }
            catch (Exception ex)
            {
                PublishLog(entry.InstanceId, "实例状态收尾失败：" + ex);
                entry.Completion.TrySetException(ex);
            }
        }
    }

    private void StopEntry(Entry entry)
    {
        RunInstanceStopResult stop;
        lock (_sync)
        {
            stop = _model.RequestStop(entry.InstanceId);
            if (!stop.Changed)
                return;
            if (stop.WasQueued)
            {
                entry.TimeoutCancellation.Cancel();
                _timeoutTracker.Remove(entry.InstanceId);
                entry.Runner.Dispose();
                entry.Completion.TrySetResult();
            }
            else
            {
                entry.Runner.Stop();
            }
        }
        StateChanged?.Invoke(stop.Snapshot);
    }

    private RunInstanceSnapshot? SelectedSnapshot()
    {
        lock (_sync)
        {
            SelectLatestRunnableLocked();
            return _selected is not null ? _model.GetSnapshot(_selected) : null;
        }
    }

    private Entry? GetSelectedEntryLocked()
    {
        SelectLatestRunnableLocked();
        return _selected is not null && _entries.TryGetValue(_selected, out var entry)
            ? entry : null;
    }

    private void SelectLatestRunnableLocked()
    {
        var debugging = _entries.Values
            .Where(entry => entry.Runner.IsDebugging &&
                !RunInstanceLifecycle.IsTerminal(_model.GetSnapshot(entry.InstanceId)))
            .OrderByDescending(entry => entry.Started)
            .FirstOrDefault();
        if (debugging is not null)
        {
            _selected = debugging.InstanceId;
            return;
        }

        if (_selected is not null && _entries.ContainsKey(_selected))
        {
            var selected = _model.GetSnapshot(_selected);
            if (!RunInstanceLifecycle.IsTerminal(selected))
                return;
        }

        var latest = _model.GetInstances()
            .Where(x => !RunInstanceLifecycle.IsTerminal(x.Snapshot))
            .OrderByDescending(x => _entries.TryGetValue(x.InstanceId, out var candidate) && candidate.Started)
            .ThenByDescending(x => x.CreatedAtUtc)
            .FirstOrDefault();
        _selected = latest?.InstanceId ?? _selected;
    }

    private static ScriptPermissionGrant CloneGrant(ScriptPermissionGrant grant) =>
        new(
            grant.FileReadRoots.ToArray(),
            grant.FileWriteRoots.ToArray(),
            grant.ClipboardRead,
            grant.ClipboardWrite,
            grant.Dialogs.ToArray(),
            grant.ProcessExecutables.ToArray(),
            grant.HttpHosts.ToArray(),
            grant.HttpMethods.ToArray(),
            grant.AllowInsecureHttp);

    private static PluginWorkerRuntimeOptions? ClonePluginRuntimeOptions(PluginWorkerRuntimeOptions? options) =>
        options is null ? null : new PluginWorkerRuntimeOptions
        {
            PackageRootPath = options.PackageRootPath,
            EntryRelativePath = options.EntryRelativePath,
            EntrySha256 = options.EntrySha256,
            ModulesRootRelativePath = options.ModulesRootRelativePath,
            ModuleHashes = new Dictionary<string, string>(options.ModuleHashes, StringComparer.OrdinalIgnoreCase)
        };

    private void PublishLog(RunInstanceId instanceId, string message)
    {
        Entry? entry;
        lock (_sync)
            _entries.TryGetValue(instanceId, out entry);

        var safeMessage = RunInstanceLogSanitizer.Sanitize(message, entry?.SensitiveValues);
        var (level, eventCode) = ClassifyLog(safeMessage);
        var structured = new RunInstanceLogEntry(
            _timeProvider.GetUtcNow(),
            level,
            eventCode,
            instanceId,
            entry?.Runner.LastWorkerAttemptId,
            entry is { CurrentIteration: > 0 } ? entry.CurrentIteration : null,
            safeMessage);
        _logStore.Add(structured);

        if ((level is RunInstanceLogLevel.Warning or RunInstanceLogLevel.Error) && entry is not null)
        {
            lock (_sync)
                if (_entries.TryGetValue(instanceId, out var current))
                    current.LastDiagnostic = safeMessage;
        }

        StructuredLogAdded?.Invoke(structured);
        InstanceLog?.Invoke(instanceId, safeMessage);
        Log?.Invoke($"[{instanceId.Value[..8]}] {safeMessage}");
    }

    private static (RunInstanceLogLevel Level, string EventCode) ClassifyLog(string message)
    {
        if (message.Contains("清理失败", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("未能全部释放", StringComparison.OrdinalIgnoreCase))
            return (RunInstanceLogLevel.Error, "INPUT.CLEANUP_FAILED");
        if (message.Contains("强制终止", StringComparison.OrdinalIgnoreCase))
            return (RunInstanceLogLevel.Warning, "WORKER.FORCED");
        if (message.Contains("运行超时", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("达到运行超时", StringComparison.OrdinalIgnoreCase))
            return (RunInstanceLogLevel.Warning, "RUN.TIMEOUT");
        if (message.Contains("意外退出", StringComparison.OrdinalIgnoreCase) ||
            (message.Contains("Worker", StringComparison.OrdinalIgnoreCase) &&
             message.Contains("退出代码", StringComparison.OrdinalIgnoreCase)))
            return (RunInstanceLogLevel.Error, "WORKER.CRASH");
        if (message.Contains("拒绝", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("未获批准", StringComparison.OrdinalIgnoreCase))
            return (RunInstanceLogLevel.Warning, "PERMISSION.REJECTED");
        if (message.Contains("失败", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("异常", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("错误", StringComparison.OrdinalIgnoreCase))
            return (RunInstanceLogLevel.Error, "RUN.ERROR");
        if (message.Contains("停止", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("取消", StringComparison.OrdinalIgnoreCase))
            return (RunInstanceLogLevel.Warning, "RUN.STOP");
        if (message.Contains("完成", StringComparison.OrdinalIgnoreCase))
            return (RunInstanceLogLevel.Information, "RUN.COMPLETED");
        if (message.Contains("队列", StringComparison.OrdinalIgnoreCase))
            return (RunInstanceLogLevel.Information, "RUN.QUEUED");
        return (RunInstanceLogLevel.Information, "SCRIPT.MESSAGE");
    }

    private static IReadOnlyList<string> GetSensitiveParameterValues(
        string scriptPath,
        IReadOnlyDictionary<string, JsonElement> parameters)
    {
        if (parameters.Count == 0)
            return [];

        var schemaPath = ScriptParameterService.SchemaPath(scriptPath);
        if (!File.Exists(schemaPath))
            return [];

        try
        {
            var schema = ScriptParameterService.LoadSchema(schemaPath);
            var sensitiveNames = schema.Parameters
                .Where(parameter => parameter.Sensitive)
                .Select(parameter => parameter.Name)
                .ToHashSet(StringComparer.Ordinal);
            return parameters
                .Where(parameter => sensitiveNames.Contains(parameter.Key))
                .SelectMany(parameter => EnumerateSensitiveScalars(parameter.Value))
                .Where(value => !string.IsNullOrEmpty(value))
                .Distinct(StringComparer.Ordinal)
                .Take(256)
                .ToArray();
        }
        catch
        {
            // If a schema exists but cannot be validated, prefer over-redaction to leaking a secret.
            return parameters.Values
                .SelectMany(EnumerateSensitiveScalars)
                .Where(value => !string.IsNullOrEmpty(value))
                .Distinct(StringComparer.Ordinal)
                .Take(256)
                .ToArray();
        }
    }

    private static IEnumerable<string> EnumerateSensitiveScalars(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                if (element.GetString() is { Length: > 0 } value)
                    yield return value;
                yield return element.GetRawText();
                break;
            case JsonValueKind.Number:
            case JsonValueKind.True:
            case JsonValueKind.False:
                yield return element.GetRawText();
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                    foreach (var value in EnumerateSensitiveScalars(item))
                        yield return value;
                break;
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                    foreach (var value in EnumerateSensitiveScalars(property.Value))
                        yield return value;
                break;
        }
    }

    private static IReadOnlyDictionary<string, JsonElement> CloneParameters(
        IReadOnlyDictionary<string, JsonElement>? parameters) =>
        parameters is null
            ? new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            : parameters.ToDictionary(
                item => item.Key,
                item => item.Value.Clone(),
                StringComparer.Ordinal);

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
                return;
            if (_model.ActiveCount > 0)
                throw new InvalidOperationException("仍有运行实例；必须先完成 StopAllAsync 再释放协调器。");
            _disposed = true;
        }

        foreach (var entry in _entries.Values)
        {
            entry.TimeoutCancellation.Cancel();
            entry.Runner.Dispose();
            _timeoutTracker.Remove(entry.InstanceId);
        }
        _inputArbiter.Dispose();
    }
}
