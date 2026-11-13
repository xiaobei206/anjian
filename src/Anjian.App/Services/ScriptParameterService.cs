using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Anjian.Core.Parameters;

namespace Anjian.App.Services;

internal static class ScriptParameterService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public static string SchemaPath(string scriptPath) => Path.ChangeExtension(Path.GetFullPath(scriptPath), ".params.json");

    public static string ConfigurationPath(string scriptPath)
    {
        var normalized = Path.GetFullPath(scriptPath).ToUpperInvariant();
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized))).ToLowerInvariant();
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Anjian2026", "ScriptParameters", hash + ".json");
    }

    public static ScriptParameterSchema LoadSchema(string path)
    {
        var info = new FileInfo(path);
        if (info.Length > 1_048_576) throw new InvalidDataException("参数定义超过 1 MiB 限制。");
        var schema = JsonSerializer.Deserialize<ScriptParameterSchema>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidDataException("参数定义为空或格式错误。");
        var issues = ScriptParameterValidator.ValidateSchema(schema);
        if (issues.Count > 0)
            throw new InvalidDataException(string.Join(Environment.NewLine, issues.Select(x => $"{x.Label}：{x.Message}")));
        return schema;
    }

    public static void SaveSchemaAtomic(string path, ScriptParameterSchema schema)
    {
        var issues = ScriptParameterValidator.ValidateSchema(schema);
        if (issues.Count > 0)
            throw new InvalidDataException(string.Join(Environment.NewLine, issues.Select(x => $"{x.Label}：{x.Message}")));
        var directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(directory);
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(schema, JsonOptions), new UTF8Encoding(false));
            if (File.Exists(path)) File.Copy(path, path + ".bak", overwrite: true);
            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            try { if (File.Exists(temp)) File.Delete(temp); } catch (IOException) { }
        }
    }

    public static IReadOnlyDictionary<string, JsonElement> LoadSavedValues(string path, ScriptParameterSchema schema)
    {
        var loaded = ParameterConfigurationStore.LoadOrRecover(path);
        if (loaded.Warning is not null)
            MessageBox.Show(loaded.Warning, "参数配置", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        var validation = ScriptParameterValidator.ValidateValues(schema, loaded.Configuration.Values);
        return validation.IsValid
            ? validation.Values
            : new Dictionary<string, JsonElement>(StringComparer.Ordinal);
    }
}
