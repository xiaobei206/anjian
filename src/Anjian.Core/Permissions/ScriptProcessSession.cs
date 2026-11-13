using System.Diagnostics;
using System.Text.Json;

namespace Anjian.Core.Permissions;

public sealed class ScriptProcessSession : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };
    private readonly Dictionary<string, ProcessEntry> _entries = new(StringComparer.Ordinal);
    private bool _disposed;

    public string Start(ScriptPermissionGrant? grant, string scriptDirectory, string? payload)
    {
        ThrowIfDisposed();
        var request = ScriptProcessPolicy.ParseStartRequest(grant, payload, scriptDirectory);
        if (_entries.Count >= ScriptProcessPolicy.MaximumProcessesPerRun)
            throw new InvalidOperationException($"单次运行最多启动 {ScriptProcessPolicy.MaximumProcessesPerRun} 个外部进程。");
        if (!File.Exists(request.ExecutablePath))
            throw new FileNotFoundException("获批的 EXE 文件不存在。", request.ExecutablePath);
        EnsureNoReparsePoints(request.ExecutablePath);

        var startInfo = new ProcessStartInfo
        {
            FileName = request.ExecutablePath,
            WorkingDirectory = Path.GetDirectoryName(request.ExecutablePath)!,
            UseShellExecute = false,
            CreateNoWindow = false
        };
        foreach (var argument in request.Arguments)
            startInfo.ArgumentList.Add(argument);

        var process = new Process { StartInfo = startInfo };
        try
        {
            if (!process.Start())
                throw new IOException("Windows 未能启动获批的 EXE。");
            var handle = Guid.NewGuid().ToString("N");
            _entries.Add(handle, new ProcessEntry(request.ExecutablePath, process));
            return JsonSerializer.Serialize(handle);
        }
        catch
        {
            process.Dispose();
            throw;
        }
    }

    public async Task<bool> WaitAsync(
        ScriptPermissionGrant? grant,
        string? payload,
        Func<int, CancellationToken, Task> pauseAwareDelay,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(pauseAwareDelay);
        var request = ScriptProcessPolicy.ParseWaitRequest(payload);
        var entry = GetOwnedEntry(grant, request.Handle);
        var remainingMilliseconds = request.TimeoutMilliseconds;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (entry.Process.HasExited)
                return true;
            if (remainingMilliseconds <= 0)
                return false;

            var slice = Math.Min(100, remainingMilliseconds);
            await pauseAwareDelay(slice, cancellationToken).ConfigureAwait(false);
            // The injected delay pauses while the script is paused, so subtracting only
            // completed active-time slices keeps Pause from consuming the user's timeout.
            remainingMilliseconds -= slice;
        }
    }

    public string GetInfo(ScriptPermissionGrant? grant, string? payload)
    {
        ThrowIfDisposed();
        var request = ScriptProcessPolicy.ParseHandleRequest(payload);
        var entry = GetOwnedEntry(grant, request.Handle);
        var exited = entry.Process.HasExited;
        var info = new ScriptProcessInfo(
            request.Handle,
            entry.Process.Id,
            exited,
            exited ? entry.Process.ExitCode : null);
        return JsonSerializer.Serialize(info, JsonOptions);
    }

    public void ClearHandles()
    {
        foreach (var entry in _entries.Values)
            entry.Process.Dispose();
        _entries.Clear();
    }

    private ProcessEntry GetOwnedEntry(ScriptPermissionGrant? grant, string handle)
    {
        if (!_entries.TryGetValue(handle, out var entry))
            throw new UnauthorizedAccessException("进程句柄不属于当前脚本运行，或已经失效。");
        grant ??= ScriptPermissionGrant.Empty;
        if (!grant.AllowsProcessStart(entry.ExecutablePath))
            throw new UnauthorizedAccessException("当前运行没有批准该进程 EXE 路径。");
        return entry;
    }

    private static void EnsureNoReparsePoints(string executablePath)
    {
        var root = Path.GetPathRoot(executablePath);
        if (string.IsNullOrEmpty(root))
            throw new InvalidDataException("EXE 必须使用完整路径。");

        var current = root;
        var remainder = executablePath[root.Length..];
        var parts = remainder.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            current = Path.Combine(current, part);
            if (!File.Exists(current) && !Directory.Exists(current))
                throw new FileNotFoundException("EXE 路径中的文件或目录不存在。", current);
            var attributes = File.GetAttributes(current);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
                throw new UnauthorizedAccessException("EXE 路径不能经过符号链接或 reparse point。");
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        ClearHandles();
        _disposed = true;
    }

    private sealed record ProcessEntry(string ExecutablePath, Process Process);
}
