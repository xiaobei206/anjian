using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Anjian.Core;
using Anjian.Core.Editor;
using Anjian.Core.Windows;

namespace Anjian.App.Services;

internal sealed class PlaybackRunner : IDisposable
{
    private readonly WindowsInputController _input = new();
    private readonly WindowsWindowManager _windows = new();
    private readonly WindowsControlAutomationProvider _controls;
    private readonly ExecutionGate _gate = new();
    private CancellationTokenSource? _runCts;
    private Process? _worker;
    private LuaDebugLaunchOptions? _debugLaunchOptions;
    private TaskCompletionSource<LuaDebugCommand>? _debugCommandSource;

    public PlaybackRunner() => _controls = new WindowsControlAutomationProvider(_windows, _input);

    public event Action<string>? Log;
    public event Action<int, int>? IterationChanged;
    public event Action<LuaDebugSnapshot>? DebugPaused;
    public event Action? DebugSessionCompleted;
    public bool IsRunning => _runCts is not null;
    public bool IsPaused => _gate.IsPaused;
    public bool IsDebugging => _runCts is not null && _debugLaunchOptions is not null;

    public void Pause() => _gate.Pause();
    public void Resume() => _gate.Resume();

    public void SubmitDebugCommand(LuaDebugCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        Volatile.Read(ref _debugCommandSource)?.TrySetResult(command);
    }

    public async Task RunAsync(
        string scriptPath,
        PlaybackOptions options,
        LuaDebugLaunchOptions? debugOptions = null)
    {
        options.Validate();
        if (_runCts is not null)
            throw new InvalidOperationException("已有脚本正在运行。");
        if (debugOptions is not null && (options.RepeatCount != 1 || options.IntervalMilliseconds != 0))
            throw new ArgumentException("调试运行固定执行单轮，不支持循环次数和轮间隔。", nameof(options));

        using var cts = new CancellationTokenSource();
        _runCts = cts;
        _debugLaunchOptions = debugOptions;
        _gate.Resume();
        var token = cts.Token;
        var completed = 0;
        try
        {
            while (options.IsInfinite || completed < options.RepeatCount)
            {
                token.ThrowIfCancellationRequested();
                var next = completed + 1;
                IterationChanged?.Invoke(next, options.RepeatCount);
                Log?.Invoke(options.IsInfinite
                    ? $"开始第 {next} 轮（无限循环）"
                    : $"开始第 {next}/{options.RepeatCount} 轮");
                await RunOneAsync(scriptPath, debugOptions, token).ConfigureAwait(false);
                completed++;
                if (!options.HasNextIteration(completed))
                    break;
                Log?.Invoke($"第 {completed} 轮完成，等待 {options.IntervalMilliseconds} ms 后开始下一轮。");
                await _gate.DelayAsync(options.IntervalMilliseconds, token).ConfigureAwait(false);
            }
            Log?.Invoke($"播放完成，共执行 {completed} 轮。");
        }
        catch (OperationCanceledException)
        {
            Log?.Invoke($"播放已停止（完成 {completed} 轮）。");
        }
        catch (Exception ex) when (token.IsCancellationRequested)
        {
            Log?.Invoke($"播放已停止（完成 {completed} 轮）：{ex.Message}");
        }
        finally
        {
            KillWorker();
            _input.ReleaseAll();
            Volatile.Read(ref _debugCommandSource)?.TrySetCanceled();
            _debugLaunchOptions = null;
            _runCts = null;
            _gate.Resume();
            DebugSessionCompleted?.Invoke();
        }
    }

    public void Stop()
    {
        _runCts?.Cancel();
        Volatile.Read(ref _debugCommandSource)?.TrySetCanceled();
        KillWorker();
        _input.ReleaseAll();
    }

    private async Task RunOneAsync(
        string scriptPath,
        LuaDebugLaunchOptions? debugOptions,
        CancellationToken token)
    {
        var pipeName = "Anjian2026_" + Guid.NewGuid().ToString("N");
        await using var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        var executable = Environment.ProcessPath ?? throw new InvalidOperationException("无法定位当前程序路径。");
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true };
        start.ArgumentList.Add("--worker");
        start.ArgumentList.Add(pipeName);
        start.ArgumentList.Add(scriptPath);
        var worker = Process.Start(start) ?? throw new InvalidOperationException("无法启动脚本执行进程。");
        _worker = worker;

