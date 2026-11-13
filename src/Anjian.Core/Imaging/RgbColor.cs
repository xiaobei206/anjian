using System.Globalization;

namespace Anjian.Core.Imaging;

public sealed record RgbColorDistance(int Red, int Green, int Blue, int MaxChannel, int Manhattan, double Euclidean);

public static class RgbColor
{
    public static uint Pack(int red, int green, int blue)
    {
        ValidateChannel(red, nameof(red));
        ValidateChannel(green, nameof(green));
        ValidateChannel(blue, nameof(blue));
        return (uint)(red << 16 | green << 8 | blue);
    }

    public static uint PackValue(double value)
    {
        if (!double.IsFinite(value) || value < 0 || value > 0xFFFFFF || value != Math.Truncate(value))
            throw new ArgumentOutOfRangeException(nameof(value), "RGB 颜色必须是 0 到 16777215 的整数（0xRRGGBB）。");
        return checked((uint)value);
    }

    public static string ToHex(int red, int green, int blue) => $"#{Pack(red, green, blue):X6}";

    public static (int Red, int Green, int Blue) ParseHex(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("十六进制颜色不能为空。", nameof(value));
        var text = value.Trim();
        if (text.StartsWith('#')) text = text[1..];
        else if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) text = text[2..];
        if (text.Length != 6 || !uint.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
            throw new FormatException("颜色格式必须是 RRGGBB、#RRGGBB 或 0xRRGGBB。");
        return ((int)(rgb >> 16), (int)((rgb >> 8) & 0xFF), (int)(rgb & 0xFF));
    }

    public static RgbColorDistance Difference(uint first, uint second)
    {
        var red = Math.Abs((int)((first >> 16) & 0xFF) - (int)((second >> 16) & 0xFF));
        var green = Math.Abs((int)((first >> 8) & 0xFF) - (int)((second >> 8) & 0xFF));
        var blue = Math.Abs((int)(first & 0xFF) - (int)(second & 0xFF));
        return new RgbColorDistance(red, green, blue, Math.Max(red, Math.Max(green, blue)),
            red + green + blue, Math.Sqrt(red * red + green * green + blue * blue));
    }

    private static void ValidateChannel(int value, string name)
    {
        if (value is < 0 or > 255)
            throw new ArgumentOutOfRangeException(name, "RGB 单通道必须在 0 到 255 之间。");
    }
}
