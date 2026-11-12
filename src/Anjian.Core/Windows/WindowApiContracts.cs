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
