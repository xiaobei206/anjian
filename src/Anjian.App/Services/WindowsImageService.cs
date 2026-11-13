using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Anjian.Core.Imaging;
using Anjian.Core.Windows;

namespace Anjian.App.Services;

/// <summary>
/// Owns per-run image resources. Captures use the visible, composed desktop via CopyFromScreen;
/// occluded windows therefore contain whatever is actually visible above them.
/// </summary>
internal sealed class WindowsImageService(WindowsWindowManager windows)
{
    private const long MaximumStoredBytes = 256L * 1024 * 1024;
    private const int MaximumResources = 128;
    private readonly object _sync = new();
    private readonly Dictionary<string, PixelImage> _resources = new(StringComparer.Ordinal);
    private long _storedBytes;

    public string CaptureScreen() => Add(CaptureScreenPixels());

    public PixelImage CaptureScreenPixels()
    {
        var rectangle = SystemInformation.VirtualScreen;
        if (rectangle.Width < 1 || rectangle.Height < 1)
            throw new InvalidOperationException("Windows 没有报告有效的虚拟桌面区域。");
        return CaptureRectangle(new PixelRect(rectangle.Left, rectangle.Top,
            checked(rectangle.Left + rectangle.Width), checked(rectangle.Top + rectangle.Height)));
    }

    public string CaptureWindow(string windowId)
    {
        var info = windows.GetInfo(RequiredId(windowId));
        EnsureCapturable(info);
        return Add(CaptureRectangle(info.WindowRect));
    }

    public string CaptureClient(string windowId)
    {
        var info = windows.GetInfo(RequiredId(windowId));
        EnsureCapturable(info);
        return Add(CaptureRectangle(info.ClientRect));
    }

    public string CaptureRegion(ImageCaptureRegionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var spaceText = RequiredText(request.Space, "坐标空间");
        if (!Enum.TryParse<CoordinateSpace>(spaceText, ignoreCase: true, out var space))
            throw new ArgumentException("坐标空间必须是 screen、window、client、anchor 或 ratio。", nameof(request));

        int left, top, right, bottom;
        if (string.IsNullOrWhiteSpace(request.WindowId))
        {
            if (space != CoordinateSpace.Screen)
                throw new ArgumentException("未提供 windowId 时，CaptureRegion 只能使用 screen 坐标。", nameof(request));
            left = Integer(request.X, int.MinValue, int.MaxValue, "区域 X");
            top = Integer(request.Y, int.MinValue, int.MaxValue, "区域 Y");
            var width = Integer(request.Width, 1, PixelImage.MaximumDimension, "区域宽度");
            var height = Integer(request.Height, 1, PixelImage.MaximumDimension, "区域高度");
            right = checked(left + width);
            bottom = checked(top + height);
        }
        else
        {
            var id = RequiredId(request.WindowId);
            var info = windows.GetInfo(id);
            EnsureCapturable(info);
            var x = Finite(request.X, "x");
            var y = Finite(request.Y, "y");
            var width = PositiveFinite(request.Width, "区域宽度");
            var height = PositiveFinite(request.Height, "区域高度");

            var first = windows.ConvertCoordinate(
                new WindowCoordinateRequest(id, spaceText, x, y, request.Anchor), toScreen: true);
            var second = windows.ConvertCoordinate(
                new WindowCoordinateRequest(id, spaceText, x + width, y + height, request.Anchor), toScreen: true);
            var topLeft = CoordinateConverter.ToPixel(first);
            var bottomRight = CoordinateConverter.ToPixel(second);
            left = topLeft.X;
            top = topLeft.Y;
            right = bottomRight.X;
            bottom = bottomRight.Y;
            if (right <= left || bottom <= top)
                throw new ArgumentOutOfRangeException(nameof(request), "转换后的截图区域必须至少覆盖一个物理像素。");
        }

        return Add(CaptureRectangle(new PixelRect(left, top, right, bottom)));
    }

    public string LoadImage(string path)
    {
        var fullPath = Path.GetFullPath(RequiredText(path, "图像路径"));
        using var loaded = Image.FromFile(fullPath, useEmbeddedColorManagement: false);
        ValidateDimensions(loaded.Width, loaded.Height);
        using var normalized = new Bitmap(loaded.Width, loaded.Height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(normalized))
        {
            graphics.Clear(Color.Transparent);
            graphics.DrawImage(loaded, new Rectangle(0, 0, loaded.Width, loaded.Height));
        }
        return Add(ReadBitmap(normalized, 0, 0, hasScreenCoordinates: false));
    }

