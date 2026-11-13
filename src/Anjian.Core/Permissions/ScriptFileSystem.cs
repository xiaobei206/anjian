using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;

namespace Anjian.Core.Permissions;

/// <summary>
/// Bounded file access for one script run. On Windows, each existing directory
/// component and final file is opened with OPEN_REPARSE_POINT. Directory handles
/// omit FILE_SHARE_DELETE while an operation is in progress, and final file handles
/// are retained for the actual I/O.
/// </summary>
public sealed class ScriptFileSystem
{
    public const int MaximumTextBytes = 4 * 1024 * 1024;
    public const int MaximumListEntries = 1000;

    private const uint GenericRead = 0x80000000;
    private const uint GenericWrite = 0x40000000;
    private const uint FileReadAttributes = 0x00000080;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint OpenExisting = 3;
    private const uint CreateNew = 1;
    private const uint FileFlagBackupSemantics = 0x02000000;
    private const uint FileFlagOpenReparsePoint = 0x00200000;
    private const uint FileAttributeDirectory = 0x00000010;
    private const uint FileAttributeReparsePoint = 0x00000400;
    private const int ErrorFileNotFound = 2;
    private const int ErrorPathNotFound = 3;

    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private readonly ScriptPermissionGrant _grant;
    private readonly string _scriptDirectory;

    public ScriptFileSystem(ScriptPermissionGrant? grant, string scriptDirectory)
    {
        _grant = grant ?? ScriptPermissionGrant.Empty;
        ArgumentException.ThrowIfNullOrWhiteSpace(scriptDirectory);
        _scriptDirectory = Path.GetFullPath(scriptDirectory);
    }

