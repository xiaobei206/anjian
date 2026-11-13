using System.Globalization;
using System.Text.Json;
using Anjian.Core.Imaging;
using Anjian.Core.Windows;
using NLua;

namespace Anjian.Core.Api;

public sealed partial class WorkerLuaApi
{
    public string CaptureScreen() => ImageId(pipe.Request("image-capture-screen"), "CaptureScreen");

    public string CaptureWindow(string windowId) =>
        ImageId(pipe.Request("image-capture-window",
            text: JsonSerializer.Serialize(new ImageWindowRequest(RequiredId(windowId)), WindowApiJson.Options)), "CaptureWindow");

    public string CaptureClient(string windowId) =>
        ImageId(pipe.Request("image-capture-client",
            text: JsonSerializer.Serialize(new ImageWindowRequest(RequiredId(windowId)), WindowApiJson.Options)), "CaptureClient");

    public string CaptureRegion(
        string windowId, string space, double x, double y, double width, double height, string anchor)
    {
        var request = new ImageCaptureRegionRequest(windowId ?? string.Empty, RequiredText(space, "坐标空间"),
            Finite(x, "x"), Finite(y, "y"), PositiveFinite(width, "区域宽度"),
            PositiveFinite(height, "区域高度"), string.IsNullOrWhiteSpace(anchor) ? "top-left" : anchor);
        return ImageId(pipe.Request("image-capture-region",
            text: JsonSerializer.Serialize(request, WindowApiJson.Options)), "CaptureRegion");
    }

    public string LoadImage(string path) =>
        ImageId(pipe.Request("image-load",
            text: JsonSerializer.Serialize(new ImageFileRequest(RequiredText(path, "图像路径")), WindowApiJson.Options)), "LoadImage");

    public bool SaveScreenshot(string imageId, string path) =>
        ImageBoolean("image-save", new ImageSaveRequest(RequiredId(imageId), RequiredText(path, "PNG 保存路径")));

    public bool ReleaseImage(string imageId) =>
        ImageBoolean("image-release", new ImageIdRequest(RequiredId(imageId)));

    public LuaTable GetImageSize(string imageId) =>
        RequestTable("image-size", new ImageIdRequest(RequiredId(imageId)))
        ?? throw new InvalidDataException("图像尺寸查询没有返回结果。");

    public string CropImage(string imageId, double x, double y, double width, double height) =>
        ImageId(pipe.Request("image-crop", text: JsonSerializer.Serialize(
            new ImageCropRequest(RequiredId(imageId), x, y, width, height), WindowApiJson.Options)), "CropImage");

    public int GetPixelColor(string imageId, double x, double y) =>
        ImageInteger("image-pixel-color", new ImagePixelRequest(RequiredId(imageId), x, y));

    public bool CompareColor(string imageId, double x, double y, double color, double tolerance) =>
        ImageBoolean("image-compare-color", new ImageCompareColorRequest(
            RequiredId(imageId), Integer(x, 0, 32767, "像素 X"), Integer(y, 0, 32767, "像素 Y"),
            RgbColor(color), Integer(tolerance, 0, 255, "颜色容差")));

    public LuaTable? FindColor(string imageId, double color, double tolerance,
        double left, double top, double width, double height, double step) =>
        RequestTable("image-find-color", ColorRequest(imageId, color, tolerance, left, top, width, height, step));

    public LuaTable? FindMultiColor(string imageId, double color, string pattern, double tolerance,
        double left, double top, double width, double height, double step) =>
        RequestTable("image-find-multi-color", new ImageMultiColorSearchRequest(
            RequiredId(imageId), RgbColor(color), RequiredText(pattern, "多点颜色模式"),
            Integer(tolerance, 0, 255, "颜色容差"), RegionValue(left, "区域左边"), RegionValue(top, "区域上边"),
            RegionDimension(width, "区域宽度"), RegionDimension(height, "区域高度"), Integer(step, 1, 64, "step")));

    public int CountColors(string imageId, double color, double tolerance,
        double left, double top, double width, double height, double step) =>
        ImageInteger("image-count-colors", ColorRequest(imageId, color, tolerance, left, top, width, height, step));

    public LuaTable? FindImage(string imageId, string templateId, double similarity, double tolerance,
        double left, double top, double width, double height, double step) =>
        RequestTable("image-find-image", TemplateRequest(imageId, templateId, similarity, tolerance,
            1, left, top, width, height, step));

    public LuaTable FindImages(string imageId, string templateId, double similarity, double tolerance,
        double maxResults, double left, double top, double width, double height, double step) =>
        RequestTable("image-find-images", TemplateRequest(imageId, templateId, similarity, tolerance,
            Integer(maxResults, 1, ImageSearchEngine.MaximumResults, "maxResults"),
            left, top, width, height, step))
        ?? throw new InvalidDataException("FindImages 没有返回结果表。");

