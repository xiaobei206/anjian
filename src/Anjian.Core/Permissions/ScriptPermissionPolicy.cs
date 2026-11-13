using System.Globalization;
using System.Text.Json;

namespace Anjian.Core.Permissions;

/// <summary>
/// A script-authored request. A request never grants access by itself; the host must
/// explicitly turn a requested subset into a per-run grant.
/// </summary>
public sealed record ScriptPermissionRequest(
    IReadOnlyList<string> FileReadRoots,
    IReadOnlyList<string> FileWriteRoots,
    bool ClipboardRead,
    bool ClipboardWrite,
    IReadOnlyList<string> Dialogs,
    IReadOnlyList<string> ProcessExecutables,
    IReadOnlyList<string> HttpHosts,
    IReadOnlyList<string> HttpMethods,
    bool AllowInsecureHttp)
{
    public static ScriptPermissionRequest Empty { get; } = new(
        Array.Empty<string>(), Array.Empty<string>(), false, false,
        Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(),
        Array.Empty<string>(), false);
}

/// <summary>Explicit user choices for one run. All collections default to empty (deny).</summary>
public sealed class ScriptPermissionApproval
{
    public List<string> FileReadRoots { get; init; } = [];
    public List<string> FileWriteRoots { get; init; } = [];
    public bool ClipboardRead { get; init; }
    public bool ClipboardWrite { get; init; }
    public List<string> Dialogs { get; init; } = [];
    public List<string> ProcessExecutables { get; init; } = [];
    public List<string> HttpHosts { get; init; } = [];
    public List<string> HttpMethods { get; init; } = [];
    public bool AllowInsecureHttp { get; init; }
}

/// <summary>
/// Immutable effective capability set passed to a Worker for a single run.
/// The host process must enforce the same set again at every IPC boundary.
/// </summary>
public sealed record ScriptPermissionGrant(
    IReadOnlyList<string> FileReadRoots,
    IReadOnlyList<string> FileWriteRoots,
    bool ClipboardRead,
    bool ClipboardWrite,
    IReadOnlyList<string> Dialogs,
    IReadOnlyList<string> ProcessExecutables,
    IReadOnlyList<string> HttpHosts,
    IReadOnlyList<string> HttpMethods,
    bool AllowInsecureHttp)
{
    public static ScriptPermissionGrant Empty { get; } = new(
        Array.Empty<string>(), Array.Empty<string>(), false, false,
        Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(),
        Array.Empty<string>(), false);

    public bool AllowsFileRead(string path) => IsWithinAnyRoot(path, FileReadRoots);
    public bool AllowsFileWrite(string path) => IsWithinAnyRoot(path, FileWriteRoots);
    public bool AllowsClipboardRead => ClipboardRead;
    public bool AllowsClipboardWrite => ClipboardWrite;
    public bool AllowsDialog(string kind) =>
        Dialogs.Contains(kind, StringComparer.OrdinalIgnoreCase);

    public bool AllowsProcessStart(string executablePath)
    {
        var fullPath = TryFullPath(executablePath);
        return fullPath is not null &&
               ProcessExecutables.Contains(fullPath, PathComparer);
    }

    public bool AllowsHttp(Uri uri, string method)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (!uri.IsAbsoluteUri || !uri.IsDefaultPort ||
            !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Fragment) ||
            !(uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
              (AllowInsecureHttp && uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))) ||
            Uri.CheckHostName(uri.IdnHost) != UriHostNameType.Dns)
            return false;

        string host;
        try { host = ScriptPermissionPolicy.NormalizeHost(uri.IdnHost); }
        catch (ArgumentException) { return false; }

        return HttpHosts.Contains(host, StringComparer.OrdinalIgnoreCase) &&
               HttpMethods.Contains(method, StringComparer.OrdinalIgnoreCase);
    }

    private bool IsWithinAnyRoot(string path, IReadOnlyList<string> roots)
    {
        var target = TryFullPath(path);
        if (target is null)
            return false;

        foreach (var root in roots)
        {
            var normalizedRoot = TryFullPath(root);
            if (normalizedRoot is null)
                continue;

            try
            {
                var relative = Path.GetRelativePath(normalizedRoot, target);
                if (relative == "." ||
                    (!Path.IsPathRooted(relative) &&
                     relative != ".." &&
                     !relative.StartsWith(".." + Path.DirectorySeparatorChar, PathComparison) &&
                     !relative.StartsWith(".." + Path.AltDirectorySeparatorChar, PathComparison)))
                    return true;
            }
            catch (ArgumentException) { }
        }

        return false;
    }

    private static string? TryFullPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;
        try { return Path.GetFullPath(path); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    private static StringComparer PathComparer =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
}

