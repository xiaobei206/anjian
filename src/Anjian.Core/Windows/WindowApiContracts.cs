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
public sealed record WindowInputRequest(
    string BindingId,
    string Backend,
    string Action,
    string Name,
    int X,
    int Y,
    string Value,
    int HoldMilliseconds);

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
