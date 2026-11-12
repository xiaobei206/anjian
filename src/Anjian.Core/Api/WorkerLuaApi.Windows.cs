using System.Text.Json;
using Anjian.Core.Windows;
using NLua;

namespace Anjian.Core.Api;

public sealed partial class WorkerLuaApi
{
    public LuaTable? WindowFind(
        string titleContains,
        string className,
        string processName,
        double processId,
        bool visibleOnly) =>
        RequestTable("window-find", CreateSelector(titleContains, className, processName, processId, visibleOnly));

    public LuaTable WindowEnumerate(
        string titleContains,
        string className,
        string processName,
        double processId,
        bool visibleOnly) =>
        RequestTable("window-enumerate", CreateSelector(titleContains, className, processName, processId, visibleOnly))
        ?? throw new InvalidOperationException("窗口枚举没有返回结果表。");

    public LuaTable WindowEnumerateChildren(string id) =>
        RequestTable("window-enumerate-children", new WindowReferenceRequest(RequiredId(id)))
        ?? throw new InvalidOperationException("子窗口枚举没有返回结果表。");

    public LuaTable WindowGetInfo(string id) =>
        RequestTable("window-get-info", new WindowReferenceRequest(RequiredId(id)))
        ?? throw new InvalidOperationException("目标窗口不存在或引用已失效。");

    public bool WindowActivate(string id) => RequestOperation("activate", id);
    public bool WindowShow(string id) => RequestOperation("show", id);
    public bool WindowHide(string id) => RequestOperation("hide", id);
    public bool WindowMinimize(string id) => RequestOperation("minimize", id);
    public bool WindowMaximize(string id) => RequestOperation("maximize", id);
    public bool WindowRestore(string id) => RequestOperation("restore", id);
    public bool WindowSetTopmost(string id, bool enabled) =>
        RequestOperation("topmost", id, enabled: enabled);

    public bool WindowMove(string id, double x, double y) =>
        RequestOperation("move", id, x: Coord(x), y: Coord(y));

    public bool WindowResize(string id, double width, double height) =>
        RequestOperation("resize", id, width: Integer(width, 1, 32768, "窗口宽度"), height: Integer(height, 1, 32768, "窗口高度"));

    public bool WindowAlign(string id, string anchor, double offsetX, double offsetY) =>
        RequestOperation(
            "align",
            id,
            anchor: RequiredText(anchor, "锚点"),
            offsetX: Coord(offsetX),
            offsetY: Coord(offsetY));

    public LuaTable? WindowWait(
        string titleContains,
        string className,
        string processName,
        double processId,
        bool visibleOnly,
        double timeoutMilliseconds)
    {
        var timeout = Duration(timeoutMilliseconds);
        return RequestTable(
            "window-wait",
            new WindowWaitRequest(CreateSelector(titleContains, className, processName, processId, visibleOnly), timeout));
    }

    public bool WindowWaitClosed(string id, double timeoutMilliseconds) =>
        RequestBoolean("window-wait-closed", new WindowWaitClosedRequest(RequiredId(id), Duration(timeoutMilliseconds)));

    public LuaTable CoordinateToScreen(string id, string space, double x, double y, string anchor) =>
        RequestTable(
            "coordinate-to-screen",
            new WindowCoordinateRequest(RequiredId(id), RequiredText(space, "坐标空间"), x, y, string.IsNullOrWhiteSpace(anchor) ? "top-left" : anchor))
        ?? throw new InvalidOperationException("坐标转换未返回结果。");

    public LuaTable CoordinateFromScreen(string id, string space, double x, double y, string anchor) =>
        RequestTable(
            "coordinate-from-screen",
            new WindowCoordinateRequest(RequiredId(id), RequiredText(space, "坐标空间"), x, y, string.IsNullOrWhiteSpace(anchor) ? "top-left" : anchor))
        ?? throw new InvalidOperationException("坐标转换未返回结果。");

    private WindowSelector CreateSelector(
        string titleContains,
        string className,
        string processName,
        double processId,
        bool visibleOnly) =>
        new(
            titleContains ?? string.Empty,
            className ?? string.Empty,
            processName ?? string.Empty,
            Integer(processId, 0, int.MaxValue, "进程 ID"),
            visibleOnly);

    private bool RequestOperation(
        string operation,
        string id,
        int x = 0,
        int y = 0,
        int width = 0,
        int height = 0,
        string anchor = "top-left",
        int offsetX = 0,
        int offsetY = 0,
        bool enabled = false) =>
        RequestBoolean(
            "window-operation",
            new WindowActionRequest(
                RequiredId(id), operation, x, y, width, height,
                string.IsNullOrWhiteSpace(anchor) ? "top-left" : anchor,
                offsetX, offsetY, enabled));

    private bool RequestBoolean(string type, object payload)
    {
        var response = pipe.Request(type, text: JsonSerializer.Serialize(payload, WindowApiJson.Options));
        if (!bool.TryParse(response, out var result))
            throw new InvalidDataException($"窗口 API {type} 返回了无效布尔值。");
        return result;
    }

    private LuaTable? RequestTable(string type, object payload)
    {
        var response = pipe.Request(type, text: JsonSerializer.Serialize(payload, WindowApiJson.Options));
        if (string.IsNullOrWhiteSpace(response))
            throw new IOException($"窗口 API {type} 没有返回数据。");

        using var document = JsonDocument.Parse(response);
        if (document.RootElement.ValueKind == JsonValueKind.Null)
            return null;
        return ConvertJson(document.RootElement) as LuaTable
            ?? throw new InvalidDataException($"窗口 API {type} 返回的数据不是 Lua 表。");
    }

    private object? ConvertJson(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
            {
                var table = NewLuaTable();
                foreach (var property in element.EnumerateObject())
                    table[property.Name] = ConvertJson(property.Value);
                return table;
            }
            case JsonValueKind.Array:
            {
                var table = NewLuaTable();
                var index = 1;
                foreach (var item in element.EnumerateArray())
                    table[index++] = ConvertJson(item);
                return table;
            }
            case JsonValueKind.String:
                return element.GetString();
            case JsonValueKind.Number:
                return element.TryGetInt64(out var integer) ? integer : element.GetDouble();
            case JsonValueKind.True:
                return true;
            case JsonValueKind.False:
                return false;
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                return null;
            default:
                throw new InvalidDataException("窗口 API 返回了不支持的 JSON 值。");
        }
    }

    private LuaTable NewLuaTable()
    {
        if (lua is null)
            throw new InvalidOperationException("该窗口 API 需要在 Lua worker 中运行。");

        var result = lua.DoString("return {}");
        return result.Length > 0 && result[0] is LuaTable table
            ? table
            : throw new InvalidOperationException("无法创建 Lua 结果表。");
    }

    private static string RequiredId(string? id) =>
        string.IsNullOrWhiteSpace(id) ? throw new ArgumentException("窗口 ID 不能为空。", nameof(id)) : id.Trim();

    private static string RequiredText(string? value, string label) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException($"{label}不能为空。", nameof(value)) : value.Trim();
}
