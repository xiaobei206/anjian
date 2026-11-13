using System.Text;
using System.Text.Json;
using Anjian.Core.Permissions;
using NLua;

namespace Anjian.Core.Api;

public sealed partial class WorkerLuaApi
{
    private static readonly JsonSerializerOptions HttpRequestJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public LuaTable HttpRequest(string url, LuaTable? options)
    {
        var values = ReadHttpOptions(options);
        var method = GetStringOption(values, "method", "GET");
        var headers = ReadHttpHeaders(values);
        string? body = GetStringOption(values, "body", null);
        if (values.TryGetValue("json", out var jsonValue))
        {
            if (body is not null)
                throw new ArgumentException("HttpRequest 的 body 与 json 选项不能同时使用。");
            var jsonObject = ReadLuaJsonValue(jsonValue, 0, new HashSet<object>(ReferenceEqualityComparer.Instance));
            body = JsonSerializer.Serialize(jsonObject);
            if (!headers.ContainsKey("Content-Type"))
                headers["Content-Type"] = "application/json; charset=utf-8";
        }

        var timeout = GetIntegerOption(values, "timeoutMs", ScriptHttpPolicy.DefaultTimeoutMilliseconds);
        var followRedirects = GetBooleanOption(values, "followRedirects", true);
        var request = ScriptHttpPolicy.CreateRequest(
            Permissions, url, method, headers, body, timeout, followRedirects);
        var payload = JsonSerializer.Serialize(request, HttpRequestJsonOptions);
        var response = pipe.Request("http-request", text: payload);
        if (string.IsNullOrWhiteSpace(response))
            throw new IOException("HttpRequest IPC 没有返回响应。");

        using var document = JsonDocument.Parse(response);
        return ConvertJson(document.RootElement) as LuaTable
            ?? throw new InvalidDataException("HttpRequest IPC 返回的数据不是 Lua table。");
    }

    private static Dictionary<string, object?> ReadHttpOptions(LuaTable? table)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (table is null)
            return result;

