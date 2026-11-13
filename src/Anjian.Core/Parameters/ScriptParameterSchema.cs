using System.Text.Json;
using System.Text.RegularExpressions;

namespace Anjian.Core.Parameters;

public sealed class ScriptParameterSchema
{
    public int SchemaVersion { get; set; } = 1;
    public string ScriptId { get; set; } = "";
    public List<ScriptParameterDefinition> Parameters { get; set; } = [];
}

public sealed class ScriptParameterDefinition
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string Label { get; set; } = "";
    public string Type { get; set; } = "text";
    public bool Required { get; set; }
    public JsonElement? DefaultValue { get; set; }
    public double? Minimum { get; set; }
    public double? Maximum { get; set; }
    public List<string> Options { get; set; } = [];
    public string Description { get; set; } = "";
    public string? Pattern { get; set; }
    public bool MustExist { get; set; }
    public bool Sensitive { get; set; }
    public int Order { get; set; }
}

public sealed record ParameterValidationIssue(string ParameterId, string Label, string Message);

public sealed class ParameterValidationResult
{
    public IReadOnlyDictionary<string, JsonElement> Values { get; }
    public IReadOnlyList<ParameterValidationIssue> Issues { get; }
    public bool IsValid => Issues.Count == 0;

    internal ParameterValidationResult(
        IDictionary<string, JsonElement> values,
        IReadOnlyList<ParameterValidationIssue> issues)
    {
        Values = new Dictionary<string, JsonElement>(values, StringComparer.Ordinal);
        Issues = issues;
    }
}

