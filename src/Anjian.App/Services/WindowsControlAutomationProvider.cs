using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using Anjian.Core.Windows;

namespace Anjian.App.Services;

/// <summary>
/// UI Automation and explicitly foreground-only SendInput adapter.
/// Every binding is backed by a validated M6 window reference and is cleared when a run ends.
/// UIA element IDs are random, run-local tokens; raw HWNDs and COM objects never cross IPC.
/// </summary>
internal sealed class WindowsControlAutomationProvider(
    WindowsWindowManager windows,
    WindowsInputController input)
{
    private const int MaxStoredElements = 4096;
    private readonly Dictionary<string, Binding> _bindings = new(StringComparer.Ordinal);

    public string Bind(string windowId)
    {
        _ = windows.GetValidatedHandle(windowId);
        var bindingId = Guid.NewGuid().ToString("N");
        _bindings.Add(bindingId, new Binding(windowId, windows.GetInfo(windowId).ProcessId));
        return bindingId;
    }

    public bool Unbind(string bindingId) => _bindings.Remove(Required(bindingId, "绑定 ID"));
    public void ClearBindings() => _bindings.Clear();

    public WindowAutomationCapabilities GetCapabilities(string bindingId)
    {
        var binding = ResolveBinding(bindingId);
        var foreground = windows.IsForeground(binding.WindowId);
        string treeStatus;
        string treeReason;
        try
        {
            _ = GetRoot(binding);
            treeStatus = "available";
            treeReason = "UI Automation 能够取得目标窗口根元素。";
        }
        catch (Exception ex) when (IsAutomationFailure(ex))
        {
            treeStatus = "unavailable";
            treeReason = DescribeFailure(ex);
        }

        var patternStatus = treeStatus == "available" ? "conditional" : "unavailable";
        var capabilities = new List<AutomationCapability>
        {
            new("controlTree", treeStatus, "UI Automation Control View", treeReason, "树枚举最多返回请求数量，单次不超过 512 个元素。"),
            new("textRead", patternStatus, "UIA TextPattern / ValuePattern", "仅当目标控件暴露 Text 或 Value pattern 时可用。", "TextPattern 最多读取 32768 个字符。"),
            new("valueRead", patternStatus, "UIA ValuePattern", "仅当目标控件暴露 Value pattern 时可用。", "不是所有编辑控件都实现 Value pattern。"),
            new("valueWrite", patternStatus, "UIA ValuePattern.SetValue", "仅当目标控件暴露可写 Value pattern 时可用；只调用 provider，不模拟键盘。", "只读或不支持 Value pattern 的控件明确拒绝。"),
            new("selectionRead", patternStatus, "UIA SelectionPattern", "仅当容器暴露 Selection pattern 时可用。", "返回 provider 当前报告的已选元素名称。"),
            new("selectionSelect", patternStatus, "UIA SelectionItemPattern", "仅当元素暴露 SelectionItem pattern 时可用。", "选择操作可能受容器单选/多选规则约束。"),
            new("toggleRead", patternStatus, "UIA TogglePattern", "仅当控件暴露 Toggle pattern 时可用。", "返回 Off、On 或 Indeterminate。"),
            new("toggle", patternStatus, "UIA TogglePattern.Toggle", "仅当控件暴露 Toggle pattern 时可用。", "每次调用切换一次，不能指定目标状态。"),
            new("invoke", patternStatus, "UIA InvokePattern.Invoke", "仅当控件暴露 Invoke pattern 时可用。", "provider 调用成功不保证应用业务操作最终成功。"),
            new("foregroundMouse", "conditional", "SendInput", "要求绑定窗口当前处于前台；WindowInput 不会自动激活或抢占焦点。", "使用屏幕物理像素；权限仍受 UIPI/桌面会话限制。"),
            new("foregroundKeyboard", "conditional", "SendInput", "要求绑定窗口当前处于前台且目标控件已具有键盘焦点。", "不会自动改变控件焦点。"),
            new("foregroundText", "conditional", "SendInput Unicode", "要求绑定窗口当前处于前台且目标控件已具有键盘焦点。", "这是前台 Unicode 输入，不是 UIA ValuePattern 设置。"),
            new("backgroundMouse", "unsupported", "none", "没有经过验证的通用后台鼠标 provider。", "不使用 PostMessage/伪造鼠标消息冒充后台点击。"),
            new("backgroundKeyboard", "unsupported", "none", "没有经过验证的通用后台键盘 provider。", "不使用 PostMessage/伪造键盘消息冒充后台按键。"),
            new("backgroundTextInput", "unsupported", "none", "没有经过验证的通用后台文本注入 provider。", "UIA ValuePattern.SetValue 是控件值语义操作，不等于键盘文本输入。")
        };
        return new WindowAutomationCapabilities(bindingId, treeStatus == "available" ? "Microsoft UI Automation Control View" : "UI Automation unavailable", binding.ProcessId, foreground, capabilities);
    }

    public IReadOnlyList<ControlElementInfo> Enumerate(string bindingId, int maxResults)
    {
        if (maxResults is < 1 or > 512)
            throw new ArgumentOutOfRangeException(nameof(maxResults), "控件数量必须在 1 到 512 之间。");

        var binding = ResolveBinding(bindingId);
        var root = GetRoot(binding);
        var walker = TreeWalker.ControlViewWalker;
        var queue = new Queue<(AutomationElement Element, int Depth)>();
        var results = new List<ControlElementInfo>();
        queue.Enqueue((root, 0));
        while (queue.Count > 0 && results.Count < maxResults)
        {
            var (element, depth) = queue.Dequeue();
            try
            {
                if (binding.Elements.Count >= MaxStoredElements)
                    throw new InvalidOperationException("本次运行的控件引用数量已达到 4096；请解绑窗口后重新绑定。");
                var info = CreateInfo(element);
                binding.Elements[info.ControlId] = element;
                results.Add(info);
                if (depth >= 16)
                    continue;

                var child = walker.GetFirstChild(element);
                var siblingCount = 0;
                while (child is not null && siblingCount++ < maxResults && queue.Count < maxResults)
                {
                    queue.Enqueue((child, depth + 1));
                    child = walker.GetNextSibling(child);
                }
            }
            catch (ElementNotAvailableException)
            {
                // Controls may disappear while a provider tree is being traversed.
            }
            catch (InvalidOperationException ex) when (ex.InnerException is ElementNotAvailableException)
            {
                // CreateInfo translates a destroyed element into a descriptive invalid-operation error.
            }
            catch (COMException)
            {
                // Providers can fail one node while the remainder of the tree is still useful.
            }
        }
        return results;
    }

    public ControlElementInfo GetInfo(string bindingId, string controlId)
    {
        var (_, element) = ResolveControl(bindingId, controlId);
        return CreateInfo(element, controlId);
    }

    public string GetValue(string bindingId, string controlId)
    {
        var (_, element) = ResolveControl(bindingId, controlId);
        EnsureNotPassword(element);
        return GetPattern<ValuePattern>(element, ValuePattern.Pattern, "Value").Current.Value;
    }

    public void SetValue(string bindingId, string controlId, string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length > 32768)
            throw new ArgumentOutOfRangeException(nameof(value), "控件值最多 32768 个字符。");
        var (_, element) = ResolveControl(bindingId, controlId);
        var pattern = GetPattern<ValuePattern>(element, ValuePattern.Pattern, "Value");
        if (pattern.Current.IsReadOnly)
            throw new InvalidOperationException("目标控件的 Value pattern 为只读，拒绝设置。");
        pattern.SetValue(value);
    }

    public string GetText(string bindingId, string controlId)
    {
        var (_, element) = ResolveControl(bindingId, controlId);
        EnsureNotPassword(element);
        if (TryGetPattern(element, TextPattern.Pattern, out TextPattern? textPattern))
            return textPattern!.DocumentRange.GetText(32768);
        if (TryGetPattern(element, ValuePattern.Pattern, out ValuePattern? valuePattern))
            return valuePattern!.Current.Value;
        throw Unsupported("Text 或 Value", "目标控件未暴露 UI Automation TextPattern/ValuePattern。");
    }

    public IReadOnlyList<string> GetSelection(string bindingId, string controlId)
    {
        var (_, element) = ResolveControl(bindingId, controlId);
        return GetPattern<SelectionPattern>(element, SelectionPattern.Pattern, "Selection")
            .Current.GetSelection().Select(SafeName).ToArray();
    }

    public void Select(string bindingId, string controlId)
    {
        var (_, element) = ResolveControl(bindingId, controlId);
        GetPattern<SelectionItemPattern>(element, SelectionItemPattern.Pattern, "SelectionItem").Select();
    }

    public string GetToggleState(string bindingId, string controlId)
    {
        var (_, element) = ResolveControl(bindingId, controlId);
        return GetPattern<TogglePattern>(element, TogglePattern.Pattern, "Toggle").Current.ToggleState.ToString();
    }

    public void Toggle(string bindingId, string controlId)
    {
        var (_, element) = ResolveControl(bindingId, controlId);
        GetPattern<TogglePattern>(element, TogglePattern.Pattern, "Toggle").Toggle();
    }

    public void Invoke(string bindingId, string controlId)
    {
        var (_, element) = ResolveControl(bindingId, controlId);
        GetPattern<InvokePattern>(element, InvokePattern.Pattern, "Invoke").Invoke();
    }

    public async Task SendInputAsync(WindowInputRequest request, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(request);
        var binding = ResolveBinding(request.BindingId);
        var backend = request.Backend.Trim().ToLowerInvariant();
        if (backend is not ("auto" or "foreground"))
            throw Unsupported("WindowInput backend", "M7 未提供通用后台鼠标/键盘/文本输入；请使用 UIA 控件 API，或显式选择 auto/foreground。");

        var action = request.Action.Trim().ToLowerInvariant();
        var info = windows.GetInfo(binding.WindowId);
        if (!info.Visible || info.Minimized || !windows.IsForeground(binding.WindowId))
            throw new InvalidOperationException("目标窗口当前不可见、已最小化或不是前台窗口。WindowInput 不会自动激活或抢占焦点；请先显式调用 WindowActivate 并确认返回 true。");

        switch (action)
        {
            case "move":
            case "click":
            {
                var client = info.ClientRect;
                if (request.X < client.Left || request.X >= client.Right
                    || request.Y < client.Top || request.Y >= client.Bottom)
                    throw new ArgumentOutOfRangeException(nameof(request), "WindowInput 的 move/click 坐标必须位于绑定窗口客户区内（屏幕物理像素）。");

                if (!windows.IsForeground(binding.WindowId))
                    throw new InvalidOperationException("目标窗口在输入注入前失去前台状态；已拒绝本次输入。");

                input.MoveTo(request.X, request.Y);
                if (action == "click")
                    await input.ClickAsync(NormalizeMouseButton(request.Name), request.HoldMilliseconds, token).ConfigureAwait(false);
                break;
            }
            case "key-press":
                if (!info.Focused)
                    throw new InvalidOperationException("目标窗口当前没有键盘焦点；WindowInput 不会自动把焦点转移到目标控件。");
                if (!windows.IsForeground(binding.WindowId))
                    throw new InvalidOperationException("目标窗口在按键注入前失去前台状态；已拒绝本次输入。");
                await input.KeyPressAsync(Required(request.Name, "按键名称"), request.HoldMilliseconds, token).ConfigureAwait(false);
                break;
            case "text":
                if (request.Value.Length > 100000)
                    throw new ArgumentOutOfRangeException(nameof(request), "单次 WindowInput 文本最多 100000 个字符。");
                if (!info.Focused)
                    throw new InvalidOperationException("目标窗口当前没有键盘焦点；WindowInput 不会自动把焦点转移到目标控件。");
                if (!windows.IsForeground(binding.WindowId))
                    throw new InvalidOperationException("目标窗口在文本注入前失去前台状态；已拒绝本次输入。");
                input.TypeText(request.Value, token);
                break;
            default:
                throw new ArgumentException("WindowInput action 必须是 move、click、key-press 或 text。", nameof(request));
        }
    }

    private Binding ResolveBinding(string bindingId)
    {
        var id = Required(bindingId, "绑定 ID");
        if (!_bindings.TryGetValue(id, out var binding))
            throw new InvalidOperationException("窗口绑定不存在或已解绑；请重新执行 WindowBind。");
        var info = windows.GetInfo(binding.WindowId);
        if (info.ProcessId != binding.ProcessId)
        {
            _bindings.Remove(id);
            throw new InvalidOperationException("窗口绑定的进程身份已变化；请重新查找并绑定目标窗口。");
        }
        return binding;
    }

    private (Binding Binding, AutomationElement Element) ResolveControl(string bindingId, string controlId)
    {
        var binding = ResolveBinding(bindingId);
        var id = Required(controlId, "控件 ID");
        if (!binding.Elements.TryGetValue(id, out var element))
            throw new InvalidOperationException("控件引用不存在或已过期；请重新调用 ControlEnumerate。");
        try
        {
            if (element.Current.ProcessId != binding.ProcessId)
                throw new InvalidOperationException("控件已不属于绑定窗口的原进程；请重新枚举控件。");
        }
        catch (ElementNotAvailableException)
        {
            binding.Elements.Remove(id);
            throw new InvalidOperationException("目标控件已销毁或重建；请重新调用 ControlEnumerate。");
        }
        return (binding, element);
    }

    private AutomationElement GetRoot(Binding binding)
    {
        var handle = windows.GetValidatedHandle(binding.WindowId);
        try
        {
            return AutomationElement.FromHandle(handle)
                ?? throw new InvalidOperationException("UI Automation 未能为目标窗口创建根元素。");
        }
        catch (Exception ex) when (IsAutomationFailure(ex))
        {
            throw new InvalidOperationException("UI Automation 无法访问目标窗口：" + DescribeFailure(ex), ex);
        }
    }

    private static ControlElementInfo CreateInfo(AutomationElement element, string? existingId = null)
    {
        try
        {
            var current = element.Current;
            var rect = current.BoundingRectangle;
            var bounds = rect.IsEmpty
                ? new PixelRect(0, 0, 0, 0)
                : new PixelRect(
                    checked((int)Math.Floor(rect.Left)),
                    checked((int)Math.Floor(rect.Top)),
                    checked((int)Math.Ceiling(rect.Right)),
                    checked((int)Math.Ceiling(rect.Bottom)));
            return new ControlElementInfo(
                existingId ?? Guid.NewGuid().ToString("N"),
                current.Name ?? string.Empty,
                current.AutomationId ?? string.Empty,
                current.ClassName ?? string.Empty,
                current.ControlType?.ProgrammaticName ?? "ControlType.Unknown",
                bounds,
                current.IsEnabled,
                current.IsOffscreen,
                current.IsKeyboardFocusable,
                current.IsPassword,
                GetSupportedPatterns(element));
        }
        catch (ElementNotAvailableException ex)
        {
            throw new InvalidOperationException("目标控件已销毁或重建；请重新调用 ControlEnumerate。", ex);
        }
    }

    private static IReadOnlyList<string> GetSupportedPatterns(AutomationElement element)
    {
        var result = new List<string>(6);
        if (HasPattern(element, ValuePattern.Pattern)) result.Add("Value");
        if (HasPattern(element, TextPattern.Pattern)) result.Add("Text");
        if (HasPattern(element, SelectionPattern.Pattern)) result.Add("Selection");
        if (HasPattern(element, SelectionItemPattern.Pattern)) result.Add("SelectionItem");
        if (HasPattern(element, TogglePattern.Pattern)) result.Add("Toggle");
        if (HasPattern(element, InvokePattern.Pattern)) result.Add("Invoke");
        return result;
    }

    private static bool HasPattern(AutomationElement element, AutomationPattern pattern)
    {
        try { return element.TryGetCurrentPattern(pattern, out _); }
        catch (ElementNotAvailableException) { return false; }
        catch (InvalidOperationException) { return false; }
        catch (COMException) { return false; }
    }

    private static bool TryGetPattern<T>(AutomationElement element, AutomationPattern pattern, out T? value)
        where T : class
    {
        try
        {
            if (element.TryGetCurrentPattern(pattern, out var current) && current is T typed)
            {
                value = typed;
                return true;
            }
        }
        catch (ElementNotAvailableException)
        {
            throw new InvalidOperationException("目标控件已销毁或重建；请重新调用 ControlEnumerate。");
        }
        value = null;
        return false;
    }

    private static T GetPattern<T>(AutomationElement element, AutomationPattern pattern, string name)
        where T : class =>
        TryGetPattern<T>(element, pattern, out var value)
            ? value!
            : throw Unsupported(name, $"目标控件未暴露 UI Automation {name} pattern。");

    private static void EnsureNotPassword(AutomationElement element)
    {
        if (element.Current.IsPassword)
            throw new InvalidOperationException("目标控件是密码字段；为避免泄露凭据，禁止读取其 Value/Text 内容。");
    }

    private static string SafeName(AutomationElement element)
    {
        try { return element.Current.Name ?? string.Empty; }
        catch (ElementNotAvailableException) { return string.Empty; }
    }

    private static string NormalizeMouseButton(string name) =>
        name.Trim().ToLowerInvariant() switch
        {
            "left" or "right" or "middle" => name.Trim().ToLowerInvariant(),
            _ => throw new ArgumentException("WindowInput click 的 name 必须是 left、right 或 middle。", nameof(name))
        };

    private static string Required(string value, string label) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException($"{label}不能为空。", nameof(value))
            : value.Trim();

    private static InvalidOperationException Unsupported(string pattern, string reason) =>
        new($"Unsupported: {pattern}。{reason}");

    private static bool IsAutomationFailure(Exception ex) =>
        ex is ElementNotAvailableException or COMException or UnauthorizedAccessException or InvalidOperationException or Win32Exception;

    private static string DescribeFailure(Exception ex) =>
        ex is COMException com
            ? $"UI Automation COM 错误 0x{com.HResult:X8}：{com.Message}"
            : ex.Message;

    private sealed class Binding(string windowId, int processId)
    {
        public string WindowId { get; } = windowId;
        public int ProcessId { get; } = processId;
        public Dictionary<string, AutomationElement> Elements { get; } = new(StringComparer.Ordinal);
    }
}
