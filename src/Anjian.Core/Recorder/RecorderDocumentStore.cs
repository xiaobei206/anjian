using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Anjian.Core.Recorder;

/// <summary>
/// Versioned sidecar persistence. Writes are staged to a sibling temporary file,
/// flushed, backed up, and atomically moved into place.
/// </summary>
public static class RecorderDocumentStore
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public static string GetSidecarPath(string scriptPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scriptPath);
        var scriptStem = Path.ChangeExtension(scriptPath, null);
        if (string.IsNullOrWhiteSpace(scriptStem))
            throw new ArgumentException("无法确定脚本文件名。", nameof(scriptPath));
        return scriptStem + ".actions.json";
    }

    public static RecorderDocument? LoadIfExists(string scriptPath)
    {
        var path = GetSidecarPath(scriptPath);
        return File.Exists(path) ? Load(path) : null;
    }

    public static RecorderDocument Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var json = File.ReadAllText(path, Encoding.UTF8);
        var document = JsonSerializer.Deserialize<RecorderDocument>(json, JsonOptions)
            ?? throw new InvalidDataException("录制动作文件为空或格式无效。");

        // Schema dispatch is explicit. Version 1 is the initial persisted contract;
        // future versions are rejected without rewriting or deleting the original.
        document = document.SchemaVersion switch
        {
            1 => document,
            _ => throw new NotSupportedException(
                $"录制动作文件 schemaVersion={document.SchemaVersion} 不受当前程序支持；原文件未修改。")
        };
        document.Validate();
        return document;
    }

    public static void Save(string scriptPath, RecorderDocument document)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scriptPath);
        ArgumentNullException.ThrowIfNull(document);
        document.Validate();

        var target = GetSidecarPath(scriptPath);
        var directory = Path.GetDirectoryName(target);
        if (string.IsNullOrWhiteSpace(directory))
            throw new InvalidOperationException("无法确定录制动作文件的保存目录。");
        Directory.CreateDirectory(directory);

        var temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
        var backup = target + ".bak";
        try
        {
            var json = JsonSerializer.Serialize(document, JsonOptions);
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                writer.Write(json);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }

            if (File.Exists(target))
                File.Copy(target, backup, overwrite: true);
            File.Move(temporary, target, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }
}