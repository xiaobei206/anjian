using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Anjian.Core.Permissions;
using Anjian.Core.Api;

namespace Anjian.Core.Plugins;

public sealed class PluginManifest
{
    public int SchemaVersion { get; set; } = 1;
    public string PluginId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Version { get; set; } = "";
    public string Description { get; set; } = "";
    public string Author { get; set; } = "";
    public string MinApiVersion { get; set; } = "";
    public string? MaxApiVersion { get; set; }
    public string Entry { get; set; } = "";
    public string? ModulesRoot { get; set; }
    public PluginPermissionManifest Permissions { get; set; } = new();
    public PluginApiContributionSet? Contributions { get; set; }
    public List<PluginPackageFile> Files { get; set; } = [];
}

public sealed class PluginPermissionManifest
{
    public int SchemaVersion { get; set; } = 1;
    public List<string> FileReadRoots { get; set; } = [];
    public List<string> FileWriteRoots { get; set; } = [];
    public bool ClipboardRead { get; set; }
    public bool ClipboardWrite { get; set; }
    public List<string> Dialogs { get; set; } = [];
    public List<string> ProcessExecutables { get; set; } = [];
    public List<string> HttpHosts { get; set; } = [];
    public List<string> HttpMethods { get; set; } = [];
    public bool AllowInsecureHttp { get; set; }
}

public sealed class PluginPackageFile
{
    public string Path { get; set; } = "";
    public string Sha256 { get; set; } = "";
}

public sealed record PluginPackageInspection(
    PluginManifest Manifest,
    string PackageRoot,
    string EntryPath,
    string? ModulesRootPath,
    IReadOnlyDictionary<string, string> FileHashes,
    ScriptPermissionRequest ManifestPermissions,
    ScriptPermissionRequest ScriptPermissions);

public sealed record PluginExecutionPlan(
    string PluginId,
    string Version,
    string PackageRoot,
    string EntryPath,
    string EntryRelativePath,
    string EntrySha256,
    string? ModulesRootPath,
    string? ModulesRootRelativePath,
    IReadOnlyDictionary<string, string> ModuleHashes,
    ScriptPermissionGrant Permissions);

public sealed record PluginPackageSummary(
    string PluginId,
    string Version,
    bool Enabled,
    string Name,
    string Description,
    string PackageDirectory,
    string? EntryPath,
    string? ValidationError);

