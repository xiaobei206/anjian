using System.Text.Json;
using Anjian.Core.Parameters;
using Xunit;

namespace Anjian.Core.Tests;

public sealed class ScriptParameterTests
{
    [Fact]
    public void ValidatorPreservesNativeJsonTypesAndAppliesDefaults()
    {
        var schema = new ScriptParameterSchema
        {
            Parameters =
            [
                new() { Id = "count", Name = "count", Label = "次数", Type = "integer", Required = true,
                    Minimum = 1, Maximum = 10, DefaultValue = Json("3") },
                new() { Id = "enabled", Name = "enabled", Label = "启用", Type = "boolean", DefaultValue = Json("true") },
                new() { Id = "title", Name = "title", Label = "标题", Type = "text" }
            ]
        };
        var result = ScriptParameterValidator.ValidateValues(schema, new Dictionary<string, JsonElement>
        {
            ["title"] = Json("\"hello\"")
        });
        Assert.True(result.IsValid);
        Assert.Equal(3, result.Values["count"].GetInt32());
        Assert.Equal(JsonValueKind.True, result.Values["enabled"].ValueKind);
        Assert.Equal("hello", result.Values["title"].GetString());
    }

    [Fact]
    public void ValidatorRejectsMissingRequiredOutOfRangeAndUnknownValues()
    {
        var schema = new ScriptParameterSchema
        {
            Parameters =
            [
                new() { Id = "count", Name = "count", Label = "次数", Type = "integer", Required = true, Minimum = 1, Maximum = 5 }
            ]
        };
        var missing = ScriptParameterValidator.ValidateValues(schema, new Dictionary<string, JsonElement>());
        Assert.False(missing.IsValid);
        Assert.Contains(missing.Issues, x => x.Message.Contains("必填"));
        var invalid = ScriptParameterValidator.ValidateValues(schema, new Dictionary<string, JsonElement> { ["count"] = Json("10") });
        Assert.False(invalid.IsValid);
        Assert.Contains(invalid.Issues, x => x.Message.Contains("不能大于"));
        var unknown = ScriptParameterValidator.ValidateValues(schema, new Dictionary<string, JsonElement> { ["evil"] = Json("1") });
        Assert.False(unknown.IsValid);
        Assert.Contains(unknown.Issues, x => x.Message.Contains("未定义"));
    }

    [Fact]
    public void ValidatorChecksSelectionsPathsHotkeysAndColors()
    {
        var schema = new ScriptParameterSchema
        {
            Parameters =
            [
                new() { Id = "mode", Name = "mode", Label = "模式", Type = "select", Options = ["fast", "safe"] },
                new() { Id = "targets", Name = "targets", Label = "目标", Type = "multiSelect", Options = ["a", "b"] },
                new() { Id = "shortcut", Name = "shortcut", Label = "快捷键", Type = "hotkey" },
                new() { Id = "tint", Name = "tint", Label = "颜色", Type = "color" }
            ]
        };
        var result = ScriptParameterValidator.ValidateValues(schema, new Dictionary<string, JsonElement>
        {
            ["mode"] = Json("\"invalid\""),
            ["targets"] = Json("[\"a\",\"a\"]"),
            ["shortcut"] = Json("\"Ctrl+Banana\""),
            ["tint"] = Json("\"red\"")
        });
        Assert.False(result.IsValid);
        Assert.Equal(4, result.Issues.Count);
    }

    [Fact]
    public void SchemaRejectsDuplicateIdsNamesAndUnknownFutureVersions()
    {
        var schema = new ScriptParameterSchema
        {
            SchemaVersion = 2,
            Parameters =
            [
                new() { Id = "same", Name = "value", Label = "值", Type = "text" },
                new() { Id = "same", Name = "value", Label = "另一个值", Type = "text" }
            ]
        };
        var issues = ScriptParameterValidator.ValidateSchema(schema);
        Assert.True(issues.Count >= 3);
        Assert.Contains(issues, x => x.Message.Contains("不支持参数 schema 版本"));
    }

    [Fact]
    public void ConfigurationStoreRoundTripsAndRecoversFromBackup()
    {
        var directory = Path.Combine(Path.GetTempPath(), "anjian-params-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "config.json");
        try
        {
            var first = new ParameterConfiguration { Values = new() { ["count"] = Json("2") } };
            ParameterConfigurationStore.SaveAtomic(path, first);
            var second = new ParameterConfiguration { Values = new() { ["count"] = Json("4") } };
            ParameterConfigurationStore.SaveAtomic(path, second);
            File.WriteAllText(path, "{ broken");
            var loaded = ParameterConfigurationStore.LoadOrRecover(path);
            Assert.True(loaded.RecoveredFromBackup);
            Assert.Equal(2, loaded.Configuration.Values["count"].GetInt32());
        }
        finally
        {
            try { Directory.Delete(directory, recursive: true); } catch (IOException) { }
        }
    }

    [Fact]
    public void UnknownFutureConfigurationVersionIsNotOverwritten()
    {
        var directory = Path.Combine(Path.GetTempPath(), "anjian-params-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "config.json");
        try
        {
            File.WriteAllText(path, "{\"SchemaVersion\":9,\"Values\":{}}");
            Assert.Throws<NotSupportedException>(() => ParameterConfigurationStore.LoadOrRecover(path));
            Assert.Contains("\"SchemaVersion\":9", File.ReadAllText(path));
        }
        finally
        {
            try { Directory.Delete(directory, recursive: true); } catch (IOException) { }
        }
    }

    [Fact]
    public void FutureConfigurationVersionIsNeverReplacedByOlderBackup()
    {
        var directory = Path.Combine(Path.GetTempPath(), "anjian-params-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "config.json");
        try
        {
            ParameterConfigurationStore.SaveAtomic(path, new ParameterConfiguration { Values = new() { ["count"] = Json("2") } });
            File.WriteAllText(path, "{\"schemaVersion\":9,\"values\":{}}");
            Assert.Throws<NotSupportedException>(() => ParameterConfigurationStore.LoadOrRecover(path));
            Assert.Contains("\"schemaVersion\":9", File.ReadAllText(path));
            Assert.Equal(2, JsonDocument.Parse(File.ReadAllText(path + ".bak")).RootElement.GetProperty("values").GetProperty("count").GetInt32());
        }
        finally
        {
            try { Directory.Delete(directory, recursive: true); } catch (IOException) { }
        }
    }

    private static JsonElement Json(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
