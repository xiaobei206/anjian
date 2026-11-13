using Anjian.Core.Imaging;
using Xunit;

namespace Anjian.Core.Tests;

public sealed class ImageSearchEngineTests
{
    private static uint Rgb(uint color) => 0xFF000000u | (color & 0xFFFFFFu);

    [Fact]
    public void PixelImageReadsRgbAndCropKeepsScreenOrigin()
    {
        var image = new PixelImage(3, 2,
            [Rgb(0x112233), Rgb(0x445566), Rgb(0x778899), Rgb(0xAABBCC), Rgb(0xDDEEFF), Rgb(0x010203)],
            -1920, 40, true);

        Assert.Equal(0x112233u, image.GetPixelColor(0, 0));
        var crop = image.Crop(1, 0, 2, 2);
        Assert.Equal((2, 2), (crop.Width, crop.Height));
        Assert.True(crop.HasScreenCoordinates);
        Assert.Equal((-1919, 40), (crop.ScreenOriginX, crop.ScreenOriginY));
        Assert.Equal(0xDDEEFFu, crop.GetPixelColor(0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => image.GetPixelColor(3, 0));
    }

    [Fact]
    public void FindColorUsesRegionToleranceAndStep()
    {
        var image = new PixelImage(4, 3,
        [
            Rgb(0x000000), Rgb(0x000000), Rgb(0x101010), Rgb(0x000000),
            Rgb(0x000000), Rgb(0xFE0001), Rgb(0x000000), Rgb(0x000000),
            Rgb(0x000000), Rgb(0x000000), Rgb(0xFF0000), Rgb(0x000000)
        ], 100, 200, true);

        Assert.Null(ImageSearchEngine.FindColor(image, 0xFF0000, 0, left: 0, top: 0, width: 2, height: 2));
        var found = ImageSearchEngine.FindColor(image, 0xFF0000, 2);
        Assert.NotNull(found);
        Assert.Equal((1, 1), (found.X, found.Y));
        Assert.Equal((101, 201), (found.ScreenX, found.ScreenY));
        Assert.Equal(2, ImageSearchEngine.CountColors(image, 0xFF0000, tolerance: 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => ImageSearchEngine.FindColor(image, 0, left: 4));
    }

    [Fact]
    public void MultiColorChecksOffsetsAndRejectsMalformedPatterns()
    {
        var image = new PixelImage(4, 3,
        [
            Rgb(0x112233), Rgb(0xFF0000), Rgb(0), Rgb(0),
            Rgb(0x00FF00), Rgb(0), Rgb(0), Rgb(0),
            Rgb(0), Rgb(0), Rgb(0x112233), Rgb(0xFF0000)
        ]);

        var probes = ImageSearchEngine.ParseColorPattern("1,0,FF0000;0,1,00FF00");
        var found = ImageSearchEngine.FindMultiColor(image, 0x112233, probes);
        Assert.NotNull(found);
        Assert.Equal((0, 0), (found.X, found.Y));
        Assert.Throws<FormatException>(() => ImageSearchEngine.ParseColorPattern("bad-pattern"));
        Assert.Null(ImageSearchEngine.FindMultiColor(image, 0x112233,
            [new ImageColorProbe(1, 0, 0xFFFFFF)]));
    }

    [Fact]
    public void TemplateMatchingSupportsToleranceThresholdTransparencyAndMultipleResults()
    {
        var source = new PixelImage(6, 3,
        [
            Rgb(0xFF0000), Rgb(0x00FF00), Rgb(0), Rgb(0), Rgb(0xFF0001), Rgb(0x00FF01),
            Rgb(0), Rgb(0), Rgb(0), Rgb(0), Rgb(0), Rgb(0),
            Rgb(0xFF0000), Rgb(0), Rgb(0), Rgb(0), Rgb(0), Rgb(0)
        ]);
        var template = new PixelImage(2, 1, [Rgb(0xFF0000), Rgb(0x00FF00)]);
        var found = ImageSearchEngine.FindImage(source, template, 1.0, tolerance: 1);
        Assert.NotNull(found);
        Assert.Equal((0, 0), (found.X, found.Y));
        Assert.Equal(1.0, found.Similarity);

        var matches = ImageSearchEngine.FindImages(source, template, 0.9, tolerance: 1, maxResults: 10);
        Assert.Equal(2, matches.Count);
        Assert.Equal(new[] { 0, 4 }, matches.Select(x => x.X).ToArray());

        var transparentTemplate = new PixelImage(2, 1, [0x00000000u, Rgb(0x00FF00)]);
        var transparentMatch = ImageSearchEngine.FindImage(source, transparentTemplate);
        Assert.NotNull(transparentMatch);
        Assert.Equal((0, 0), (transparentMatch.X, transparentMatch.Y));
        Assert.Throws<ArgumentException>(() =>
            ImageSearchEngine.FindImage(source, new PixelImage(1, 1, [0x00000000u])));
    }

    [Fact]
    public void ConsecutiveConditionTrackerRequiresStableObservationsAndResets()
    {
        var tracker = new ConsecutiveConditionTracker(3);
        Assert.False(tracker.Observe(true));
        Assert.False(tracker.Observe(true));
        Assert.False(tracker.Observe(false));
        Assert.Equal(0, tracker.ConsecutiveCount);
        Assert.False(tracker.Observe(true));
        Assert.False(tracker.Observe(true));
        Assert.True(tracker.Observe(true));
        Assert.True(tracker.Observe(true));
        Assert.Equal(3, tracker.ConsecutiveCount);
        Assert.Throws<ArgumentOutOfRangeException>(() => new ConsecutiveConditionTracker(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ConsecutiveConditionTracker(21));
    }

    [Fact]
    public void TemplateMatchingSupportsGrayscaleAndNearestNeighborScale()
    {
        var source = new PixelImage(4, 2,
        [
            Rgb(0xFF0000), Rgb(0xFF0000), Rgb(0x00FF00), Rgb(0x00FF00),
            Rgb(0xFF0000), Rgb(0xFF0000), Rgb(0x00FF00), Rgb(0x00FF00)
        ]);
        var template = new PixelImage(2, 1, [Rgb(0xFF0000), Rgb(0x00FF00)]);

        var unscaled = ImageSearchEngine.FindImage(source, template, 1.0);
        var scaled = ImageSearchEngine.FindImage(source, template, 1.0, scale: 2.0);
        Assert.NotNull(unscaled);
        Assert.NotNull(scaled);
        Assert.Equal(1, unscaled.X);
        Assert.Equal(0, scaled.X);

        var red = new PixelImage(1, 1, [Rgb(0xFF0000)]);
        var equivalentGray = new PixelImage(1, 1, [Rgb(0x4C4C4C)]);
        Assert.Null(ImageSearchEngine.FindImage(red, equivalentGray, 1.0));
        var grayscale = ImageSearchEngine.FindImage(red, equivalentGray, 1.0, grayscale: true);
        Assert.NotNull(grayscale);
        Assert.Equal(1.0, grayscale.Similarity);

        Assert.Throws<ArgumentOutOfRangeException>(() => ImageSearchEngine.FindImage(source, template, scale: 4.1));
    }

    [Fact]
    public void LongImageSearchOperationsObserveCancellation()
    {
        var image = new PixelImage(2, 2, [Rgb(1), Rgb(2), Rgb(3), Rgb(4)]);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
            ImageSearchEngine.FindColor(image, 99, cancellationToken: cancellation.Token));
        Assert.Throws<OperationCanceledException>(() =>
            ImageSearchEngine.FindMultiColor(image, 99, [new ImageColorProbe(1, 0, 2)],
                cancellationToken: cancellation.Token));
        Assert.Throws<OperationCanceledException>(() =>
            ImageSearchEngine.CountColors(image, 99, cancellationToken: cancellation.Token));
        Assert.Throws<OperationCanceledException>(() =>
            ImageSearchEngine.FindImage(image, new PixelImage(1, 1, [Rgb(1)]),
                cancellationToken: cancellation.Token));
        Assert.Throws<OperationCanceledException>(() =>
            ImageSearchEngine.FindImages(image, new PixelImage(1, 1, [Rgb(1)]),
                cancellationToken: cancellation.Token));
    }

    [Fact]
    public void FindColorBlocksGroupsAdjacentSamplesAndPreservesScreenOrigin()
    {
        var image = new PixelImage(6, 4,
        [
            Rgb(0xFF0000), Rgb(0xFF0000), Rgb(0), Rgb(0), Rgb(0), Rgb(0),
            Rgb(0xFF0000), Rgb(0xFF0000), Rgb(0), Rgb(0), Rgb(0xFF0000), Rgb(0),
            Rgb(0), Rgb(0), Rgb(0), Rgb(0), Rgb(0), Rgb(0),
            Rgb(0), Rgb(0), Rgb(0), Rgb(0), Rgb(0), Rgb(0)
        ], -100, 20, true);

        var blocks = ImageSearchEngine.FindColorBlocks(image, 0xFF0000, minPixels: 1);
        Assert.Equal(2, blocks.Count);
        Assert.Equal((0, 0, 2, 2, 4), (blocks[0].X, blocks[0].Y, blocks[0].Width, blocks[0].Height, blocks[0].PixelCount));
        Assert.Equal((-100, 20), (blocks[0].ScreenX, blocks[0].ScreenY));
        Assert.Equal(1, blocks[1].PixelCount);
        Assert.Equal(0, ImageSearchEngine.FindColorBlocks(image, 0xFF0000, minPixels: 2, left: 4, top: 0, width: 2, height: 2).Count);
    }

    [Fact]
    public void FindColorBlocksHonorsStepAndCancellation()
    {
        var image = new PixelImage(4, 4, Enumerable.Repeat(Rgb(0x00FF00), 16).ToArray());
        var blocks = ImageSearchEngine.FindColorBlocks(image, 0x00FF00, minPixels: 1, step: 2);
        Assert.Single(blocks);
        Assert.Equal(4, blocks[0].PixelCount);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() =>
            ImageSearchEngine.FindColorBlocks(image, 0x00FF00, cancellationToken: cancellation.Token));
    }

    [Fact]
    public void CountColorAndImageRegionsAreValidated()
    {
        var image = new PixelImage(3, 2, [Rgb(1), Rgb(1), Rgb(1), Rgb(2), Rgb(1), Rgb(2)]);
        Assert.Equal(3, ImageSearchEngine.CountColors(image, 1, left: 1, top: 0, width: 2, height: 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => ImageSearchEngine.NormalizeRegion(image, 2, 1, 2, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => ImageSearchEngine.FindImages(image, image, maxResults: 1001));
        Assert.False(ImageSearchEngine.CompareColor(image, 0, 0, 2));
        Assert.True(ImageSearchEngine.CompareColor(image, 0, 0, 1, tolerance: 0));
    }
}