    public bool SaveScreenshot(ImageSaveRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var image = Get(request.ImageId);
        var fullPath = Path.GetFullPath(RequiredText(request.Path, "PNG 保存路径"));
        if (!string.Equals(Path.GetExtension(fullPath), ".png", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("SaveScreenshot 只写 PNG 文件，请使用 .png 扩展名。", nameof(request));

        using var bitmap = new Bitmap(image.Width, image.Height, PixelFormat.Format32bppArgb);
        WriteBitmap(image, bitmap);
        bitmap.Save(fullPath, ImageFormat.Png);
        return true;
    }

    public string CropImage(ImageCropRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var x = Integer(request.X, 0, PixelImage.MaximumDimension - 1, "裁剪 X");
        var y = Integer(request.Y, 0, PixelImage.MaximumDimension - 1, "裁剪 Y");
        var width = Integer(request.Width, 1, PixelImage.MaximumDimension, "裁剪宽度");
        var height = Integer(request.Height, 1, PixelImage.MaximumDimension, "裁剪高度");
        return Add(Get(request.ImageId).Crop(x, y, width, height));
    }

    public bool ReleaseImage(string imageId)
    {
        lock (_sync)
        {
            if (!_resources.Remove(RequiredId(imageId), out var image))
                return false;
            _storedBytes -= image.ByteLength;
            return true;
        }
    }

    public void Clear()
    {
        lock (_sync)
        {
            _resources.Clear();
            _storedBytes = 0;
        }
    }

    public PixelImage Get(string imageId)
    {
        lock (_sync)
            return _resources.TryGetValue(RequiredId(imageId), out var image)
                ? image
                : throw new InvalidOperationException("图像资源 ID 不存在或已释放；请重新捕获/加载。");
    }

    public ImageSizeInfo GetSize(string imageId)
    {
        var image = Get(imageId);
        return new ImageSizeInfo(image.Width, image.Height, image.HasScreenCoordinates,
            image.ScreenOriginX, image.ScreenOriginY);
    }

    public uint GetPixelColor(ImagePixelRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Get(request.ImageId).GetPixelColor(
            Integer(request.X, 0, PixelImage.MaximumDimension - 1, "像素 X"),
            Integer(request.Y, 0, PixelImage.MaximumDimension - 1, "像素 Y"));
    }

    public bool CompareColor(ImageCompareColorRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ImageSearchEngine.CompareColor(Get(request.ImageId),
            Integer(request.X, 0, PixelImage.MaximumDimension - 1, "像素 X"),
            Integer(request.Y, 0, PixelImage.MaximumDimension - 1, "像素 Y"),
            request.Color, request.Tolerance);
    }

    public ImageMatchPoint? FindColor(ImageColorSearchRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ImageSearchEngine.FindColor(Get(request.ImageId), request.Color, request.Tolerance,
            request.Left, request.Top, request.Width, request.Height, request.Step, cancellationToken);
    }

    public ImageMatchPoint? FindMultiColor(ImageMultiColorSearchRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var probes = ImageSearchEngine.ParseColorPattern(request.Pattern);
        return ImageSearchEngine.FindMultiColor(Get(request.ImageId), request.Color, probes, request.Tolerance,
            request.Left, request.Top, request.Width, request.Height, request.Step, cancellationToken);
    }

    public int CountColors(ImageColorSearchRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ImageSearchEngine.CountColors(Get(request.ImageId), request.Color, request.Tolerance,
            request.Left, request.Top, request.Width, request.Height, request.Step, cancellationToken);
    }

    public ImageMatchPoint? FindImage(ImageTemplateSearchRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ImageSearchEngine.FindImage(Get(request.ImageId), Get(request.TemplateId),
            request.Similarity, request.Tolerance, request.Left, request.Top, request.Width, request.Height, request.Step, cancellationToken);
    }

    public IReadOnlyList<ImageMatchPoint> FindImages(ImageTemplateSearchRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ImageSearchEngine.FindImages(Get(request.ImageId), Get(request.TemplateId),
            request.Similarity, request.Tolerance, request.MaxResults, request.Left, request.Top,
            request.Width, request.Height, request.Step, cancellationToken);
    }

    private string Add(PixelImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        lock (_sync)
        {
            if (_resources.Count >= MaximumResources)
                throw new InvalidOperationException($"每轮脚本最多保留 {MaximumResources} 个图像资源；请先 ReleaseImage。");
            if (image.ByteLength > MaximumStoredBytes - _storedBytes)
                throw new InvalidOperationException("图像资源池超过 256 MiB；请裁剪图像或释放不再需要的资源。");
            var id = Guid.NewGuid().ToString("N");
            _resources.Add(id, image);
            _storedBytes += image.ByteLength;
            return id;
        }
    }

    private static PixelImage CaptureRectangle(PixelRect rect)
    {
        if (!rect.HasArea)
            throw new ArgumentOutOfRangeException(nameof(rect), "截图区域宽高必须大于 0。");
        var width = rect.Width;
        var height = rect.Height;
        ValidateDimensions(width, height);

        using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
            graphics.CopyFromScreen(rect.Left, rect.Top, 0, 0, new Size(width, height), CopyPixelOperation.SourceCopy);
        return ReadBitmap(bitmap, rect.Left, rect.Top, hasScreenCoordinates: true);
    }

    private static PixelImage ReadBitmap(Bitmap bitmap, int originX, int originY, bool hasScreenCoordinates)
    {
        ValidateDimensions(bitmap.Width, bitmap.Height);
        var pixels = new uint[checked(bitmap.Width * bitmap.Height)];
        var bounds = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        var data = bitmap.LockBits(bounds, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var row = new byte[checked(bitmap.Width * 4)];
            for (var y = 0; y < bitmap.Height; y++)
            {
                Marshal.Copy(IntPtr.Add(data.Scan0, checked(y * data.Stride)), row, 0, row.Length);
                for (var x = 0; x < bitmap.Width; x++)
                {
                    var i = x * 4;
                    pixels[y * bitmap.Width + x] =
                        (uint)(row[i + 3] << 24 | row[i + 2] << 16 | row[i + 1] << 8 | row[i]);
                }
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
        return new PixelImage(bitmap.Width, bitmap.Height, pixels, originX, originY, hasScreenCoordinates);
    }

    private static void WriteBitmap(PixelImage image, Bitmap bitmap)
    {
        var bounds = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        var data = bitmap.LockBits(bounds, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
            var source = image.CopyArgbPixels();
            var row = new byte[checked(bitmap.Width * 4)];
            for (var y = 0; y < bitmap.Height; y++)
            {
                for (var x = 0; x < bitmap.Width; x++)
                {
                    var argb = source[y * bitmap.Width + x];
                    var i = x * 4;
                    row[i] = (byte)argb;
                    row[i + 1] = (byte)(argb >> 8);
                    row[i + 2] = (byte)(argb >> 16);
                    row[i + 3] = (byte)(argb >> 24);
                }
                Marshal.Copy(row, 0, IntPtr.Add(data.Scan0, checked(y * data.Stride)), row.Length);
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }

    private static void EnsureCapturable(WindowInfo info)
    {
        if (!info.Visible || info.Minimized)
            throw new InvalidOperationException("截图目标窗口必须可见且未最小化。后台/遮挡窗口内容不保证可获取。");
        if (!info.WindowRect.HasArea)
            throw new InvalidOperationException("目标窗口当前没有有效外框区域。");
    }

    private static void ValidateDimensions(int width, int height)
    {
        if (width is < 1 or > PixelImage.MaximumDimension || height is < 1 or > PixelImage.MaximumDimension)
            throw new ArgumentOutOfRangeException(nameof(width), "截图宽高必须在 1 到 32768 像素之间。");
        if ((long)width * height > PixelImage.MaximumPixelCount)
            throw new ArgumentOutOfRangeException(nameof(width), $"单张图像最多 {PixelImage.MaximumPixelCount:N0} 像素。");
    }

    private static int Integer(double value, int min, int max, string label)
    {
        if (!double.IsFinite(value) || value < min || value > max || value != Math.Truncate(value))
            throw new ArgumentOutOfRangeException(label, $"{label}必须是 {min} 到 {max} 之间的整数。");
        return checked((int)value);
    }

    private static double Finite(double value, string label) =>
        double.IsFinite(value) ? value : throw new ArgumentOutOfRangeException(label, "坐标必须是有限数。");

    private static double PositiveFinite(double value, string label) =>
        double.IsFinite(value) && value > 0
            ? value
            : throw new ArgumentOutOfRangeException(label, "区域宽高必须是大于 0 的有限数。");

    private static string RequiredId(string value) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("图像 ID 不能为空。", nameof(value)) : value.Trim();

    private static string RequiredText(string value, string label) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException($"{label}不能为空。", nameof(value)) : value.Trim();
}
