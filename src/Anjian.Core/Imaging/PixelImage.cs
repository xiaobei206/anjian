namespace Anjian.Core.Imaging;

/// <summary>
/// Immutable packed ARGB pixels. Captures retain their physical-screen origin; file-backed
/// images have no meaningful screen origin. Fully transparent template pixels are wildcards.
/// </summary>
public sealed class PixelImage
{
    public const int MaximumDimension = 32768;
    public const int MaximumPixelCount = 32_000_000;
    private readonly uint[] _argb;

    public PixelImage(
        int width,
        int height,
        uint[] argbPixels,
        int screenOriginX = 0,
        int screenOriginY = 0,
        bool hasScreenCoordinates = false)
    {
        ArgumentNullException.ThrowIfNull(argbPixels);
        if (width is < 1 or > MaximumDimension || height is < 1 or > MaximumDimension)
            throw new ArgumentOutOfRangeException(nameof(width), "图像宽高必须在 1 到 32768 像素之间。");
        var pixelCount = checked((long)width * height);
        if (pixelCount > MaximumPixelCount)
            throw new ArgumentOutOfRangeException(nameof(width), $"单张图像最多 {MaximumPixelCount:N0} 像素。");
        if (argbPixels.LongLength != pixelCount)
            throw new ArgumentException("像素数组长度与图像宽高不一致。", nameof(argbPixels));

        Width = width;
        Height = height;
        ScreenOriginX = screenOriginX;
        ScreenOriginY = screenOriginY;
        HasScreenCoordinates = hasScreenCoordinates;
        _argb = (uint[])argbPixels.Clone();
    }

    public int Width { get; }
    public int Height { get; }
    public int ScreenOriginX { get; }
    public int ScreenOriginY { get; }
    public bool HasScreenCoordinates { get; }
    public long ByteLength => checked((long)_argb.Length * sizeof(uint));

    public uint GetPixelColor(int x, int y) => GetRawPixel(x, y) & 0x00FFFFFF;

    public uint GetRawPixel(int x, int y)
    {
        if ((uint)x >= (uint)Width || (uint)y >= (uint)Height)
            throw new ArgumentOutOfRangeException(nameof(x), "像素坐标超出图像边界。");
        return _argb[checked(y * Width + x)];
    }

    internal uint GetRawPixelUnchecked(int x, int y) => _argb[y * Width + x];

    public PixelImage Crop(int x, int y, int width, int height)
    {
        if (x < 0 || y < 0 || width < 1 || height < 1
            || (long)x + width > Width || (long)y + height > Height)
            throw new ArgumentOutOfRangeException(nameof(x), "裁剪区域必须完全位于原图内，且宽高必须大于 0。");

        var cropped = new uint[checked(width * height)];
        for (var row = 0; row < height; row++)
            Array.Copy(_argb, (y + row) * Width + x, cropped, row * width, width);

        return new PixelImage(
            width, height, cropped,
            HasScreenCoordinates ? checked(ScreenOriginX + x) : 0,
            HasScreenCoordinates ? checked(ScreenOriginY + y) : 0,
            HasScreenCoordinates);
    }

    public uint[] CopyArgbPixels() => (uint[])_argb.Clone();
}
