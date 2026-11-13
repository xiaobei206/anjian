using PaddleOcrNet.Models;
using PaddleOcrNet.Services;

namespace Anjian.App.Services;

internal static class OcrModelPaths
{
    public static string CacheDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Anjian2026", "Ocr", "Models");

    public static PaddleOcrServiceOptions CreateServiceOptions(bool offline, string? modelCacheDirectory = null) => new()
    {
        ModelCachePath = string.IsNullOrWhiteSpace(modelCacheDirectory)
            ? CacheDirectory
            : Path.GetFullPath(modelCacheDirectory),
        MaxResidentRecognizers = 1,
        MaxImagePixels = 16_000_000,
        ExecutionProvider = OcrExecutionProvider.Cpu,
        Download = new ModelDownloadOptions
        {
            Offline = offline,
            MaxRetries = 3
        }
    };
}
