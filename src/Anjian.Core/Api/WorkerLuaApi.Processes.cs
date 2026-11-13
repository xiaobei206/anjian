using System.Collections;
using System.Text.Json;
using Anjian.Core.Permissions;
using NLua;

namespace Anjian.Core.Api;

public sealed partial class WorkerLuaApi
{
    public string StartProcess(string executablePath, LuaTable arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        var request = ScriptProcessPolicy.CreateStartRequest(
            Permissions, executablePath, ReadProcessArguments(arguments), ScriptDirectory);
        var response = pipe.Request("process-start", text: JsonSerializer.Serialize(request,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
        if (string.IsNullOrWhiteSpace(response))
            throw new IOException("StartProcess IPC 没有返回进程句柄。");
        return ScriptProcessPolicy.NormalizeHandle(
            JsonSerializer.Deserialize<string>(response)
            ?? throw new InvalidDataException("StartProcess IPC 返回了无效句柄。"));
    }

    public bool WaitProcess(string processHandle, double timeoutMilliseconds)
    {
        var request = ScriptProcessPolicy.CreateWaitRequest(processHandle, timeoutMilliseconds);
        var payload = JsonSerializer.Serialize(request,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var response = pipe.Request("process-wait", text: payload);
        if (!bool.TryParse(response, out var completed))
            throw new InvalidDataException("WaitProcess IPC 返回了无效布尔值。");
        return completed;
    }

    public LuaTable GetProcessInfo(string processHandle)
    {
        var request = ScriptProcessPolicy.CreateHandleRequest(processHandle);
        return RequestTable("process-info", request)
            ?? throw new InvalidDataException("GetProcessInfo IPC 没有返回进程信息。");
    }

    private static IReadOnlyList<string> ReadProcessArguments(LuaTable table)
    {
        var indexed = new SortedDictionary<int, string>();
        var enumerator = table.GetEnumerator()
            ?? throw new InvalidDataException("进程 arguments 必须是 Lua 字符串数组。");
        while (enumerator.MoveNext())
        {
            var key = enumerator.Key;
            var index = key switch
            {
                byte value => value,
                sbyte value => value,
                short value => value,
                ushort value => value,
                int value => value,
                uint value when value <= int.MaxValue => (int)value,
                long value when value is >= 1 and <= ScriptProcessPolicy.MaximumArgumentCount => (int)value,
                ulong value when value <= ScriptProcessPolicy.MaximumArgumentCount => (int)value,
                float value when float.IsFinite(value) && value == MathF.Truncate(value) => checked((int)value),
                double value when double.IsFinite(value) && value == Math.Truncate(value) => checked((int)value),
                decimal value when value == decimal.Truncate(value) => checked((int)value),
                _ => throw new InvalidDataException("进程 arguments 必须使用从 1 开始的连续数字索引。")
            };
            if (index is < 1 or > ScriptProcessPolicy.MaximumArgumentCount)
                throw new InvalidDataException($"进程 arguments 索引必须在 1–{ScriptProcessPolicy.MaximumArgumentCount} 范围内。");
            if (enumerator.Value is not string argument)
                throw new InvalidDataException("进程 arguments 中每项都必须是字符串。");
            if (!indexed.TryAdd(index, argument))
                throw new InvalidDataException("进程 arguments 存在重复索引。");
        }

        var result = new List<string>(indexed.Count);
        for (var index = 1; index <= indexed.Count; index++)
        {
            if (!indexed.TryGetValue(index, out var argument))
                throw new InvalidDataException("进程 arguments 必须是无空洞的连续数组。");
            result.Add(argument);
        }
        ScriptProcessPolicy.ValidateArguments(result);
        return result;
    }
}
