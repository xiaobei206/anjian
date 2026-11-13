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
            ["nested"] = Json("{\"child\":{\"value\":42}}")
        };
        LuaParameterInjector.Inject(lua, values);
        var result = lua.DoString("""
            local before = Args.count
            local stringValue = Args.name
            local arrayValue = Args.items[1]
            local nestedValue = Args.nested.child.value
            local ok1 = pcall(function() Args.count = 99 end)
            local ok2 = pcall(function() Args.nested.child.value = 0 end)
            local ok3 = pcall(function() Args = {} end)
            local nestedFromPairs
            for key, value in pairs(Args) do
                if key == "nested" then nestedFromPairs = value end
            end
            local childFromPairs
            for key, value in pairs(nestedFromPairs) do
                if key == "child" then childFromPairs = value end
            end
            local ok4 = pcall(function() childFromPairs.value = 0 end)
            return before, type(Args.enabled), stringValue, arrayValue, nestedValue, ok1, ok2, ok3, ok4, Args.nested.child.value
            """);
        Assert.Equal(7, Convert.ToInt64(result[0]));
        Assert.Equal("boolean", result[1]?.ToString());
        Assert.Equal("print('INJECTED')", result[2]?.ToString());
        Assert.Equal("one", result[3]?.ToString());
        Assert.Equal(42, Convert.ToInt32(result[4]));
        Assert.False(Convert.ToBoolean(result[5]));
        Assert.False(Convert.ToBoolean(result[6]));
        Assert.False(Convert.ToBoolean(result[7]));
        Assert.False(Convert.ToBoolean(result[8]));
        Assert.Equal(42, Convert.ToInt32(result[9]));
    }

    private static JsonElement Json(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
