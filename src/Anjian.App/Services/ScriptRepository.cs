using Anjian.App.Models;
using System.Text;
using Anjian.Core.Recorder;

namespace Anjian.App.Services;

public sealed class ScriptRepository
{
    public string RootDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Anjian2026", "Scripts");

    public ScriptRepository() => Directory.CreateDirectory(RootDirectory);

    public IReadOnlyList<ScriptEntry> GetScripts() =>
        Directory.EnumerateFiles(RootDirectory, "*.lua", SearchOption.TopDirectoryOnly)
            .Select(path => new ScriptEntry(Path.GetFileNameWithoutExtension(path), path))
            .OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

    public ScriptEntry Create(string? name = null)
    {
        var baseName = MakeSafeName(string.IsNullOrWhiteSpace(name)
            ? $"新建脚本_{DateTime.Now:yyyyMMdd_HHmmss}"
            : name);
        var candidate = baseName;
        var index = 2;
        while (File.Exists(GetPath(candidate)))
            candidate = $"{baseName}_{index++}";

        var path = GetPath(candidate);
        File.WriteAllText(path, "-- 按键精灵 2026 脚本\n-- 示例：MoveTo(500, 300)\n-- LeftClick(50)\n-- Delay(500)\n", new UTF8Encoding(false));
        return new ScriptEntry(candidate, path);
    }

