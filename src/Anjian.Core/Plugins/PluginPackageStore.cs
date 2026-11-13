using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Anjian.Core.Permissions;
using Anjian.Core.Api;

namespace Anjian.Core.Plugins;

public sealed class PluginCatalogDocument
{
    public int SchemaVersion { get; set; } = 1;
    public List<PluginCatalogEntry> Plugins { get; set; } = [];
}

public sealed record PluginCatalogEntry(string PluginId, string Version, bool Enabled);

/// <summary>Local-only plugin install/catalog lifecycle. Network access is intentionally absent.</summary>
public sealed class PluginPackageStore
{
    private const int MaximumCatalogBytes = 256 * 1024;
    private const int MaximumZipEntries = PluginPackageValidator.MaximumFiles * 2 + 1;
    private static readonly Regex PluginIdPattern = new("^[a-z0-9]+(?:[.-][a-z0-9]+)*$", RegexOptions.CultureInvariant);
    private static readonly Regex VersionPattern = new("^[0-9]+\\.[0-9]+\\.[0-9]+(?:-[0-9A-Za-z.-]+)?(?:\\+[0-9A-Za-z.-]+)?$", RegexOptions.CultureInvariant);
    private readonly object _sync = new();
    private readonly string _root;
    private readonly string _catalogPath;
    private readonly string _currentApiVersion;

    public string RootDirectory => _root;
    public string CatalogPath => _catalogPath;