    public string ReadTextFile(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var fullPath = ResolveAndDemandFile(path, write: false);
        using var parents = LockDirectoryChain(Path.GetDirectoryName(fullPath)!);
        var handle = OpenFileHandle(fullPath, GenericRead | FileReadAttributes,
            FileShareRead | FileShareWrite, OpenExisting);
        using var stream = new FileStream(handle, FileAccess.Read, 4096, isAsync: false);
        var bytes = ReadBounded(stream, MaximumTextBytes, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return StrictUtf8.GetString(bytes);
    }

    /// <summary>Writes UTF-8 without BOM through a same-directory temporary file and atomic replacement.</summary>
    public bool WriteTextFile(string path, string content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        cancellationToken.ThrowIfCancellationRequested();
        var bytes = EncodeBounded(content);
        var fullPath = ResolveAndDemandFile(path, write: true);
        var parentPath = Path.GetDirectoryName(fullPath)!;
        using var parents = LockDirectoryChain(parentPath);

        // An existing target is opened without following a link; a raced-in final link
        // is replaced as a directory entry by File.Move, never opened or followed.
        try
        {
            using var existing = OpenFileHandle(fullPath, FileReadAttributes,
                FileShareRead | FileShareWrite, OpenExisting);
        }
        catch (FileNotFoundException) { }
        catch (DirectoryNotFoundException) { }

        var tempPath = Path.Combine(parentPath,
            "." + Path.GetFileName(fullPath) + "." + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            var tempHandle = OpenFileHandle(tempPath, GenericWrite | FileReadAttributes,
                FileShareRead, CreateNew);
            using (var stream = new FileStream(tempHandle, FileAccess.Write, 4096, isAsync: false))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(tempPath, fullPath, overwrite: true);
            // Verify the new entry without following a link before reporting success.
            using var verify = OpenFileHandle(fullPath, FileReadAttributes,
                FileShareRead | FileShareWrite, OpenExisting);
            cancellationToken.ThrowIfCancellationRequested();
            return true;
        }
        finally
        {
            try { File.Delete(tempPath); } catch { }
        }
    }

    public bool AppendTextFile(string path, string content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        cancellationToken.ThrowIfCancellationRequested();
        var bytes = EncodeBounded(content);
        var fullPath = ResolveAndDemandFile(path, write: true);
        using var parents = LockDirectoryChain(Path.GetDirectoryName(fullPath)!);
        var handle = OpenFileHandle(fullPath, GenericWrite | FileReadAttributes,
            FileShareRead, 4); // OPEN_ALWAYS
        using var stream = new FileStream(handle, FileAccess.Write, 4096, isAsync: false);
        if (stream.Length + bytes.LongLength > MaximumTextBytes)
            throw new InvalidDataException($"文本文件超过 {MaximumTextBytes} 字节上限。");
        stream.Position = stream.Length;
        stream.Write(bytes);
        stream.Flush(flushToDisk: true);
        cancellationToken.ThrowIfCancellationRequested();
        return true;
    }

    public bool FileExists(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var fullPath = ResolveAndDemandFile(path, write: false);
        try
        {
            using var parents = LockDirectoryChain(Path.GetDirectoryName(fullPath)!);
            using var handle = OpenFileHandle(fullPath, FileReadAttributes,
                FileShareRead | FileShareWrite, OpenExisting);
            return true;
        }
        catch (FileNotFoundException) { return false; }
        catch (DirectoryNotFoundException) { return false; }
    }

    public bool DirectoryExists(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var fullPath = ResolveAndDemand(path, write: false);
        try
        {
            using var directory = LockDirectoryChain(fullPath);
            return true;
        }
        catch (FileNotFoundException) { return false; }
        catch (DirectoryNotFoundException) { return false; }
    }

    /// <summary>Returns only regular files directly inside a directory; does not recurse.</summary>
    public string[] ListFiles(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var fullPath = ResolveAndDemand(path, write: false);
        using var directory = LockDirectoryChain(fullPath);
        var results = new List<string>();
        var visited = 0;
        foreach (var candidate in Directory.EnumerateFiles(fullPath, "*", SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (++visited > MaximumListEntries)
                throw new InvalidDataException($"目录文件超过 {MaximumListEntries} 项上限。");
            try
            {
                using var handle = OpenFileHandle(candidate, FileReadAttributes,
                    FileShareRead | FileShareWrite, OpenExisting);
                results.Add(Path.GetFileName(candidate));
            }
            catch (Exception ex) when (ex is ReparsePointException or HardLinkException)
            {
                // Links and hard-linked files are not ordinary in-root files.
            }
        }
        return results.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    /// <summary>Returns only verified directories directly inside a directory; does not recurse.</summary>
    public string[] ListDirectories(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var fullPath = ResolveAndDemand(path, write: false);
        using var directory = LockDirectoryChain(fullPath);
        var results = new List<string>();
        var visited = 0;
        foreach (var candidate in Directory.EnumerateDirectories(fullPath, "*", SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (++visited > MaximumListEntries)
                throw new InvalidDataException($"目录子目录超过 {MaximumListEntries} 项上限。");
            try
            {
                using var child = LockDirectoryChain(candidate);
                results.Add(Path.GetFileName(candidate));
            }
            catch (ReparsePointException)
            {
                // Links are not ordinary directories and are intentionally omitted.
            }
        }
        return results.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    /// <summary>Creates one directory level below an existing, verified parent directory.</summary>
    public bool CreateDirectory(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var fullPath = ResolveAndDemand(path, write: true);
        var parentPath = Path.GetDirectoryName(fullPath)
            ?? throw new InvalidDataException("不能把卷根目录作为新建目录。");
        using var parents = LockDirectoryChain(parentPath);

        try
        {
            using var existing = LockDirectoryChain(fullPath);
            return true;
        }
        catch (DirectoryNotFoundException) { }
        catch (FileNotFoundException) { }

        cancellationToken.ThrowIfCancellationRequested();
        if (!CreateDirectoryW(fullPath, IntPtr.Zero))
        {
            var error = Marshal.GetLastWin32Error();
            // ERROR_ALREADY_EXISTS is safe only after an OPEN_REPARSE_POINT verification below.
            if (error != 183)
                throw CreatePathException(fullPath, error, directory: true);
        }

        using var created = LockDirectoryChain(fullPath);
        cancellationToken.ThrowIfCancellationRequested();
        return true;
    }

    /// <summary>Resolve relative paths against the script directory and reject ambiguous/device paths.</summary>
    public static string ResolvePath(string path, string scriptDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(scriptDirectory);
        if (path.StartsWith(@"\\?\", StringComparison.Ordinal) ||
            path.StartsWith(@"\\.\", StringComparison.Ordinal))
            throw new InvalidDataException("不允许使用 Windows 设备或扩展设备路径。");

        var fullPath = Path.IsPathFullyQualified(path)
            ? Path.GetFullPath(path)
            : Path.IsPathRooted(path)
                ? throw new InvalidDataException("不接受盘符相对路径；请使用绝对路径或脚本目录相对路径。")
                : Path.GetFullPath(Path.Combine(scriptDirectory, path));
        if (fullPath.Length > 32760)
            throw new PathTooLongException("文件路径超过 Windows 路径长度上限。");

        var root = Path.GetPathRoot(fullPath) ?? "";
        var remainder = fullPath[root.Length..];
        if (remainder.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                StringSplitOptions.RemoveEmptyEntries).Any(component => component.Contains(':')))
            throw new InvalidDataException("不允许使用 NTFS 备用数据流路径。");
        return Path.TrimEndingDirectorySeparator(fullPath);
    }

    private string ResolveAndDemandFile(string path, bool write)
    {
        var fullPath = ResolveAndDemand(path, write);
        if (Path.GetFileName(fullPath).Length == 0)
            throw new InvalidDataException("文件 API 必须指定文件名，不能访问卷根目录。");
        return fullPath;
    }

    private string ResolveAndDemand(string path, bool write)
    {
        var fullPath = ResolvePath(path, _scriptDirectory);
        var allowed = write ? _grant.AllowsFileWrite(fullPath) : _grant.AllowsFileRead(fullPath);
        if (!allowed)
            throw new UnauthorizedAccessException(write
                ? "文件写入被拒绝：路径不在本次运行已批准的写入目录内。"
                : "文件读取被拒绝：路径不在本次运行已批准的读取目录内。");
        return fullPath;
    }

    private static byte[] EncodeBounded(string content)
    {
        var bytes = StrictUtf8.GetBytes(content);
        if (bytes.Length > MaximumTextBytes)
            throw new InvalidDataException($"文本内容超过 {MaximumTextBytes} 字节上限。");
        return bytes;
    }

    private static byte[] ReadBounded(Stream stream, int maximumBytes, CancellationToken token)
    {
        if (stream.CanSeek && stream.Length > maximumBytes)
            throw new InvalidDataException($"文件超过 {maximumBytes} 字节上限。");
        using var output = new MemoryStream();
        var buffer = new byte[81920];
        while (true)
        {
            token.ThrowIfCancellationRequested();
            var read = stream.Read(buffer, 0, buffer.Length);
            if (read == 0) break;
            if (output.Length + read > maximumBytes)
                throw new InvalidDataException($"文件超过 {maximumBytes} 字节上限。");
            output.Write(buffer, 0, read);
        }
        return output.ToArray();
    }

    private static DirectoryHandleSet LockDirectoryChain(string directoryPath)
    {
        EnsureWindows();
        var fullPath = Path.GetFullPath(directoryPath);
        var root = Path.GetPathRoot(fullPath);
        if (string.IsNullOrEmpty(root))
            throw new InvalidDataException("目录路径不是完整 Windows 路径。");

        var handles = new List<SafeFileHandle>();
        var current = root;
        try
        {
            handles.Add(OpenDirectoryHandle(current));
            var remainder = fullPath[root.Length..];
            var components = remainder.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                StringSplitOptions.RemoveEmptyEntries);
            foreach (var component in components)
            {
                current = Path.Combine(current, component);
                handles.Add(OpenDirectoryHandle(current));
            }
            return new DirectoryHandleSet(handles);
        }
        catch
        {
            foreach (var handle in handles) handle.Dispose();
            throw;
        }
    }

    private static SafeFileHandle OpenDirectoryHandle(string path)
    {
        var handle = CreateFileW(path, FileReadAttributes, FileShareRead | FileShareWrite,
            IntPtr.Zero, OpenExisting, FileFlagBackupSemantics | FileFlagOpenReparsePoint, IntPtr.Zero);
        if (handle.IsInvalid)
        {
            var error = Marshal.GetLastWin32Error();
            handle.Dispose();
            throw CreatePathException(path, error, directory: true);
        }

        try
        {
            var info = GetFileInformation(handle, path);
            if ((info.FileAttributes & FileAttributeReparsePoint) != 0)
                throw new ReparsePointException($"拒绝经过符号链接/重解析点访问目录：{path}");
            if ((info.FileAttributes & FileAttributeDirectory) == 0)
                throw new DirectoryNotFoundException($"路径不是目录：{path}");
            return handle;
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    private static SafeFileHandle OpenFileHandle(
        string path, uint access, uint share, uint disposition)
    {
        EnsureWindows();
        var handle = CreateFileW(path, access, share, IntPtr.Zero, disposition,
            FileFlagOpenReparsePoint, IntPtr.Zero);
        if (handle.IsInvalid)
        {
            var error = Marshal.GetLastWin32Error();
            handle.Dispose();
            throw CreatePathException(path, error, directory: false);
        }

        try
        {
            var info = GetFileInformation(handle, path);
            if ((info.FileAttributes & FileAttributeReparsePoint) != 0)
                throw new ReparsePointException($"拒绝打开符号链接/重解析点文件：{path}");
            if ((info.FileAttributes & FileAttributeDirectory) != 0)
                throw new FileNotFoundException($"目标是目录而不是普通文件：{path}", path);
            if (info.NumberOfLinks > 1)
                throw new HardLinkException($"拒绝访问具有多个硬链接的文件：{path}");
            return handle;
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    private static ByHandleFileInformation GetFileInformation(SafeFileHandle handle, string path)
    {
        if (!GetFileInformationByHandle(handle, out var info))
            throw new IOException($"无法检查文件句柄属性：{path}", new Win32Exception(Marshal.GetLastWin32Error()));
        return info;
    }

    private static Exception CreatePathException(string path, int error, bool directory) => error switch
    {
        ErrorFileNotFound => directory
            ? new DirectoryNotFoundException($"目录不存在：{path}")
            : new FileNotFoundException($"文件不存在：{path}", path),
        ErrorPathNotFound => new DirectoryNotFoundException($"路径不存在：{path}"),
        5 => new UnauthorizedAccessException($"访问文件系统路径被拒绝：{path}", new Win32Exception(error)),
        _ => new IOException($"无法安全打开路径：{path}", new Win32Exception(error))
    };

    private static void EnsureWindows()
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("受控文件访问目前只在 Windows 10/11 上启用。");
    }

    private sealed class ReparsePointException(string message) : UnauthorizedAccessException(message) { }
    private sealed class HardLinkException(string message) : UnauthorizedAccessException(message) { }

    private sealed class DirectoryHandleSet(List<SafeFileHandle> handles) : IDisposable
    {
        public void Dispose()
        {
            for (var index = handles.Count - 1; index >= 0; index--)
                handles[index].Dispose();
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ByHandleFileInformation
    {
        public uint FileAttributes;
        public uint CreationTimeLow;
        public uint CreationTimeHigh;
        public uint LastAccessTimeLow;
        public uint LastAccessTimeHigh;
        public uint LastWriteTimeLow;
        public uint LastWriteTimeHigh;
        public uint VolumeSerialNumber;
        public uint FileSizeHigh;
        public uint FileSizeLow;
        public uint NumberOfLinks;
        public uint FileIndexHigh;
        public uint FileIndexLow;
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(
        string fileName, uint desiredAccess, uint shareMode, IntPtr securityAttributes,
        uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);

    [DllImport("kernel32.dll", EntryPoint = "CreateDirectoryW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateDirectoryW(string path, IntPtr securityAttributes);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(
        SafeFileHandle handle, out ByHandleFileInformation fileInformation);
}

public sealed record ScriptFilePathRequest(string Path);
public sealed record ScriptFileTextRequest(string Path, string Content);

/// <summary>
/// Main-process IPC boundary. This dispatcher always applies the host's current Grant,
/// independently from the check in WorkerLuaApi. Unrecognized IPC operations fail closed.
/// </summary>
public static class ScriptFileSystemHostDispatcher
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static string? Dispatch(
        ScriptPermissionGrant? grant,
        string scriptDirectory,
        string command,
        string? name,
        string? text,
        CancellationToken cancellationToken)
    {
        var fileSystem = new ScriptFileSystem(grant, scriptDirectory);
        cancellationToken.ThrowIfCancellationRequested();
        switch (command)
        {
            case "file-read-text":
                return JsonSerializer.Serialize(fileSystem.ReadTextFile(RequiredPath(name), cancellationToken));
            case "file-write-text":
            {
                var request = ReadTextRequest(text);
                return JsonSerializer.Serialize(fileSystem.WriteTextFile(request.Path, request.Content, cancellationToken));
            }
            case "file-append-text":
            {
                var request = ReadTextRequest(text);
                return JsonSerializer.Serialize(fileSystem.AppendTextFile(request.Path, request.Content, cancellationToken));
            }
            case "file-exists":
                return JsonSerializer.Serialize(fileSystem.FileExists(RequiredPath(name), cancellationToken));
            case "directory-exists":
                return JsonSerializer.Serialize(fileSystem.DirectoryExists(RequiredPath(name), cancellationToken));
            case "directory-list-files":
            {
                var request = ReadPathRequest(text);
                return JsonSerializer.Serialize(fileSystem.ListFiles(request.Path, cancellationToken));
            }
            case "directory-list-directories":
            {
                var request = ReadPathRequest(text);
                return JsonSerializer.Serialize(fileSystem.ListDirectories(request.Path, cancellationToken));
            }
            case "directory-create":
                return JsonSerializer.Serialize(fileSystem.CreateDirectory(RequiredPath(name), cancellationToken));
            default:
                throw new InvalidDataException($"未知文件系统 IPC 命令：{command}");
        }
    }

    private static string RequiredPath(string? path) =>
        string.IsNullOrWhiteSpace(path)
            ? throw new InvalidDataException("文件系统 IPC 缺少路径。")
            : path;

    private static ScriptFilePathRequest ReadPathRequest(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > ScriptFileSystem.MaximumTextBytes)
            throw new InvalidDataException("文件系统 IPC 请求为空或超过大小上限。");
        return JsonSerializer.Deserialize<ScriptFilePathRequest>(json, JsonOptions)
            ?? throw new InvalidDataException("文件系统 IPC 路径请求无效。");
    }

    private static ScriptFileTextRequest ReadTextRequest(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > ScriptFileSystem.MaximumTextBytes * 2)
            throw new InvalidDataException("文件系统 IPC 文本请求为空或超过大小上限。");
        var request = JsonSerializer.Deserialize<ScriptFileTextRequest>(json, JsonOptions)
            ?? throw new InvalidDataException("文件系统 IPC 文本请求无效。");
        if (request.Path is null || request.Content is null)
            throw new InvalidDataException("文件系统 IPC 文本请求缺少路径或内容。");
        return request;
    }
}
