using System.Text.Json;
using Anjian.Core.Runtime;

namespace Anjian.App.Services;

internal sealed class ScheduledTaskStore
{
    private const int CurrentSchemaVersion = 1;
    private readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Anjian2026", "scheduled-tasks.json");
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private sealed record StoreDocument(int SchemaVersion, List<ScheduledTaskSnapshot> Tasks);

    public IReadOnlyList<ScheduledTaskSnapshot> Load()
    {
        if (!File.Exists(_path))
            return [];

        var document = ReadDocument(_path);
        if (document.SchemaVersion != CurrentSchemaVersion)
            throw new InvalidDataException(
                $"定时任务文件 schemaVersion={document.SchemaVersion}，当前程序只支持 {CurrentSchemaVersion}；原文件已保留，拒绝覆盖。");

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in document.Tasks)
        {
            item.Definition.Validate();
            if (!ids.Add(item.Definition.TaskId))
                throw new InvalidDataException($"定时任务 ID 重复：{item.Definition.TaskId}");
            if (item.Cursor.ConsecutiveFailures < 0)
                throw new InvalidDataException("定时任务连续失败计数不能为负数。");
        }

        return document.Tasks;
    }

    public void Save(IReadOnlyList<ScheduledTaskSnapshot> tasks)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);

        // Refuse to overwrite an unreadable or future-schema file.
        if (File.Exists(_path))
        {
            var existing = ReadDocument(_path);
            if (existing.SchemaVersion != CurrentSchemaVersion)
                throw new InvalidDataException(
                    $"定时任务文件 schemaVersion={existing.SchemaVersion}，拒绝覆盖未知格式。");
            File.Copy(_path, _path + ".bak", overwrite: true);
        }

        foreach (var item in tasks)
            item.Definition.Validate();

        var document = new StoreDocument(CurrentSchemaVersion, tasks.ToList());
        var temporary = _path + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(document, JsonOptions));
            File.Move(temporary, _path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }

    private static StoreDocument ReadDocument(string path)
    {
        try
        {
            using var json = JsonDocument.Parse(File.ReadAllText(path));
            if (!json.RootElement.TryGetProperty("SchemaVersion", out var schema) &&
                !json.RootElement.TryGetProperty("schemaVersion", out schema))
                throw new InvalidDataException("定时任务文件缺少 schemaVersion。");
            if (!schema.TryGetInt32(out var version))
                throw new InvalidDataException("定时任务文件 schemaVersion 格式无效。");
            var document = JsonSerializer.Deserialize<StoreDocument>(json.RootElement.GetRawText(), JsonOptions);
            if (document is null)
                throw new InvalidDataException("定时任务文件内容为空。");
            return document with { SchemaVersion = version };
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            throw new InvalidDataException("定时任务文件损坏或不可读取；原文件已保留，未自动重置。", ex);
        }
    }
}

internal sealed class ScheduledTaskService : IAsyncDisposable
{
    private readonly object _sync = new();
    private readonly ScheduledTaskEngine _engine;
    private readonly ScheduledTaskStore _store;
    private readonly Func<ScheduledTaskOccurrence, int, CancellationToken, Task<RunInstanceSnapshot?>> _execute;
    private readonly TimeProvider _timeProvider;
    private readonly Func<bool> _canAccept;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly SemaphoreSlim _tickLock = new(1, 1);
    private readonly Dictionary<string, int> _inFlight = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ScheduledTaskOccurrence> _pendingOne = new(StringComparer.Ordinal);
    private Task? _loop;
    private bool _disposed;

    public event Action? Changed;
    public event Action<string>? Log;

    public ScheduledTaskService(
        Func<ScheduledTaskOccurrence, int, CancellationToken, Task<RunInstanceSnapshot?>> execute,
        ScheduledTaskStore? store = null,
        TimeProvider? timeProvider = null,
        Func<bool>? canAccept = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _store = store ?? new ScheduledTaskStore();
        _timeProvider = timeProvider ?? TimeProvider.System;
        _canAccept = canAccept ?? (() => true);
        _engine = new ScheduledTaskEngine(_timeProvider);

        foreach (var item in _store.Load())
            _engine.AddOrUpdate(item.Definition, item.Cursor);
    }

    public IReadOnlyList<ScheduledTaskSnapshot> GetSnapshots() => _engine.GetSnapshots();

    public void AddOrUpdate(ScheduledTaskDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        definition.Validate();
        lock (_sync)
        {
            ThrowIfDisposed();
            _engine.AddOrUpdate(definition);
            PersistLocked();
        }
        Changed?.Invoke();
    }