/// <summary>
/// Parses script permission sidecars and creates a grant that is provably a subset of
/// the request. Path checks here are lexical; the Windows host must additionally reject
/// reparse points/symlinks while walking an existing path before opening it.
/// </summary>
public static class ScriptPermissionPolicy
{
    public const int MaximumManifestBytes = 256 * 1024;
    private static readonly HashSet<string> AllowedDialogs =
        new(["message", "input", "confirm"], StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> AllowedHttpMethods =
        new(["GET", "POST", "PUT", "PATCH", "DELETE", "HEAD"], StringComparer.OrdinalIgnoreCase);

    public static ScriptPermissionRequest ParseRequest(string json, string scriptDirectory)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentException.ThrowIfNullOrWhiteSpace(scriptDirectory);
        if (System.Text.Encoding.UTF8.GetByteCount(json) > MaximumManifestBytes)
            throw new InvalidDataException($"权限清单超过 {MaximumManifestBytes} 字节上限。");

        PermissionManifestDto dto;
        try
        {
            dto = JsonSerializer.Deserialize<PermissionManifestDto>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidDataException("权限清单为空。");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("权限清单不是有效 JSON。", ex);
        }

        if (dto.SchemaVersion != 1)
            throw new InvalidDataException($"不支持的权限清单 schemaVersion：{dto.SchemaVersion}。");

        var baseDirectory = Path.GetFullPath(scriptDirectory);
        var readRoots = NormalizePaths(dto.FileReadRoots, baseDirectory, "文件读取根目录");
        var writeRoots = NormalizePaths(dto.FileWriteRoots, baseDirectory, "文件写入根目录");
        var processes = NormalizeExecutables(dto.ProcessExecutables, baseDirectory);
        var dialogs = NormalizeTokens(dto.Dialogs, AllowedDialogs, "对话框能力", lowerCase: true);
        var hosts = NormalizeHosts(dto.HttpHosts);
        var methods = NormalizeTokens(dto.HttpMethods, AllowedHttpMethods, "HTTP 方法", lowerCase: false);

        if (hosts.Count == 0 && methods.Count > 0)
            throw new InvalidDataException("声明 HTTP 方法时必须至少声明一个精确主机名。");
        if (hosts.Count > 0 && methods.Count == 0)
            throw new InvalidDataException("声明 HTTP 主机时必须同时声明允许的方法。");
        if (dto.AllowInsecureHttp && hosts.Count == 0)
            throw new InvalidDataException("请求明文 HTTP 权限时必须同时声明至少一个精确 HTTP 主机。");

        return new ScriptPermissionRequest(readRoots, writeRoots, dto.ClipboardRead,
            dto.ClipboardWrite, dialogs, processes, hosts, methods, dto.AllowInsecureHttp);
    }

    public static ScriptPermissionGrant CreateGrant(
        ScriptPermissionRequest request,
        ScriptPermissionApproval? approval,
        string scriptDirectory)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(scriptDirectory);
        if (approval is null)
            return ScriptPermissionGrant.Empty;

        var baseDirectory = Path.GetFullPath(scriptDirectory);
        var readRoots = NormalizePaths(approval.FileReadRoots, baseDirectory, "已批准的文件读取根目录");
        var writeRoots = NormalizePaths(approval.FileWriteRoots, baseDirectory, "已批准的文件写入根目录");
        var processes = NormalizeExecutables(approval.ProcessExecutables, baseDirectory);
        var dialogs = NormalizeTokens(approval.Dialogs, AllowedDialogs, "已批准的对话框能力", lowerCase: true);
        var hosts = NormalizeHosts(approval.HttpHosts);
        var methods = NormalizeTokens(approval.HttpMethods, AllowedHttpMethods, "已批准的 HTTP 方法", lowerCase: false);

        RequireSubset(readRoots, request.FileReadRoots, "文件读取根目录");
        RequireSubset(writeRoots, request.FileWriteRoots, "文件写入根目录");
        RequireSubset(dialogs, request.Dialogs, "对话框能力");
        RequireSubset(processes, request.ProcessExecutables, "进程可执行文件");
        RequireSubset(hosts, request.HttpHosts, "HTTP 主机");
        RequireSubset(methods, request.HttpMethods, "HTTP 方法");

