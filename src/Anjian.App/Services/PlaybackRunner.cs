using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Anjian.Core;

namespace Anjian.App.Services;

internal sealed class PlaybackRunner : IDisposable
{
    private readonly WindowsInputController _input = new();
    private readonly ExecutionGate _gate = new();
    private CancellationTokenSource? _runCts;
    private Process? _worker;

    public event Action<string>? Log;
    public event Action<int, int>? IterationChanged;
    public bool IsRunning => _runCts is not null;
    public bool IsPaused => _gate.IsPaused;

    public void Pause() => _gate.Pause();
    public void Resume() => _gate.Resume();

    public async Task RunAsync(string scriptPath, PlaybackOptions options)
    {
        options.Validate();
        if (_runCts is not null)
            throw new InvalidOperationException("已有脚本正在运行。");

        using var cts = new CancellationTokenSource();
        _runCts = cts;
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
                await RunOneAsync(scriptPath, token).ConfigureAwait(false);
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
            _runCts = null;
            _gate.Resume();
        }
    }

    public void Stop()
    {
        _runCts?.Cancel();
        KillWorker();
        _input.ReleaseAll();
    }

    private async Task RunOneAsync(string scriptPath, CancellationToken token)
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
                    await DispatchAsync(message, token).ConfigureAwait(false);
                    await writer.WriteLineAsync("{\"ok\":true}").ConfigureAwait(false);
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
        }
    }

    private async Task DispatchAsync(IpcMessage message, CancellationToken token)
    {
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
            default:
                throw new InvalidDataException($"未知执行命令：{message.Type}");
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

    public void Dispose() => Stop();
}