public static class ScriptParameterValidator
{
    private static readonly Regex LuaIdentifier = new(@"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant);
    private static readonly Regex ColorValue = new(@"^#(?:[0-9A-Fa-f]{6}|[0-9A-Fa-f]{8})$", RegexOptions.CultureInvariant);
    private static readonly Regex HotkeyValue = new(@"^(?:(?:Ctrl|Alt|Shift|Win)\+){0,4}(?:[A-Z0-9]|F(?:[1-9]|1[0-2])|Enter|Escape|Space|Tab|Backspace|Delete|Insert|Home|End|PageUp|PageDown|Up|Down|Left|Right)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly HashSet<string> SupportedTypes = new(StringComparer.Ordinal)
    {
        "text", "integer", "number", "boolean", "select", "multiSelect",
        "filePath", "directoryPath", "hotkey", "color"
    };

    public static IReadOnlyList<ParameterValidationIssue> ValidateSchema(ScriptParameterSchema schema)
    {
        ArgumentNullException.ThrowIfNull(schema);
        var issues = new List<ParameterValidationIssue>();
        if (schema.SchemaVersion != 1)
            issues.Add(new("", "参数定义", $"不支持参数 schema 版本 {schema.SchemaVersion}。"));
        if (schema.Parameters is null)
        {
            issues.Add(new("", "参数定义", "Parameters 不能为 null。"));
            return issues;
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var p in schema.Parameters)
        {
            var label = string.IsNullOrWhiteSpace(p.Label) ? p.Name : p.Label;
            if (string.IsNullOrWhiteSpace(p.Id) || !ids.Add(p.Id))
                issues.Add(new(p.Id ?? "", label, "参数 ID 不能为空且必须唯一。"));
            if (string.IsNullOrWhiteSpace(p.Name) || !LuaIdentifier.IsMatch(p.Name) || !names.Add(p.Name))
                issues.Add(new(p.Id, label, "参数名必须是唯一的 Lua 标识符。"));
            if (string.IsNullOrWhiteSpace(p.Label))
                issues.Add(new(p.Id, label, "中文标签不能为空。"));
            if (!SupportedTypes.Contains(p.Type))
                issues.Add(new(p.Id, label, $"不支持的参数类型：{p.Type}。"));
            if (p.Minimum.HasValue && p.Maximum.HasValue && p.Minimum > p.Maximum)
                issues.Add(new(p.Id, label, "最小值不能大于最大值。"));
            if (p.Type is "select" or "multiSelect")
            {
                if (p.Options is null || p.Options.Count == 0)
                    issues.Add(new(p.Id, label, "单选/多选参数至少需要一个选项。"));
                else if (p.Options.Distinct(StringComparer.Ordinal).Count() != p.Options.Count)
                    issues.Add(new(p.Id, label, "选项不能重复。"));
            }
            if (!string.IsNullOrWhiteSpace(p.Pattern))
            {
                try { _ = new Regex(p.Pattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)); }
                catch (ArgumentException) { issues.Add(new(p.Id, label, "正则验证规则无效。")); }
            }
            if (p.DefaultValue is { } defaultValue && defaultValue.ValueKind != JsonValueKind.Undefined)
            {
                var result = ValidateValue(p, defaultValue, baseDirectory: null);
                issues.AddRange(result.Select(message => new ParameterValidationIssue(p.Id, label, "默认值无效：" + message)));
            }
        }
        return issues;
    }

    public static ParameterValidationResult ValidateValues(
        ScriptParameterSchema schema,
        IReadOnlyDictionary<string, JsonElement>? suppliedValues,
        string? baseDirectory = null)
    {
        var issues = ValidateSchema(schema).ToList();
        var values = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        if (issues.Count > 0)
            return new(values, issues);

        suppliedValues ??= new Dictionary<string, JsonElement>();
        var definitions = schema.Parameters.ToDictionary(p => p.Name, StringComparer.Ordinal);
        foreach (var supplied in suppliedValues)
        {
            if (!definitions.ContainsKey(supplied.Key))
                issues.Add(new(supplied.Key, supplied.Key, "提供了未定义的参数。"));
        }

        foreach (var p in schema.Parameters.OrderBy(p => p.Order))
        {
            var label = p.Label;
            JsonElement value;
            if (suppliedValues.TryGetValue(p.Name, out var supplied))
                value = supplied;
            else if (p.DefaultValue is { } defaultValue && defaultValue.ValueKind != JsonValueKind.Undefined)
                value = defaultValue;
            else
            {
                if (p.Required)
                    issues.Add(new(p.Id, label, "此参数为必填项。"));
                continue;
            }

            var errors = ValidateValue(p, value, baseDirectory);
            foreach (var error in errors)
                issues.Add(new(p.Id, label, error));
            if (errors.Count == 0)
                values[p.Name] = value.Clone();
        }
        return new(values, issues);
    }

    private static List<string> ValidateValue(ScriptParameterDefinition p, JsonElement value, string? baseDirectory)
    {
        var errors = new List<string>();
        switch (p.Type)
        {
            case "text":
                if (value.ValueKind != JsonValueKind.String) errors.Add("必须是文本。");
                else if (!MatchesPattern(p, value.GetString()!, errors)) { }
                break;
            case "integer":
                if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out var integer))
                    errors.Add("必须是整数。");
                else ValidateRange(p, integer, errors);
                break;
            case "number":
                if (value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out var number) || !double.IsFinite(number))
                    errors.Add("必须是有限数值。");
                else ValidateRange(p, number, errors);
                break;
            case "boolean":
                if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) errors.Add("必须是布尔值。");
                break;
            case "select":
                if (value.ValueKind != JsonValueKind.String) errors.Add("必须是单选字符串。");
                else if (!p.Options.Contains(value.GetString()!, StringComparer.Ordinal)) errors.Add("值不在允许选项中。");
                break;
            case "multiSelect":
                if (value.ValueKind != JsonValueKind.Array) errors.Add("必须是多选数组。");
                else
                {
                    var selected = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var item in value.EnumerateArray())
                    {
                        if (item.ValueKind != JsonValueKind.String) { errors.Add("每个多选项都必须是字符串。"); continue; }
                        var text = item.GetString()!;
                        if (!p.Options.Contains(text, StringComparer.Ordinal)) errors.Add($"包含未允许的选项：{text}。");
                        if (!selected.Add(text)) errors.Add($"多选项重复：{text}。");
                    }
                }
                break;
            case "filePath":
            case "directoryPath":
                if (value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
                    errors.Add("必须是非空路径。");
                else if (p.MustExist)
                {
                    var raw = value.GetString()!;
                    var path = Path.IsPathRooted(raw) ? raw : Path.GetFullPath(raw, baseDirectory ?? Environment.CurrentDirectory);
                    var exists = p.Type == "filePath" ? File.Exists(path) : Directory.Exists(path);
                    if (!exists) errors.Add(p.Type == "filePath" ? "文件不存在。" : "目录不存在。");
                }
                break;
            case "hotkey":
                if (value.ValueKind != JsonValueKind.String || !HotkeyValue.IsMatch(value.GetString() ?? ""))
                    errors.Add("热键格式无效；示例：Ctrl+Shift+F8。");
                break;
            case "color":
                if (value.ValueKind != JsonValueKind.String || !ColorValue.IsMatch(value.GetString() ?? ""))
                    errors.Add("颜色必须是 #RRGGBB 或 #AARRGGBB。");
                break;
        }
        return errors;
    }

    private static bool MatchesPattern(ScriptParameterDefinition p, string value, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(p.Pattern)) return true;
        try
        {
            if (!Regex.IsMatch(value, p.Pattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)))
                errors.Add("文本不符合格式规则。");
            return errors.Count == 0;
        }
        catch (ArgumentException)
        {
            errors.Add("正则验证规则无效。");
            return false;
        }
    }

    private static void ValidateRange(ScriptParameterDefinition p, double value, List<string> errors)
    {
        if (p.Minimum.HasValue && value < p.Minimum.Value) errors.Add($"不能小于 {p.Minimum.Value}。");
        if (p.Maximum.HasValue && value > p.Maximum.Value) errors.Add($"不能大于 {p.Maximum.Value}。");
    }
}
