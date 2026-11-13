using System.Text.Json;

namespace Anjian.Core.Permissions;

public sealed record ScriptProcessStartRequest(string ExecutablePath, IReadOnlyList<string> Arguments);
public sealed record ScriptProcessHandleRequest(string Handle);
public sealed record ScriptProcessWaitRequest(string Handle, int TimeoutMilliseconds);
public sealed record ScriptProcessInfo(string ProcessHandle, int ProcessId, bool HasExited, int? ExitCode);

/// <summary>Shared worker/host validation for exact-path process APIs.</summary>
public static class ScriptProcessPolicy
{
    public const int MaximumProcessesPerRun = 32;
    public const int MaximumArgumentCount = 64;
    public const int MaximumArgumentLength = 8_192;
    public const int MaximumArgumentsTotalLength = 32_768;
    public const int MaximumWaitMilliseconds = 600_000;
    public const int DefaultWaitMilliseconds = 30_000;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static ScriptProcessStartRequest CreateStartRequest(
        ScriptPermissionGrant? grant,
        string executablePath,
        IEnumerable<string>? arguments,
        string scriptDirectory)
    {
        grant ??= ScriptPermissionGrant.Empty;
        ArgumentException.ThrowIfNullOrWhiteSpace(scriptDirectory);
        var fullPath = NormalizeExecutablePath(executablePath, scriptDirectory);
        if (!grant.AllowsProcessStart(fullPath))
            throw new UnauthorizedAccessException("脚本没有获得本次运行启动该 EXE 的权限。");

        var values = (arguments ?? Array.Empty<string>()).ToArray();
        ValidateArguments(values);
        return new ScriptProcessStartRequest(fullPath, values);
    }

    public static ScriptProcessStartRequest ParseStartRequest(
        ScriptPermissionGrant? grant,
        string? payload,
        string scriptDirectory)
    {
        if (string.IsNullOrWhiteSpace(payload))
            throw new InvalidDataException("进程启动 IPC 缺少请求数据。");

        ScriptProcessStartRequest request;
        try
        {
            request = JsonSerializer.Deserialize<ScriptProcessStartRequest>(payload, JsonOptions)
                ?? throw new InvalidDataException("进程启动请求为空。");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("进程启动请求不是有效 JSON。", ex);
        }

        if (request.Arguments is null)
            throw new InvalidDataException("进程启动请求缺少参数数组。");
        return CreateStartRequest(grant, request.ExecutablePath, request.Arguments, scriptDirectory);
    }

    public static ScriptProcessWaitRequest CreateWaitRequest(string processHandle, double timeoutMilliseconds)
    {
        var timeout = ValidateTimeout(timeoutMilliseconds);
        return new ScriptProcessWaitRequest(NormalizeHandle(processHandle), timeout);
    }

    public static ScriptProcessWaitRequest ParseWaitRequest(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
            throw new InvalidDataException("进程等待 IPC 缺少请求数据。");

        ScriptProcessWaitRequest request;
        try
        {
            request = JsonSerializer.Deserialize<ScriptProcessWaitRequest>(payload, JsonOptions)
                ?? throw new InvalidDataException("进程等待请求为空。");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("进程等待请求不是有效 JSON。", ex);
        }

        if (request.TimeoutMilliseconds is < 0 or > MaximumWaitMilliseconds)
            throw new InvalidDataException($"进程等待超时必须在 0–{MaximumWaitMilliseconds}ms 范围内。");
        return new ScriptProcessWaitRequest(NormalizeHandle(request.Handle), request.TimeoutMilliseconds);
    }

    public static ScriptProcessHandleRequest CreateHandleRequest(string processHandle) =>
        new(NormalizeHandle(processHandle));

    public static ScriptProcessHandleRequest ParseHandleRequest(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
            throw new InvalidDataException("进程查询 IPC 缺少句柄。");

        ScriptProcessHandleRequest request;
        try
        {
            request = JsonSerializer.Deserialize<ScriptProcessHandleRequest>(payload, JsonOptions)
                ?? throw new InvalidDataException("进程查询请求为空。");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("进程查询请求不是有效 JSON。", ex);
        }

        return CreateHandleRequest(request.Handle);
    }

    public static string NormalizeExecutablePath(string executablePath, string scriptDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(scriptDirectory);
        if (executablePath.IndexOf('\0') >= 0 || executablePath.Length > 32_767)
            throw new InvalidDataException("EXE 路径包含无效字符或超过长度上限。");

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(Path.IsPathRooted(executablePath)
                ? executablePath
                : Path.Combine(scriptDirectory, executablePath));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new InvalidDataException("EXE 路径无效。", ex);
        }

        if (!Path.GetExtension(fullPath).Equals(".exe", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("进程 API 只允许启动 .exe 文件。");
        return fullPath;
    }

    public static string NormalizeHandle(string? processHandle)
    {
        if (string.IsNullOrWhiteSpace(processHandle) ||
            !Guid.TryParseExact(processHandle, "N", out var value))
            throw new InvalidDataException("进程句柄无效；必须使用当前运行 StartProcess 返回的句柄。");
        return value.ToString("N");
    }

    public static void ValidateArguments(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        if (arguments.Count > MaximumArgumentCount)
            throw new ArgumentOutOfRangeException(nameof(arguments), $"进程参数最多 {MaximumArgumentCount} 项。");

        var total = 0;
        foreach (var argument in arguments)
        {
            if (argument is null)
                throw new InvalidDataException("进程参数不能为 null。");
            if (argument.IndexOf('\0') >= 0)
                throw new InvalidDataException("进程参数不能包含 NUL 字符。");
            if (argument.Length > MaximumArgumentLength)
                throw new ArgumentOutOfRangeException(nameof(arguments), $"单项进程参数最多 {MaximumArgumentLength} 个字符。");
            total = checked(total + argument.Length);
            if (total > MaximumArgumentsTotalLength)
                throw new ArgumentOutOfRangeException(nameof(arguments), $"进程参数总长度最多 {MaximumArgumentsTotalLength} 个字符。");
        }
    }

    private static int ValidateTimeout(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) ||
            value != Math.Truncate(value) || value < 0 || value > MaximumWaitMilliseconds)
            throw new ArgumentOutOfRangeException(nameof(value),
                $"等待时间必须是 0–{MaximumWaitMilliseconds} 之间的整数毫秒。");
        return checked((int)value);
    }
}
