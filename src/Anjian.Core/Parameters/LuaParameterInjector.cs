using System.Text.Json;
using NLua;

namespace Anjian.Core.Parameters;

public static class LuaParameterInjector
{
    private const string ReadOnlyProxyScript = """
        local source = __anjian_args_internal or {}
        local function readonly(value)
            if type(value) ~= "table" then return value end
            local proxy = {}
            return setmetatable(proxy, {
                __index = function(_, key) return readonly(value[key]) end,
                __newindex = function() error("Args 参数表是只读的", 2) end,
                __pairs = function() return next, value, nil end,
                __len = function() return #value end,
                __metatable = false
            })
        end
        local argsProxy = readonly(source)
        __anjian_args_internal = nil
        setmetatable(_G, {
            __index = function(_, key)
                if key == "Args" then return argsProxy end
            end,
            __newindex = function(target, key, value)
                if key == "Args" then error("Args 参数表是只读的", 2) end
                rawset(target, key, value)
            end
        })
        """;

    public static void Inject(Lua lua, IReadOnlyDictionary<string, JsonElement> values)
    {
        ArgumentNullException.ThrowIfNull(lua);
        ArgumentNullException.ThrowIfNull(values);
        var root = (LuaTable)lua.DoString("return {}")[0]!;
        foreach (var pair in values)
            root[pair.Key] = ToLuaValue(lua, pair.Value);
        lua["__anjian_args_internal"] = root;
        lua.DoString(ReadOnlyProxyScript);
    }

    private static object? ToLuaValue(Lua lua, JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Number when value.TryGetInt64(out var integer) => integer,
        JsonValueKind.Number when value.TryGetDouble(out var number) && double.IsFinite(number) => number,
        JsonValueKind.Array or JsonValueKind.Object => ToLuaTable(lua, value),
        JsonValueKind.Null => null,
        _ => throw new InvalidDataException($"不支持将 JSON {value.ValueKind} 注入 Lua 参数。")
    };

    private static LuaTable ToLuaTable(Lua lua, JsonElement value)
    {
        var table = (LuaTable)lua.DoString("return {}")[0]!;
        if (value.ValueKind == JsonValueKind.Array)
        {
            var index = 1;
            foreach (var item in value.EnumerateArray())
                table[index++] = ToLuaValue(lua, item);
        }
        else
        {
            foreach (var property in value.EnumerateObject())
                table[property.Name] = ToLuaValue(lua, property.Value);
        }
        return table;
    }
}
