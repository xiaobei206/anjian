using System.Text.Json;

namespace Anjian.Core.Windows;

public sealed record WindowSelector(
    string TitleContains,
    string ClassName,
    string ProcessName,
    int ProcessId,
    bool VisibleOnly);

public sealed record WindowReferenceRequest(string Id);

public sealed record WindowWaitRequest(WindowSelector Selector, int TimeoutMilliseconds);

public sealed record WindowWaitClosedRequest(string Id, int TimeoutMilliseconds);

public sealed record WindowCoordinateRequest(
    string Id,
    string Space,
    double X,
    double Y,
    string Anchor);

public sealed record WindowActionRequest(
    string Id,
    string Operation,
    int X,
    int Y,
    int Width,
    int Height,
    string Anchor,
    int OffsetX,
    int OffsetY,
    bool Enabled);

public sealed record WindowInfo(
    string Id,
    string Title,
    string ClassName,
    int ProcessId,
    string ProcessName,
    bool Visible,
    bool Minimized,
    bool Maximized,
    bool Foreground,
    bool Focused,
    bool IsChild,
    PixelRect WindowRect,
    PixelRect ClientRect,
    bool DestroyTracking);

public static class WindowApiJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };
}


public sealed record WindowBindingRequest(string WindowId);
public sealed record WindowBindingIdRequest(string BindingId);
public sealed record ControlEnumerateRequest(string BindingId, int MaxResults);
public sealed record ControlReferenceRequest(string BindingId, string ControlId);
public sealed record ControlSetValueRequest(string BindingId, string ControlId, string Value);
// Internal one-run descriptor sent only over the local named pipe to the isolated worker.
// Handle is never registered as or returned from a Lua API.
public sealed record WindowAutomationTarget(
    string WindowId, long Handle, int ProcessId, uint ThreadId,
    string ClassName, long ProcessStartTicks, bool IsChild);

public sealed record WindowInputRequest(
    string WindowId, string Backend, string Action, string Name,
    int X, int Y, string Value, int HoldMilliseconds);

public sealed record AutomationCapability(
    string Name,
    string Status,
    string Backend,
    string Reason,
    string Limitations);

public sealed record WindowAutomationCapabilities(
    string BindingId,
    string Provider,
    int ProcessId,
    bool Foreground,
    IReadOnlyList<AutomationCapability> Capabilities);

public sealed record ControlElementInfo(
    string ControlId,
    string Name,
    string AutomationId,
    string ClassName,
    string ControlType,
    PixelRect Bounds,
    bool IsEnabled,
    bool IsOffscreen,
    bool IsKeyboardFocusable,
    bool IsPassword,
    IReadOnlyList<string> SupportedPatterns);


/// <summary>Worker-local UI Automation adapter inside the killable Lua worker.</summary>
public interface IWindowAutomationService : IDisposable
{
    string Bind(WindowAutomationTarget target);
    string GetWindowId(string bindingId);
    bool Unbind(string bindingId);
    void ClearBindings();
    WindowAutomationCapabilities GetCapabilities(string bindingId);
    IReadOnlyList<ControlElementInfo> Enumerate(string bindingId, int maxResults);
    ControlElementInfo GetInfo(string bindingId, string controlId);
    string GetValue(string bindingId, string controlId);
    void SetValue(string bindingId, string controlId, string value);
    string GetText(string bindingId, string controlId);
    IReadOnlyList<string> GetSelection(string bindingId, string controlId);
    void Select(string bindingId, string controlId);
    string GetToggleState(string bindingId, string controlId);
    void Toggle(string bindingId, string controlId);
    void Invoke(string bindingId, string controlId);
}
