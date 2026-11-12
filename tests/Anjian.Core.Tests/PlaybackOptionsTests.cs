using Anjian.Core;

namespace Anjian.Core.Tests;

public sealed class PlaybackOptionsTests
{
    [Fact]
    public void DefaultsToOneRunAndTwoSecondGap()
    {
        var options = new PlaybackOptions();
        Assert.Equal(1, options.RepeatCount);
        Assert.Equal(2_000, options.IntervalMilliseconds);
        Assert.False(options.HasNextIteration(1));
    }

    [Fact]
    public void OneHundredRunsHaveNinetyNineGaps()
    {
        var options = new PlaybackOptions { RepeatCount = 100, IntervalMilliseconds = 750 };
        Assert.True(options.HasNextIteration(1));
        Assert.True(options.HasNextIteration(99));
        Assert.False(options.HasNextIteration(100));
    }

    [Fact]
    public void ZeroMeansInfinite()
    {
        var options = new PlaybackOptions { RepeatCount = 0 };
        Assert.True(options.IsInfinite);
        Assert.True(options.HasNextIteration(1));
        Assert.True(options.HasNextIteration(100_000));
    }

    [Fact]
    public void RejectsOutOfRangeSettings()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PlaybackOptions { RepeatCount = -1 }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new PlaybackOptions { RepeatCount = 100_001 }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new PlaybackOptions { IntervalMilliseconds = 600_001 }.Validate());
    }
}
