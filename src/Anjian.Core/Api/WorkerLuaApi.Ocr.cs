using System.Diagnostics;
using System.Text.Json;
using Anjian.Core.Imaging;
using Anjian.Core.Ocr;
using NLua;

namespace Anjian.Core.Api;

public sealed partial class WorkerLuaApi
{
    public LuaTable OCR(string imageId, string language, double minConfidence)
    {
        var recognized = RecognizeImage(imageId, language, minConfidence);
        return ToLuaResult(recognized.Result, recognized.Size);
    }

    public LuaTable? FindText(
        string imageId, string text, string language, double minConfidence, string matchMode)
    {
        ValidateText(text);
        var recognized = RecognizeImage(imageId, language, minConfidence);
        var line = FindMatchingLine(recognized.Result, text, minConfidence, matchMode);
        return line is null ? null : ToLuaLine(line, recognized.Size, 1);
    }

    public LuaTable? WaitText(
        string text, double timeoutMs, double intervalMs, string language, double minConfidence, string matchMode)
    {
        ValidateText(text);
        var timeout = Integer(timeoutMs, 0, 600000, "OCR 超时时间");
        var interval = Integer(intervalMs, 10, 5000, "OCR 轮询间隔");
        ValidateConfidence(minConfidence);
        ValidateMatchMode(matchMode);
        var timer = Stopwatch.StartNew();
        var first = true;

        while (first || timer.ElapsedMilliseconds < timeout)
        {
            first = false;
            _ = pipe.Request("checkpoint");
            var imageId = pipe.Request("image-capture-screen");
            if (string.IsNullOrWhiteSpace(imageId))
                throw new IOException("WaitText 无法获得桌面截图资源。");

            try
            {
                var recognized = RecognizeImage(imageId, language, minConfidence);
                var line = FindMatchingLine(recognized.Result, text, minConfidence, matchMode);
                if (line is not null)
                    return ToLuaLine(line, recognized.Size, recognized.Result.Lines.IndexOf(line) + 1);
            }
            finally
            {
                try
                {
                    _ = pipe.Request("image-release",
                        text: JsonSerializer.Serialize(new ImageIdRequest(imageId), WindowApiJson.Options));
                }
                catch (IOException) { }
            }

            if (timer.ElapsedMilliseconds >= timeout)
                break;
            var remaining = timeout - (int)Math.Min(timeout, timer.ElapsedMilliseconds);
            Thread.Sleep(Math.Min(interval, remaining));
        }

        return null;
    }

    private (OcrRecognitionResult Result, ImageSizeInfo Size) RecognizeImage(
        string imageId, string language, double minConfidence)
    {
        _ = RequiredId(imageId);
        language = NormalizeOcrLanguage(language);
        ValidateConfidence(minConfidence);
        if (ocrService is null)
            throw new InvalidOperationException("OCR 引擎不可用；请重新启动按键精灵 2026。");

        var idRequest = new ImageIdRequest(imageId);
        var json = JsonSerializer.Serialize(idRequest, WindowApiJson.Options);
        var sizePayload = pipe.Request("image-size", text: json);
        var size = string.IsNullOrWhiteSpace(sizePayload)
            ? throw new InvalidDataException("OCR 无法读取图像尺寸。")
            : JsonSerializer.Deserialize<ImageSizeInfo>(sizePayload, WindowApiJson.Options)
                ?? throw new InvalidDataException("OCR 收到无效图像尺寸。");

        var encoded = pipe.Request("image-export-png", text: json);
        if (string.IsNullOrWhiteSpace(encoded))
            throw new InvalidDataException("OCR 无法读取图像 PNG 数据。");
        byte[] bytes;
        try { bytes = Convert.FromBase64String(encoded); }
        catch (FormatException ex) { throw new InvalidDataException("OCR 收到无效 PNG 编码。", ex); }
        if (bytes.Length is < 8 or > 64 * 1024 * 1024)
            throw new InvalidDataException("OCR PNG 数据为空或超过 64 MiB 限制。");

        var result = ocrService.Recognize(bytes, language, minConfidence);
        if (result.Width != size.Width || result.Height != size.Height)
            throw new InvalidDataException("OCR 引擎返回尺寸与图像资源不一致。");
        return (result, size);
    }

