using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Windows.Automation;
using Anjian.Core.Api;
using Anjian.Core.Windows;

namespace Anjian.App.Services;

/// <summary>
/// Worker-local UI Automation provider. COM calls are isolated in the killable Lua worker.
/// M6 window identity is revalidated by the main process before each operation.
/// </summary>
internal sealed class WindowsControlAutomationProvider(ILuaCommandPipe pipe) : IWindowAutomationService
{
    private const int MaxStoredElements = 4096;
    private const uint GaRoot = 2;
    private readonly Dictionary<string, Binding> _bindings = new(StringComparer.Ordinal);

    public string Bind(WindowAutomationTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        ValidateTarget(target);
        var id = Guid.NewGuid().ToString("N");
        _bindings.Add(id, new Binding(target));
        return id;
    }

    public string GetWindowId(string bindingId) => ResolveBinding(bindingId).Target.WindowId;
    public bool Unbind(string bindingId) => _bindings.Remove(Required(bindingId, "绑定 ID"));
    public void ClearBindings() => _bindings.Clear();

    public WindowAutomationCapabilities GetCapabilities(string bindingId)
    {
        var binding = ResolveBinding(bindingId);
        string status, reason;
        try
        {
            _ = GetRoot(binding);
            status = "available";
            reason = "UI Automation 能够取得目标窗口根元素。";
        }
        catch (Exception ex) when (IsAutomationFailure(ex))
        {
            status = "unavailable";
            reason = DescribeFailure(ex);
        }

        var conditional = status == "available" ? "conditional" : "unavailable";
        var capabilities = new List<AutomationCapability>
        {
            new("controlTree",status,"UI Automation Control View",reason,"深度最多 16 层；单次最多返回 512 个元素。"),
            new("textRead",conditional,"UIA TextPattern / ValuePattern","目标控件须暴露 Text 或 Value pattern；密码字段禁止读取。","最多读取 32768 个字符。"),
            new("valueRead",conditional,"UIA ValuePattern","目标控件须暴露 Value pattern；密码字段禁止读取。","只返回 provider 报告的值。"),
            new("valueWrite",conditional,"UIA ValuePattern.SetValue","目标控件须暴露可写 Value pattern。","只读或不支持时明确拒绝，不回退为键盘输入。"),
            new("selectionRead",conditional,"UIA SelectionPattern","目标容器须暴露 Selection pattern。","返回 provider 报告的已选元素名称。"),
            new("selectionSelect",conditional,"UIA SelectionItemPattern","目标元素须暴露 SelectionItem pattern。","受容器单选/多选约束。"),
            new("toggleRead",conditional,"UIA TogglePattern","目标控件须暴露 Toggle pattern。","返回 Off/On/Indeterminate。"),
            new("toggle",conditional,"UIA TogglePattern.Toggle","目标控件须暴露 Toggle pattern。","每次调用切换一次。"),
            new("invoke",conditional,"UIA InvokePattern.Invoke","目标控件须暴露 Invoke pattern。","provider 接受调用不保证业务流程最终完成。"),
            new("backgroundControlValue",conditional,"UIA ValuePattern.SetValue","支持时可对后台控件执行语义值设置，不要求窗口在前台。","这不是后台键盘输入。"),
            new("backgroundControlSelection",conditional,"UIA Selection / SelectionItem","支持时可对后台控件读取/选择项。","受 provider 实现和权限约束。"),
            new("backgroundControlToggle",conditional,"UIA TogglePattern","支持时可对后台控件读取/切换。","受 provider 实现和权限约束。"),
            new("backgroundControlInvoke",conditional,"UIA InvokePattern","支持时可对后台控件执行 Invoke。","provider 接受调用不保证业务流程最终完成。"),
            new("foregroundMouse","conditional","SendInput","由主进程执行；目标窗口必须可见、未最小化且处于前台。","move/click 只允许目标客户区。"),
            new("foregroundKeyboard","conditional","SendInput","由主进程执行；目标窗口须在前台且窗口内有键盘焦点。","不会自动转移焦点。"),
            new("foregroundText","conditional","SendInput Unicode","由主进程执行；目标窗口须在前台且窗口内有键盘焦点。","不是 UIA ValuePattern 设置。"),
            new("backgroundMouse","unsupported","none","未实现经过验证的通用后台鼠标 provider。","不伪造 PostMessage 点击。"),
            new("backgroundKeyboard","unsupported","none","未实现经过验证的通用后台键盘 provider。","不伪造 PostMessage 按键。"),
            new("backgroundTextInput","unsupported","none","未实现经过验证的通用后台文本注入 provider。","控件值设置不等价于键盘文本输入。")
        };
        return new WindowAutomationCapabilities(bindingId, "Microsoft UI Automation Control View",
            binding.Target.ProcessId, IsForeground(binding.Target), capabilities);
    }

