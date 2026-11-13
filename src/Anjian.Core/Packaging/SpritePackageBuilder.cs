using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Anjian.Core;
using Anjian.Core.Imaging;
using Anjian.Core.Parameters;
using Anjian.Core.Permissions;

namespace Anjian.Core.Packaging;

public sealed class SpritePackageBuildOptions
{
    public required string RuntimeDirectory { get; init; }
    public required string ProjectRoot { get; init; }
    public required IReadOnlyList<string> EntryScriptPaths { get; init; }
    public required string OutputDirectory { get; init; }
    public required string ProductName { get; init; }
    public required string Version { get; init; }
    public string Description { get; init; } = "";
    public string Author { get; init; } = "";
    public string Copyright { get; init; } = "";
    public string? AboutText { get; init; }
    public string? IconPath { get; init; }
    public string MinimumApiVersion { get; init; } = "0.7";
    public string MinimumRuntimeVersion { get; init; } = "10.0";
    public SpritePackageRuntimeOptions RuntimeOptions { get; init; } = new(1, 0, null, false, true, 0, false, true);
    public IReadOnlyDictionary<string, string> OcrModelDirectories { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

public sealed record SpritePackageFile(
    string Path,
    long Size,
    string Sha256,
    string Kind);

public sealed record SpritePackageEntry(
    string Id,
    string DisplayName,
    string ScriptPath,
    string? ParameterSchemaPath,
    string ModuleRootPath,
    string? ImageManifestPath,
    IReadOnlyList<string> OcrLanguages,
    string? PermissionManifestPath = null);

public sealed record SpritePackageOcrModel(string Language, string RelativePath, IReadOnlyList<string> Files);

public sealed record SpritePackageRuntimeOptions(
    int RepeatCount,
    int IntervalMilliseconds,
    string? StartupHotkey,
    bool AutoStart,
    bool SingleInstance,
    int MaximumRetries,
    bool AdvancedLog,
    bool CleanupOnExit);

public sealed record SpritePackageManifest(
    int SchemaVersion,
    string PackageId,
    string ProductName,
    string Version,
    string Description,
    string Author,
    string Copyright,
    string? AboutText,
    string LauncherExecutable,
    string? IconPath,
    string MinimumApiVersion,
    string MinimumRuntimeVersion,
    SpritePackageRuntimeOptions RuntimeOptions,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<SpritePackageEntry> Entries,
    IReadOnlyList<SpritePackageOcrModel> OcrModels,
    IReadOnlyList<SpritePackageFile> Files);

public sealed record SpritePackageBuildResult(
    string PackageDirectory,
    string LauncherZip,
    string LauncherZipChecksumPath,
    SpritePackageManifest Manifest,
    IReadOnlyList<string> Warnings);

public sealed record LuaStaticDependencyScan(
    IReadOnlyList<string> RequiredModules,
    IReadOnlyList<string> ImagePaths,
    bool HasDynamicRequire,
    bool HasDynamicImageLoad);

/// <summary>
/// Lexes enough Lua syntax to locate literal require("module") and LoadImage("path") calls
/// without executing the script. Comments and unrelated string contents are ignored.
/// </summary>
public static class LuaStaticDependencyScanner
{
    private enum TokenKind { Identifier, String, Punctuation }
    private readonly record struct Token(TokenKind Kind, string Value);

    public static LuaStaticDependencyScan Scan(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var tokens = ReadTokens(source);
        var modules = new HashSet<string>(StringComparer.Ordinal);
        var images = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var dynamicRequire = false;
        var dynamicImage = false;

        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            if (token.Kind != TokenKind.Identifier ||
                (token.Value != "require" && token.Value != "LoadImage"))
                continue;

            var isRequire = token.Value == "require";
            var argumentIndex = i + 1;
            if (argumentIndex >= tokens.Count)
                continue;

            var hasOpeningParenthesis = tokens[argumentIndex].Value == "(";
            if (hasOpeningParenthesis)
                argumentIndex++;

            if (argumentIndex >= tokens.Count || tokens[argumentIndex].Kind != TokenKind.String ||
                (hasOpeningParenthesis && (argumentIndex + 1 >= tokens.Count || tokens[argumentIndex + 1].Value != ")")))
            {
                if (isRequire) dynamicRequire = true;
                else dynamicImage = true;
                continue;
            }

            var value = tokens[argumentIndex].Value.Trim();
            if (value.Length == 0)
            {
                if (isRequire) dynamicRequire = true;
                else dynamicImage = true;
                continue;
            }

            if (isRequire)
                modules.Add(value);
            else
                images.Add(value);
        }

        return new(
            modules.OrderBy(x => x, StringComparer.Ordinal).ToArray(),
            images.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray(),
            dynamicRequire,
            dynamicImage);
    }

    private static List<Token> ReadTokens(string source)
    {
        var result = new List<Token>();
        var index = 0;
        while (index < source.Length)
        {
            var current = source[index];
            if (char.IsWhiteSpace(current))
            {
                index++;
                continue;
            }

            if (current == '-' && index + 1 < source.Length && source[index + 1] == '-')
            {
                index += 2;
                if (TryReadLongBracket(source, index, out var commentEnd, out _))
                    index = commentEnd;
                else
                    while (index < source.Length && source[index] is not ('\r' or '\n')) index++;
                continue;
            }

            if (current == '[' && TryReadLongBracket(source, index, out var longEnd, out var longValue))
            {
                result.Add(new(TokenKind.String, longValue));
                index = longEnd;
                continue;
            }

            if (current is '"' or '\'')
            {
                var quote = current;
                index++;
                var value = new StringBuilder();
                while (index < source.Length)
                {
                    var character = source[index++];
                    if (character == quote)
                        break;
                    if (character == '\\' && index < source.Length)
                    {
                        var escaped = source[index++];
                        value.Append(escaped switch
                        {
                            'n' => '\n',
                            'r' => '\r',
                            't' => '\t',
                            '\\' => '\\',
                            '"' => '"',
                            '\'' => '\'',
                            _ => escaped
                        });
                    }
                    else
                    {
                        value.Append(character);
                    }
                }
                result.Add(new(TokenKind.String, value.ToString()));
                continue;
            }

            if (char.IsLetter(current) || current == '_')
            {
                var start = index++;
                while (index < source.Length && (char.IsLetterOrDigit(source[index]) || source[index] == '_'))
                    index++;
                result.Add(new(TokenKind.Identifier, source[start..index]));
                continue;
            }

            result.Add(new(TokenKind.Punctuation, current.ToString()));
            index++;
        }
        return result;
    }

    private static bool TryReadLongBracket(string source, int start, out int end, out string value)
    {
        end = start;
        value = "";
        if (start >= source.Length || source[start] != '[')
            return false;

        var cursor = start + 1;
        while (cursor < source.Length && source[cursor] == '=')
            cursor++;
        if (cursor >= source.Length || source[cursor] != '[')
            return false;

        var equals = cursor - start - 1;
        var contentStart = cursor + 1;
        var close = "]" + new string('=', equals) + "]";
        var closeIndex = source.IndexOf(close, contentStart, StringComparison.Ordinal);
        if (closeIndex < 0)
        {
            end = source.Length;
            value = source[contentStart..];
            return true;
        }

        value = source[contentStart..closeIndex];
        end = closeIndex + close.Length;
        return true;
    }
}

public static class SpritePackageBuilder
{
    public const int CurrentManifestVersion = 1;
    public const string ManifestFileName = "Anjian.Sprite.json";
    public const string ChecksumFileName = "SHA256SUMS.txt";
    public const long MaximumPackageBytes = 8L * 1024 * 1024 * 1024;
    private const int MaximumPackageFiles = 20_000;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private sealed class BuildContext(string sourceRoot, string entryRoot)
    {
        public string SourceRoot { get; } = sourceRoot;
        public string EntryRoot { get; } = entryRoot;
        public HashSet<string> CopiedFiles { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<string> ModuleNames { get; } = [];
        public List<string> Warnings { get; } = [];
    }

    public static SpritePackageBuildResult Build(SpritePackageBuildOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ValidateOptions(options);

        var runtimeRoot = Path.GetFullPath(options.RuntimeDirectory);
        var projectRoot = Path.GetFullPath(options.ProjectRoot);
        var outputRoot = Path.GetFullPath(options.OutputDirectory);
        EnsureNotNested(outputRoot, runtimeRoot, "输出目录不能位于运行时目录内部。");
        EnsureNotNested(outputRoot, projectRoot, "输出目录不能位于脚本工程目录内部。");

        var safeName = MakeSafeFileName(options.ProductName);
        var packageFolderName = safeName + "-" + options.Version + "-win-x64";
        var packageDirectory = Path.Combine(outputRoot, packageFolderName);
        var launcherZip = Path.Combine(outputRoot, packageFolderName + "-launcher.zip");
        if (Directory.Exists(packageDirectory) || File.Exists(packageDirectory) ||
            File.Exists(launcherZip) || Directory.Exists(launcherZip))
            throw new IOException("目标输出已存在。为避免覆盖用户交付包，请选择新的输出目录或版本号。");

        Directory.CreateDirectory(outputRoot);
        var staging = Path.Combine(outputRoot, "." + packageFolderName + "." + Guid.NewGuid().ToString("N") + ".tmp");
        var zipTemporary = launcherZip + "." + Guid.NewGuid().ToString("N") + ".tmp";
        var warnings = new List<string>();
        var entryManifests = new List<SpritePackageEntry>();
        var ocrModels = new List<SpritePackageOcrModel>();

        try
        {
            Directory.CreateDirectory(staging);
            var launcherName = safeName + ".exe";
            CopyRuntime(runtimeRoot, staging, launcherName);

            var contentRoot = Path.Combine(staging, "Content", "Entries");
            Directory.CreateDirectory(contentRoot);
            var normalizedEntries = options.EntryScriptPaths
                .Select(Path.GetFullPath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var packageId = CreateStableId(
                "anjian-sprite-package-v1\n" +
                projectRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).ToUpperInvariant() +
                "\n" + options.ProductName.Trim().ToUpperInvariant());

            foreach (var entryScript in normalizedEntries)
            {
                EnsureInside(projectRoot, entryScript, "入口脚本必须位于选择的工程目录中。");
                if (!File.Exists(entryScript))
                    throw new FileNotFoundException("入口脚本不存在。", entryScript);
                if (!string.Equals(Path.GetExtension(entryScript), ".lua", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("入口文件必须是 .lua 脚本。");

                var entryRelativeWithinProject = Path.GetRelativePath(projectRoot, entryScript);
                var entryId = CreateStableId(
                    "anjian-sprite-entry-v1\n" + packageId + "\n" +
                    ToPackagePath(entryRelativeWithinProject).ToUpperInvariant());
                var entryOutputRoot = Path.Combine(contentRoot, entryId);
                Directory.CreateDirectory(entryOutputRoot);
                var scriptOutput = ResolveWithin(entryOutputRoot, entryRelativeWithinProject);
                Directory.CreateDirectory(Path.GetDirectoryName(scriptOutput)!);
                CopyFile(entryScript, scriptOutput);

                var context = new BuildContext(projectRoot, entryOutputRoot);
                var scriptDirectory = Path.GetDirectoryName(entryScript)!;
                var scanQueue = new Queue<string>();
                var visitedModules = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                scanQueue.Enqueue(entryScript);

                while (scanQueue.Count > 0)
                {
                    var currentScript = scanQueue.Dequeue();
                    var scan = LuaStaticDependencyScanner.Scan(File.ReadAllText(currentScript));
                    if (scan.HasDynamicRequire)
                        throw new InvalidDataException($"脚本 {Path.GetRelativePath(projectRoot, currentScript)} 存在动态 require(...)；无法安全确定打包依赖。请改为 require(\"module.name\")。");
                    if (scan.HasDynamicImageLoad)
                    {
                        var manifestPath = Path.ChangeExtension(entryScript, ".images.json");
                        if (!File.Exists(manifestPath))
                            throw new InvalidDataException($"脚本 {Path.GetRelativePath(projectRoot, currentScript)} 存在动态 LoadImage(...)，但没有图像资源清单；无法保证资源完整。");
                        context.Warnings.Add($"{Path.GetRelativePath(projectRoot, currentScript)} 使用动态 LoadImage；已包含对应 .assets 目录全部登记资源，仍无法识别清单外的动态路径。");
                    }

                    foreach (var imagePath in scan.ImagePaths)
                    {
                        var resolvedImage = ResolveSourceFile(scriptDirectory, imagePath, projectRoot);
                        if (!File.Exists(resolvedImage))
                            throw new FileNotFoundException($"脚本引用的图片不存在：{imagePath}", resolvedImage);
                        var relative = Path.GetRelativePath(projectRoot, resolvedImage);
                        var destination = ResolveWithin(entryOutputRoot, relative);
                        CopyFileIfMissing(resolvedImage, destination);
                    }

                    foreach (var module in scan.RequiredModules)
                    {
                        var modulePath = ResolveModule(projectRoot, module);
                        if (visitedModules.Add(modulePath))
                        {
                            var relativeModule = Path.GetRelativePath(projectRoot, modulePath);
                            var moduleDestination = ResolveWithin(Path.Combine(entryOutputRoot, "Modules"), relativeModule);
                            CopyFileIfMissing(modulePath, moduleDestination);
                            scanQueue.Enqueue(modulePath);
                            context.ModuleNames.Add(module);
                        }
                    }
                }

                var parameterPath = Path.ChangeExtension(entryScript, ".params.json");
                string? packagedParameterPath = null;
                if (File.Exists(parameterPath))
                {
                    var info = new FileInfo(parameterPath);
                    if (info.Length > 1_048_576)
                        throw new InvalidDataException($"参数定义超过 1 MiB：{parameterPath}");
                    var schema = JsonSerializer.Deserialize<ScriptParameterSchema>(File.ReadAllText(parameterPath), JsonOptions)
                        ?? throw new InvalidDataException($"参数定义为空：{parameterPath}");
                    var parameterIssues = ScriptParameterValidator.ValidateSchema(schema);
                    if (parameterIssues.Count > 0)
                        throw new InvalidDataException("参数定义校验失败：" + string.Join("；", parameterIssues.Select(x => x.Label + "：" + x.Message)));
                    var destination = ResolveWithin(entryOutputRoot, Path.GetRelativePath(projectRoot, parameterPath));
                    CopyFile(parameterPath, destination);
                    packagedParameterPath = ToPackagePath(Path.GetRelativePath(staging, destination));
                }

                var permissionSourcePath = entryScript + ".permissions.json";
                string? packagedPermissionManifestPath = null;
                if (File.Exists(permissionSourcePath))
                {
                    EnsureInside(projectRoot, permissionSourcePath, "权限 sidecar 必须位于所选工程目录中。");
                    if (HasReparsePointOnPath(projectRoot, ToPackagePath(Path.GetRelativePath(projectRoot, permissionSourcePath))))
                        throw new InvalidDataException("不允许通过符号链接/重解析点打包权限 sidecar：" + permissionSourcePath);
                    var permissionInfo = new FileInfo(permissionSourcePath);
                    if (permissionInfo.Length > ScriptPermissionPolicy.MaximumManifestBytes)
                        throw new InvalidDataException($"权限 sidecar 超过 {ScriptPermissionPolicy.MaximumManifestBytes} 字节上限：{permissionSourcePath}");
                    _ = ScriptPermissionPolicy.ParseRequest(
                        File.ReadAllText(permissionSourcePath),
                        Path.GetDirectoryName(entryScript)!);
                    var destination = ResolveWithin(entryOutputRoot, Path.GetRelativePath(projectRoot, permissionSourcePath));
                    CopyFile(permissionSourcePath, destination);
                    packagedPermissionManifestPath = ToPackagePath(Path.GetRelativePath(staging, destination));
                }

                var imageCatalog = new ImageResourceCatalog(entryScript);
                var imageManifestPath = imageCatalog.ManifestPath;
                string? packagedImageManifest = null;
                if (File.Exists(imageManifestPath))
                {
                    var imageValidation = imageCatalog.ValidateAll();
                    var invalid = imageValidation.Where(x => x.Status != "valid").ToArray();
                    if (invalid.Length > 0)
                        throw new InvalidDataException("图像资源校验失败：" + string.Join("；", invalid.Select(x => x.Name + "：" + x.Message)));
                    var destinationManifest = ResolveWithin(entryOutputRoot, Path.GetRelativePath(projectRoot, imageManifestPath));
                    CopyFile(imageManifestPath, destinationManifest);
                    packagedImageManifest = ToPackagePath(Path.GetRelativePath(staging, destinationManifest));
                    if (Directory.Exists(imageCatalog.AssetDirectory))
                    {
                        var relativeAssets = Path.GetRelativePath(projectRoot, imageCatalog.AssetDirectory);
                        CopyDirectory(imageCatalog.AssetDirectory, ResolveWithin(entryOutputRoot, relativeAssets));
                    }
                }

                var modulesRoot = Path.Combine(entryOutputRoot, "Modules");
                Directory.CreateDirectory(modulesRoot);
                entryManifests.Add(new SpritePackageEntry(
                    entryId,
                    Path.GetFileNameWithoutExtension(entryScript),
                    ToPackagePath(Path.GetRelativePath(staging, scriptOutput)),
                    packagedParameterPath,
                    ToPackagePath(Path.GetRelativePath(staging, modulesRoot)),
                    packagedImageManifest,
                    [],
                    packagedPermissionManifestPath));
                warnings.AddRange(context.Warnings);
            }

            foreach (var model in options.OcrModelDirectories.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
            {
                var language = model.Key.Trim().ToLowerInvariant();
                if (language is not ("ch" or "en" or "japan"))
                    throw new InvalidDataException($"不支持的 OCR 语言模型：{model.Key}");
                var source = Path.GetFullPath(model.Value);
                if (!Directory.Exists(source))
                    throw new DirectoryNotFoundException($"已选择的 OCR 模型目录不存在：{source}");
                var relative = "Content/Ocr/Models";
                var destination = ResolveWithin(staging, relative);
                var modelFiles = CopyDirectoryMerge(source, destination);
                if (modelFiles.Count == 0)
                    throw new InvalidDataException($"OCR 模型目录为空：{source}");
                ocrModels.Add(new SpritePackageOcrModel(language, relative, modelFiles));
                warnings.Add($"已按用户显式选择包含 OCR {language} 模型；运行时仍应使用离线模式。");
            }

            if (ocrModels.Count > 0)
            {
                var languages = ocrModels.Select(x => x.Language).OrderBy(x => x, StringComparer.Ordinal).ToArray();
                entryManifests = entryManifests.Select(entry => entry with { OcrLanguages = languages }).ToList();
            }

            string? iconRelative = null;
            if (!string.IsNullOrWhiteSpace(options.IconPath))
            {
                var icon = Path.GetFullPath(options.IconPath);
                if (!File.Exists(icon))
                    throw new FileNotFoundException("产品图标不存在。", icon);
                var extension = Path.GetExtension(icon);
                if (extension.Length is < 2 or > 8)
                    throw new InvalidDataException("产品图标扩展名无效。");
                var destination = ResolveWithin(staging, "Branding/icon" + extension.ToLowerInvariant());
                CopyFile(icon, destination);
                iconRelative = ToPackagePath(Path.GetRelativePath(staging, destination));
            }

            var buildLog = new StringBuilder()
                .AppendLine("按键精灵 2026 小精灵构建日志")
                .AppendLine($"Product: {options.ProductName}")
                .AppendLine($"Version: {options.Version}")
                .AppendLine($"Entries: {entryManifests.Count}")
                .AppendLine($"Minimum API: {options.MinimumApiVersion}")
                .AppendLine($"Minimum runtime: {options.MinimumRuntimeVersion}")
                .AppendLine("Runtime source: self-contained win-x64 publish directory")
                .AppendLine($"Created: {DateTimeOffset.UtcNow:O}");
            foreach (var warning in warnings.Distinct(StringComparer.Ordinal))
                buildLog.AppendLine("WARNING: " + warning);
            WriteText(Path.Combine(staging, "build.log"), buildLog.ToString());

            var readme = new StringBuilder()
                .AppendLine(options.ProductName)
                .AppendLine("独立小精灵发布包")
                .AppendLine()
                .AppendLine("启动方式：双击 " + launcherName)
                .AppendLine("本包采用 .NET 10 自包含文件夹运行时；EXE 是启动入口，不是单文件程序。请保留全部伴随文件和 Content 目录。")
                .AppendLine("运行参数与用户配置存放在 %LOCALAPPDATA%\\Anjian2026\\Sprites，不写入本发布目录。")
                .AppendLine("离线函数参考：Content/Help/FUNCTION-REFERENCE-CN.md")
                .AppendLine("完整性检查：使用随包提供的 SHA256SUMS.txt 验证文件。")
                .AppendLine();
            WriteText(Path.Combine(staging, "README-Sprite.txt"), readme.ToString());

            var files = EnumeratePackageFiles(staging)
                .Where(path => !string.Equals(Path.GetFileName(path), ManifestFileName, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(Path.GetFileName(path), ChecksumFileName, StringComparison.OrdinalIgnoreCase))
                .Select(path => CreateFileRecord(staging, path, DetermineKind(staging, path)))
                .OrderBy(x => x.Path, StringComparer.Ordinal)
                .ToArray();

            var manifest = new SpritePackageManifest(
                CurrentManifestVersion,
                packageId,
                options.ProductName.Trim(),
                options.Version.Trim(),
                options.Description.Trim(),
                options.Author.Trim(),
                options.Copyright.Trim(),
                options.AboutText,
                launcherName,
                iconRelative,
                options.MinimumApiVersion.Trim(),
                options.MinimumRuntimeVersion.Trim(),
                options.RuntimeOptions,
                DateTimeOffset.UtcNow,
                entryManifests,
                ocrModels,
                files);
            WriteText(Path.Combine(staging, ManifestFileName), JsonSerializer.Serialize(manifest, JsonOptions));

            var checksum = new StringBuilder();
            foreach (var file in files)
                checksum.Append(file.Sha256).Append("  ").Append(file.Path).AppendLine();
            var manifestHash = HashFile(Path.Combine(staging, ManifestFileName));
            checksum.Append(manifestHash).Append("  ").Append(ManifestFileName).AppendLine();
            WriteText(Path.Combine(staging, ChecksumFileName), checksum.ToString());

            var size = EnumeratePackageFiles(staging).Sum(path => new FileInfo(path).Length);
            if (size > MaximumPackageBytes)
                throw new InvalidDataException($"发布包超过 {MaximumPackageBytes / (1024 * 1024 * 1024)} GiB 限制。");
            if (files.Length > MaximumPackageFiles)
                throw new InvalidDataException($"发布包文件数超过 {MaximumPackageFiles} 限制。");

            var verification = Verify(staging);
            if (verification.Count > 0)
                throw new InvalidDataException("发布包生成后的完整性校验失败：" + string.Join("；", verification));

            Directory.Move(staging, packageDirectory);
            ZipFile.CreateFromDirectory(packageDirectory, zipTemporary, CompressionLevel.Optimal, includeBaseDirectory: true);
            File.Move(zipTemporary, launcherZip);
            var zipChecksumPath = launcherZip + ".sha256";
            WriteText(zipChecksumPath, HashFile(launcherZip) + "  " + Path.GetFileName(launcherZip) + Environment.NewLine);
            return new(packageDirectory, launcherZip, zipChecksumPath, manifest, warnings.Distinct(StringComparer.Ordinal).ToArray());
        }
        catch
        {
            try { if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true); } catch (IOException) { }
            try { if (Directory.Exists(packageDirectory)) Directory.Delete(packageDirectory, recursive: true); } catch (IOException) { }
            try { if (File.Exists(zipTemporary)) File.Delete(zipTemporary); } catch (IOException) { }
            try { if (File.Exists(launcherZip)) File.Delete(launcherZip); } catch (IOException) { }
            try { if (File.Exists(launcherZip + ".sha256")) File.Delete(launcherZip + ".sha256"); } catch (IOException) { }
            throw;
        }
    }

    /// <summary>
    /// Revalidates the selected entry's optional permission sidecar against the loaded package manifest.
    /// The sidecar is a request only; this method never creates an authorization grant.
    /// </summary>
    public static IReadOnlyList<string> VerifyEntryPermissionSidecar(
        string packageDirectory,
        SpritePackageManifest manifest,
        SpritePackageEntry entry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageDirectory);
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(entry);
        var issues = new List<string>();
        try
        {
            var root = Path.GetFullPath(packageDirectory);
            if (!IsSafeRelativePath(entry.ScriptPath))
                return ["入口脚本路径无效，无法绑定权限 sidecar：" + entry.ScriptPath];

            var expectedPath = ToPackagePath(entry.ScriptPath) + ".permissions.json";
            var expectedFullPath = ResolveWithin(root, expectedPath);
            var records = (manifest.Files ?? [])
                .Where(file => string.Equals(ToPackagePath(file.Path), expectedPath, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (entry.PermissionManifestPath is null)
            {
                if (File.Exists(expectedFullPath) || records.Length > 0)
                    issues.Add("入口存在权限 sidecar，但 manifest 未将它绑定到该入口：" + expectedPath);
                return issues;
            }

            var boundPath = entry.PermissionManifestPath;
            if (!IsSafeRelativePath(boundPath) ||
                !string.Equals(ToPackagePath(boundPath), expectedPath, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add("权限 sidecar 路径与入口不匹配：" + boundPath);
                return issues;
            }
            if (records.Length != 1)
                issues.Add("权限 sidecar 必须在 manifest files 中唯一登记：" + expectedPath);
            if (!File.Exists(expectedFullPath))
            {
                issues.Add("权限 sidecar 文件缺失：" + expectedPath);
                return issues;
            }
            if (HasReparsePointOnPath(root, expectedPath))
            {
                issues.Add("权限 sidecar 路径包含符号链接/重解析点：" + expectedPath);
                return issues;
            }

            var info = new FileInfo(expectedFullPath);
            if (info.Length > ScriptPermissionPolicy.MaximumManifestBytes)
            {
                issues.Add($"权限 sidecar 超过 {ScriptPermissionPolicy.MaximumManifestBytes} 字节上限：" + expectedPath);
                return issues;
            }

            if (records.Length == 1)
            {
                var record = records[0];
                if (info.Length != record.Size)
                    issues.Add("权限 sidecar 文件大小与 manifest 不一致：" + expectedPath);
                var actualHash = HashFile(expectedFullPath);
                if (!string.Equals(actualHash, record.Sha256, StringComparison.OrdinalIgnoreCase))
                    issues.Add("权限 sidecar SHA-256 不一致：" + expectedPath);
            }

            try
            {
                var scriptFullPath = ResolveWithin(root, entry.ScriptPath);
                ScriptPermissionPolicy.ParseRequest(
                    File.ReadAllText(expectedFullPath),
                    Path.GetDirectoryName(scriptFullPath)!);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException or ArgumentException or NotSupportedException)
            {
                issues.Add("权限 sidecar 无效：" + expectedPath + "：" + ex.Message);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidDataException or NotSupportedException or PathTooLongException)
        {
            issues.Add("无法安全验证权限 sidecar：" + ex.Message);
        }

        return issues.Distinct(StringComparer.Ordinal).ToArray();
    }

    public static IReadOnlyList<string> Verify(string packageDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageDirectory);
        var root = Path.GetFullPath(packageDirectory);
        var manifestPath = Path.Combine(root, ManifestFileName);
        if (!File.Exists(manifestPath))
            return ["缺少 " + ManifestFileName + "。"];

        SpritePackageManifest manifest;
        try
        {
            var info = new FileInfo(manifestPath);
            if (info.Length > 4 * 1024 * 1024)
                return ["小精灵 manifest 超过 4 MiB 限制。"];
            manifest = JsonSerializer.Deserialize<SpritePackageManifest>(File.ReadAllText(manifestPath), JsonOptions)
                ?? throw new InvalidDataException("manifest 为空。");
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException)
        {
            return ["manifest 无法读取：" + ex.Message];
        }

        var issues = new List<string>();
        var manifestFiles = manifest.Files ?? [];
        if (manifest.SchemaVersion != CurrentManifestVersion)
            issues.Add($"不支持 manifest schema 版本 {manifest.SchemaVersion}。");
        if (string.IsNullOrWhiteSpace(manifest.PackageId) || string.IsNullOrWhiteSpace(manifest.ProductName))
            issues.Add("manifest 缺少 PackageId 或 ProductName。");
        if (manifest.RuntimeOptions is null)
            issues.Add("manifest 缺少运行选项。");
        if (manifest.Entries is null || manifest.Entries.Count == 0)
            issues.Add("manifest 至少需要一个入口脚本。");
        if (manifest.OcrModels is null)
            issues.Add("manifest 的 OCR 模型列表缺失。");
        if (!IsSafeRelativePath(manifest.LauncherExecutable))
            issues.Add("启动 EXE 路径无效。");
        else if (!File.Exists(ResolveWithin(root, manifest.LauncherExecutable)))
            issues.Add("启动 EXE 不存在：" + manifest.LauncherExecutable);

        var declared = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long totalBytes = 0;
        foreach (var file in manifestFiles)
        {
            if (!IsSafeRelativePath(file.Path) || !declared.Add(file.Path))
            {
                issues.Add("manifest 文件路径无效或重复：" + file.Path);
                continue;
            }
            var fullPath = ResolveWithin(root, file.Path);
            if (!File.Exists(fullPath))
            {
                issues.Add("文件缺失：" + file.Path);
                continue;
            }
            var info = new FileInfo(fullPath);
            totalBytes += info.Length;
            if (info.Length != file.Size)
            {
                issues.Add("文件大小不一致：" + file.Path);
                continue;
            }
            var actualHash = HashFile(fullPath);
            if (!string.Equals(actualHash, file.Sha256, StringComparison.OrdinalIgnoreCase))
                issues.Add("SHA-256 不一致：" + file.Path);
        }

        if (totalBytes > MaximumPackageBytes)
            issues.Add("已验证文件总大小超过限制。");
        if (manifestFiles.Count > MaximumPackageFiles)
            issues.Add("manifest 文件数超过限制。");

        var manifestRelative = ManifestFileName;
        if (!declared.Add(manifestRelative))
            issues.Add("manifest 不应重复出现在自身 files 列表。");

        var expected = new HashSet<string>(declared, StringComparer.OrdinalIgnoreCase)
        {
            ChecksumFileName
        };
        var actualFiles = EnumeratePackageFiles(root)
            .Select(path => ToPackagePath(Path.GetRelativePath(root, path)))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var path in expected.Except(actualFiles, StringComparer.OrdinalIgnoreCase))
            issues.Add("包内缺少清单文件：" + path);
        foreach (var path in actualFiles.Except(expected, StringComparer.OrdinalIgnoreCase))
            issues.Add("包内存在未登记文件：" + path);

        var checksumPath = Path.Combine(root, ChecksumFileName);
        if (!File.Exists(checksumPath))
        {
            issues.Add("缺少 SHA256SUMS.txt。");
        }
        else
        {
            try
            {
                var rows = File.ReadAllLines(checksumPath);
                var checksumMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var row in rows)
                {
                    var separator = row.IndexOf("  ", StringComparison.Ordinal);
                    if (separator != 64 || row.Length <= separator + 2)
                    {
                        issues.Add("SHA256SUMS.txt 存在无效行。");
                        continue;
                    }
                    checksumMap[row[(separator + 2)..]] = row[..separator];
                }
                foreach (var path in expected.Where(x => x != ChecksumFileName))
                {
                    if (!checksumMap.TryGetValue(path, out var hash))
                    {
                        issues.Add("SHA256SUMS.txt 缺少：" + path);
                        continue;
                    }
                    var fullPath = ResolveWithin(root, path);
                    if (File.Exists(fullPath) && !string.Equals(HashFile(fullPath), hash, StringComparison.OrdinalIgnoreCase))
                        issues.Add("SHA256SUMS.txt 校验失败：" + path);
                }
                foreach (var path in checksumMap.Keys.Except(expected.Where(x => x != ChecksumFileName), StringComparer.OrdinalIgnoreCase))
                    issues.Add("SHA256SUMS.txt 含有未登记路径：" + path);
            }
            catch (IOException ex)
            {
                issues.Add("SHA256SUMS.txt 无法读取：" + ex.Message);
            }
        }

        var requiredRuntimeFiles = new[]
        {
            "Anjian.App.dll", "Anjian.App.deps.json", "Anjian.App.runtimeconfig.json",
            "hostfxr.dll", "coreclr.dll", "Content/Help/FUNCTION-REFERENCE-CN.md"
        };
        foreach (var required in requiredRuntimeFiles)
        {
            if (!declared.Contains(required))
                issues.Add("运行时清单缺少必要文件：" + required);
        }

        foreach (var entry in manifest.Entries ?? [])
        {
            issues.AddRange(VerifyEntryPermissionSidecar(root, manifest, entry));
            if (!IsSafeRelativePath(entry.ScriptPath) || !File.Exists(ResolveWithin(root, entry.ScriptPath)))
                issues.Add("入口脚本缺失或路径无效：" + entry.ScriptPath);
            if (!IsSafeRelativePath(entry.ModuleRootPath) || !Directory.Exists(ResolveWithin(root, entry.ModuleRootPath)))
                issues.Add("模块目录缺失或路径无效：" + entry.ModuleRootPath);
            if (entry.ParameterSchemaPath is not null)
            {
                if (!IsSafeRelativePath(entry.ParameterSchemaPath) || !File.Exists(ResolveWithin(root, entry.ParameterSchemaPath)))
                {
                    issues.Add("参数定义缺失或路径无效：" + entry.ParameterSchemaPath);
                }
                else
                {
                    try
                    {
                        var schema = JsonSerializer.Deserialize<ScriptParameterSchema>(
                            File.ReadAllText(ResolveWithin(root, entry.ParameterSchemaPath)), JsonOptions)
                            ?? throw new InvalidDataException("参数定义为空。");
                        var schemaIssues = ScriptParameterValidator.ValidateSchema(schema);
                        if (schemaIssues.Count > 0)
                            issues.Add("参数定义无效：" + entry.ParameterSchemaPath + "：" +
                                string.Join("；", schemaIssues.Select(x => x.Label + "：" + x.Message)));
                    }
                    catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException)
                    {
                        issues.Add("参数定义无法读取：" + entry.ParameterSchemaPath + "：" + ex.Message);
                    }
                }
            }
            if (entry.ImageManifestPath is not null)
            {
                if (!IsSafeRelativePath(entry.ImageManifestPath) || !File.Exists(ResolveWithin(root, entry.ImageManifestPath)))
                {
                    issues.Add("图像资源清单缺失或路径无效：" + entry.ImageManifestPath);
                }
                else
                {
                    try
                    {
                        var catalog = new ImageResourceCatalog(ResolveWithin(root, entry.ScriptPath));
                        var invalidResources = catalog.ValidateAll().Where(x => x.Status != "valid").ToArray();
                        if (invalidResources.Length > 0)
                            issues.Add("图像资源清单校验失败：" + string.Join("；", invalidResources.Select(x => x.Name + "：" + x.Message)));
                    }
                    catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException or ArgumentException)
                    {
                        issues.Add("图像资源清单无法读取：" + entry.ImageManifestPath + "：" + ex.Message);
                    }
                }
            }
        }

        foreach (var model in manifest.OcrModels ?? [])
        {
            if (!IsSafeRelativePath(model.RelativePath) || !Directory.Exists(ResolveWithin(root, model.RelativePath)))
            {
                issues.Add("OCR 模型目录缺失或路径无效：" + model.RelativePath);
                continue;
            }
            foreach (var file in model.Files ?? [])
            {
                if (!IsSafeRelativePath(file) || !File.Exists(ResolveWithin(ResolveWithin(root, model.RelativePath), file)))
                    issues.Add("OCR 模型文件缺失或路径无效：" + model.Language + "/" + file);
            }
        }

        return issues;
    }

    private static bool HasReparsePointOnPath(string root, string relativePath)
    {
        var current = Path.GetFullPath(root);
        if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            return true;

        foreach (var segment in relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if (!File.Exists(current) && !Directory.Exists(current))
                return false;
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                return true;
        }

        return false;
    }

    private static void ValidateOptions(SpritePackageBuildOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.RuntimeDirectory) || !Directory.Exists(options.RuntimeDirectory))
            throw new DirectoryNotFoundException("请选择已发布的 .NET 10 self-contained win-x64 运行时目录。");
        if (string.IsNullOrWhiteSpace(options.ProjectRoot) || !Directory.Exists(options.ProjectRoot))
            throw new DirectoryNotFoundException("脚本工程目录不存在。");
        if (options.EntryScriptPaths is null || options.EntryScriptPaths.Count == 0)
            throw new InvalidDataException("至少选择一个 Lua 入口脚本。");
        if (string.IsNullOrWhiteSpace(options.OutputDirectory))
            throw new InvalidDataException("输出目录不能为空。");
        if (string.IsNullOrWhiteSpace(options.ProductName) || options.ProductName.Trim().Length > 80)
            throw new InvalidDataException("产品名称必须为 1–80 个字符。");
        if (!System.Text.RegularExpressions.Regex.IsMatch(options.Version, @"^\d+\.\d+\.\d+(?:[-+][0-9A-Za-z.-]+)?$"))
            throw new InvalidDataException("版本号必须符合 SemVer 形式，例如 1.0.0。");
        if (string.IsNullOrWhiteSpace(options.MinimumApiVersion) || options.MinimumApiVersion.Length > 32)
            throw new InvalidDataException("最低 API 版本不能为空或过长。");
        if (string.IsNullOrWhiteSpace(options.MinimumRuntimeVersion) || options.MinimumRuntimeVersion.Length > 32)
            throw new InvalidDataException("最低运行时版本不能为空或过长。");
        if (options.RuntimeOptions.RepeatCount is < 0 or > PlaybackOptions.MaximumRepeatCount)
            throw new InvalidDataException($"重复次数必须为 0–{PlaybackOptions.MaximumRepeatCount}（0 表示无限）。");
        if (options.RuntimeOptions.IntervalMilliseconds is < 0 or > PlaybackOptions.MaximumIntervalMilliseconds)
            throw new InvalidDataException($"轮间隔必须为 0–{PlaybackOptions.MaximumIntervalMilliseconds} ms。");
        if (options.RuntimeOptions.MaximumRetries is < 0 or > 20)
            throw new InvalidDataException("失败重试次数必须为 0–20。");
        if (!options.RuntimeOptions.CleanupOnExit)
            throw new InvalidDataException("退出时释放 Worker 和模拟输入是必需安全项，不能关闭。");
        EnsureSelfContainedRuntime(options.RuntimeDirectory);
    }