    private LuaTable ToLuaResult(OcrRecognitionResult result, ImageSizeInfo size)
    {
        var table = NewLuaTable();
        table["text"] = result.Text;
        table["lineCount"] = result.Lines.Count;
        table["width"] = result.Width;
        table["height"] = result.Height;
        table["durationMs"] = result.DurationMilliseconds;
        table["hasScreenCoordinates"] = size.HasScreenCoordinates;
        table["screenOriginX"] = size.ScreenOriginX;
        table["screenOriginY"] = size.ScreenOriginY;

        var languages = NewLuaTable();
        for (var i = 0; i < result.Languages.Count; i++)
            languages[i + 1] = result.Languages[i];
        table["languages"] = languages;

        var detected = NewLuaTable();
        for (var i = 0; i < result.DetectedLanguages.Count; i++)
            detected[i + 1] = result.DetectedLanguages[i];
        table["detectedLanguages"] = detected;

        var lines = NewLuaTable();
        for (var i = 0; i < result.Lines.Count; i++)
            lines[i + 1] = ToLuaLine(result.Lines[i], size, i + 1);
        table["lines"] = lines;
        return table;
    }

    private LuaTable ToLuaLine(OcrTextLine line, ImageSizeInfo size, int index)
    {
        var table = NewLuaTable();
        table["index"] = index;
        table["text"] = line.Text;
        table["confidence"] = line.Confidence;
        table["x"] = line.Left;
        table["y"] = line.Top;
        table["width"] = line.Width;
        table["height"] = line.Height;
        if (size.HasScreenCoordinates)
        {
            table["screenX"] = size.ScreenOriginX + line.Left;
            table["screenY"] = size.ScreenOriginY + line.Top;
        }

        var polygon = NewLuaTable();
        for (var i = 0; i < line.Polygon.Count; i++)
        {
            var point = NewLuaTable();
            point["x"] = line.Polygon[i].X;
            point["y"] = line.Polygon[i].Y;
            polygon[i + 1] = point;
        }
        table["polygon"] = polygon;
        return table;
    }

    private static OcrTextLine? FindMatchingLine(
        OcrRecognitionResult result, string searchText, double minConfidence, string matchMode)
    {
        ValidateConfidence(minConfidence);
        ValidateMatchMode(matchMode);
        foreach (var line in result.Lines)
        {
            if (line.Confidence < minConfidence) continue;
            var matched = matchMode.Equals("exact", StringComparison.OrdinalIgnoreCase)
                ? line.Text.Equals(searchText, StringComparison.OrdinalIgnoreCase)
                : line.Text.Contains(searchText, StringComparison.OrdinalIgnoreCase);
            if (matched) return line;
        }
        return null;
    }

    private static void ValidateMatchMode(string matchMode)
    {
        if (string.IsNullOrWhiteSpace(matchMode)
            || !(matchMode.Equals("contains", StringComparison.OrdinalIgnoreCase)
                || matchMode.Equals("exact", StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("matchMode 只能是 contains 或 exact。", nameof(matchMode));
    }

    private static string NormalizeOcrLanguage(string language)
    {
        if (string.IsNullOrWhiteSpace(language))
            throw new ArgumentException("OCR 语言代码不能为空。", nameof(language));
        var code = language.Trim().ToLowerInvariant() switch
        {
            "zh" or "zh-cn" or "zh-hans" or "simplified-chinese" => "ch",
            "ja" or "jp" => "japan",
            var value => value
        };
        if (code is not ("ch" or "en" or "japan"))
            throw new ArgumentException("当前版本支持 OCR 语言代码 ch（简体中文）、en（英语）、japan（日本语）。", nameof(language));
        return code;
    }

    private static void ValidateText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("OCR 待查找文本不能为空。", nameof(text));
        if (text.Length > 4096)
            throw new ArgumentOutOfRangeException(nameof(text), "OCR 待查找文本最多 4096 个字符。");
    }

    private static void ValidateConfidence(double value)
    {
        if (!double.IsFinite(value) || value is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(value), "OCR 置信度阈值必须在 0 到 1 之间。");
    }
}
