using System.Text.Json;
using Anjian.Core.Permissions;
using NLua;

namespace Anjian.Core.Api;

public sealed partial class WorkerLuaApi
{
    public string ReadTextFile(string path)
    {
        var fullPath = ResolveFileApiPath(path, write: false);
        var response = pipe.Request("file-read-text", name: fullPath);
        if (string.IsNullOrWhiteSpace(response))
            throw new IOException("文件读取 IPC 没有返回数据。");
        return JsonSerializer.Deserialize<string>(response)
            ?? throw new InvalidDataException("文件读取 IPC 返回了无效文本。");
    }

    public bool WriteTextFile(string path, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var fullPath = ResolveFileApiPath(path, write: true);
        return RequestFileBoolean("file-write-text", new ScriptFileTextRequest(fullPath, text));
    }

    public bool AppendTextFile(string path, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var fullPath = ResolveFileApiPath(path, write: true);
        return RequestFileBoolean("file-append-text", new ScriptFileTextRequest(fullPath, text));
    }

    public bool FileExists(string path)
    {
        var fullPath = ResolveFileApiPath(path, write: false);
        return RequestFilePathBoolean("file-exists", fullPath);
    }

    public bool DirectoryExists(string path)
    {
        var fullPath = ResolveFileApiPath(path, write: false);
        return RequestFilePathBoolean("directory-exists", fullPath);
    }

    public LuaTable ListFiles(string path)
    {
        var fullPath = ResolveFileApiPath(path, write: false);
        return RequestTable("directory-list-files", new ScriptFilePathRequest(fullPath))
            ?? throw new InvalidDataException("文件枚举 IPC 没有返回列表。");
    }

    public LuaTable ListDirectories(string path)
    {
        var fullPath = ResolveFileApiPath(path, write: false);
        return RequestTable("directory-list-directories", new ScriptFilePathRequest(fullPath))
            ?? throw new InvalidDataException("目录枚举 IPC 没有返回列表。");
    }

    public bool CreateDirectory(string path)
    {
        var fullPath = ResolveFileApiPath(path, write: true);
        return RequestFilePathBoolean("directory-create", fullPath);
    }

    public bool CopyFile(string sourcePath, string destinationPath, bool overwrite = false)
    {
        var source = ResolveFileApiPath(sourcePath, write: false);
        var destination = ResolveFileApiPath(destinationPath, write: true);
        if (PathsEqual(source, destination))
            throw new InvalidDataException("源文件与目标文件不能是同一路径。");
        return RequestFileBoolean("file-copy",
            new ScriptFileTransferRequest(source, destination, overwrite));
    }

    public bool MoveFile(string sourcePath, string destinationPath, bool overwrite = false)
    {
        var source = ResolveFileApiPath(sourcePath, write: false);
        _ = ResolveFileApiPath(sourcePath, write: true);
        var destination = ResolveFileApiPath(destinationPath, write: true);
        return RequestFileBoolean("file-move",
            new ScriptFileTransferRequest(source, destination, overwrite));
    }

    public bool RenameFile(string path, string newName, bool overwrite = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newName);
        if (newName is "." or ".." || newName.Contains('/') || newName.Contains('\\') ||
            newName.Contains(':') || Path.IsPathFullyQualified(newName))
            throw new InvalidDataException("新文件名必须是同一目录中的普通文件名。");

        var source = ResolveFileApiPath(path, write: false);
        _ = ResolveFileApiPath(path, write: true);
        _ = ResolveFileApiPath(
            Path.Combine(Path.GetDirectoryName(source)!, newName), write: true);
        return RequestFileBoolean("file-rename",
            new ScriptFileRenameRequest(source, newName, overwrite));
    }

    public string CreateTempFile(string directoryPath)
    {
        var directory = ResolveFileApiPath(directoryPath, write: true);
        return RequestFileString("file-create-temp", directory);
    }

    public string CreateTempDirectory(string parentDirectoryPath)
    {
        var parent = ResolveFileApiPath(parentDirectoryPath, write: true);
        return RequestFileString("directory-create-temp", parent);
    }

    private static bool PathsEqual(string left, string right) =>
        string.Equals(left, right, OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal);


    private string ResolveFileApiPath(string path, bool write)
    {
        var fullPath = ScriptFileSystem.ResolvePath(path, ScriptDirectory);
        var allowed = write
            ? Permissions.AllowsFileWrite(fullPath)
            : Permissions.AllowsFileRead(fullPath);
        if (!allowed)
            throw new UnauthorizedAccessException(write
                ? "文件写入被拒绝：路径不在本次运行已批准的写入目录内。"
                : "文件读取被拒绝：路径不在本次运行已批准的读取目录内。");
        return fullPath;
    }

    private bool RequestFileBoolean(string type, object payload)
    {
        var response = pipe.Request(type, text: JsonSerializer.Serialize(payload));
        if (!bool.TryParse(response, out var result))
            throw new InvalidDataException($"文件 API {type} 返回了无效布尔值。");
        return result;
    }

    private bool RequestFilePathBoolean(string type, string path)
    {
        var response = pipe.Request(type, name: path);
        if (!bool.TryParse(response, out var result))
            throw new InvalidDataException($"文件 API {type} 返回了无效布尔值。");
        return result;
    }

    private string RequestFileString(string type, string path)
    {
        var response = pipe.Request(type, name: path);
        if (string.IsNullOrWhiteSpace(response))
            throw new IOException($"文件 API {type} 没有返回路径。");
        return JsonSerializer.Deserialize<string>(response)
            ?? throw new InvalidDataException($"文件 API {type} 返回了无效路径。");
    }
}