        try
        {
            await server.WaitForConnectionAsync(token).ConfigureAwait(false);
            using var reader = new StreamReader(server, new UTF8Encoding(false), false, 4096, leaveOpen: true);
            using var writer = new StreamWriter(server, new UTF8Encoding(false), 4096, leaveOpen: true) { AutoFlush = true };
            while (!token.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(token).ConfigureAwait(false);
                if (line is null) break;
                var message = JsonSerializer.Deserialize<IpcMessage>(line) ?? throw new InvalidDataException("收到空的执行请求。");
                try
                {
                    var payload = await DispatchAsync(message, debugOptions, token).ConfigureAwait(false);
                    await writer.WriteLineAsync(JsonSerializer.Serialize(new { ok = true, payload })).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    Log?.Invoke("命令执行失败：" + ex.Message);
                    await writer.WriteLineAsync(JsonSerializer.Serialize(new { ok = false, error = ex.Message })).ConfigureAwait(false);
                }
            }

            await worker.WaitForExitAsync(token).ConfigureAwait(false);
            if (worker.ExitCode != 0 && !token.IsCancellationRequested)
                throw new InvalidOperationException($"脚本执行失败（退出代码 {worker.ExitCode}），请检查运行日志。");
        }
        finally
        {
            if (!worker.HasExited && token.IsCancellationRequested)
                KillWorker();
            worker.Dispose();
            _worker = null;
            _input.ReleaseAll();
            _controls.ClearBindings();
            _windows.ClearReferences();
        }
    }

    private async Task<string?> DispatchAsync(
        IpcMessage message,
        LuaDebugLaunchOptions? debugOptions,
        CancellationToken token)
    {
        if (message.Type == "worker-ready")
            return debugOptions is null ? null : JsonSerializer.Serialize(debugOptions);

        if (message.Type == "debug-break")
        {
            if (debugOptions is null || string.IsNullOrWhiteSpace(message.Text))
                throw new InvalidDataException("收到非调试会话的断点请求。");
            var snapshot = JsonSerializer.Deserialize<LuaDebugSnapshot>(message.Text)
                ?? throw new InvalidDataException("无法解析 Lua 调试快照。");
            return JsonSerializer.Serialize(await WaitForDebuggerCommandAsync(snapshot, token).ConfigureAwait(false));
        }

        // Pause takes effect at every command boundary. Explicit Up commands remain allowed
        // so a paused script cannot strand a held key or mouse button.
        if (message.Type is not ("mouse-up" or "key-up"))
            await _gate.WaitUntilRunnableAsync(token).ConfigureAwait(false);

        switch (message.Type)
        {
            case "checkpoint":
                await _gate.WaitUntilRunnableAsync(token).ConfigureAwait(false);
                break;
            case "move":
                _input.MoveTo(checked((int)message.X), checked((int)message.Y));
                break;
            case "click":
                await _input.ClickAsync(message.Name ?? "left", message.Milliseconds, token).ConfigureAwait(false);
                break;
            case "mouse-down":
                _input.MouseDown(message.Name ?? "left");
                break;
            case "mouse-up":
                _input.MouseUp(message.Name ?? "left");
                break;
            case "wheel":
                _input.MouseWheel(message.Milliseconds);
                break;
            case "key-press":
                await _input.KeyPressAsync(message.Name ?? "ENTER", message.Milliseconds, token).ConfigureAwait(false);
                break;
            case "key-down":
                _input.KeyDown(message.Name ?? "ENTER");
                break;
            case "key-up":
                _input.KeyUp(message.Name ?? "ENTER");
                break;
            case "type-text":
                _input.TypeText(message.Text ?? "", token);
                break;
            case "delay":
                await _gate.DelayAsync(message.Milliseconds, token).ConfigureAwait(false);
                break;
            case "log":
                Log?.Invoke(message.Text ?? "");
                break;
            case "error":
                Log?.Invoke(message.Text ?? "脚本发生未知错误。");
                break;
            case "window-find":
            {
                var selector = ReadWindowPayload<WindowSelector>(message.Text);
                return JsonSerializer.Serialize(_windows.Find(selector), WindowApiJson.Options);
            }
            case "window-enumerate":
            {
                var selector = ReadWindowPayload<WindowSelector>(message.Text);
                return JsonSerializer.Serialize(_windows.Enumerate(selector), WindowApiJson.Options);
            }
            case "window-enumerate-children":
            {
                var request = ReadWindowPayload<WindowReferenceRequest>(message.Text);
                return JsonSerializer.Serialize(_windows.EnumerateChildren(request.Id), WindowApiJson.Options);
            }
            case "window-get-info":
            {
                var request = ReadWindowPayload<WindowReferenceRequest>(message.Text);
                return JsonSerializer.Serialize(_windows.GetInfo(request.Id), WindowApiJson.Options);
            }
            case "window-operation":
            {
                var request = ReadWindowPayload<WindowActionRequest>(message.Text);
                var result = request.Operation switch
                {
                    "activate" => _windows.Activate(request.Id),
                    "show" => _windows.Show(request.Id),
                    "hide" => _windows.Hide(request.Id),
                    "minimize" => _windows.Minimize(request.Id),
                    "maximize" => _windows.Maximize(request.Id),
                    "restore" => _windows.Restore(request.Id),
                    "topmost" => _windows.SetTopmost(request.Id, request.Enabled),
                    "move" => _windows.Move(request.Id, request.X, request.Y),
                    "resize" => _windows.Resize(request.Id, request.Width, request.Height),
                    "align" => _windows.Align(request.Id, ParseWindowAnchor(request.Anchor), request.OffsetX, request.OffsetY),
                    _ => throw new InvalidDataException("未知窗口操作：" + request.Operation)
                };
                return JsonSerializer.Serialize(result);
            }
            case "window-wait":
            {
                var request = ReadWindowPayload<WindowWaitRequest>(message.Text);
                var result = await _windows.WaitForWindowAsync(request.Selector, request.TimeoutMilliseconds, token)
                    .ConfigureAwait(false);
                return JsonSerializer.Serialize(result, WindowApiJson.Options);
            }
            case "window-wait-closed":
            {
                var request = ReadWindowPayload<WindowWaitClosedRequest>(message.Text);
                var result = await _windows.WaitForClosedAsync(request.Id, request.TimeoutMilliseconds, token)
                    .ConfigureAwait(false);
                return JsonSerializer.Serialize(result);
            }
            case "window-bind":
            {
                var request = ReadWindowPayload<WindowBindingRequest>(message.Text);
                return _controls.Bind(request.WindowId);
            }
            case "window-unbind":
            {
                var request = ReadWindowPayload<WindowBindingIdRequest>(message.Text);
                return JsonSerializer.Serialize(_controls.Unbind(request.BindingId));
            }
            case "window-capabilities":
            {
                var request = ReadWindowPayload<WindowBindingIdRequest>(message.Text);
                return JsonSerializer.Serialize(_controls.GetCapabilities(request.BindingId), WindowApiJson.Options);
            }
            case "control-enumerate":
            {
                var request = ReadWindowPayload<ControlEnumerateRequest>(message.Text);
                return JsonSerializer.Serialize(_controls.Enumerate(request.BindingId, request.MaxResults), WindowApiJson.Options);
            }
            case "control-get-info":
            {
                var request = ReadWindowPayload<ControlReferenceRequest>(message.Text);
                return JsonSerializer.Serialize(_controls.GetInfo(request.BindingId, request.ControlId), WindowApiJson.Options);
            }
            case "control-get-value":
            {
                var request = ReadWindowPayload<ControlReferenceRequest>(message.Text);
                return JsonSerializer.Serialize(_controls.GetValue(request.BindingId, request.ControlId));
            }
            case "control-set-value":
            {
                var request = ReadWindowPayload<ControlSetValueRequest>(message.Text);
                _controls.SetValue(request.BindingId, request.ControlId, request.Value);
                return JsonSerializer.Serialize(true);
            }
            case "control-get-text":
            {
                var request = ReadWindowPayload<ControlReferenceRequest>(message.Text);
                return JsonSerializer.Serialize(_controls.GetText(request.BindingId, request.ControlId));
            }
            case "control-get-selection":
            {
                var request = ReadWindowPayload<ControlReferenceRequest>(message.Text);
                return JsonSerializer.Serialize(_controls.GetSelection(request.BindingId, request.ControlId), WindowApiJson.Options);
            }
            case "control-select":
            {
                var request = ReadWindowPayload<ControlReferenceRequest>(message.Text);
                _controls.Select(request.BindingId, request.ControlId);
                return JsonSerializer.Serialize(true);
            }
            case "control-get-toggle":
            {
                var request = ReadWindowPayload<ControlReferenceRequest>(message.Text);
                return JsonSerializer.Serialize(_controls.GetToggleState(request.BindingId, request.ControlId));
            }
            case "control-toggle":
            {
                var request = ReadWindowPayload<ControlReferenceRequest>(message.Text);
                _controls.Toggle(request.BindingId, request.ControlId);
                return JsonSerializer.Serialize(true);
            }
            case "control-invoke":
            {
                var request = ReadWindowPayload<ControlReferenceRequest>(message.Text);
                _controls.Invoke(request.BindingId, request.ControlId);
                return JsonSerializer.Serialize(true);
            }
            case "window-input":
            {
                var request = ReadWindowPayload<WindowInputRequest>(message.Text);
                await _controls.SendInputAsync(request, token).ConfigureAwait(false);
                return JsonSerializer.Serialize(true);
            }
            case "coordinate-to-screen":
            case "coordinate-from-screen":
            {
                var request = ReadWindowPayload<WindowCoordinateRequest>(message.Text);
                var result = _windows.ConvertCoordinate(request, toScreen: message.Type == "coordinate-to-screen");
                return JsonSerializer.Serialize(result, WindowApiJson.Options);
            }
            default:
                throw new InvalidDataException($"未知执行命令：{message.Type}");
        }

        return null;
    }

    private static T ReadWindowPayload<T>(string? json) =>
        string.IsNullOrWhiteSpace(json)
            ? throw new InvalidDataException("窗口 API 缺少请求数据。")
            : JsonSerializer.Deserialize<T>(json, WindowApiJson.Options)
                ?? throw new InvalidDataException("窗口 API 请求数据无效。");

    private static WindowAnchor ParseWindowAnchor(string value) =>
        (value ?? string.Empty).Trim().ToLowerInvariant().Replace("_", "-") switch
        {
            "top-left" => WindowAnchor.TopLeft,
            "top-center" or "top-middle" => WindowAnchor.TopCenter,
            "top-right" => WindowAnchor.TopRight,
            "center-left" or "middle-left" => WindowAnchor.CenterLeft,
            "center" or "middle" => WindowAnchor.Center,
            "center-right" or "middle-right" => WindowAnchor.CenterRight,
            "bottom-left" => WindowAnchor.BottomLeft,
            "bottom-center" or "bottom-middle" => WindowAnchor.BottomCenter,
            "bottom-right" => WindowAnchor.BottomRight,
            _ => throw new ArgumentException("不支持的窗口锚点。", nameof(value))
        };

    private async Task<LuaDebugCommand> WaitForDebuggerCommandAsync(
        LuaDebugSnapshot snapshot,
        CancellationToken token)
    {
        var source = new TaskCompletionSource<LuaDebugCommand>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (Interlocked.CompareExchange(ref _debugCommandSource, source, null) is not null)
            throw new InvalidOperationException("调试器已经在等待控制命令。");

        try
        {
            DebugPaused?.Invoke(snapshot);
            return await source.Task.WaitAsync(token).ConfigureAwait(false);
        }
        finally
        {
            Interlocked.CompareExchange(ref _debugCommandSource, null, source);
        }
    }

    private void KillWorker()
    {
        try
        {
            if (_worker is { HasExited: false })
                _worker.Kill(entireProcessTree: true);
        }
        catch { }
    }

    public void Dispose()
    {
        Stop();
        _windows.Dispose();
    }
}
