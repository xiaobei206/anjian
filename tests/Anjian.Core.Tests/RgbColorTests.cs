using Anjian.Core.Imaging;
using Xunit;

namespace Anjian.Core.Tests;

public sealed class RgbColorTests
{
    [Theory]
    [InlineData("FF00A1", 255, 0, 161)]
    [InlineData("#ff00a1", 255, 0, 161)]
    [InlineData("0xFF00A1", 255, 0, 161)]
    public void ParseHexAcceptsDocumentedForms(string value, int red, int green, int blue)
        => Assert.Equal((red, green, blue), RgbColor.ParseHex(value));

    [Theory]
    [InlineData("#FFF", false)]
    [InlineData("GG0000", false)]
    [InlineData("", false)]
    public void ParseHexRejectsInvalidValues(string value, bool _)
        => Assert.ThrowsAny<Exception>(() => RgbColor.ParseHex(value));

    [Fact]
    public void DifferenceReportsPerChannelAndAggregateDistances()
    {
        var distance = RgbColor.Difference(0x102030, 0x132638);
        Assert.Equal((3, 6, 8), (distance.Red, distance.Green, distance.Blue));
        Assert.Equal(8, distance.MaxChannel);
        Assert.Equal(17, distance.Manhattan);
        Assert.Equal(Math.Sqrt(109), distance.Euclidean, 10);
        Assert.Equal("#102030", RgbColor.ToHex(16, 32, 48));
    }
}
