using Anjian.Core.Windows;
using Xunit;

namespace Anjian.Core.Tests;

public sealed class CoordinateSystemTests
{
    private static readonly WindowGeometry Geometry = new(
        new PixelRect(-1920, 100, 0, 1180),
        new PixelRect(-1904, 138, -16, 1164));

    [Theory]
    [InlineData(CoordinateSpace.Screen, -1100d, 520d)]
    [InlineData(CoordinateSpace.Window, 820d, 420d)]
    [InlineData(CoordinateSpace.Client, 804d, 382d)]
    [InlineData(CoordinateSpace.Anchor, -140d, -120d)]
    [InlineData(CoordinateSpace.Ratio, 0.5d, 0.5d)]
    public void CoordinateSpacesRoundTripAcrossNegativeScreenCoordinates(
        CoordinateSpace space,
        double expectedScreenX,
        double expectedScreenY)
    {
        var screen = space switch
        {
            CoordinateSpace.Screen => new CoordinatePoint(expectedScreenX, expectedScreenY),
            CoordinateSpace.Window => CoordinateConverter.ToScreen(new CoordinatePoint(expectedScreenX, expectedScreenY), space, Geometry),
            CoordinateSpace.Client => CoordinateConverter.ToScreen(new CoordinatePoint(expectedScreenX, expectedScreenY), space, Geometry),
            CoordinateSpace.Anchor => CoordinateConverter.ToScreen(new CoordinatePoint(expectedScreenX, expectedScreenY), space, Geometry, WindowAnchor.Center),
            CoordinateSpace.Ratio => CoordinateConverter.ToScreen(new CoordinatePoint(expectedScreenX, expectedScreenY), space, Geometry),
            _ => throw new ArgumentOutOfRangeException(nameof(space))
        };

        var local = CoordinateConverter.FromScreen(screen, space, Geometry,
            space == CoordinateSpace.Anchor ? WindowAnchor.Center : WindowAnchor.TopLeft);
        var expected = space == CoordinateSpace.Screen
            ? new CoordinatePoint(expectedScreenX, expectedScreenY)
            : space == CoordinateSpace.Ratio
                ? new CoordinatePoint(0.5, 0.5)
                : space == CoordinateSpace.Anchor
                    ? new CoordinatePoint(expectedScreenX, expectedScreenY)
                    : new CoordinatePoint(expectedScreenX, expectedScreenY);

        Assert.Equal(expected.X, local.X, 8);
        Assert.Equal(expected.Y, local.Y, 8);
    }

    [Fact]
    public void WindowAndClientUseDifferentOrigins()
    {
        var windowPoint = CoordinateConverter.ToScreen(new CoordinatePoint(10, 20), CoordinateSpace.Window, Geometry);
        var clientPoint = CoordinateConverter.ToScreen(new CoordinatePoint(10, 20), CoordinateSpace.Client, Geometry);

        Assert.Equal(new CoordinatePoint(-1910, 120), windowPoint);
        Assert.Equal(new CoordinatePoint(-1894, 158), clientPoint);
        Assert.NotEqual(windowPoint, clientPoint);
    }

    [Theory]
    [InlineData(WindowAnchor.TopLeft, -1920d, 100d)]
    [InlineData(WindowAnchor.TopCenter, -960d, 100d)]
    [InlineData(WindowAnchor.TopRight, 0d, 100d)]
    [InlineData(WindowAnchor.CenterLeft, -1920d, 640d)]
    [InlineData(WindowAnchor.Center, -960d, 640d)]
    [InlineData(WindowAnchor.CenterRight, 0d, 640d)]
    [InlineData(WindowAnchor.BottomLeft, -1920d, 1180d)]
    [InlineData(WindowAnchor.BottomCenter, -960d, 1180d)]
    [InlineData(WindowAnchor.BottomRight, 0d, 1180d)]
    public void AnchorUsesWindowOuterRectangle(
        WindowAnchor anchor,
        double expectedX,
        double expectedY)
    {
        var actual = CoordinateConverter.ToScreen(new CoordinatePoint(0, 0), CoordinateSpace.Anchor, Geometry, anchor);
        Assert.Equal(new CoordinatePoint(expectedX, expectedY), actual);
    }

    [Fact]
    public void RatioUsesClientAreaAndAllowsInverseOutsideItsBounds()
    {
        var center = CoordinateConverter.ToScreen(new CoordinatePoint(0.5, 0.5), CoordinateSpace.Ratio, Geometry);
        Assert.Equal(new CoordinatePoint(-960, 651), center);

        var outside = CoordinateConverter.FromScreen(new CoordinatePoint(-2000, 100), CoordinateSpace.Ratio, Geometry);
        Assert.True(outside.X < 0);
        Assert.True(outside.Y < 0);
    }

    [Fact]
    public void RatioInputMustStayWithinClosedUnitSquare()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CoordinateConverter.ToScreen(new CoordinatePoint(1.01, 0.5), CoordinateSpace.Ratio, Geometry));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CoordinateConverter.ToScreen(new CoordinatePoint(0.5, -0.01), CoordinateSpace.Ratio, Geometry));
    }

    [Fact]
    public void PixelConversionRoundsAwayFromZeroAndKeepsNegativeValues()
    {
        Assert.Equal(new PixelPoint(-2, 3), CoordinateConverter.ToPixel(new CoordinatePoint(-1.5, 2.5)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CoordinateConverter.ToPixel(new CoordinatePoint(double.NaN, 0)));
    }

    [Theory]
    [InlineData(WindowAnchor.TopLeft, -1920, 0)]
    [InlineData(WindowAnchor.Center, -1160, 390)]
    [InlineData(WindowAnchor.BottomRight, -400, 780)]
    public void AlignWindowPositionsTheSameAnchorInMonitorWorkArea(
        WindowAnchor anchor,
        int expectedX,
        int expectedY)
    {
        var location = CoordinateConverter.AlignWindow(
            new PixelRect(0, 0, 400, 300),
            new PixelRect(-1920, 0, 0, 1080),
            anchor);

        Assert.Equal(new PixelPoint(expectedX, expectedY), location);
    }

    [Fact]
    public void AlignWindowAppliesPhysicalPixelOffsets()
    {
        var location = CoordinateConverter.AlignWindow(
            new PixelRect(0, 0, 400, 300),
            new PixelRect(-1920, 0, 0, 1080),
            WindowAnchor.TopLeft,
            offsetX: 16,
            offsetY: 24);

        Assert.Equal(new PixelPoint(-1904, 24), location);
    }

    [Fact]
    public void EmptyGeometryIsRejectedForWindowRelativeConversions()
    {
        var invalid = new WindowGeometry(new PixelRect(0, 0, 0, 0), new PixelRect(0, 0, 0, 0));
        Assert.Throws<ArgumentException>(() =>
            CoordinateConverter.ToScreen(new CoordinatePoint(1, 1), CoordinateSpace.Window, invalid));
        Assert.Throws<ArgumentException>(() =>
            CoordinateConverter.FromScreen(new CoordinatePoint(1, 1), CoordinateSpace.Client, invalid));
    }
}