    public bool SetEnabled(string taskId, bool enabled)
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            if (!_engine.SetEnabled(taskId, enabled))
                return false;
            if (!enabled)
                _pendingOne.Remove(taskId);
            PersistLocked();
        }
        Changed?.Invoke();
        return true;
    }

    public bool Remove(string taskId)
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            _pendingOne.Remove(taskId);
            if (!_engine.Remove(taskId))
                return false;
            PersistLocked();
        }
        Changed?.Invoke();
        return true;
    }

    public void Start()
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            _loop ??= RunLoopAsync(_shutdown.Token);
        }
    }

    public async Task TickOnceAsync(CancellationToken cancellationToken = default)
    {
        await _tickLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ScheduledTaskTickResult result;
            lock (_sync)
            {
                ThrowIfDisposed();
                result = _engine.Tick();
                if (result.ChangedTasks.Count > 0)
                    PersistLocked();
            }

            if (result.ChangedTasks.Count > 0)
                Changed?.Invoke();

            DrainPending();
            foreach (var occurrence in result.Occurrences)
                DispatchOrQueue(occurrence);
            DrainPending();
        }
        finally
        {
            _tickLock.Release();
        }
    }

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Let the main window finish its first-show/tray transition before any due
            // task can request a parameter or permission dialog.
            await Task.Delay(TimeSpan.FromSeconds(1), _timeProvider, cancellationToken).ConfigureAwait(false);
            while (!cancellationToken.IsCancellationRequested)
            {
                await TickOnceAsync(cancellationToken).ConfigureAwait(false);
                await Task.Delay(TimeSpan.FromSeconds(1), _timeProvider, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal service shutdown.
        }
        catch (Exception ex)
        {
            Log?.Invoke("定时任务调度循环已停止：" + ex);
        }
    }

    private void DispatchOrQueue(ScheduledTaskOccurrence occurrence)
    {
        var taskId = occurrence.Task.TaskId;
        var shouldStart = false;
        var capacityAvailable = _canAccept();
        lock (_sync)
        {
            if (_disposed || _shutdown.IsCancellationRequested)
                return;

            var active = _inFlight.GetValueOrDefault(taskId);
            var decision = ScheduledTaskPolicy.DecideOverlap(
                occurrence.Task.OverlapPolicy, active, _pendingOne.ContainsKey(taskId));
            if (!capacityAvailable &&
                decision is ScheduledOverlapDecision.Start or ScheduledOverlapDecision.StartParallel)
            {
                if (_pendingOne.TryAdd(taskId, occurrence))
                    Log?.Invoke($"全局运行实例容量已满；任务“{occurrence.Task.Name}”保留一个触发等待容量释放。");
                else
                    Log?.Invoke($"全局运行实例容量已满；任务“{occurrence.Task.Name}”的触发已合并。");
                return;
            }
            switch (decision)
            {
                case ScheduledOverlapDecision.Start:
                case ScheduledOverlapDecision.StartParallel:
                    _inFlight[taskId] = active + 1;
                    shouldStart = true;
                    break;
                case ScheduledOverlapDecision.QueueOne:
                    _pendingOne.Add(taskId, occurrence);
                    Log?.Invoke($"任务“{occurrence.Task.Name}”正在运行；已保留一个待执行触发。");
                    break;
                case ScheduledOverlapDecision.KeepExistingPending:
                    Log?.Invoke($"任务“{occurrence.Task.Name}”已有待执行触发，合并本次触发。");
                    break;
                case ScheduledOverlapDecision.Skip:
                    Log?.Invoke($"任务“{occurrence.Task.Name}”仍在运行，按 Skip 策略跳过本次触发。");
                    break;
            }
        }

        if (shouldStart)
            _ = RunOccurrenceAsync(occurrence);
    }

    private async Task RunOccurrenceAsync(ScheduledTaskOccurrence occurrence)
    {
        var deferPendingUntilCapacityCheck = false;
        try
        {
            for (var attempt = 0; attempt <= occurrence.Task.MaximumRetries; attempt++)
            {
                if (_shutdown.IsCancellationRequested)
                    return;
                lock (_sync)
                {
                    if (!_engine.IsEnabled(occurrence.Task.TaskId))
                        return;
                }

                RunInstanceSnapshot? result;
                try
                {
                    result = await _execute(occurrence, attempt, _shutdown.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
                {
                    return;
                }
                catch (RunInstanceCapacityException)
                {
                    deferPendingUntilCapacityCheck = true;
                    lock (_sync)
                        _pendingOne.TryAdd(occurrence.Task.TaskId, occurrence);
                    Log?.Invoke($"任务“{occurrence.Task.Name}”遇到全局容量竞争；触发已暂存，不计为脚本失败。");
                    return;
                }
                catch (Exception ex)
                {
                    Log?.Invoke($"任务“{occurrence.Task.Name}”启动/执行请求失败：{ex.Message}");
                    result = new RunInstanceSnapshot(
                        new RunInstanceId(occurrence.GetStartRequestId(attempt)),
                        RunInstanceState.Cleaned,
                        RunInstanceOutcome.Failed,
                        RunInstanceTerminationMode.None,
                        RunInstanceCleanupStatus.Succeeded);
                }

                if (result is null || result.Outcome == RunInstanceOutcome.Cancelled)
                    return;

                if (result.Outcome == RunInstanceOutcome.Succeeded &&
                    result.CleanupStatus == RunInstanceCleanupStatus.Succeeded)
                {
                    lock (_sync)
                    {
                        if (_engine.GetCursor(occurrence.Task.TaskId) is not null)
                        {
                            _engine.RecordSuccess(occurrence.Task.TaskId);
                            PersistLocked();
                        }
                    }
                    Changed?.Invoke();
                    return;
                }

                if (result.CleanupStatus == RunInstanceCleanupStatus.Failed)
                {
                    lock (_sync)
                    {
                        if (_engine.SetEnabled(occurrence.Task.TaskId, false))
                            PersistLocked();
                    }
                    Log?.Invoke($"任务“{occurrence.Task.Name}”输入/资源清理失败，已禁用后续触发；请先检查键鼠状态。");
                    Changed?.Invoke();
                    return;
                }

                ScheduledFailureDecision decision;
                lock (_sync)
                {
                    if (_engine.GetCursor(occurrence.Task.TaskId) is null)
                        return;
                    decision = _engine.RecordFailure(occurrence.Task.TaskId, attempt);
                    if (decision.StopSchedule)
                        _engine.SetEnabled(occurrence.Task.TaskId, false);
                    PersistLocked();
                }

                Log?.Invoke($"任务“{occurrence.Task.Name}”第 {attempt + 1} 次尝试结果为 {result.Outcome}。");
                if (decision.StopSchedule)
                {
                    Persist();
                    Changed?.Invoke();
                    Log?.Invoke($"任务“{occurrence.Task.Name}”已按失败策略或连续失败上限禁用。");
                    return;
                }
                if (!decision.Retry || decision.RetryAfter is not { } delay)
                    return;

                Log?.Invoke($"任务“{occurrence.Task.Name}”将在 {delay} 后以新 Worker attempt 重试，并重新审阅权限。");
                await Task.Delay(delay, _timeProvider, _shutdown.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
        {
            // Application is exiting; the runtime coordinator owns Worker StopAll cleanup.
        }
        catch (Exception ex)
        {
            Log?.Invoke($"任务“{occurrence.Task.Name}”调度处理异常：{ex}");
        }
        finally
        {
            ScheduledTaskOccurrence? pending = null;
            lock (_sync)
            {
                var taskId = occurrence.Task.TaskId;
                var active = _inFlight.GetValueOrDefault(taskId);
                if (active <= 1)
                {
                    _inFlight.Remove(taskId);
                    if (!_engine.IsEnabled(taskId))
                    {
                        _pendingOne.Remove(taskId);
                    }
                    else if (!deferPendingUntilCapacityCheck &&
                        _pendingOne.Remove(taskId, out var next) &&
                        !_disposed && !_shutdown.IsCancellationRequested)
                    {
                        pending = next;
                        _inFlight[taskId] = 1;
                    }
                }
                else
                {
                    _inFlight[taskId] = active - 1;
                }
            }

            if (pending is not null)
                _ = RunOccurrenceAsync(pending);
            else
                DrainPending();
        }
    }

    private void DrainPending()
    {
        var start = new List<ScheduledTaskOccurrence>();
        lock (_sync)
        {
            if (_disposed || _shutdown.IsCancellationRequested)
                return;

            foreach (var item in _pendingOne.ToArray())
            {
                if (!_canAccept())
                    break;
                var active = _inFlight.GetValueOrDefault(item.Key);
                if (item.Value.Task.OverlapPolicy == ScheduledOverlapPolicy.QueueOne && active > 0)
                    continue;
                if (!_engine.IsEnabled(item.Key))
                {
                    _pendingOne.Remove(item.Key);
                    continue;
                }

                _pendingOne.Remove(item.Key);
                _inFlight[item.Key] = active + 1;
                start.Add(item.Value);
            }
        }

        foreach (var occurrence in start)
            _ = RunOccurrenceAsync(occurrence);
    }

    private void Persist()
    {
        lock (_sync)
            PersistLocked();
    }

    private void PersistLocked() => _store.Save(_engine.GetSnapshots());

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(_disposed, this);

    public async Task StopAsync()
    {
        _shutdown.Cancel();
        Task? loop;
        lock (_sync)
            loop = _loop;
        if (loop is not null)
        {
            try { await loop.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        await StopAsync().ConfigureAwait(false);
        lock (_sync)
            _disposed = true;
        _shutdown.Dispose();
        _tickLock.Dispose();
    }
}
