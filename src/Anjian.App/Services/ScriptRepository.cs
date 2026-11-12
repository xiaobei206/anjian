using Anjian.App.Models;
using System.Text;

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
            File.Move(entry.FullPath, target);
        return new ScriptEntry(safe, target);
    }

    public void Delete(ScriptEntry entry)
    {
        if (File.Exists(entry.FullPath))
            File.Delete(entry.FullPath);
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
