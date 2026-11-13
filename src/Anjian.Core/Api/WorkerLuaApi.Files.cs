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
}
