using System.Text.Json;
using Anjian.Core.Parameters;
using NLua;
using Xunit;

namespace Anjian.Core.Tests;

public sealed class LuaParameterInjectorTests
{
    [Fact]
    public void InjectsNativeTypesAndNestedReadOnlyTablesWithoutExecutingStrings()
    {
        using var lua = new Lua(openLibs: true);
        var values = new Dictionary<string, JsonElement>
        {
            ["count"] = Json("7"),
            ["enabled"] = Json("true"),
            ["name"] = Json("\"print('INJECTED')\""),
            ["items"] = Json("[\"one\",\"two\"]"),
            ["nested"] = Json("{\"value\":42}")
        };
        LuaParameterInjector.Inject(lua, values);
        var result = lua.DoString("""
            local before = Args.count
            local stringValue = Args.name
            local arrayValue = Args.items[1]
            local nestedValue = Args.nested.value
            local ok1 = pcall(function() Args.count = 99 end)
            local ok2 = pcall(function() Args.nested.value = 0 end)
            return before, type(Args.enabled), stringValue, arrayValue, nestedValue, ok1, ok2
            """);
        Assert.Equal(7, Convert.ToInt64(result[0]));
        Assert.Equal("boolean", result[1]?.ToString());
        Assert.Equal("print('INJECTED')", result[2]?.ToString());
        Assert.Equal("one", result[3]?.ToString());
        Assert.Equal(42, Convert.ToInt32(result[4]));
        Assert.False(Convert.ToBoolean(result[5]));
        Assert.False(Convert.ToBoolean(result[6]));
    }

    private static JsonElement Json(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