    private static void EnsureSelfContainedRuntime(string directory)
    {
        var required = new[]
        {
            "Anjian.App.exe", "Anjian.App.dll", "Anjian.App.deps.json",
            "Anjian.App.runtimeconfig.json", "hostfxr.dll", "coreclr.dll"
        };
        var missing = required.Where(file => !File.Exists(Path.Combine(directory, file))).ToArray();
        if (missing.Length > 0)
            throw new InvalidDataException(
                "运行时目录不是完整的 self-contained win-x64 发布目录。缺少：" +
                string.Join("、", missing) +
                "。请从 Windows x64 自包含发布目录构建，不要把 framework-dependent 开发输出打成小精灵。");
    }

    private static void EnsureNotNested(string candidate, string parent, string message)
    {
        var relative = Path.GetRelativePath(parent, candidate);
        if (relative == "." || (!Path.IsPathRooted(relative) &&
            relative != ".." && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)))
            throw new InvalidDataException(message);
    }

    private static void EnsureInside(string root, string path, string message)
    {
        var relative = Path.GetRelativePath(root, path);
        if (Path.IsPathRooted(relative) || relative == ".." ||
            relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidDataException(message);
    }

    private static string ResolveSourceFile(string baseDirectory, string relativePath, string projectRoot)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
            throw new InvalidDataException("资源路径必须是非空相对路径。");
        var resolved = Path.GetFullPath(Path.Combine(baseDirectory, relativePath));
        EnsureInside(projectRoot, resolved, "资源路径越过工程根目录：" + relativePath);
        return resolved;
    }