public static class PluginPackageValidator
{
    public const int CurrentManifestVersion = 1;
    public const string ManifestFileName = "Anjian.Plugin.json";
    public const string CatalogFileName = "Anjian.Plugins.json";
    public const long MaximumPackageBytes = 64L * 1024 * 1024;
    public const long MaximumFileBytes = 8L * 1024 * 1024;
    public const int MaximumFiles = 2_048;
    public const int MaximumManifestBytes = 256 * 1024;
    private const int BufferSize = 64 * 1024;
    private static readonly Regex PluginIdPattern = new("^[a-z0-9]+(?:[.-][a-z0-9]+)*$", RegexOptions.CultureInvariant);
    private static readonly Regex SemanticVersionPattern = new("^[0-9]+\\.[0-9]+\\.[0-9]+(?:-[0-9A-Za-z.-]+)?(?:\\+[0-9A-Za-z.-]+)?$", RegexOptions.CultureInvariant);
    private static readonly Regex ApiVersionPattern = new("^[0-9]+\\.[0-9]+$", RegexOptions.CultureInvariant);
    private static readonly Regex Sha256Pattern = new("^[0-9A-Fa-f]{64}$", RegexOptions.CultureInvariant);
    private static readonly Regex ModuleSegmentPattern = new("^[A-Za-z0-9_-]+$", RegexOptions.CultureInvariant);
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".lua", ".json", ".md", ".txt", ".png", ".jpg", ".jpeg", ".ico"
    };

    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 32,
        WriteIndented = true
    };

    public static PluginPackageInspection Inspect(string packageDirectory, string currentApiVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageDirectory);
        var root = Path.GetFullPath(packageDirectory);
        if (!Directory.Exists(root))
            throw new DirectoryNotFoundException("插件包目录不存在：" + root);
        RejectReparsePoint(root, "插件包根目录");

        var manifestBytes = ReadBoundedFile(root, ManifestFileName, MaximumManifestBytes);
        PluginManifest manifest;
        try
        {
            var manifestJson = new UTF8Encoding(false, true).GetString(manifestBytes);
            using var document = JsonDocument.Parse(manifestJson, new JsonDocumentOptions { MaxDepth = 32 });
            RequireSchemaVersion(document.RootElement, "插件 manifest");
            _ = GetRequiredPropertyValue(document.RootElement, "entry", "插件 entry");
            var permissionElement = GetRequiredProperty(document.RootElement, "permissions", "插件 permissions");
            RequireSchemaVersion(permissionElement, "插件 permissions");
            var contributionProperties = document.RootElement.EnumerateObject()
                .Where(property => property.Name.Equals("contributions", StringComparison.OrdinalIgnoreCase)).ToArray();
            if (contributionProperties.Length > 1)
                throw new InvalidDataException("插件 contributions 只能声明一次。");
            if (contributionProperties.Length == 1 &&
                contributionProperties[0].Value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Object))
                throw new InvalidDataException("插件 contributions 必须是 JSON 对象或省略。");
            if (contributionProperties.Length == 1 &&
                contributionProperties[0].Value.ValueKind == JsonValueKind.Object)
                RequireSchemaVersion(contributionProperties[0].Value, "插件 contributions");
            manifest = JsonSerializer.Deserialize<PluginManifest>(manifestJson, JsonOptions)
                ?? throw new InvalidDataException("插件 manifest 为空。");
        }
        catch (Exception ex) when (ex is JsonException or DecoderFallbackException)
        {
            throw new InvalidDataException("插件 manifest 不是有效 UTF-8 JSON，或包含未知字段。", ex);
        }

        ValidateManifestMetadata(manifest, currentApiVersion);
        var hashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in manifest.Files)
        {
            if (file is null)
                throw new InvalidDataException("manifest files 不能包含 null 项。");
            var relative = NormalizeRelativePath(file.Path);
            if (relative.Equals(ManifestFileName, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("manifest 本身不能列入 files。");
            var sha256 = file.Sha256 ?? "";
            if (!Sha256Pattern.IsMatch(sha256))
                throw new InvalidDataException("文件 SHA-256 必须是 64 位十六进制：" + relative);
            ValidateAllowedFileType(relative);
            if (!hashes.TryAdd(relative, sha256.ToUpperInvariant()))
                throw new InvalidDataException("manifest 存在重复文件路径：" + relative);
        }

        if (!hashes.ContainsKey(manifest.Entry))
            throw new InvalidDataException("entry 必须列入 files 并提供 SHA-256：" + manifest.Entry);

        string? modulesRootPath = null;
        string? normalizedModulesRoot = null;
        if (!string.IsNullOrWhiteSpace(manifest.ModulesRoot))
        {
            normalizedModulesRoot = NormalizeRelativePath(manifest.ModulesRoot);
            if (normalizedModulesRoot.Contains('/') ||
                normalizedModulesRoot.Equals(ManifestFileName, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("modulesRoot 必须是包根目录下的单级目录名。");
            modulesRootPath = GetSafeFullPath(root, normalizedModulesRoot);
            ValidateModuleNames(hashes.Keys, normalizedModulesRoot);
        }

        var actualFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var actualDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        EnumerateTree(root, root, actualFiles, actualDirectories);
        if (!actualFiles.Contains(ManifestFileName))
            throw new InvalidDataException("插件 manifest 缺失。");
        var actualPayload = new HashSet<string>(actualFiles, StringComparer.OrdinalIgnoreCase);
        actualPayload.Remove(ManifestFileName);
        if (!actualPayload.SetEquals(hashes.Keys))
        {
            var missing = hashes.Keys.Except(actualPayload, StringComparer.OrdinalIgnoreCase).Take(8);
            var extra = actualPayload.Except(hashes.Keys, StringComparer.OrdinalIgnoreCase).Take(8);
            throw new InvalidDataException("插件文件集与 manifest 不闭合。缺失=[" +
                string.Join(", ", missing) + "]；未登记=[" + string.Join(", ", extra) + "]。");
        }
        var expectedDirectories = ExpectedDirectories(hashes.Keys);
        if (!actualDirectories.SetEquals(expectedDirectories))
        {
            var extra = actualDirectories.Except(expectedDirectories, StringComparer.OrdinalIgnoreCase).Take(8);
            throw new InvalidDataException("插件包存在未声明的空目录或额外目录：" + string.Join(", ", extra));
        }
        if (hashes.Count > MaximumFiles)
            throw new InvalidDataException($"插件文件数超过 {MaximumFiles} 上限。");

        long totalBytes = 0;
        foreach (var pair in hashes)
        {
            var path = GetSafeFullPath(root, pair.Key);
            var actualHash = ComputeSha256(path, MaximumFileBytes, out var length);
            totalBytes = checked(totalBytes + length);
            if (totalBytes > MaximumPackageBytes)
                throw new InvalidDataException($"插件内容总大小超过 {MaximumPackageBytes} 字节上限。");
            if (!actualHash.Equals(pair.Value, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("插件文件 SHA-256 不匹配：" + pair.Key);
        }

        ValidateApiContributions(manifest.Contributions, normalizedModulesRoot, hashes.Keys);

        ScriptPermissionRequest manifestPermissions;
        try
        {
            ValidatePermissionManifest(manifest.Permissions);
            var permissionJson = JsonSerializer.Serialize(manifest.Permissions, JsonOptions);
            manifestPermissions = ScriptPermissionPolicy.ParseRequest(permissionJson, root);
        }
        catch (Exception ex) when (ex is InvalidDataException or ArgumentException or JsonException or NullReferenceException)
        {
            throw new InvalidDataException("插件 manifest 的 permissions 无效。", ex);
        }

        var scriptPermissions = ScriptPermissionRequest.Empty;
        var sidecarRelative = manifest.Entry + ".permissions.json";
        var sidecarPath = GetSafeFullPath(root, sidecarRelative);
        if (File.Exists(sidecarPath))
        {
            if (!hashes.ContainsKey(sidecarRelative))
                throw new InvalidDataException("入口权限 sidecar 必须包含在 manifest files 中：" + sidecarRelative);
            try
            {
                var sidecar = new UTF8Encoding(false, true).GetString(
                    ReadBoundedFile(root, sidecarRelative, ScriptPermissionPolicy.MaximumManifestBytes));
                scriptPermissions = ScriptPermissionPolicy.ParseRequest(sidecar, root);
            }
            catch (Exception ex) when (ex is InvalidDataException or ArgumentException or JsonException or DecoderFallbackException)
            {
                throw new InvalidDataException("插件入口脚本权限 sidecar 无效。", ex);
            }
        }

        return new PluginPackageInspection(
            manifest, root, GetSafeFullPath(root, manifest.Entry), modulesRootPath,
            new Dictionary<string, string>(hashes, StringComparer.OrdinalIgnoreCase),
            manifestPermissions, scriptPermissions);
    }


    private static readonly Regex ContributionIdPattern =
        new("^[a-z][a-z0-9]*(?:[.-][a-z0-9]+)*$", RegexOptions.CultureInvariant);
    private static readonly Regex LuaIdentifierPattern =
        new("^[A-Za-z_][A-Za-z0-9_]{0,63}$", RegexOptions.CultureInvariant);
    private static readonly Regex LuaModuleNamePattern =
        new("^[A-Za-z0-9_-]+(?:\\.[A-Za-z0-9_-]+)*$", RegexOptions.CultureInvariant);
    private static readonly HashSet<string> LuaReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "and","break","do","else","elseif","end","false","for","function","goto","if","in","local",
        "nil","not","or","repeat","return","then","true","until","while","_G","_VERSION","assert",
        "collectgarbage","coroutine","debug","dofile","error","getmetatable","io","ipairs","load",
        "loadfile","math","next","os","package","pairs","pcall","print","rawequal","rawget","rawlen",
        "rawset","require","select","setmetatable","string","table","tonumber","tostring","type",
        "utf8","warn","xpcall","luanet"
    };

    private static void ValidateApiContributions(
        PluginApiContributionSet? contributions,
        string? modulesRoot,
        IEnumerable<string> filePaths)
    {
        if (contributions is null)
            return;
        if (contributions.SchemaVersion != 1 || contributions.Functions is null ||
            contributions.Functions.Count > 64)
            throw new InvalidDataException("插件 contributions 必须为 schemaVersion=1，且最多声明 64 个函数。");
        if (contributions.Functions.Count == 0)
            return;
        if (string.IsNullOrWhiteSpace(modulesRoot))
            throw new InvalidDataException("声明插件 API 函数时必须提供 modulesRoot。");

        var files = filePaths.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var baseNames = LuaApiRegistry.Load().Apis.Select(api => api.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var function in contributions.Functions)
        {
            if (function is null)
                throw new InvalidDataException("插件 API 函数清单不能包含 null。");
            if (string.IsNullOrWhiteSpace(function.Id) || function.Id.Length > 80 ||
                !ContributionIdPattern.IsMatch(function.Id) || !ids.Add(function.Id))
                throw new InvalidDataException("插件 API 函数 id 必须稳定、唯一并符合小写 ASCII 格式。");
            if (string.IsNullOrWhiteSpace(function.Name) || !LuaIdentifierPattern.IsMatch(function.Name) ||
                function.Name.StartsWith("__", StringComparison.Ordinal) ||
                LuaReservedNames.Contains(function.Name) || baseNames.Contains(function.Name) ||
                !names.Add(function.Name))
                throw new InvalidDataException("插件 API 函数名非法、保留或与内置/其他贡献函数冲突：" + function.Name);
            if (string.IsNullOrWhiteSpace(function.Title) || function.Title.Length > 128 ||
                string.IsNullOrWhiteSpace(function.Description) || function.Description.Length > 2048 ||
                string.IsNullOrWhiteSpace(function.Example) || function.Example.Length > 4096 ||
                function.Title.Any(char.IsControl) || function.Description.Any(char.IsControl))
                throw new InvalidDataException("插件 API 标题、说明或示例缺失/超长。");
            if (string.IsNullOrWhiteSpace(function.Module) || !LuaModuleNamePattern.IsMatch(function.Module) ||
                string.IsNullOrWhiteSpace(function.Export) || !LuaIdentifierPattern.IsMatch(function.Export))
                throw new InvalidDataException("插件 API 必须引用有效的 modulesRoot 模块名与导出标识符。");
            var moduleRelative = ResolveContributionModulePath(modulesRoot, function.Module, files);
            if (!files.Contains(moduleRelative))
                throw new InvalidDataException("插件 API 引用的 Lua 模块未在 files/hash 清单内：" + moduleRelative);
            if (function.Parameters is null || function.Parameters.Count > 16 ||
                function.Parameters.Any(parameter => parameter is null))
                throw new InvalidDataException("插件 API 参数最多 16 个且不能为 null。");
            var parameterNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var parameter in function.Parameters)
            {
                if (string.IsNullOrWhiteSpace(parameter.Name) || !LuaIdentifierPattern.IsMatch(parameter.Name) ||
                    !parameterNames.Add(parameter.Name) || !parameter.Required ||
                    parameter.DefaultValue.HasValue && parameter.DefaultValue.Value.ValueKind != JsonValueKind.Null ||
                    parameter.Type is not ("string" or "number" or "integer" or "boolean") ||
                    string.IsNullOrWhiteSpace(parameter.Description) || parameter.Description.Length > 512 ||
                    parameter.Minimum.HasValue || parameter.Maximum.HasValue || !string.IsNullOrEmpty(parameter.Unit) ||
                    parameter.AllowedValues.Count != 0 || parameter.ValuePatterns.Count != 0 ||
                    parameter.MinimumCount.HasValue || parameter.Variadic)
                    throw new InvalidDataException("插件 API 参数仅支持必填标量 string/number/integer/boolean，且提示元数据必须与运行时契约一致。");
            }
            var expectedSignature = function.Name + "(" + string.Join(", ", function.Parameters.Select(parameter => parameter.Name)) + ")";
            if (!string.Equals(function.Signature, expectedSignature, StringComparison.Ordinal))
                throw new InvalidDataException("插件 API signature 必须与函数名和有序参数名完全一致：" + function.Name);
            if (function.Returns is null ||
                function.Returns.Type is not ("string" or "number" or "integer" or "boolean" or "table" or "nil") ||
                string.IsNullOrWhiteSpace(function.Returns.Description) || function.Returns.Description.Length > 512)
                throw new InvalidDataException("插件 API 返回值必须声明受限类型和说明。");
            if (function.Errors is null || function.Errors.Count == 0 || function.Errors.Count > 16 ||
                function.Errors.Any(error => string.IsNullOrWhiteSpace(error) || error.Length > 512))
                throw new InvalidDataException("插件 API 必须声明 1–16 个明确错误契约。");
            if (function.Behavior is null ||
                string.IsNullOrWhiteSpace(function.Behavior.PauseBehavior) ||
                string.IsNullOrWhiteSpace(function.Behavior.CancellationBehavior) ||
                string.IsNullOrWhiteSpace(function.Behavior.CoordinateSpace) ||
                string.IsNullOrWhiteSpace(function.Behavior.PermissionNotes) ||
                function.Behavior.SideEffects is null || function.Behavior.SideEffects.Count != 0)
                throw new InvalidDataException("插件 API 必须声明运行语义；纯 Lua helper 不得声明外部副作用。");
        }
    }

    public static string ResolveContributionModulePath(
        string modulesRoot, string moduleName, IEnumerable<string> manifestFilePaths)
    {
        var root = NormalizeRelativePath(modulesRoot);
        if (!LuaModuleNamePattern.IsMatch(moduleName ?? ""))
            throw new InvalidDataException("插件 API 模块名不合法：" + moduleName);
        var relative = moduleName.Replace('.', '/');
        var candidates = new[] { root + "/" + relative + ".lua", root + "/" + relative + "/init.lua" };
        var files = manifestFilePaths.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var matches = candidates.Where(files.Contains).ToArray();
        if (matches.Length != 1)
            throw new InvalidDataException("插件 API 模块必须且只能映射到一个已登记的 Lua 文件：" + moduleName);
        return matches[0];
    }

    public static string NormalizeRelativePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Length > 1_024 ||
            path.Contains('\\') || path.Contains(':') || path.StartsWith('/') ||
            Path.IsPathRooted(path) || path.Any(char.IsControl))
            throw new InvalidDataException("插件路径必须是安全的相对正斜线路径：" + path);
        var parts = path.Split('/');
        if (parts.Any(part => part.Length == 0 || part is "." or ".." ||
            part.EndsWith(' ') || part.EndsWith('.') || part.Any(char.IsControl)))
            throw new InvalidDataException("插件路径包含空段、点段或 Windows 非规范路径：" + path);
        return string.Join('/', parts);
    }

    public static string GetSafeFullPath(string packageRoot, string relativePath)
    {
        var normalized = NormalizeRelativePath(relativePath);
        var root = Path.GetFullPath(packageRoot);
        var full = Path.GetFullPath(Path.Combine(root, normalized.Replace('/', Path.DirectorySeparatorChar)));
        var prefix = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!full.StartsWith(prefix, comparison) || full.Equals(root, comparison))
            throw new InvalidDataException("插件路径越过包根目录：" + relativePath);
        return full;
    }

    public static string ComputeSha256(string path) =>
        ComputeSha256(path, MaximumFileBytes, out _);

    public static string ReadVerifiedText(
        string packageRoot, string relativePath, string expectedSha256, long maximumBytes = MaximumFileBytes)
    {
        try
        {
            return new UTF8Encoding(false, true).GetString(ReadVerifiedBytes(packageRoot, relativePath, expectedSha256, maximumBytes));
        }
        catch (DecoderFallbackException ex)
        {
            throw new InvalidDataException("插件 Lua/文本文件不是有效 UTF-8：" + relativePath, ex);
        }
    }

    public static byte[] ReadVerifiedBytes(
        string packageRoot, string relativePath, string expectedSha256, long maximumBytes = MaximumFileBytes)
    {
        var bytes = ReadBoundedFile(packageRoot, relativePath, maximumBytes);
        var actual = Convert.ToHexString(SHA256.HashData(bytes));
        if (!actual.Equals(expectedSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("插件文件在安装后发生变化，SHA-256 校验失败：" + relativePath);
        return bytes;
    }

    internal static byte[] ReadBoundedFile(string packageRoot, string relativePath, long maximumBytes)
    {
        var fullPath = GetSafeFullPath(packageRoot, relativePath);
        RejectReparsePathChain(packageRoot, relativePath);
        using var input = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read,
            BufferSize, FileOptions.SequentialScan);
        using var output = new MemoryStream((int)Math.Min(maximumBytes, BufferSize));
        var buffer = new byte[BufferSize];
        long total = 0;
        int count;
        while ((count = input.Read(buffer, 0, buffer.Length)) > 0)
        {
            total = checked(total + count);
            if (total > maximumBytes)
                throw new InvalidDataException($"插件文件超过 {maximumBytes} 字节上限：" + relativePath);
            output.Write(buffer, 0, count);
        }
        return output.ToArray();
    }

    internal static string ComputeSha256(string path, long maximumBytes, out long length)
    {
        RejectReparsePoint(path, "插件文件");
        if ((File.GetAttributes(path) & FileAttributes.Directory) != 0)
            throw new InvalidDataException("插件内容项必须为普通文件：" + path);
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            BufferSize, FileOptions.SequentialScan);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[BufferSize];
        length = 0;
        int count;
        while ((count = input.Read(buffer, 0, buffer.Length)) > 0)
        {
            length = checked(length + count);
            if (length > maximumBytes)
                throw new InvalidDataException($"插件文件超过 {maximumBytes} 字节上限：" + path);
            hash.AppendData(buffer, 0, count);
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    internal static void RejectReparsePathChain(string packageRoot, string relativePath)
    {
        var root = Path.GetFullPath(packageRoot);
        RejectReparsePoint(root, "插件包目录");
        var current = root;
        foreach (var segment in NormalizeRelativePath(relativePath).Split('/'))
        {
            current = Path.Combine(current, segment);
            if (File.Exists(current) || Directory.Exists(current))
                RejectReparsePoint(current, "插件路径");
        }
    }

    internal static void RejectReparsePoint(string path, string label)
    {
        var attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException(label + "不能是符号链接或重解析点：" + path);
    }


    private static void RequireSchemaVersion(JsonElement element, string label)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException(label + " 必须是 JSON 对象。");
        var properties = element.EnumerateObject()
            .Where(property => property.Name.Equals("schemaVersion", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (properties.Length != 1 ||
            properties[0].Value.ValueKind != JsonValueKind.Number ||
            !properties[0].Value.TryGetInt32(out var version) ||
            version != CurrentManifestVersion)
            throw new InvalidDataException($"{label} 必须且只能声明 schemaVersion={CurrentManifestVersion}。");
    }

    private static JsonElement GetRequiredProperty(JsonElement element, string name, string label)
    {
        var value = GetRequiredPropertyValue(element, name, label);
        if (value.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException(label + " 必须是 JSON 对象。");
        return value;
    }

    private static JsonElement GetRequiredPropertyValue(JsonElement element, string name, string label)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException(label + " 缺失或类型错误。");
        var matches = element.EnumerateObject()
            .Where(property => property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (matches.Length != 1)
            throw new InvalidDataException(label + " 必须且只能声明一次。");
        return matches[0].Value;
    }

    private static void ValidateManifestMetadata(PluginManifest manifest, string currentApiVersion)
    {
        if (manifest.SchemaVersion != CurrentManifestVersion)
            throw new InvalidDataException($"不支持插件 manifest schemaVersion：{manifest.SchemaVersion}。");
        if (string.IsNullOrWhiteSpace(manifest.PluginId) || manifest.PluginId.Length > 160 || !PluginIdPattern.IsMatch(manifest.PluginId))
            throw new InvalidDataException("pluginId 必须是稳定、全小写的 ASCII ID。");
        if (string.IsNullOrWhiteSpace(manifest.Name) || manifest.Name.Length > 128 || manifest.Name.Any(char.IsControl))
            throw new InvalidDataException("插件 name 不能为空、长度最多 128，且不能包含控制字符。");
        if ((manifest.Description?.Length ?? 0) > 2_048 || (manifest.Author?.Length ?? 0) > 256 ||
            manifest.Description?.Any(char.IsControl) == true || manifest.Author?.Any(char.IsControl) == true)
            throw new InvalidDataException("插件 description/author 长度或字符不合法。");
        if (string.IsNullOrWhiteSpace(manifest.Version) || manifest.Version.Length > 128 ||
            !SemanticVersionPattern.IsMatch(manifest.Version))
            throw new InvalidDataException("插件 version 必须使用 major.minor.patch 语义版本格式。");

        var current = ParseApiVersion(currentApiVersion, "当前 API 版本");
        var minimum = ParseApiVersion(manifest.MinApiVersion, "minApiVersion");
        if (minimum.CompareTo(current) > 0)
            throw new InvalidDataException($"插件要求最低 API {manifest.MinApiVersion}，当前仅有 {currentApiVersion}。");
        if (!string.IsNullOrWhiteSpace(manifest.MaxApiVersion))
        {
            var maximum = ParseApiVersion(manifest.MaxApiVersion, "maxApiVersion");
            if (maximum.CompareTo(minimum) < 0 || maximum.CompareTo(current) < 0)
                throw new InvalidDataException($"插件 API 兼容区间与当前版本 {currentApiVersion} 不相交。");
        }

        var entry = NormalizeRelativePath(manifest.Entry);
        if (!entry.Equals(manifest.Entry, StringComparison.Ordinal) || entry.Contains('/') ||
            !Path.GetExtension(entry).Equals(".lua", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("entry 必须是包根目录下的单个 .lua 文件。");
        if (manifest.Permissions is null || manifest.Files is null)
            throw new InvalidDataException("manifest permissions/files 不能为 null。");
        if (manifest.Files.Count is < 1 or > MaximumFiles)
            throw new InvalidDataException($"manifest files 必须包含 1–{MaximumFiles} 个内容文件。");
        if (!string.IsNullOrWhiteSpace(manifest.ModulesRoot))
        {
            var root = NormalizeRelativePath(manifest.ModulesRoot);
            if (root.Contains('/') || root.Equals(ManifestFileName, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("modulesRoot 必须是包根目录下的单级目录名。");
        }
    }

    private static void ValidatePermissionManifest(PluginPermissionManifest permissions)
    {
        if (permissions.FileReadRoots is null || permissions.FileWriteRoots is null ||
            permissions.Dialogs is null || permissions.ProcessExecutables is null ||
            permissions.HttpHosts is null || permissions.HttpMethods is null)
            throw new InvalidDataException("permissions 中的集合不能为 null。");
        foreach (var value in permissions.FileReadRoots.Concat(permissions.FileWriteRoots))
        {
            if (value == ".")
                continue;
            _ = NormalizeRelativePath(value);
        }
        foreach (var executable in permissions.ProcessExecutables)
        {
            if (string.IsNullOrWhiteSpace(executable) || executable.Any(char.IsControl))
                throw new InvalidDataException("processExecutables 不能包含空值或控制字符。");
            if (Path.IsPathRooted(executable))
            {
                if (!Path.IsPathFullyQualified(executable))
                    throw new InvalidDataException("processExecutables 的绝对路径必须完全限定。");
                _ = Path.GetFullPath(executable);
            }
            else
            {
                _ = NormalizeRelativePath(executable);
            }
        }
        if (permissions.Dialogs.Any(string.IsNullOrWhiteSpace) ||
            permissions.ProcessExecutables.Any(string.IsNullOrWhiteSpace) ||
            permissions.HttpHosts.Any(string.IsNullOrWhiteSpace) ||
            permissions.HttpMethods.Any(string.IsNullOrWhiteSpace))
            throw new InvalidDataException("permissions 中不能包含空值。");
    }

    private static void ValidateAllowedFileType(string path)
    {
        if (!AllowedExtensions.Contains(Path.GetExtension(path)))
            throw new InvalidDataException("插件包包含不允许的文件类型：" + path);
    }

    private static void ValidateModuleNames(IEnumerable<string> paths, string modulesRoot)
    {
        var prefix = modulesRoot + "/";
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in paths)
        {
            if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                continue;
            if (!Path.GetExtension(path).Equals(".lua", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("modulesRoot 内只允许 Lua 模块：" + path);
            var relative = path[prefix.Length..];
            string name;
            if (relative.EndsWith("/init.lua", StringComparison.OrdinalIgnoreCase))
                name = relative[..^9].Replace('/', '.');
            else if (relative.Equals("init.lua", StringComparison.OrdinalIgnoreCase))
                name = "";
            else
                name = relative[..^4].Replace('/', '.');
            if (name.Length == 0 || name.Split('.').Any(part => !ModuleSegmentPattern.IsMatch(part)))
                throw new InvalidDataException("模块文件名无法映射到稳定的 require 名称：" + path);
            if (!names.Add(name))
                throw new InvalidDataException("插件模块名称冲突：" + name);
        }
    }

    private static void EnumerateTree(
        string root, string directory, HashSet<string> files, HashSet<string> directories)
    {
        foreach (var path in Directory.EnumerateFileSystemEntries(directory))
        {
            RejectReparsePoint(path, "插件包文件/目录");
            var attrs = File.GetAttributes(path);
            var relative = NormalizeRelativePath(Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/'));
            if ((attrs & FileAttributes.Directory) != 0)
            {
                if (!directories.Add(relative))
                    throw new InvalidDataException("插件包存在重复目录路径：" + relative);
                EnumerateTree(root, path, files, directories);
            }
            else if (!files.Add(relative))
            {
                throw new InvalidDataException("插件包存在重复文件路径：" + relative);
            }
        }
    }

    private static HashSet<string> ExpectedDirectories(IEnumerable<string> paths)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in paths.Append(ManifestFileName))
        {
            var pieces = path.Split('/');
            for (var i = 1; i < pieces.Length; i++)
                result.Add(string.Join('/', pieces.Take(i)));
        }
        return result;
    }

    private static (int Major, int Minor) ParseApiVersion(string value, string label)
    {
        if (string.IsNullOrWhiteSpace(value) || !ApiVersionPattern.IsMatch(value))
            throw new InvalidDataException(label + " 必须是 major.minor 格式。");
        var parts = value.Split('.');
        if (!int.TryParse(parts[0], out var major) || !int.TryParse(parts[1], out var minor))
            throw new InvalidDataException(label + " 数值无效。");
        return (major, minor);
    }
}