    public PluginPackageStore(string rootDirectory, string currentApiVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentApiVersion);
        _root = Path.GetFullPath(rootDirectory);
        _catalogPath = Path.Combine(_root, PluginPackageValidator.CatalogFileName);
        _currentApiVersion = currentApiVersion;
        Directory.CreateDirectory(_root);
        PluginPackageValidator.RejectReparsePoint(_root, "插件存储目录");
        lock (_sync)
            _ = ReadCatalog();
    }

    public PluginPackageSummary InstallDirectory(string sourceDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirectory);
        lock (_sync)
        {
            var source = Path.GetFullPath(sourceDirectory);
            var inspection = PluginPackageValidator.Inspect(source, _currentApiVersion);
            var staging = CreateStagingDirectory();
            try
            {
                CopyPackageToStaging(inspection, staging);
                var copied = PluginPackageValidator.Inspect(staging, _currentApiVersion);
                return InstallStaging(staging, copied);
            }
            finally
            {
                DeleteStagingIfSafe(staging);
            }
        }
    }

    public PluginPackageSummary InstallZip(string zipPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(zipPath);
        lock (_sync)
        {
            var fullZipPath = Path.GetFullPath(zipPath);
            if (!Path.GetExtension(fullZipPath).Equals(".zip", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("插件导入文件必须是 .zip。");
            var zipInfo = new FileInfo(fullZipPath);
            if (!zipInfo.Exists || zipInfo.Length > PluginPackageValidator.MaximumPackageBytes + MaximumCatalogBytes)
                throw new InvalidDataException("插件 ZIP 不存在或压缩文件超过大小上限。");
            PluginPackageValidator.RejectReparsePoint(fullZipPath, "插件 ZIP");
            var staging = CreateStagingDirectory();
            try
            {
                ExtractZipSafely(fullZipPath, staging);
                var inspection = PluginPackageValidator.Inspect(staging, _currentApiVersion);
                return InstallStaging(staging, inspection);
            }
            finally
            {
                DeleteStagingIfSafe(staging);
            }
        }
    }

    public IReadOnlyList<PluginPackageSummary> ListInstalled()
    {
        lock (_sync)
        {
            var catalog = ReadCatalog();
            var result = new List<PluginPackageSummary>(catalog.Plugins.Count);
            foreach (var entry in catalog.Plugins)
            {
                var directory = GetPackageDirectorySafe(entry.PluginId, entry.Version);
                try
                {
                    var inspection = PluginPackageValidator.Inspect(directory, _currentApiVersion);
                    result.Add(ToSummary(inspection, entry.Enabled, null));
                }
                catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or JsonException or NotSupportedException)
                {
                    result.Add(new PluginPackageSummary(entry.PluginId, entry.Version, entry.Enabled,
                        entry.PluginId, "插件包校验失败", directory, null, ex.Message));
                }
            }
            return result.OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(x => x.PluginId, StringComparer.Ordinal)
                .ThenBy(x => x.Version, StringComparer.Ordinal).ToArray();
        }
    }

    public void Enable(string pluginId, string version)
    {
        lock (_sync)
        {
            var item = FindCatalogEntry(ReadCatalog(), pluginId, version);
            if (item is null)
                throw new KeyNotFoundException("插件版本未安装：" + pluginId + " " + version);
            _ = PluginPackageValidator.Inspect(GetPackageDirectorySafe(pluginId, version), _currentApiVersion);
            var catalog = ReadCatalog();
            for (var i = 0; i < catalog.Plugins.Count; i++)
            {
                var candidate = catalog.Plugins[i];
                if (candidate.PluginId.Equals(pluginId, StringComparison.Ordinal))
                    catalog.Plugins[i] = candidate with { Enabled = candidate.Version.Equals(version, StringComparison.Ordinal) };
            }
            ValidateEnabledApiContributions(catalog);
            SaveCatalog(catalog);
        }
    }

    public IReadOnlyList<PluginApiRuntimePlan> GetEnabledApiContributionPlans()
    {
        lock (_sync)
            return BuildEnabledApiContributionPlans(ReadCatalog());
    }

    private IReadOnlyList<PluginApiRuntimePlan> BuildEnabledApiContributionPlans(PluginCatalogDocument catalog)
    {
        var plans = new List<PluginApiRuntimePlan>();
        foreach (var entry in catalog.Plugins.Where(item => item.Enabled))
        {
            var inspection = PluginPackageValidator.Inspect(
                GetPackageDirectorySafe(entry.PluginId, entry.Version), _currentApiVersion);
            if (inspection.Manifest.Contributions is not { Functions.Count: > 0 } contributions)
                continue;
            var modulesRoot = inspection.Manifest.ModulesRoot
                ?? throw new InvalidDataException("插件 API contributions 缺少 modulesRoot。");
            var prefix = modulesRoot + "/";
            var moduleHashes = inspection.FileHashes
                .Where(pair => pair.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                    Path.GetExtension(pair.Key).Equals(".lua", StringComparison.OrdinalIgnoreCase))
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
            plans.Add(new PluginApiRuntimePlan
            {
                PluginId = inspection.Manifest.PluginId,
                Version = inspection.Manifest.Version,
                PackageRoot = inspection.PackageRoot,
                ModulesRootRelativePath = modulesRoot,
                ModuleHashes = moduleHashes,
                Functions = contributions.Functions
            });
        }

        _ = LuaApiRegistry.Load().WithPluginContributions(plans);
        return plans;
    }

    private void ValidateEnabledApiContributions(PluginCatalogDocument catalog) =>
        _ = BuildEnabledApiContributionPlans(catalog);

    public void Disable(string pluginId, string version)
    {
        lock (_sync)
        {
            var catalog = ReadCatalog();
            var index = FindCatalogIndex(catalog, pluginId, version);
            if (index < 0)
                throw new KeyNotFoundException("插件版本未安装：" + pluginId + " " + version);
            catalog.Plugins[index] = catalog.Plugins[index] with { Enabled = false };
            SaveCatalog(catalog);
        }
    }

    public void Uninstall(string pluginId, string version)
    {
        lock (_sync)
        {
            var catalog = ReadCatalog();
            var index = FindCatalogIndex(catalog, pluginId, version);
            if (index < 0)
                throw new KeyNotFoundException("插件版本未安装：" + pluginId + " " + version);
            var item = catalog.Plugins[index];
            if (item.Enabled)
                throw new InvalidOperationException("必须先停用插件，才能卸载。");

            var directory = GetPackageDirectorySafe(pluginId, version);
            if (Directory.Exists(directory))
            {
                EnsureTreeHasNoReparsePoints(directory);
                Directory.Delete(directory, recursive: true);
            }
            else if (File.Exists(directory))
            {
                throw new InvalidDataException("插件版本路径被普通文件占用：" + directory);
            }
            catalog.Plugins.RemoveAt(index);
            SaveCatalog(catalog);
            var pluginDirectory = Path.Combine(_root, pluginId);
            if (Directory.Exists(pluginDirectory) && !Directory.EnumerateFileSystemEntries(pluginDirectory).Any())
                Directory.Delete(pluginDirectory);
        }
    }

    public PluginExecutionPlan ResolveExecutionPlan(
        string pluginId, string version, ScriptPermissionGrant? scriptGrant)
    {
        lock (_sync)
        {
            var catalog = ReadCatalog();
            var item = FindCatalogEntry(catalog, pluginId, version);
            if (item is null)
                throw new KeyNotFoundException("插件版本未安装：" + pluginId + " " + version);
            if (!item.Enabled)
                throw new InvalidOperationException("插件当前处于停用状态：" + pluginId);
            var inspection = PluginPackageValidator.Inspect(GetPackageDirectorySafe(pluginId, version), _currentApiVersion);
            var approved = scriptGrant ?? ScriptPermissionGrant.Empty;

            // Bind the provided grant to the original script request, then to the plugin's declared ceiling.
            var scriptBound = PluginPermissionIntersection.Intersect(inspection.ScriptPermissions, approved);
            var effective = PluginPermissionIntersection.Intersect(inspection.ManifestPermissions, scriptBound);
            var manifest = inspection.Manifest;
            var entryHash = inspection.FileHashes[manifest.Entry];
            var modulesRoot = string.IsNullOrWhiteSpace(manifest.ModulesRoot) ? null : manifest.ModulesRoot;
            var moduleHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (modulesRoot is not null)
            {
                var prefix = modulesRoot + "/";
                foreach (var pair in inspection.FileHashes)
                    if (pair.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                        Path.GetExtension(pair.Key).Equals(".lua", StringComparison.OrdinalIgnoreCase))
                        moduleHashes[pair.Key] = pair.Value;
            }
            if (moduleHashes.Count == 0)
            {
                modulesRoot = null;
                inspection = inspection with { ModulesRootPath = null };
            }

            return new PluginExecutionPlan(
                manifest.PluginId, manifest.Version, inspection.PackageRoot, inspection.EntryPath,
                manifest.Entry, entryHash, inspection.ModulesRootPath, modulesRoot, moduleHashes, effective);
        }
    }

    private PluginPackageSummary InstallStaging(string staging, PluginPackageInspection inspection)
    {
        var manifest = inspection.Manifest;
        var destination = GetPackageDirectorySafe(manifest.PluginId, manifest.Version);
        if (File.Exists(destination))
            throw new IOException("插件目标版本路径已被文件占用：" + destination);

        var catalog = ReadCatalog();
        var existingEntryIndex = FindCatalogIndex(catalog, manifest.PluginId, manifest.Version);
        if (Directory.Exists(destination))
        {
            var existing = PluginPackageValidator.Inspect(destination, _currentApiVersion);
            if (!ManifestsEquivalent(existing.Manifest, manifest))
                throw new IOException("相同 pluginId/version 已存在不同内容；为避免覆盖，安装已拒绝。");
            if (existingEntryIndex < 0)
            {
                catalog.Plugins.Add(new PluginCatalogEntry(manifest.PluginId, manifest.Version, false));
                SaveCatalog(catalog);
            }
            var enabled = existingEntryIndex >= 0 && catalog.Plugins[existingEntryIndex].Enabled;
            return ToSummary(existing, enabled, null);
        }

        var pluginDirectory = Path.Combine(_root, manifest.PluginId);
        Directory.CreateDirectory(pluginDirectory);
        PluginPackageValidator.RejectReparsePoint(pluginDirectory, "插件 ID 目录");
        Directory.Move(staging, destination);
        try
        {
            catalog = ReadCatalog();
            var index = FindCatalogIndex(catalog, manifest.PluginId, manifest.Version);
            if (index >= 0)
                catalog.Plugins[index] = catalog.Plugins[index] with { Enabled = false };
            else
                catalog.Plugins.Add(new PluginCatalogEntry(manifest.PluginId, manifest.Version, false));
            SaveCatalog(catalog);
        }
        catch
        {
            EnsureTreeHasNoReparsePoints(destination);
            Directory.Delete(destination, recursive: true);
            throw;
        }
        return ToSummary(PluginPackageValidator.Inspect(destination, _currentApiVersion), false, null);
    }

    private string CreateStagingDirectory()
    {
        var path = Path.Combine(_root, ".stage-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        PluginPackageValidator.RejectReparsePoint(path, "插件 staging 目录");
        return path;
    }

    private static void CopyPackageToStaging(PluginPackageInspection inspection, string staging)
    {
        var sourceManifest = Path.Combine(inspection.PackageRoot, PluginPackageValidator.ManifestFileName);
        PluginPackageValidator.RejectReparsePathChain(inspection.PackageRoot, PluginPackageValidator.ManifestFileName);
        File.Copy(sourceManifest, Path.Combine(staging, PluginPackageValidator.ManifestFileName));
        foreach (var relative in inspection.FileHashes.Keys)
        {
            PluginPackageValidator.RejectReparsePathChain(inspection.PackageRoot, relative);
            var source = PluginPackageValidator.GetSafeFullPath(inspection.PackageRoot, relative);
            var target = PluginPackageValidator.GetSafeFullPath(staging, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(source, target);
        }
    }

    private static void ExtractZipSafely(string zipPath, string staging)
    {
        using var file = new FileStream(zipPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var archive = new ZipArchive(file, ZipArchiveMode.Read, leaveOpen: false);
        if (archive.Entries.Count is < 1 or > MaximumZipEntries)
            throw new InvalidDataException("插件 ZIP 项目数量不合法。");

        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var files = 0;
        long total = 0;
        foreach (var entry in archive.Entries)
        {
            var rawName = entry.FullName;
            if (string.IsNullOrWhiteSpace(rawName) || rawName.Contains('\\') || rawName.Contains(':') ||
                rawName.StartsWith('/') || rawName.Any(char.IsControl))
                throw new InvalidDataException("ZIP 内含不安全的路径：" + rawName);

            var directory = rawName.EndsWith("/", StringComparison.Ordinal) ||
                            (entry.ExternalAttributes & (int)FileAttributes.Directory) != 0;
            var trimmed = rawName.EndsWith("/", StringComparison.Ordinal) ? rawName[..^1] : rawName;
            var relative = PluginPackageValidator.NormalizeRelativePath(trimmed);
            if (!seenPaths.Add(relative))
                throw new InvalidDataException("ZIP 内含重复路径（大小写不敏感）：" + relative);
            ValidateZipEntryType(entry, directory);
            var unixType = (entry.ExternalAttributes >> 16) & 0xF000;
            if (unixType != 0 && unixType != (directory ? 0x4000 : 0x8000))
                throw new InvalidDataException("ZIP 只允许普通文件和目录，不允许符号链接/特殊文件：" + relative);

            var target = PluginPackageValidator.GetSafeFullPath(staging, relative);
            if (directory)
            {
                if (entry.Length != 0)
                    throw new InvalidDataException("ZIP 目录项不能包含正文：" + relative);
                Directory.CreateDirectory(target);
                continue;
            }

            files++;
            if (files > PluginPackageValidator.MaximumFiles + 1)
                throw new InvalidDataException("ZIP 文件数量超过上限。");
            if (entry.Length < 0 || entry.Length > PluginPackageValidator.MaximumFileBytes)
                throw new InvalidDataException("ZIP 单文件超过上限：" + relative);
            total = checked(total + entry.Length);
            if (total > PluginPackageValidator.MaximumPackageBytes + PluginPackageValidator.MaximumManifestBytes)
                throw new InvalidDataException("ZIP 解压后的内容超过总大小上限。");
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            using var input = entry.Open();
            using var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            var buffer = new byte[64 * 1024];
            long copied = 0;
            int count;
            while ((count = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                copied = checked(copied + count);
                if (copied > entry.Length || copied > PluginPackageValidator.MaximumFileBytes)
                    throw new InvalidDataException("ZIP 文件实际解压大小超出声明：" + relative);
                output.Write(buffer, 0, count);
            }
            if (copied != entry.Length)
                throw new InvalidDataException("ZIP 项长度与实际解压长度不符：" + relative);
            output.Flush(true);
        }
    }

    private static void ValidateZipEntryType(ZipArchiveEntry entry, bool directory)
    {
        var unixType = (entry.ExternalAttributes >> 16) & 0xF000;
        if (unixType != 0 && unixType != (directory ? 0x4000 : 0x8000))
            throw new InvalidDataException("ZIP 包含链接或非普通文件：" + entry.FullName);
        if ((entry.ExternalAttributes & (int)FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("ZIP 项不能是 reparse point：" + entry.FullName);
    }

    private PluginCatalogDocument ReadCatalog()
    {
        if (Directory.Exists(_catalogPath))
            throw new InvalidDataException("插件 catalog 路径被目录占用，未自动覆盖。");
        if (!File.Exists(_catalogPath))
            return new PluginCatalogDocument();
        PluginPackageValidator.RejectReparsePoint(_catalogPath, "插件 catalog");
        var info = new FileInfo(_catalogPath);
        if (info.Length > MaximumCatalogBytes)
            throw new InvalidDataException("插件 catalog 超过大小上限；为避免丢失状态，没有重建或覆盖。");
        PluginCatalogDocument catalog;
        try
        {
            catalog = JsonSerializer.Deserialize<PluginCatalogDocument>(
                new UTF8Encoding(false, true).GetString(
                    PluginPackageValidator.ReadBoundedFile(_root, PluginPackageValidator.CatalogFileName, MaximumCatalogBytes)),
                PluginPackageValidator.JsonOptions)
                ?? throw new InvalidDataException("插件 catalog 为空。");
        }
        catch (Exception ex) when (ex is JsonException or DecoderFallbackException)
        {
            throw new InvalidDataException("插件 catalog 已损坏；为避免丢失状态，没有自动覆盖。", ex);
        }
        if (catalog.SchemaVersion != 1 || catalog.Plugins is null)
            throw new InvalidDataException($"不支持插件 catalog schemaVersion：{catalog.SchemaVersion}。");
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in catalog.Plugins)
        {
            if (entry is null || !PluginIdPattern.IsMatch(entry.PluginId) ||
                !VersionPattern.IsMatch(entry.Version))
                throw new InvalidDataException("插件 catalog 含有无效 pluginId/version。");
            if (!seen.Add(entry.PluginId + "/" + entry.Version))
                throw new InvalidDataException("插件 catalog 存在重复的 pluginId/version。");
        }
        return catalog;
    }

    private void SaveCatalog(PluginCatalogDocument catalog)
    {
        var json = JsonSerializer.Serialize(catalog, PluginPackageValidator.JsonOptions);
        var bytes = new UTF8Encoding(false).GetBytes(json);
        if (bytes.Length > MaximumCatalogBytes)
            throw new InvalidDataException("插件 catalog 会超过大小上限。");
        var temporary = _catalogPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                output.Write(bytes);
                output.Flush(true);
            }
            if (File.Exists(_catalogPath))
                PluginPackageValidator.RejectReparsePoint(_catalogPath, "插件 catalog");
            File.Move(temporary, _catalogPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }

    private string GetPackageDirectorySafe(string pluginId, string version)
    {
        if (!PluginIdPattern.IsMatch(pluginId) || !VersionPattern.IsMatch(version))
            throw new InvalidDataException("插件身份不能映射到安全的存储路径。");
        var pluginDirectory = Path.Combine(_root, pluginId);
        if (Directory.Exists(pluginDirectory))
            PluginPackageValidator.RejectReparsePoint(pluginDirectory, "插件 ID 目录");
        var path = Path.GetFullPath(Path.Combine(pluginDirectory, version));
        var prefix = _root.EndsWith(Path.DirectorySeparatorChar) ? _root : _root + Path.DirectorySeparatorChar;
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!path.StartsWith(prefix, comparison))
            throw new InvalidDataException("插件存储路径越过根目录。");
        if (Directory.Exists(path))
            PluginPackageValidator.RejectReparsePoint(path, "插件版本目录");
        return path;
    }

    private static PluginCatalogEntry? FindCatalogEntry(PluginCatalogDocument catalog, string pluginId, string version)
    {
        var index = FindCatalogIndex(catalog, pluginId, version);
        return index >= 0 ? catalog.Plugins[index] : null;
    }

    private static int FindCatalogIndex(PluginCatalogDocument catalog, string pluginId, string version) =>
        catalog.Plugins.FindIndex(entry =>
            entry.PluginId.Equals(pluginId, StringComparison.Ordinal) &&
            entry.Version.Equals(version, StringComparison.Ordinal));

    private static PluginPackageSummary ToSummary(
        PluginPackageInspection inspection, bool enabled, string? error) =>
        new(inspection.Manifest.PluginId, inspection.Manifest.Version, enabled, inspection.Manifest.Name,
            inspection.Manifest.Description, inspection.PackageRoot, inspection.EntryPath, error);

    private static bool ManifestsEquivalent(PluginManifest left, PluginManifest right)
    {
        if (left.PluginId != right.PluginId || left.Version != right.Version ||
            left.Name != right.Name || left.Description != right.Description || left.Author != right.Author ||
            left.MinApiVersion != right.MinApiVersion || left.MaxApiVersion != right.MaxApiVersion ||
            left.Entry != right.Entry || left.ModulesRoot != right.ModulesRoot)
            return false;
        var leftPermissions = JsonSerializer.Serialize(left.Permissions, PluginPackageValidator.JsonOptions);
        var rightPermissions = JsonSerializer.Serialize(right.Permissions, PluginPackageValidator.JsonOptions);
        var leftContributions = JsonSerializer.Serialize(left.Contributions, PluginPackageValidator.JsonOptions);
        var rightContributions = JsonSerializer.Serialize(right.Contributions, PluginPackageValidator.JsonOptions);
        if (leftContributions != rightContributions || leftPermissions != rightPermissions || left.Files.Count != right.Files.Count)
            return false;
        var rightHashes = right.Files.ToDictionary(x => x.Path, x => x.Sha256, StringComparer.OrdinalIgnoreCase);
        return left.Files.All(x => rightHashes.TryGetValue(x.Path, out var hash) &&
            hash.Equals(x.Sha256, StringComparison.OrdinalIgnoreCase));
    }

    private static void EnsureTreeHasNoReparsePoints(string directory)
    {
        PluginPackageValidator.RejectReparsePoint(directory, "卸载目标目录");
        foreach (var path in Directory.EnumerateFileSystemEntries(directory))
        {
            PluginPackageValidator.RejectReparsePoint(path, "卸载目标内容");
            if ((File.GetAttributes(path) & FileAttributes.Directory) != 0)
                EnsureTreeHasNoReparsePoints(path);
        }
    }

    private static void DeleteStagingIfSafe(string directory)
    {
        if (!Directory.Exists(directory))
            return;
        try
        {
            EnsureTreeHasNoReparsePoints(directory);
            Directory.Delete(directory, recursive: true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
