using System.Text.Json;

namespace Anjian.Core.Parameters;

public sealed class ParameterConfiguration
{
    public int SchemaVersion { get; set; } = 1;
    public Dictionary<string, JsonElement> Values { get; set; } = new(StringComparer.Ordinal);
}

public sealed record ParameterConfigurationLoadResult(
    ParameterConfiguration Configuration,
    bool RecoveredFromBackup,
    string? Warning);

public static class ParameterConfigurationStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private const long MaximumFileBytes = 1_048_576;

    public static ParameterConfigurationLoadResult LoadOrRecover(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path))
            return new(new ParameterConfiguration(), false, null);

        try
        {
            return new(LoadOne(path), false, null);
        }
        catch (Exception primary) when (primary is IOException or JsonException or InvalidDataException)
        {
            var backup = path + ".bak";
            if (File.Exists(backup))
            {
                try
                {
                    var recovered = LoadOne(backup);
                    return new(recovered, true, "配置文件损坏，已从上一份有效备份恢复。");
                }
                catch (Exception backupError) when (backupError is IOException or JsonException or InvalidDataException)
                {
                    return new(new ParameterConfiguration(), false, "配置文件和备份均不可用，已使用默认参数；原文件保留未覆盖。");
                }
            }
            return new(new ParameterConfiguration(), false, "配置文件不可读取，已使用默认参数；原文件保留未覆盖。");
        }
    }

    public static void SaveAtomic(string path, ParameterConfiguration configuration)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(configuration);
        if (configuration.SchemaVersion != 1)
            throw new InvalidDataException($"不支持参数配置版本 {configuration.SchemaVersion}。");
        var directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, "." + Path.GetFileName(path) + "." + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                JsonSerializer.Serialize(stream, configuration, JsonOptions);
                stream.Flush(flushToDisk: true);
            }

            if (File.Exists(path))
            {
                // Preserve the prior file before replacement. The caller validates it before saving.
                var backupTemporary = path + ".bak.tmp";
                File.Copy(path, backupTemporary, overwrite: true);
                File.Move(backupTemporary, path + ".bak", overwrite: true);
            }
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); } catch (IOException) { }
        }
    }

    private static ParameterConfiguration LoadOne(string path)
    {
        var info = new FileInfo(path);
        if (info.Length > MaximumFileBytes)
            throw new InvalidDataException("参数配置超过 1 MiB 限制。");
        using var stream = File.OpenRead(path);
        var result = JsonSerializer.Deserialize<ParameterConfiguration>(stream)
            ?? throw new InvalidDataException("参数配置为空。");
        if (result.SchemaVersion != 1)
            throw new InvalidDataException($"不支持参数配置版本 {result.SchemaVersion}。");
        result.Values ??= new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        return result;
    }
}
