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


public sealed partial class WorkerLuaApi
{
    private IWindowAutomationService Automation =>
        automation ?? throw new InvalidOperationException("当前 Lua worker 未初始化 UI Automation provider。");

    public string WindowBind(string id)
    {
        var response = pipe.Request("window-automation-target",
            text: JsonSerializer.Serialize(new WindowReferenceRequest(RequiredId(id)), WindowApiJson.Options));
        if (string.IsNullOrWhiteSpace(response))
            throw new IOException("主进程没有返回窗口目标描述。");
        var target = JsonSerializer.Deserialize<WindowAutomationTarget>(response, WindowApiJson.Options)
            ?? throw new InvalidDataException("窗口目标描述无效。");
        return Automation.Bind(target);
    }

    public bool WindowUnbind(string bindingId) => Automation.Unbind(RequiredId(bindingId));
    public LuaTable WindowGetCapabilities(string bindingId) => ToLuaTable(Automation.GetCapabilities(RequiredId(bindingId)));
    public LuaTable ControlEnumerate(string bindingId, double maxResults) =>
        ToLuaTable(Automation.Enumerate(RequiredId(bindingId), Integer(maxResults, 1, 512, "控件最大数量")));
    public LuaTable ControlGetInfo(string bindingId, string controlId) =>
        ToLuaTable(Automation.GetInfo(RequiredId(bindingId), RequiredId(controlId)));
    public string ControlGetValue(string bindingId, string controlId) =>
        Automation.GetValue(RequiredId(bindingId), RequiredId(controlId));
    public bool ControlSetValue(string bindingId, string controlId, string value)
    {
        Automation.SetValue(RequiredId(bindingId), RequiredId(controlId), value ?? string.Empty);
        return true;
    }
    public string ControlGetText(string bindingId, string controlId) =>
        Automation.GetText(RequiredId(bindingId), RequiredId(controlId));
    public LuaTable ControlGetSelection(string bindingId, string controlId) =>
        ToLuaTable(Automation.GetSelection(RequiredId(bindingId), RequiredId(controlId)));
    public bool ControlSelect(string bindingId, string controlId)
    {
        Automation.Select(RequiredId(bindingId), RequiredId(controlId));
        return true;
    }
    public string ControlGetToggleState(string bindingId, string controlId) =>
        Automation.GetToggleState(RequiredId(bindingId), RequiredId(controlId));
    public bool ControlToggle(string bindingId, string controlId)
    {
        Automation.Toggle(RequiredId(bindingId), RequiredId(controlId));
        return true;
    }
    public bool ControlInvoke(string bindingId, string controlId)
    {
        Automation.Invoke(RequiredId(bindingId), RequiredId(controlId));
        return true;
    }
    public bool WindowInput(string bindingId, string backend, string action, string name,
        double x, double y, string value, double holdMs) =>
        RequestBoolean("window-input", new WindowInputRequest(Automation.GetWindowId(RequiredId(bindingId)),
            RequiredText(backend, "输入后端"), RequiredText(action, "输入动作"), name ?? string.Empty,
            Coord(x), Coord(y), value ?? string.Empty, Duration(holdMs)));

    private LuaTable ToLuaTable<T>(T value)
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(value, WindowApiJson.Options));
        return ConvertJson(document.RootElement) as LuaTable
            ?? throw new InvalidDataException("控件自动化结果无法转换为 Lua table。");
    }
}