    public LuaTable? WaitImage(string templateId, double similarity, double tolerance,
        double timeoutMilliseconds, double intervalMilliseconds) =>
        RequestTable("image-wait-image", WaitImageRequest(templateId, similarity, tolerance, timeoutMilliseconds, intervalMilliseconds));

    public bool WaitImageGone(string templateId, double similarity, double tolerance,
        double timeoutMilliseconds, double intervalMilliseconds) =>
        ImageBoolean("image-wait-image-gone",
            WaitImageRequest(templateId, similarity, tolerance, timeoutMilliseconds, intervalMilliseconds));

    public LuaTable? WaitColor(double color, double tolerance, double timeoutMilliseconds, double intervalMilliseconds) =>
        RequestTable("image-wait-color", WaitColorRequest(color, tolerance, timeoutMilliseconds, intervalMilliseconds));

    public bool WaitColorGone(double color, double tolerance, double timeoutMilliseconds, double intervalMilliseconds) =>
        ImageBoolean("image-wait-color-gone", WaitColorRequest(color, tolerance, timeoutMilliseconds, intervalMilliseconds));

    private ImageColorSearchRequest ColorRequest(
        string imageId, double color, double tolerance, double left, double top, double width, double height, double step) =>
        new(RequiredId(imageId), RgbColor(color), Integer(tolerance, 0, 255, "颜色容差"),
            RegionValue(left, "区域左边"), RegionValue(top, "区域上边"),
            RegionDimension(width, "区域宽度"), RegionDimension(height, "区域高度"),
            Integer(step, 1, 64, "step"));

    private ImageTemplateSearchRequest TemplateRequest(
        string imageId, string templateId, double similarity, double tolerance, int maxResults,
        double left, double top, double width, double height, double step)
    {
        if (!double.IsFinite(similarity) || similarity is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(similarity), "相似度阈值必须在 0 到 1 之间。");
        return new ImageTemplateSearchRequest(RequiredId(imageId), RequiredId(templateId), similarity,
            Integer(tolerance, 0, 255, "颜色容差"), maxResults, RegionValue(left, "区域左边"),
            RegionValue(top, "区域上边"), RegionDimension(width, "区域宽度"),
            RegionDimension(height, "区域高度"), Integer(step, 1, 64, "step"));
    }

    private ImageWaitImageRequest WaitImageRequest(string templateId, double similarity, double tolerance,
        double timeoutMilliseconds, double intervalMilliseconds)
    {
        if (!double.IsFinite(similarity) || similarity is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(similarity), "相似度阈值必须在 0 到 1 之间。");
        return new ImageWaitImageRequest(RequiredId(templateId), similarity,
            Integer(tolerance, 0, 255, "颜色容差"), Integer(timeoutMilliseconds, 0, 600000, "超时时间"),
            Integer(intervalMilliseconds, 10, 5000, "轮询间隔"));
    }

    private ImageWaitColorRequest WaitColorRequest(double color, double tolerance,
        double timeoutMilliseconds, double intervalMilliseconds) =>
        new(RgbColor(color), Integer(tolerance, 0, 255, "颜色容差"),
            Integer(timeoutMilliseconds, 0, 600000, "超时时间"),
            Integer(intervalMilliseconds, 10, 5000, "轮询间隔"));

    private bool ImageBoolean(string type, object payload) => RequestBoolean(type, payload);

    private int ImageInteger(string type, object payload)
    {
        var response = pipe.Request(type, text: JsonSerializer.Serialize(payload, WindowApiJson.Options));
        return int.TryParse(response, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
            ? result
            : throw new InvalidDataException($"图像 API {type} 返回了无效整数。");
    }

    private static string ImageId(string? value, string api) =>
        string.IsNullOrWhiteSpace(value) ? throw new IOException($"{api} 没有返回有效图像 ID。") : value;

    private static uint RgbColor(double value) =>
        checked((uint)Integer(value, 0, 0xFFFFFF, "RGB 颜色（0xRRGGBB）"));

    private static int RegionValue(double value, string label) => Integer(value, 0, 32767, label);
    private static int RegionDimension(double value, string label) => Integer(value, 0, 32768, label);

    private static double Finite(double value, string label) =>
        double.IsFinite(value) ? value : throw new ArgumentOutOfRangeException(label, "坐标必须是有限数。");

    private static double PositiveFinite(double value, string label) =>
        double.IsFinite(value) && value > 0
            ? value
            : throw new ArgumentOutOfRangeException(label, "区域宽高必须是大于 0 的有限数。");
}
