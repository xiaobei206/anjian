using Anjian.Core.Ocr;
using PaddleOcrNet.Extraction;
using PaddleOcrNet.Models;
using PaddleOcrNet.Services;

namespace Anjian.App.Services;

/// <summary>
/// Windows App adapter around PaddleOcrNet. Model inference always runs in the isolated Lua worker.
/// The worker uses strict offline mode; only the explicit model manager downloads model files.
/// </summary>
internal sealed class PaddleOcrRecognitionService : IOcrRecognitionService
{
    private readonly object _sync = new();
    private readonly PaddleOcrServiceOptions _options;
    private PaddleOcrService? _engine;
    private bool _disposed;

    public PaddleOcrRecognitionService(string? modelCacheDirectory = null)
    {
        _options = OcrModelPaths.CreateServiceOptions(offline: true, modelCacheDirectory);
    }

    public OcrRecognitionResult Recognize(ReadOnlyMemory<byte> pngBytes, string language, double minConfidence)
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (pngBytes.IsEmpty)
                throw new ArgumentException("OCR 图像数据不能为空。", nameof(pngBytes));
            if (!double.IsFinite(minConfidence) || minConfidence is < 0 or > 1)
                throw new ArgumentOutOfRangeException(nameof(minConfidence));

            var selectedLanguage = OcrLanguageExtensions.FromCode(language);
            EnsureModelsAvailable(selectedLanguage);
            _engine ??= new PaddleOcrService(_options);

            var options = new RecognitionOptions
            {
                DropScore = minConfidence,
                UseTextLineOrientation = false,
                UseDocOrientation = false,
                UseDocUnwarp = false,
                MaxDegreeOfParallelism = Math.Clamp(Environment.ProcessorCount, 1, 4)
            };
            var result = _engine.ExtractTextFromImage(pngBytes, selectedLanguage, options)
                .GetAwaiter().GetResult();

            var lines = result.Lines.Select(line => new OcrTextLine(
                line.Text,
                line.Confidence,
                line.BoundingBox.MinX,
                line.BoundingBox.MinY,
                line.BoundingBox.Width,
                line.BoundingBox.Height,
                line.BoundingPolygon.Select(point => new OcrTextPoint(point.X, point.Y)).ToArray()
            )).ToArray();

            return new OcrRecognitionResult(
                result.FullText,
                lines,
                result.Languages.ToArray(),
                result.DetectedLanguages.ToArray(),
                result.SourceWidth,
                result.SourceHeight,
                (long)Math.Round(result.Duration.TotalMilliseconds));
        }
    }

    private void EnsureModelsAvailable(OcrLanguage language)
    {
        var modelFiles = PaddleOcrModels.GetModelFileNames(
            new[] { language }, PaddleModelSet.Ocr, _options);
        var cache = PaddleOcrModels.GetCacheDirectory(_options);
        var missing = modelFiles.Where(file => !File.Exists(Path.Combine(cache, file))).ToArray();
        if (missing.Length > 0)
        {
            throw new InvalidOperationException(
                $"OCR 模型尚未安装：{string.Join("、", missing.Take(4))}" +
                (missing.Length > 4 ? $" 等 {missing.Length} 个文件" : string.Empty) +
                $"。请先点击主界面“OCR 模型”按钮，显式下载 {language} 模型。识别期间不会自动联网。");
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed) return;
            _disposed = true;
            _engine?.Dispose();
            _engine = null;
        }
    }
}