        if (approval.ClipboardRead && !request.ClipboardRead)
            throw new InvalidDataException("脚本未请求剪贴板读取权限。");
        if (approval.ClipboardWrite && !request.ClipboardWrite)
            throw new InvalidDataException("脚本未请求剪贴板写入权限。");
        if (approval.AllowInsecureHttp && !request.AllowInsecureHttp)
            throw new InvalidDataException("脚本未请求明文 HTTP 权限。");

        return new ScriptPermissionGrant(readRoots, writeRoots, approval.ClipboardRead,
            approval.ClipboardWrite, dialogs, processes, hosts, methods, approval.AllowInsecureHttp);
    }

    public static string NormalizeHost(string host)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        var candidate = host.Trim().TrimEnd('.');
        if (candidate.Length == 0 || candidate.Contains('*') ||
            candidate.Contains('/') || candidate.Contains('\\') ||
            Uri.CheckHostName(candidate) != UriHostNameType.Dns)
            throw new ArgumentException("HTTP 权限必须使用不含通配符的精确 DNS 主机名。", nameof(host));

        try { return new IdnMapping().GetAscii(candidate).ToLowerInvariant(); }
        catch (ArgumentException ex)
        {
            throw new ArgumentException("HTTP 主机名无效。", nameof(host), ex);
        }
    }

    private static IReadOnlyList<string> NormalizePaths(
        IEnumerable<string>? values, string baseDirectory, string label)
    {
        var result = new HashSet<string>(OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal);
        foreach (var value in values ?? Array.Empty<string>())
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidDataException($"{label}不能是空值。");
            try
            {
                var path = Path.IsPathRooted(value)
                    ? Path.GetFullPath(value)
                    : Path.GetFullPath(Path.Combine(baseDirectory, value));
                result.Add(path);
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                throw new InvalidDataException($"{label}路径无效：{value}", ex);
            }
        }
        return result.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static IReadOnlyList<string> NormalizeExecutables(
        IEnumerable<string>? values, string baseDirectory)
    {
        var paths = NormalizePaths(values, baseDirectory, "进程可执行文件");
        foreach (var path in paths)
        {
            if (!Path.IsPathRooted(path) ||
                !Path.GetExtension(path).Equals(".exe", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"进程权限必须指向明确的 .exe 文件：{path}");
        }
        return paths;
    }

    private static IReadOnlyList<string> NormalizeHosts(IEnumerable<string>? values)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in values ?? Array.Empty<string>())
        {
            try { result.Add(NormalizeHost(value)); }
            catch (ArgumentException ex) { throw new InvalidDataException(ex.Message, ex); }
        }
        return result.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static IReadOnlyList<string> NormalizeTokens(
        IEnumerable<string>? values, HashSet<string> allowed, string label, bool lowerCase)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in values ?? Array.Empty<string>())
        {
            var normalized = value?.Trim() ?? "";
            if (!allowed.Contains(normalized))
                throw new InvalidDataException($"{label}不支持：{value}");
            result.Add(lowerCase ? normalized.ToLowerInvariant() : normalized.ToUpperInvariant());
        }
        return result.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static void RequireSubset(
        IReadOnlyList<string> approved,
        IReadOnlyList<string> requested,
        string label)
    {
        var requestedSet = new HashSet<string>(requested, PathValueComparer(label));
        if (approved.Any(value => !requestedSet.Contains(value)))
            throw new InvalidDataException($"批准的{label}超出脚本声明范围。");
    }

    private static StringComparer PathValueComparer(string label) =>
        label.Contains("路径", StringComparison.Ordinal) ||
        label.Contains("可执行文件", StringComparison.Ordinal)
            ? (OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal)
            : StringComparer.OrdinalIgnoreCase;

    private sealed class PermissionManifestDto
    {
        public int SchemaVersion { get; init; }
        public List<string> FileReadRoots { get; init; } = [];
        public List<string> FileWriteRoots { get; init; } = [];
        public bool ClipboardRead { get; init; }
        public bool ClipboardWrite { get; init; }
        public List<string> Dialogs { get; init; } = [];
        public List<string> ProcessExecutables { get; init; } = [];
        public List<string> HttpHosts { get; init; } = [];
        public List<string> HttpMethods { get; init; } = [];
        public bool AllowInsecureHttp { get; init; }
    }
}