    public IReadOnlyList<ControlElementInfo> Enumerate(string bindingId, int maxResults)
    {
        if (maxResults is < 1 or > 512)
            throw new ArgumentOutOfRangeException(nameof(maxResults), "控件数量必须在 1 到 512 之间.");

        var binding = ResolveBinding(bindingId);
        var queue = new Queue<(AutomationElement Element, int Depth)>();
        var results = new List<ControlElementInfo>();
        var walker = TreeWalker.ControlViewWalker;
        queue.Enqueue((GetRoot(binding), 0));

        while (queue.Count > 0 && results.Count < maxResults)
        {
            var (element, depth) = queue.Dequeue();
            try
            {
                if (binding.Elements.Count >= MaxStoredElements)
                    throw new InvalidOperationException("本次运行控件引用已达到 4096；请解绑并重新绑定.");
                var info = CreateInfo(element);
                binding.Elements.Add(info.ControlId, element);
                results.Add(info);
                if (depth >= 16) continue;

                var child = walker.GetFirstChild(element);
                var siblings = 0;
                while (child is not null && siblings++ < maxResults && queue.Count < maxResults)
                {
                    queue.Enqueue((child, depth + 1));
                    child = walker.GetNextSibling(child);
                }
            }
            catch (ElementNotAvailableException)
            {
                // A control can disappear while the tree is being traversed.
            }
            catch (InvalidOperationException ex) when (ex.InnerException is ElementNotAvailableException)
            {
                // CreateInfo translates a destroyed element to a descriptive exception.
            }
            catch (COMException)
            {
                // Skip an individual provider node failure; preserve remaining results.
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
        if (value.Length > 32768) throw new ArgumentOutOfRangeException(nameof(value), "控件值最多 32768 个字符。");
        var (_, element) = ResolveControl(bindingId, controlId);
        var pattern = GetPattern<ValuePattern>(element, ValuePattern.Pattern, "Value");
        if (pattern.Current.IsReadOnly) throw new InvalidOperationException("目标控件为只读 Value pattern，拒绝设置。");
        pattern.SetValue(value);
    }

    public string GetText(string bindingId, string controlId)
    {
        var (_, element) = ResolveControl(bindingId, controlId);
        EnsureNotPassword(element);
        if (TryGetPattern(element, TextPattern.Pattern, out TextPattern? tp)) return tp!.DocumentRange.GetText(32768);
        if (TryGetPattern(element, ValuePattern.Pattern, out ValuePattern? vp)) return vp!.Current.Value;
        throw Unsupported("Text / Value", "目标控件未暴露 TextPattern 或 ValuePattern。");
    }

    public IReadOnlyList<string> GetSelection(string bindingId, string controlId)
    {
        var (_, e) = ResolveControl(bindingId, controlId);
        return GetPattern<SelectionPattern>(e, SelectionPattern.Pattern, "Selection")
            .Current.GetSelection().Select(SafeName).ToArray();
    }

    public void Select(string bindingId, string controlId)
    {
        var (_, e) = ResolveControl(bindingId, controlId);
        GetPattern<SelectionItemPattern>(e, SelectionItemPattern.Pattern, "SelectionItem").Select();
    }

    public string GetToggleState(string bindingId, string controlId)
    {
        var (_, e) = ResolveControl(bindingId, controlId);
        return GetPattern<TogglePattern>(e, TogglePattern.Pattern, "Toggle").Current.ToggleState.ToString();
    }

    public void Toggle(string bindingId, string controlId)
    {
        var (_, e) = ResolveControl(bindingId, controlId);
        GetPattern<TogglePattern>(e, TogglePattern.Pattern, "Toggle").Toggle();
    }

    public void Invoke(string bindingId, string controlId)
    {
        var (_, e) = ResolveControl(bindingId, controlId);
        GetPattern<InvokePattern>(e, InvokePattern.Pattern, "Invoke").Invoke();
    }

    private Binding ResolveBinding(string bindingId)
    {
        var id = Required(bindingId, "绑定 ID");
        if (!_bindings.TryGetValue(id, out var binding))
            throw new InvalidOperationException("窗口绑定不存在或已失效；请重新执行 WindowBind。");

        try
        {
            var response = pipe.Request("window-automation-target",
                text: JsonSerializer.Serialize(new WindowReferenceRequest(binding.Target.WindowId), WindowApiJson.Options));
            if (string.IsNullOrWhiteSpace(response))
                throw new InvalidOperationException("主进程未确认 M6 窗口引用仍有效。");
            var current = JsonSerializer.Deserialize<WindowAutomationTarget>(response, WindowApiJson.Options)
                ?? throw new InvalidDataException("主进程返回了无效窗口身份描述。");
            if (!SameIdentity(binding.Target, current))
                throw new InvalidOperationException("窗口进程/线程身份已变化；拒绝使用旧绑定。");
            ValidateTarget(current);
            return binding;
        }
        catch
        {
            _bindings.Remove(id);
            throw;
        }
    }

    private (Binding Binding, AutomationElement Element) ResolveControl(string bindingId, string controlId)
    {
        var b = ResolveBinding(bindingId);
        var id = Required(controlId, "控件 ID");
        if (!b.Elements.TryGetValue(id, out var element))
            throw new InvalidOperationException("控件 ID 不存在或已过期；请重新调用 ControlEnumerate。");
        try
        {
            if (element.Current.ProcessId != b.Target.ProcessId)
                throw new InvalidOperationException("控件不属于绑定窗口的原进程；请重新枚举。");
        }
        catch (ElementNotAvailableException)
        {
            b.Elements.Remove(id);
            throw new InvalidOperationException("目标控件已销毁或重建；请重新枚举控件。");
        }
        return (b, element);
    }

    private static AutomationElement GetRoot(Binding b)
    {
        ValidateTarget(b.Target);
        return AutomationElement.FromHandle(new IntPtr(b.Target.Handle))
            ?? throw new InvalidOperationException("UI Automation 无法为目标窗口创建根元素。");
    }

    private static ControlElementInfo CreateInfo(AutomationElement e, string? id = null)
    {
        try
        {
            var c = e.Current;
            var r = c.BoundingRectangle;
            var bounds = r.IsEmpty ? new PixelRect(0, 0, 0, 0) :
                new PixelRect(checked((int)Math.Floor(r.Left)), checked((int)Math.Floor(r.Top)),
                    checked((int)Math.Ceiling(r.Right)), checked((int)Math.Ceiling(r.Bottom)));
            return new ControlElementInfo(id ?? Guid.NewGuid().ToString("N"), c.Name ?? "",
                c.AutomationId ?? "", c.ClassName ?? "", c.ControlType?.ProgrammaticName ?? "ControlType.Unknown",
                bounds, c.IsEnabled, c.IsOffscreen, c.IsKeyboardFocusable, c.IsPassword, Patterns(e));
        }
        catch (ElementNotAvailableException ex)
        {
            throw new InvalidOperationException("目标控件已销毁或重建；请重新枚举控件。", ex);
        }
    }

    private static IReadOnlyList<string> Patterns(AutomationElement e)
    {
        var r = new List<string>(6);
        if (Has(e, ValuePattern.Pattern)) r.Add("Value");
        if (Has(e, TextPattern.Pattern)) r.Add("Text");
        if (Has(e, SelectionPattern.Pattern)) r.Add("Selection");
        if (Has(e, SelectionItemPattern.Pattern)) r.Add("SelectionItem");
        if (Has(e, TogglePattern.Pattern)) r.Add("Toggle");
        if (Has(e, InvokePattern.Pattern)) r.Add("Invoke");
        return r;
    }

    private static bool Has(AutomationElement e, AutomationPattern p)
    {
        try { return e.TryGetCurrentPattern(p, out _); }
        catch (ElementNotAvailableException) { return false; }
        catch (InvalidOperationException) { return false; }
        catch (COMException) { return false; }
    }

    private static bool TryGetPattern<T>(AutomationElement e, AutomationPattern p, out T? value) where T : class
    {
        try
        {
            if (e.TryGetCurrentPattern(p, out var found) && found is T typed) { value = typed; return true; }
        }
        catch (ElementNotAvailableException)
        {
            throw new InvalidOperationException("目标控件已销毁或重建；请重新枚举控件。");
        }
        value = null;
        return false;
    }

    private static T GetPattern<T>(AutomationElement e, AutomationPattern p, string name) where T : class =>
        TryGetPattern<T>(e, p, out var result) ? result! : throw Unsupported(name, $"目标控件未暴露 {name} pattern。");

    private static void EnsureNotPassword(AutomationElement e)
    {
        if (e.Current.IsPassword)
            throw new InvalidOperationException("目标控件是密码字段；为避免泄露凭据，禁止读取 Value/Text。");
    }

    private static string SafeName(AutomationElement e)
    {
        try { return e.Current.Name ?? ""; } catch (ElementNotAvailableException) { return ""; }
    }

    private static bool SameIdentity(WindowAutomationTarget a, WindowAutomationTarget b) =>
        a.WindowId == b.WindowId && a.Handle == b.Handle && a.ProcessId == b.ProcessId &&
        a.ThreadId == b.ThreadId && a.ClassName == b.ClassName &&
        a.ProcessStartTicks == b.ProcessStartTicks && a.IsChild == b.IsChild;

    private static string ReadClassName(IntPtr h)
    {
        var buffer = new StringBuilder(512);
        if (GetClassName(h, buffer, buffer.Capacity) == 0)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "读取目标窗口类名失败。");
        return buffer.ToString();
    }

    private static void ValidateTarget(WindowAutomationTarget t)
    {
        var h = new IntPtr(t.Handle);
        if (t.Handle == 0 || !IsWindowNative(h)) throw new InvalidOperationException("窗口已关闭或 HWND 已失效。");
        var thread = GetWindowThreadProcessId(h, out var processId);
        if (thread != t.ThreadId || processId != t.ProcessId) throw new InvalidOperationException("窗口进程/线程身份变化。");
        if (!string.Equals(ReadClassName(h), t.ClassName, StringComparison.Ordinal)) throw new InvalidOperationException("窗口类名变化。");
        if (t.ProcessStartTicks > 0)
        {
            using var process = Process.GetProcessById(t.ProcessId);
            if (process.StartTime.ToUniversalTime().Ticks != t.ProcessStartTicks) throw new InvalidOperationException("窗口所属进程已重启。");
        }
    }

    private static bool IsForeground(WindowAutomationTarget t)
    {
        ValidateTarget(t);
        var h = new IntPtr(t.Handle);
        var root = GetAncestor(h, GaRoot);
        return GetForegroundWindow() == (root == IntPtr.Zero ? h : root);
    }

    private static string Required(string value, string label) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException($"{label}不能为空。", nameof(value)) : value.Trim();
    private static InvalidOperationException Unsupported(string p, string reason) => new($"Unsupported: {p}。{reason}");
    private static bool IsAutomationFailure(Exception e) =>
        e is ElementNotAvailableException or COMException or UnauthorizedAccessException or InvalidOperationException or Win32Exception;
    private static string DescribeFailure(Exception e) =>
        e is COMException c ? $"UIA COM 错误 0x{c.HResult:X8}：{c.Message}" : e.Message;

    private sealed class Binding(WindowAutomationTarget target)
    {
        public WindowAutomationTarget Target { get; } = target;
        public Dictionary<string, AutomationElement> Elements { get; } = new(StringComparer.Ordinal);
    }

    public void Dispose() => ClearBindings();

    [DllImport("user32.dll", EntryPoint = "IsWindow", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowNative(IntPtr h);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll", EntryPoint = "GetClassNameW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetAncestor(IntPtr h, uint flags);
}
