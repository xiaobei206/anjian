using System.Globalization;
using Anjian.Core.Windows;

namespace Anjian.Core.Imaging;

public readonly record struct ImageSearchRegion(int Left, int Top, int Width, int Height);
public readonly record struct ImageColorProbe(int OffsetX, int OffsetY, uint Color);
public sealed record ImageMatchPoint(
    int X, int Y, int ScreenX, int ScreenY, bool HasScreenCoordinates, double Similarity = 1.0);

/// <summary>Deterministic RGB comparisons and template matching, independent of Windows/GDI.</summary>
public static class ImageSearchEngine
{
    public const int MaximumPatternProbes = 256;
    public const int MaximumResults = 1000;
    private const long MaximumPixelComparisons = 50_000_000;

    public static bool CompareColor(PixelImage image, int x, int y, uint expected, int tolerance = 0)
    {
        ArgumentNullException.ThrowIfNull(image);
        ValidateTolerance(tolerance);
        return ColorsMatch(image.GetPixelColor(x, y), expected, tolerance);
    }

    public static ImageMatchPoint? FindColor(
        PixelImage image, uint expected, int tolerance = 0,
        int left = 0, int top = 0, int width = 0, int height = 0, int step = 1, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);
        ValidateTolerance(tolerance);
        ValidateStep(step);
        var region = NormalizeRegion(image, left, top, width, height);
        for (var y = region.Top; y < region.Top + region.Height; y += step)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (var x = region.Left; x < region.Left + region.Width; x += step)
            {
                if (ColorsMatch(image.GetPixelColor(x, y), expected, tolerance))
                    return CreatePoint(image, x, y);
            }
        }
        return null;
    }

    public static ImageMatchPoint? FindMultiColor(
        PixelImage image, uint baseColor, IReadOnlyList<ImageColorProbe> probes, int tolerance = 0,
        int left = 0, int top = 0, int width = 0, int height = 0, int step = 1, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(probes);
        ValidateTolerance(tolerance);
        ValidateStep(step);
        if (probes.Count is < 1 or > MaximumPatternProbes)
            throw new ArgumentOutOfRangeException(nameof(probes), $"多点找色必须有 1 到 {MaximumPatternProbes} 个相对采样点。");

        var region = NormalizeRegion(image, left, top, width, height);
        for (var y = region.Top; y < region.Top + region.Height; y += step)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (var x = region.Left; x < region.Left + region.Width; x += step)
            {
                if (!ColorsMatch(image.GetPixelColor(x, y), baseColor, tolerance))
                    continue;

                var matched = true;
                foreach (var probe in probes)
                {
                    var px = (long)x + probe.OffsetX;
                    var py = (long)y + probe.OffsetY;
                    if (px < 0 || py < 0 || px >= image.Width || py >= image.Height
                        || !ColorsMatch(image.GetPixelColor((int)px, (int)py), probe.Color, tolerance))
                    {
                        matched = false;
                        break;
                    }
                }

                if (matched)
                    return CreatePoint(image, x, y);
            }
        }
        return null;
    }

    public static int CountColors(
        PixelImage image, uint expected, int tolerance = 0,
        int left = 0, int top = 0, int width = 0, int height = 0, int step = 1, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);
        ValidateTolerance(tolerance);
        ValidateStep(step);
        var region = NormalizeRegion(image, left, top, width, height);
        var count = 0;
        for (var y = region.Top; y < region.Top + region.Height; y += step)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (var x = region.Left; x < region.Left + region.Width; x += step)
                if (ColorsMatch(image.GetPixelColor(x, y), expected, tolerance))
                    count = checked(count + 1);
        }
        return count;
    }

    public static ImageMatchPoint? FindImage(
        PixelImage source, PixelImage template, double similarity = 0.95, int tolerance = 0,
        int left = 0, int top = 0, int width = 0, int height = 0, int step = 1,
        bool grayscale = false, double scale = 1.0, CancellationToken cancellationToken = default)
    {
        var matches = FindImages(source, template, similarity, tolerance, 1, left, top, width, height, step, grayscale, scale, cancellationToken);
        return matches.Count == 0 ? null : matches[0];
    }

    public static IReadOnlyList<ImageMatchPoint> FindImages(
        PixelImage source, PixelImage template, double similarity = 0.95, int tolerance = 0, int maxResults = 100,
        int left = 0, int top = 0, int width = 0, int height = 0, int step = 1,
        bool grayscale = false, double scale = 1.0, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(template);
        ValidateSimilarity(similarity);
        ValidateTolerance(tolerance);
        ValidateStep(step);
        ValidateScale(scale);
        if (maxResults is < 1 or > MaximumResults)
            throw new ArgumentOutOfRangeException(nameof(maxResults), $"maxResults 必须在 1 到 {MaximumResults} 之间。");

        var effectiveTemplate = scale == 1.0 ? template : ResizeTemplate(template, scale, cancellationToken);
        var region = NormalizeRegion(source, left, top, width, height);
        if (effectiveTemplate.Width > region.Width || effectiveTemplate.Height > region.Height)
            return Array.Empty<ImageMatchPoint>();

        var totalSamples = CountOpaquePixels(effectiveTemplate, cancellationToken);
        if (totalSamples == 0)
            throw new ArgumentException("模板图像不能全部透明。", nameof(template));
        var requiredSamples = (int)Math.Ceiling(similarity * totalSamples);
        var matches = new List<ImageMatchPoint>(Math.Min(maxResults, 16));
        long comparisons = 0;

        var lastY = region.Top + region.Height - effectiveTemplate.Height;
        var lastX = region.Left + region.Width - effectiveTemplate.Width;
        for (var y = region.Top; y <= lastY && matches.Count < maxResults; y += step)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (var x = region.Left; x <= lastX && matches.Count < maxResults;)
            {
                var score = ScoreAt(source, effectiveTemplate, x, y, tolerance, totalSamples, requiredSamples, grayscale, ref comparisons, cancellationToken);
                if (score >= similarity)
                {
                    var candidate = CreatePoint(source, x, y, score);
                    if (!OverlapsExisting(candidate, effectiveTemplate.Width, effectiveTemplate.Height, matches))
                    {
                        matches.Add(candidate);
                        x += Math.Max(step, effectiveTemplate.Width);
                    }
                    else
                    {
                        x += step;
                    }
                }
                else
                {
                    x += step;
                }
            }
        }

        return matches;
    }

    public static PixelImage ResizeTemplate(PixelImage template, double scale, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(template);
        ValidateScale(scale);
        if (scale == 1.0)
            return template;

        var widthDouble = Math.Round(template.Width * scale, MidpointRounding.AwayFromZero);
        var heightDouble = Math.Round(template.Height * scale, MidpointRounding.AwayFromZero);
        if (!double.IsFinite(widthDouble) || !double.IsFinite(heightDouble)
            || widthDouble < 1 || heightDouble < 1
            || widthDouble > PixelImage.MaximumDimension || heightDouble > PixelImage.MaximumDimension
            || widthDouble * heightDouble > PixelImage.MaximumPixelCount)
            throw new ArgumentOutOfRangeException(nameof(scale), "缩放后的模板尺寸超出图像资源限制。");

        var width = checked((int)widthDouble);
        var height = checked((int)heightDouble);
        var sourcePixels = template.CopyArgbPixels();
        var result = new uint[checked(width * height)];
        for (var y = 0; y < height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sourceY = Math.Min(template.Height - 1, (int)((long)y * template.Height / height));
            for (var x = 0; x < width; x++)
            {
                var sourceX = Math.Min(template.Width - 1, (int)((long)x * template.Width / width));
                result[y * width + x] = sourcePixels[sourceY * template.Width + sourceX];
            }
        }
        return new PixelImage(width, height, result);
    }

    public static ImageSearchRegion NormalizeRegion(
        PixelImage image, int left, int top, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (left < 0 || top < 0 || left >= image.Width || top >= image.Height)
            throw new ArgumentOutOfRangeException(nameof(left), "搜索区域左上角必须位于图像内。");
        if (width < 0 || height < 0)
            throw new ArgumentOutOfRangeException(nameof(width), "搜索区域宽高不能为负数。");

        var normalizedWidth = width == 0 ? image.Width - left : width;
        var normalizedHeight = height == 0 ? image.Height - top : height;
        if (normalizedWidth < 1 || normalizedHeight < 1
            || (long)left + normalizedWidth > image.Width
            || (long)top + normalizedHeight > image.Height)
            throw new ArgumentOutOfRangeException(nameof(width), "搜索区域必须完全位于图像内。");
        return new ImageSearchRegion(left, top, normalizedWidth, normalizedHeight);
    }

    public static IReadOnlyList<ImageColorProbe> ParseColorPattern(string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern) || pattern.Length > 16_384)
            throw new ArgumentException("多点颜色模式不能为空，且最多 16384 个字符。", nameof(pattern));

        var probes = new List<ImageColorProbe>();
        foreach (var item in pattern.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = item.Split(',', StringSplitOptions.TrimEntries);
            if (parts.Length != 3
                || !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var dx)
                || !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var dy)
                || dx is < -32768 or > 32768 || dy is < -32768 or > 32768)
                throw new FormatException("多点模式格式应为 dx,dy,RRGGBB;dx,dy,RRGGBB。");

            var colorText = parts[2].Trim();
            if (colorText.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                colorText = colorText[2..];
            if (colorText.StartsWith('#'))
                colorText = colorText[1..];
            if (colorText.Length is < 1 or > 6
                || !uint.TryParse(colorText, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var color)
                || color > 0xFFFFFF)
                throw new FormatException("多点模式颜色必须是十六进制 RRGGBB（允许 0x 或 # 前缀）。");

            probes.Add(new ImageColorProbe(dx, dy, color));
            if (probes.Count > MaximumPatternProbes)
                throw new ArgumentOutOfRangeException(nameof(pattern), $"多点模式最多 {MaximumPatternProbes} 个采样点。");
        }

        if (probes.Count == 0)
            throw new FormatException("多点颜色模式至少需要一个相对采样点。");
        return probes;
    }

    public static bool ColorsMatch(uint actual, uint expected, int tolerance, bool grayscale = false)
    {
        ValidateTolerance(tolerance);
        actual &= 0xFFFFFF;
        expected &= 0xFFFFFF;
        if (grayscale)
            return Math.Abs(Gray(actual) - Gray(expected)) <= tolerance;
        return Math.Abs((int)((actual >> 16) & 0xFF) - (int)((expected >> 16) & 0xFF)) <= tolerance
            && Math.Abs((int)((actual >> 8) & 0xFF) - (int)((expected >> 8) & 0xFF)) <= tolerance
            && Math.Abs((int)(actual & 0xFF) - (int)(expected & 0xFF)) <= tolerance;
    }

    private static int Gray(uint rgb)
    {
        var red = (int)((rgb >> 16) & 0xFF);
        var green = (int)((rgb >> 8) & 0xFF);
        var blue = (int)(rgb & 0xFF);
        return (red * 299 + green * 587 + blue * 114 + 500) / 1000;
    }

    private static bool OverlapsExisting(
        ImageMatchPoint candidate, int width, int height, IReadOnlyList<ImageMatchPoint> matches)
    {
        foreach (var match in matches)
        {
            if ((long)candidate.X < (long)match.X + width
                && (long)match.X < (long)candidate.X + width
                && (long)candidate.Y < (long)match.Y + height
                && (long)match.Y < (long)candidate.Y + height)
                return true;
        }
        return false;
    }

    private static double ScoreAt(
        PixelImage source, PixelImage template, int originX, int originY,
        int tolerance, int totalSamples, int requiredSamples, bool grayscale, ref long comparisons, CancellationToken cancellationToken)
    {
        var matched = 0;
        var examined = 0;
        for (var y = 0; y < template.Height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (var x = 0; x < template.Width; x++)
            {
                var expected = template.GetRawPixelUnchecked(x, y);
                if ((expected >> 24) == 0)
                    continue;

                if ((comparisons & 1023) == 0) cancellationToken.ThrowIfCancellationRequested();
                if (++comparisons > MaximumPixelComparisons)
                    throw new InvalidOperationException(
                        $"模板搜索超过 {MaximumPixelComparisons:N0} 次像素比较上限；请缩小区域/模板或增大 step。");

                examined++;
                if (ColorsMatch(source.GetPixelColor(originX + x, originY + y), expected & 0xFFFFFF, tolerance, grayscale))
                    matched++;

                if (matched + (totalSamples - examined) < requiredSamples)
                    return (double)matched / totalSamples;
            }
        }

        return (double)matched / totalSamples;
    }

    private static int CountOpaquePixels(PixelImage image, CancellationToken cancellationToken)
    {
        var count = 0;
        for (var y = 0; y < image.Height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (var x = 0; x < image.Width; x++)
                if ((image.GetRawPixelUnchecked(x, y) >> 24) != 0)
                    count++;
        }
        return count;
    }

    private static ImageMatchPoint CreatePoint(PixelImage image, int x, int y, double similarity = 1.0) =>
        new(x, y,
            image.HasScreenCoordinates ? checked(image.ScreenOriginX + x) : x,
            image.HasScreenCoordinates ? checked(image.ScreenOriginY + y) : y,
            image.HasScreenCoordinates,
            similarity);

    private static void ValidateTolerance(int tolerance)
    {
        if (tolerance is < 0 or > 255)
            throw new ArgumentOutOfRangeException(nameof(tolerance), "颜色容差必须在 0 到 255 之间。");
    }

    private static void ValidateStep(int step)
    {
        if (step is < 1 or > 64)
            throw new ArgumentOutOfRangeException(nameof(step), "step 必须在 1 到 64 之间。");
    }

    private static void ValidateSimilarity(double similarity)
    {
        if (!double.IsFinite(similarity) || similarity is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(similarity), "相似度阈值必须在 0 到 1 之间。");
    }

    private static void ValidateScale(double scale)
    {
        if (!double.IsFinite(scale) || scale is < 0.25 or > 4.0)
            throw new ArgumentOutOfRangeException(nameof(scale), "模板 scale 必须在 0.25 到 4 之间。");
    }
}
