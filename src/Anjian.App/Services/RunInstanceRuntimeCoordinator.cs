using System.Text.Json;
using Anjian.Core.Editor;
using Anjian.Core.Parameters;
using Anjian.Core.Permissions;
using Anjian.Core.Plugins;
using Anjian.Core.Runtime;

namespace Anjian.App.Services;

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
        PluginWorkerRuntimeOptions? pluginRuntimeOptions)
    {
        public RunInstanceId InstanceId { get; } = instanceId;
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
    }

    private readonly object _sync = new();
    private readonly Anjian.Core.Runtime.RunInstanceCoordinator _model = new();
    private readonly InputArbiter _inputArbiter = new();
    private readonly Dictionary<RunInstanceId, Entry> _entries = [];
    private RunInstanceId? _selected;
    private bool _shuttingDown;
    private bool _disposed;

    public event Action<PlaybackRunner>? RunnerCreated;
    private Control? _uiOwner;

    public event Action<string>? Log;
    public event Action<RunInstanceId, string>? InstanceLog;
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
        PluginWorkerRuntimeOptions? pluginRuntimeOptions = null) =>
        StartAsync(
            Guid.NewGuid().ToString("N"),
            scriptPath,
            options,
            debugOptions,
            parameters,
            permissions,
            spriteRuntimeOptions,
            pluginRuntimeOptions);

    public void Pause() => PauseSelected();
    public void Resume() => ResumeSelected();
    public void Stop() => StopSelected();

    public int ActiveCount => _model.ActiveCount;
    public int QueuedCount => _model.QueuedCount;
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
    public bool SelectedIsDebugging => SelectedRunner?.IsDebugging == true;
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
        PluginWorkerRuntimeOptions? pluginRuntimeOptions = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
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
                options,
                fullPath,
                debugOptions,
                CloneParameters(parameters),
                permissions ?? ScriptPermissionGrant.Empty,
                spriteRuntimeOptions,
                pluginRuntimeOptions);
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

    public void PauseSelected()
    {
        Entry? entry;
        lock (_sync)
            entry = GetSelectedEntryLocked();
        if (entry is null || !_model.TryPause(entry.InstanceId, out var snapshot))
            return;
        entry.Runner.Pause();
        StateChanged?.Invoke(snapshot);
    }

    public void ResumeSelected()
    {
        Entry? entry;
        lock (_sync)
            entry = GetSelectedEntryLocked();
        if (entry is null || !_model.TryResume(entry.InstanceId, out var snapshot))
            return;
        entry.Runner.Resume();
        StateChanged?.Invoke(snapshot);
    }

    public void SubmitDebugCommand(LuaDebugCommand command) =>
        SelectedRunner?.SubmitDebugCommand(command);

    public void StopSelected()
    {
        Entry? entry;
        lock (_sync)
            entry = GetSelectedEntryLocked();
        if (entry is null)
            return;

        var stop = _model.RequestStop(entry.InstanceId);
        if (!stop.Changed)
            return;

        StateChanged?.Invoke(stop.Snapshot);
        if (stop.WasQueued)
        {
            entry.Runner.Dispose();
            entry.Completion.TrySetResult();
        }
        else
        {
            entry.Runner.Stop();
        }
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
        Entry[] entries;
        lock (_sync)
        {
            _shuttingDown = true;
            entries = _entries.Values.ToArray();
        }

        var waits = new List<Task>();
        foreach (var entry in entries)
        {
            var state = _model.GetSnapshot(entry.InstanceId);
            if (RunInstanceLifecycle.IsTerminal(state))
                continue;

            var stop = _model.RequestStop(entry.InstanceId);
            if (stop.Changed)
            {
                StateChanged?.Invoke(stop.Snapshot);
                if (stop.WasQueued)
                {
                    entry.Runner.Dispose();
                    entry.Completion.TrySetResult();
                }
                else
                {
                    entry.Runner.Stop();
                    waits.Add(entry.Completion.Task);
                }
            }
            else if (!entry.Completion.Task.IsCompleted)
            {
                waits.Add(entry.Completion.Task);
            }
        }

        lock (_sync)
            waits = _entries.Values.Select(entry => entry.Completion.Task).ToList();

        await Task.WhenAll(waits).ConfigureAwait(true);
    }

    public IReadOnlyList<RunInstanceDescriptor> GetInstances() => _model.GetInstances();

    public bool TryRecoverInputAfterManualAcknowledgement()
    {
        var recovered = _inputArbiter.TryRecoverAfterManualAcknowledgement();
        if (recovered)
            Log?.Invoke("用户已确认键鼠状态恢复，全局输入仲裁解除故障锁定。");
        return recovered;
    }

    private void WireRunner(Entry entry)
    {
        var runner = entry.Runner;
        runner.Log += message => PublishLog(entry.InstanceId, message);
        runner.IterationChanged += (current, total) =>
        {
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
            if (_model.TryMarkRunning(entry.InstanceId, out var snapshot))
                StateChanged?.Invoke(snapshot);
        };
        runner.WorkerForceTerminating += () =>
        {
            try
            {
                var snapshot = _model.MarkForceTerminating(entry.InstanceId);
                StateChanged?.Invoke(snapshot);
            }
            catch (Exception ex)
            {
                PublishLog(entry.InstanceId, "更新强制终止状态失败：" + ex.Message);
            }
        };
    }

    private async Task StartEntryAsync(Entry entry)
    {
        lock (_sync)
        {
            if (entry.Started || _disposed)
                return;
            entry.Started = true;
        }

        if (_shuttingDown)
        {
            var stop = _model.RequestStop(entry.InstanceId);
            StateChanged?.Invoke(stop.Snapshot);
            entry.Runner.Stop();
        }

        Exception? failure = null;
        try
        {
            await entry.Runner.RunAsync(
                entry.ScriptPath,
                entry.Options,
                entry.DebugOptions,
                entry.Parameters,
                entry.SpriteRuntimeOptions,
                entry.Permissions,
                entry.PluginRuntimeOptions).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            failure = ex;
            PublishLog(entry.InstanceId, "实例运行失败：" + ex);
        }
        finally
        {
            try { entry.Runner.Dispose(); }
            catch (Exception ex) { PublishLog(entry.InstanceId, "实例释放资源失败：" + ex.Message); }

            RunInstanceOutcome outcome;
            try
            {
                var current = _model.GetSnapshot(entry.InstanceId);
                outcome = current.Outcome != RunInstanceOutcome.None
                    ? current.Outcome
                    : failure is null ? RunInstanceOutcome.Succeeded : RunInstanceOutcome.Failed;
                var cleanup = entry.Runner.LastCleanupSucceeded
                    ? RunInstanceCleanupStatus.Succeeded
                    : RunInstanceCleanupStatus.Failed;
                var completed = _model.Complete(entry.InstanceId, outcome, cleanup);
                StateChanged?.Invoke(completed.Completed);
                entry.Completion.TrySetResult();
                if (completed.Promoted is not null)
                {
                    Entry? promoted;
                    lock (_sync)
                        _entries.TryGetValue(completed.Promoted.InstanceId, out promoted);
                    if (promoted is not null)
                    {
                        PublishLog(promoted.InstanceId, "FIFO 队列轮到本实例，开始 Worker 启动。");
                        _ = StartEntryAsync(promoted);
                    }
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
        var stop = _model.RequestStop(entry.InstanceId);
        if (!stop.Changed)
            return;
        StateChanged?.Invoke(stop.Snapshot);
        if (stop.WasQueued)
        {
            entry.Runner.Dispose();
            entry.Completion.TrySetResult();
        }
        else
        {
            entry.Runner.Stop();
        }
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

    private void PublishLog(RunInstanceId instanceId, string message)
    {
        InstanceLog?.Invoke(instanceId, message);
        Log?.Invoke($"[{instanceId.Value[..8]}] {message}");
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
            entry.Runner.Dispose();
        _inputArbiter.Dispose();
    }
}