    private static string ResolveModule(string root, string moduleName)
    {
        if (string.IsNullOrWhiteSpace(moduleName) || moduleName.Length > 180 ||
            moduleName.Contains('\\') || moduleName.Contains(':') ||
            moduleName.StartsWith('.') || moduleName.EndsWith('.') ||
            moduleName.Split('.').Any(part => part.Length == 0 || part is "." or ".." ||
                part.Any(character => !(char.IsLetterOrDigit(character) || character == '_' || character == '-'))))
            throw new InvalidDataException("require 模块名无效或包含路径跳转：" + moduleName);

        var relative = moduleName.Replace('.', Path.DirectorySeparatorChar);
        var candidates = new[]
        {
            Path.Combine(root, relative + ".lua"),
            Path.Combine(root, relative, "init.lua")
        };
        foreach (var candidate in candidates)
        {
            EnsureInside(root, candidate, "require 模块路径越过工程根目录。");
            if (File.Exists(candidate))
                return candidate;
        }
        throw new FileNotFoundException("找不到 require 模块：" + moduleName +
            "。应存在 " + relative.Replace(Path.DirectorySeparatorChar, '/') + ".lua 或其 init.lua。");
    }

    private static void CopyRuntime(string sourceRoot, string destinationRoot, string launcherName)
    {
        foreach (var source in EnumeratePackageFiles(sourceRoot))
        {
            var relative = Path.GetRelativePath(sourceRoot, source);
            var name = Path.GetFileName(source);
            if (name.Equals("Anjian.App.exe", StringComparison.OrdinalIgnoreCase))
                relative = Path.Combine(Path.GetDirectoryName(relative) ?? "", launcherName);
            else if (name.Equals("FUNCTION-REFERENCE-CN.md", StringComparison.OrdinalIgnoreCase))
                relative = Path.Combine("Content", "Help", name);
            var destination = ResolveWithin(destinationRoot, relative);
            CopyFile(source, destination);
        }
    }

