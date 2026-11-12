namespace Anjian.Core.Windows;

public enum CoordinateSpace
{
    Screen,
    Window,
    Client,
    Anchor,
    Ratio
}

public enum WindowAnchor
{
    TopLeft,
    TopCenter,
    TopRight,
    CenterLeft,
    Center,
    CenterRight,
    BottomLeft,
    BottomCenter,
    BottomRight
}

public readonly record struct CoordinatePoint(double X, double Y);
public readonly record struct PixelPoint(int X, int Y);

/// <summary>Physical-pixel rectangle using Win32's left/top inclusive and right/bottom exclusive edges.</summary>
public readonly record struct PixelRect(int Left, int Top, int Right, int Bottom)
{
    public int Width => checked(Right - Left);
    public int Height => checked(Bottom - Top);
    public bool HasArea => Right > Left && Bottom > Top;
}

public readonly record struct WindowGeometry(PixelRect WindowRect, PixelRect ClientRect);

/// <summary>
/// Converts coordinates using physical screen pixels. Window coordinates use the outer
/// window rectangle; client and ratio coordinates use the client area in screen space.
/// </summary>
public static class CoordinateConverter
{
    public static CoordinatePoint ToScreen(
        CoordinatePoint point,
        CoordinateSpace space,
        WindowGeometry geometry,
        WindowAnchor anchor = WindowAnchor.TopLeft)
    {
        EnsureFinite(point);
        return space switch
        {
            CoordinateSpace.Screen => point,
            CoordinateSpace.Window => Offset(point, RequireArea(geometry.WindowRect, "窗口外框"), false),
            CoordinateSpace.Client => Offset(point, RequireArea(geometry.ClientRect, "客户区"), false),
            CoordinateSpace.Anchor => Add(point, AnchorPoint(RequireArea(geometry.WindowRect, "窗口外框"), anchor)),
            CoordinateSpace.Ratio => RatioToScreen(point, RequireArea(geometry.ClientRect, "客户区")),
            _ => throw new ArgumentOutOfRangeException(nameof(space))
        };
    }

    public static CoordinatePoint FromScreen(
        CoordinatePoint screenPoint,
        CoordinateSpace space,
        WindowGeometry geometry,
        WindowAnchor anchor = WindowAnchor.TopLeft)
    {
        EnsureFinite(screenPoint);
        return space switch
        {
            CoordinateSpace.Screen => screenPoint,
            CoordinateSpace.Window => Offset(screenPoint, RequireArea(geometry.WindowRect, "窗口外框"), true),
            CoordinateSpace.Client => Offset(screenPoint, RequireArea(geometry.ClientRect, "客户区"), true),
            CoordinateSpace.Anchor => Subtract(screenPoint, AnchorPoint(RequireArea(geometry.WindowRect, "窗口外框"), anchor)),
            CoordinateSpace.Ratio => ScreenToRatio(screenPoint, RequireArea(geometry.ClientRect, "客户区")),
            _ => throw new ArgumentOutOfRangeException(nameof(space))
        };
    }

    public static PixelPoint ToPixel(CoordinatePoint point)
    {
        EnsureFinite(point);
        if (point.X < int.MinValue || point.X > int.MaxValue || point.Y < int.MinValue || point.Y > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(point), "换算后的物理像素坐标超出 Int32 范围。");

        return new PixelPoint(
            checked((int)Math.Round(point.X, MidpointRounding.AwayFromZero)),
            checked((int)Math.Round(point.Y, MidpointRounding.AwayFromZero)));
    }

    /// <summary>Aligns the same anchor of a window to the chosen anchor of a monitor work area.</summary>
    public static PixelPoint AlignWindow(
        PixelRect windowBounds,
        PixelRect workArea,
        WindowAnchor anchor,
        int offsetX = 0,
        int offsetY = 0)
    {
        RequireArea(windowBounds, "窗口外框");
        RequireArea(workArea, "显示器工作区");

        var targetAnchor = AnchorPoint(workArea, anchor);
        var localWindowAnchor = AnchorPoint(
            new PixelRect(0, 0, windowBounds.Width, windowBounds.Height), anchor);

        return ToPixel(new CoordinatePoint(
            targetAnchor.X - localWindowAnchor.X + offsetX,
            targetAnchor.Y - localWindowAnchor.Y + offsetY));
    }

    private static CoordinatePoint RatioToScreen(CoordinatePoint point, PixelRect rect)
    {
        if (point.X is < 0 or > 1 || point.Y is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(point), "ratio 坐标必须位于 0 到 1 的闭区间。");

        return new CoordinatePoint(rect.Left + point.X * rect.Width, rect.Top + point.Y * rect.Height);
    }

    private static CoordinatePoint ScreenToRatio(CoordinatePoint point, PixelRect rect) =>
        new((point.X - rect.Left) / rect.Width, (point.Y - rect.Top) / rect.Height);

    private static CoordinatePoint Offset(CoordinatePoint point, PixelRect rect, bool subtract) =>
        subtract
            ? new CoordinatePoint(point.X - rect.Left, point.Y - rect.Top)
            : new CoordinatePoint(point.X + rect.Left, point.Y + rect.Top);

    private static CoordinatePoint Add(CoordinatePoint left, CoordinatePoint right) =>
        new(left.X + right.X, left.Y + right.Y);

    private static CoordinatePoint Subtract(CoordinatePoint left, CoordinatePoint right) =>
        new(left.X - right.X, left.Y - right.Y);

    private static CoordinatePoint AnchorPoint(PixelRect rect, WindowAnchor anchor)
    {
        var x = anchor switch
        {
            WindowAnchor.TopLeft or WindowAnchor.CenterLeft or WindowAnchor.BottomLeft => rect.Left,
            WindowAnchor.TopCenter or WindowAnchor.Center or WindowAnchor.BottomCenter => rect.Left + rect.Width / 2d,
            WindowAnchor.TopRight or WindowAnchor.CenterRight or WindowAnchor.BottomRight => rect.Right,
            _ => throw new ArgumentOutOfRangeException(nameof(anchor))
        };
        var y = anchor switch
        {
            WindowAnchor.TopLeft or WindowAnchor.TopCenter or WindowAnchor.TopRight => rect.Top,
            WindowAnchor.CenterLeft or WindowAnchor.Center or WindowAnchor.CenterRight => rect.Top + rect.Height / 2d,
            WindowAnchor.BottomLeft or WindowAnchor.BottomCenter or WindowAnchor.BottomRight => rect.Bottom,
            _ => throw new ArgumentOutOfRangeException(nameof(anchor))
        };
        return new CoordinatePoint(x, y);
    }

    private static PixelRect RequireArea(PixelRect rect, string label)
    {
        if (!rect.HasArea)
            throw new ArgumentException($"{label}矩形无效或尺寸为零。");
        return rect;
    }

    private static void EnsureFinite(CoordinatePoint point)
    {
        if (!double.IsFinite(point.X) || !double.IsFinite(point.Y))
            throw new ArgumentOutOfRangeException(nameof(point), "坐标必须是有限数值。");
    }
}
