namespace Anjian.Core.Ocr;

/// <summary>One point in an OCR text polygon, in the source image's local pixel coordinates.</summary>
public sealed record OcrTextPoint(double X, double Y);

/// <summary>A recognized text line and its image-local geometry.</summary>
public sealed record OcrTextLine(
    string Text,
    double Confidence,
    double Left,
    double Top,
    double Width,
    double Height,
    IReadOnlyList<OcrTextPoint> Polygon);

/// <summary>Engine-neutral OCR result returned from an isolated recognition adapter.</summary>
public sealed record OcrRecognitionResult(
    string Text,
    IReadOnlyList<OcrTextLine> Lines,
    IReadOnlyList<string> Languages,
    IReadOnlyList<string> DetectedLanguages,
    int Width,
    int Height,
    long DurationMilliseconds);

/// <summary>
/// OCR adapter boundary. The implementation belongs to the Windows App layer so model/runtime
/// dependencies never leak into Core, the Lua API contract, or tests.
/// </summary>
public interface IOcrRecognitionService : IDisposable
{
    OcrRecognitionResult Recognize(ReadOnlyMemory<byte> pngBytes, string language, double minConfidence);
}