    private static IReadOnlyList<string> CopyDirectory(string sourceDirectory, string destinationDirectory)
    {
        var copied = new List<string>();
        foreach (var source in EnumeratePackageFiles(sourceDirectory))
        {
            var relative = Path.GetRelativePath(sourceDirectory, source);
            var destination = ResolveWithin(destinationDirectory, relative);
            CopyFile(source, destination);
            copied.Add(ToPackagePath(Path.GetRelativePath(destinationDirectory, destination)));
        }
        return copied;
    }

    private static IReadOnlyList<string> CopyDirectoryMerge(string sourceDirectory, string destinationDirectory)
    {
        var copied = new List<string>();
        foreach (var source in EnumeratePackageFiles(sourceDirectory))
        {
            var relative = Path.GetRelativePath(sourceDirectory, source);
            var destination = ResolveWithin(destinationDirectory, relative);
            CopyFileIfMissing(source, destination);
            copied.Add(ToPackagePath(relative));
        }
        return copied;
    }

    private static void CopyFileIfMissing(string source, string destination)
    {
        if (File.Exists(destination))
        {
            if (!string.Equals(HashFile(source), HashFile(destination), StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("同一包内相对路径映射到不同源文件：" + destination);
            return;
        }
        CopyFile(source, destination);
    }

    private static void CopyFile(string source, string destination)
    {
        var attributes = File.GetAttributes(source);
        if ((attributes & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("不允许通过符号链接/重解析点打包文件：" + source);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        input.CopyTo(output);
        output.Flush(flushToDisk: true);
    }

    private static IEnumerable<string> EnumeratePackageFiles(string root)
    {
        if (!Directory.Exists(root))
            yield break;
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            foreach (var directory in Directory.EnumerateDirectories(current))
            {
                var attributes = File.GetAttributes(directory);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("不允许通过符号链接/重解析点遍历目录：" + directory);
                pending.Push(directory);
            }
            foreach (var file in Directory.EnumerateFiles(current))
            {
                var attributes = File.GetAttributes(file);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("不允许通过符号链接/重解析点读取文件：" + file);
                yield return file;
            }
        }
    }

    private static SpritePackageFile CreateFileRecord(string root, string path, string kind)
    {
        var info = new FileInfo(path);
        return new(
            ToPackagePath(Path.GetRelativePath(root, path)),
            info.Length,
            HashFile(path),
            kind);
    }

    private static string DetermineKind(string root, string path)
    {
        var relative = ToPackagePath(Path.GetRelativePath(root, path));
        if (relative.StartsWith("Content/Ocr/", StringComparison.OrdinalIgnoreCase)) return "ocr-model";
        if (relative.StartsWith("Content/Help/", StringComparison.OrdinalIgnoreCase)) return "offline-help";
        if (relative.StartsWith("Content/Entries/", StringComparison.OrdinalIgnoreCase))
        {
            if (relative.EndsWith(".params.json", StringComparison.OrdinalIgnoreCase)) return "parameter-schema";
            if (relative.EndsWith(".images.json", StringComparison.OrdinalIgnoreCase)) return "image-manifest";
            if (relative.Contains(".assets/", StringComparison.OrdinalIgnoreCase)) return "image-asset";
            if (relative.Contains("/Modules/", StringComparison.OrdinalIgnoreCase)) return "lua-module";
            if (relative.EndsWith(".lua", StringComparison.OrdinalIgnoreCase)) return "entry-or-script";
            return "script-resource";
        }
        if (relative.StartsWith("Branding/", StringComparison.OrdinalIgnoreCase)) return "branding";
        if (relative.Equals("build.log", StringComparison.OrdinalIgnoreCase)) return "build-log";
        return "runtime";
    }

    private static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static string CreateStableId(string identity) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)))
            .ToLowerInvariant()[..32];