        var enumerator = table.GetEnumerator()
            ?? throw new InvalidDataException("HttpRequest options 必须是 Lua table。");
        while (enumerator.MoveNext())
        {
            if (enumerator.Key is not string key ||
                key is not ("method" or "headers" or "body" or "json" or "timeoutMs" or "followRedirects"))
                throw new InvalidDataException("HttpRequest options 包含未知字段或非字符串字段名。");
            if (!result.TryAdd(key, enumerator.Value))
                throw new InvalidDataException($"HttpRequest options 字段重复：{key}");
        }
        return result;
    }

    private static Dictionary<string, string> ReadHttpHeaders(Dictionary<string, object?> options)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!options.TryGetValue("headers", out var value) || value is null)
            return result;
        if (value is not LuaTable table)
            throw new InvalidDataException("HttpRequest headers 必须是字符串键值 Lua table。");

        var enumerator = table.GetEnumerator()
            ?? throw new InvalidDataException("HttpRequest headers 无效。");
        while (enumerator.MoveNext())
        {
            if (enumerator.Key is not string key || enumerator.Value is not string headerValue)
                throw new InvalidDataException("HttpRequest headers 的键和值都必须是字符串。");
            if (!result.TryAdd(key, headerValue))
                throw new InvalidDataException($"HttpRequest headers 存在重复名称：{key}");
        }
        return result;
    }

    private static string? GetStringOption(
        Dictionary<string, object?> options,
        string name,
        string? defaultValue)
    {
        if (!options.TryGetValue(name, out var value))
            return defaultValue;
        return value as string
            ?? throw new InvalidDataException($"HttpRequest options.{name} 必须是字符串。");
    }

    private static int GetIntegerOption(
        Dictionary<string, object?> options,
        string name,
        int defaultValue)
    {
        if (!options.TryGetValue(name, out var value))
            return defaultValue;
        var number = value switch
        {
            byte v => v,
            sbyte v => v,
            short v => v,
            ushort v => v,
            int v => v,
            uint v => v,
            long v => v,
            ulong v => v,
            float v => v,
            double v => v,
            decimal v => (double)v,
            _ => throw new InvalidDataException($"HttpRequest options.{name} 必须是整数。")
        };
        if (!double.IsFinite(number) || number != Math.Truncate(number) ||
            number < int.MinValue || number > int.MaxValue)
            throw new InvalidDataException($"HttpRequest options.{name} 必须是有效整数。");
        return checked((int)number);
    }

    private static bool GetBooleanOption(
        Dictionary<string, object?> options,
        string name,
        bool defaultValue)
    {
        if (!options.TryGetValue(name, out var value))
            return defaultValue;
        return value is bool boolean
            ? boolean
            : throw new InvalidDataException($"HttpRequest options.{name} 必须是布尔值。");
    }

    private static object? ReadLuaJsonValue(
        object? value,
        int depth,
        HashSet<object> activeTables)
    {
        if (depth > ScriptHttpPolicy.MaximumJsonDepth)
            throw new InvalidDataException($"HttpRequest json 的嵌套层级不能超过 {ScriptHttpPolicy.MaximumJsonDepth}。");

        if (value is null || value is string || value is bool)
            return value;

        switch (value)
        {
            case byte or sbyte or short or ushort or int or uint or long or ulong or decimal:
                return value;
            case float number when float.IsFinite(number):
                return number;
            case double number when double.IsFinite(number):
                return number;
            case float or double:
                throw new InvalidDataException("HttpRequest json 不允许 NaN 或 Infinity。");
            case LuaTable table:
                return ReadLuaJsonTable(table, depth, activeTables);
            default:
                throw new InvalidDataException("HttpRequest json 只支持字符串、布尔、有限数字和 Lua table。");
        }
    }

    private static object ReadLuaJsonTable(
        LuaTable table,
        int depth,
        HashSet<object> activeTables)
    {
        if (!activeTables.Add(table))
            throw new InvalidDataException("HttpRequest json 不允许循环引用的 Lua table。");

        try
        {
            var stringValues = new Dictionary<string, object?>(StringComparer.Ordinal);
            var indexedValues = new SortedDictionary<int, object?>();
            var hasStringKey = false;
            var hasNumericKey = false;
            var enumerator = table.GetEnumerator()
                ?? throw new InvalidDataException("HttpRequest json table 无效。");

            while (enumerator.MoveNext())
            {
                var key = enumerator.Key;
                if (key is string stringKey)
                {
                    hasStringKey = true;
                    if (!stringValues.TryAdd(stringKey,
                        ReadLuaJsonValue(enumerator.Value, depth + 1, activeTables)))
                        throw new InvalidDataException("HttpRequest json 对象存在重复字段。");
                }
                else
                {
                    var index = TryGetArrayIndex(key);
                    if (index is null)
                    {
                        if (key is byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal)
                            throw new InvalidDataException("HttpRequest json 数组必须使用从 1 开始的连续整数索引。");
                        throw new InvalidDataException("HttpRequest json table 的键只能是字符串或连续数组索引。");
                    }

                    hasNumericKey = true;
                    if (!indexedValues.TryAdd(index.Value,
                        ReadLuaJsonValue(enumerator.Value, depth + 1, activeTables)))
                        throw new InvalidDataException("HttpRequest json 数组存在重复索引。");
                }
            }

            if (hasStringKey && hasNumericKey)
                throw new InvalidDataException("HttpRequest json table 不能混用对象字段和数组索引。");
            if (!hasNumericKey)
                return stringValues;

            if (indexedValues.Count > 100_000)
                throw new InvalidDataException("HttpRequest json 数组项数超过限制。");
            var array = new List<object?>(indexedValues.Count);
            for (var index = 1; index <= indexedValues.Count; index++)
            {
                if (!indexedValues.TryGetValue(index, out var item))
                    throw new InvalidDataException("HttpRequest json 数组必须是从 1 开始的连续索引。");
                array.Add(item);
            }
            return array;
        }
        finally
        {
            activeTables.Remove(table);
        }
    }

    private static int? TryGetArrayIndex(object? key)
    {
        double number;
        try
        {
            number = key switch
            {
                byte v => v,
                sbyte v => v,
                short v => v,
                ushort v => v,
                int v => v,
                uint v => v,
                long v => v,
                ulong v => v,
                float v => v,
                double v => v,
                decimal v => (double)v,
                _ => double.NaN
            };
        }
        catch (OverflowException)
        {
            return null;
        }

        if (!double.IsFinite(number) || number < 1 || number > int.MaxValue ||
            number != Math.Truncate(number))
            return null;
        return checked((int)number);
    }
}