    public void Save(ScriptEntry entry, string text)
    {
        var temp = entry.FullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temp, text, new UTF8Encoding(false));
            if (File.Exists(entry.FullPath))
            {
                var backup = entry.FullPath + ".bak";
                File.Copy(entry.FullPath, backup, overwrite: true);
            }
            File.Move(temp, entry.FullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    public ScriptEntry Rename(ScriptEntry entry, string newName)
    {
        var safe = MakeSafeName(newName);
        var target = GetPath(safe);
        if (File.Exists(target) && !string.Equals(target, entry.FullPath, StringComparison.OrdinalIgnoreCase))
            throw new IOException("同名脚本已经存在。");
        if (!string.Equals(target, entry.FullPath, StringComparison.OrdinalIgnoreCase))
        {
            var oldSidecar = RecorderDocumentStore.GetSidecarPath(entry.FullPath);
            var newSidecar = RecorderDocumentStore.GetSidecarPath(target);
            var oldBackup = oldSidecar + ".bak";
            var newBackup = newSidecar + ".bak";
            var oldScriptBackup = entry.FullPath + ".bak";
            var newScriptBackup = target + ".bak";
            var oldImageManifest = Path.ChangeExtension(entry.FullPath, ".images.json");
            var newImageManifest = Path.ChangeExtension(target, ".images.json");
            var oldAssetDirectory = Path.ChangeExtension(entry.FullPath, null) + ".assets";
            var newAssetDirectory = Path.ChangeExtension(target, null) + ".assets";
            if (File.Exists(newSidecar) || File.Exists(newBackup) || File.Exists(newScriptBackup)
                || File.Exists(newImageManifest) || Directory.Exists(newAssetDirectory))
                throw new IOException("目标名称对应的脚本、录制动作文件或备份已经存在，拒绝覆盖。");

            var scriptMoved = false;
            var sidecarMoved = false;
            var backupMoved = false;
            var scriptBackupMoved = false;
            var imageManifestMoved = false;
            var assetDirectoryMoved = false;
            var assetReferencesRewritten = false;
            var imagePathsRebased = false;
            try
            {
                File.Move(entry.FullPath, target);
                scriptMoved = true;
                if (File.Exists(oldSidecar))
                {
                    File.Move(oldSidecar, newSidecar);
                    sidecarMoved = true;
                }
                if (File.Exists(oldBackup))
                {
                    File.Move(oldBackup, newBackup);
                    backupMoved = true;
                }
                if (File.Exists(oldScriptBackup))
                {
                    File.Move(oldScriptBackup, newScriptBackup);
                    scriptBackupMoved = true;
                }
                if (File.Exists(oldImageManifest))
                {
                    File.Move(oldImageManifest, newImageManifest);
                    imageManifestMoved = true;
                    RebaseImageManifest(newImageManifest, Path.GetFileName(oldAssetDirectory),
                        Path.GetFileName(newAssetDirectory), regenerateScriptId: false);
                    imagePathsRebased = true;
                }
                if (Directory.Exists(oldAssetDirectory))
                {
                    Directory.Move(oldAssetDirectory, newAssetDirectory);
                    assetDirectoryMoved = true;
                    RewriteAssetReferences(target, Path.GetFileName(oldAssetDirectory), Path.GetFileName(newAssetDirectory));
                    assetReferencesRewritten = true;
                }
            }
            catch
            {
                if (scriptBackupMoved && File.Exists(newScriptBackup))
                    File.Move(newScriptBackup, oldScriptBackup);
                if (assetReferencesRewritten && File.Exists(target))
                    RewriteAssetReferences(target, Path.GetFileName(newAssetDirectory), Path.GetFileName(oldAssetDirectory));
                if (imagePathsRebased && File.Exists(newImageManifest))
                    RebaseImageManifest(newImageManifest, Path.GetFileName(newAssetDirectory),
                        Path.GetFileName(oldAssetDirectory), regenerateScriptId: false);
                if (assetDirectoryMoved && Directory.Exists(newAssetDirectory))
                    Directory.Move(newAssetDirectory, oldAssetDirectory);
                if (imageManifestMoved && File.Exists(newImageManifest))
                    File.Move(newImageManifest, oldImageManifest);
                if (backupMoved && File.Exists(newBackup))
                    File.Move(newBackup, oldBackup);
                if (sidecarMoved && File.Exists(newSidecar))
                    File.Move(newSidecar, oldSidecar);
                if (scriptMoved && File.Exists(target))
                    File.Move(target, entry.FullPath);
                throw;
            }
        }
        return new ScriptEntry(safe, target);
    }

    public ScriptEntry Duplicate(ScriptEntry entry)
    {
        var copy = Create(entry.Name + "_副本");
        try
        {
            File.Copy(entry.FullPath, copy.FullPath, overwrite: true);
            var sourceSidecar = RecorderDocumentStore.GetSidecarPath(entry.FullPath);
            var copySidecar = RecorderDocumentStore.GetSidecarPath(copy.FullPath);
            var sourceImageManifest = Path.ChangeExtension(entry.FullPath, ".images.json");
            var copyImageManifest = Path.ChangeExtension(copy.FullPath, ".images.json");
            var sourceAssetDirectory = Path.ChangeExtension(entry.FullPath, null) + ".assets";
            var copyAssetDirectory = Path.ChangeExtension(copy.FullPath, null) + ".assets";
            if (Directory.Exists(sourceAssetDirectory))
                RewriteAssetReferences(copy.FullPath, Path.GetFileName(sourceAssetDirectory), Path.GetFileName(copyAssetDirectory));
            if (File.Exists(sourceImageManifest))
            {
                File.Copy(sourceImageManifest, copyImageManifest, overwrite: true);
                RebaseImageManifest(copyImageManifest, Path.GetFileName(sourceAssetDirectory),
                    Path.GetFileName(copyAssetDirectory));
            }
            if (Directory.Exists(sourceAssetDirectory))
                CopyDirectory(sourceAssetDirectory, copyAssetDirectory);
            if (File.Exists(sourceSidecar))
            {
                File.Copy(sourceSidecar, copySidecar, overwrite: true);
                try
                {
                    var document = RecorderDocumentStore.Load(copySidecar);
                    RecorderDocumentStore.Save(copy.FullPath, document with
                    {
                        ScriptId = Guid.NewGuid().ToString("N"),
                        RecordingId = Guid.NewGuid().ToString("N")
                    });
                }
                catch (NotSupportedException)
                {
                    // Preserve a future schema byte-for-byte; never rewrite data we cannot migrate.
                }
            }
            return copy;
        }
        catch
        {
            var copySidecar = RecorderDocumentStore.GetSidecarPath(copy.FullPath);
            if (File.Exists(copySidecar)) File.Delete(copySidecar);
            var failedManifest = Path.ChangeExtension(copy.FullPath, ".images.json");
            var failedAssets = Path.ChangeExtension(copy.FullPath, null) + ".assets";
            if (File.Exists(failedManifest)) File.Delete(failedManifest);
            if (Directory.Exists(failedAssets)) Directory.Delete(failedAssets, recursive: true);
            if (File.Exists(copy.FullPath)) File.Delete(copy.FullPath);
            throw;
        }
    }

    public void Delete(ScriptEntry entry)
    {
        if (File.Exists(entry.FullPath))
            File.Delete(entry.FullPath);
        var sidecar = RecorderDocumentStore.GetSidecarPath(entry.FullPath);
        if (File.Exists(sidecar))
            File.Delete(sidecar);
        if (File.Exists(sidecar + ".bak"))
            File.Delete(sidecar + ".bak");
        var imageManifest = Path.ChangeExtension(entry.FullPath, ".images.json");
        var assetDirectory = Path.ChangeExtension(entry.FullPath, null) + ".assets";
        if (File.Exists(imageManifest)) File.Delete(imageManifest);
        if (File.Exists(imageManifest + ".bak")) File.Delete(imageManifest + ".bak");
        if (Directory.Exists(assetDirectory)) Directory.Delete(assetDirectory, recursive: true);
    }

    private static void RewriteAssetReferences(string scriptPath, string oldFolder, string newFolder)
    {
        var original = File.ReadAllText(scriptPath);
        var updated = original
            .Replace(oldFolder + "/", newFolder + "/", StringComparison.OrdinalIgnoreCase)
            .Replace(oldFolder + "\\", newFolder + "\\", StringComparison.OrdinalIgnoreCase);
        if (string.Equals(original, updated, StringComparison.Ordinal)) return;
        var temporary = scriptPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, updated, new UTF8Encoding(false));
            File.Move(temporary, scriptPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private static void RebaseImageManifest(string manifestPath, string oldAssetName, string newAssetName, bool regenerateScriptId = true)
    {
        var root = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(manifestPath))?.AsObject()
            ?? throw new InvalidDataException("复制的图像资源清单不是有效 JSON。");
        var scriptIdKey = root.Select(x => x.Key).FirstOrDefault(x => string.Equals(x, "ScriptId", StringComparison.OrdinalIgnoreCase));
        if (regenerateScriptId && scriptIdKey is not null)
            root[scriptIdKey] = Guid.NewGuid().ToString("N");
        var resourcesKey = root.Select(x => x.Key).FirstOrDefault(x => string.Equals(x, "Resources", StringComparison.OrdinalIgnoreCase));
        if (resourcesKey is not null && root[resourcesKey] is System.Text.Json.Nodes.JsonArray resources)
        {
            foreach (var resource in resources.OfType<System.Text.Json.Nodes.JsonObject>())
            {
                var pathKey = resource.Select(x => x.Key).FirstOrDefault(x => string.Equals(x, "RelativePath", StringComparison.OrdinalIgnoreCase));
                if (pathKey is null || resource[pathKey]?.GetValue<string>() is not { } path) continue;
                var prefix = oldAssetName + "/";
                if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    resource[pathKey] = newAssetName + path[prefix.Length..];
            }
        }
        File.WriteAllText(manifestPath, root.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    }

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var file in Directory.EnumerateFiles(source))
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
        foreach (var directory in Directory.EnumerateDirectories(source))
            CopyDirectory(directory, Path.Combine(target, Path.GetFileName(directory)));
    }

    public string Read(ScriptEntry entry) => File.ReadAllText(entry.FullPath);

    private string GetPath(string name) => Path.Combine(RootDirectory, name + ".lua");

    private static string MakeSafeName(string name)
    {
        var cleaned = string.Concat(name.Trim().Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        cleaned = cleaned.Trim('.', ' ');
        if (string.IsNullOrWhiteSpace(cleaned))
            throw new ArgumentException("脚本名称不能为空。", nameof(name));
        return cleaned;
    }
}