    private static void WriteText(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough);
        var bytes = new UTF8Encoding(false).GetBytes(content);
        stream.Write(bytes);
        stream.Flush(flushToDisk: true);
    }

    private static string ResolveWithin(string root, string relative)
    {
        if (!IsSafeRelativePath(relative))
            throw new InvalidDataException("路径必须是安全的包内相对路径：" + relative);
        var fullRoot = Path.GetFullPath(root);
        var fullPath = Path.GetFullPath(Path.Combine(fullRoot, relative));
        EnsureInside(fullRoot, fullPath, "包内路径越界：" + relative);
        return fullPath;
    }

    private static bool IsSafeRelativePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) ||
            path.Contains(':') || path.StartsWith('/') || path.StartsWith('\\'))
            return false;
        var normalized = path.Replace('\\', '/');
        return normalized.Split('/').All(segment => segment.Length > 0 && segment is not ("." or ".."));
    }

    private static string ToPackagePath(string path) => path.Replace('\\', '/');

    private static string MakeSafeFileName(string value)
    {
        var forbidden = new HashSet<char>("<>:\"/\\|?*".ToCharArray());
        var builder = new StringBuilder();
        foreach (var character in value.Trim())
        {
            if (char.IsControl(character) || forbidden.Contains(character))
                continue;
            builder.Append(character);
        }
        var result = builder.ToString().Trim().TrimEnd('.');
        if (result.Length == 0 || result is "." or "..")
            throw new InvalidDataException("产品名无法生成有效的 EXE 文件名。");
        if (result.Length > 64)
            result = result[..64].TrimEnd('.');
        return result;
    }
}