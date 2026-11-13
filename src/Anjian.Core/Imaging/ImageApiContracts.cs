namespace Anjian.Core.Imaging;

public sealed record ImageIdRequest(string ImageId);
public sealed record ImageWindowRequest(string WindowId);
public sealed record ImageCaptureRegionRequest(
    string WindowId, string Space, double X, double Y, double Width, double Height, string Anchor);
public sealed record ImageFileRequest(string Path);
public sealed record ImageSaveRequest(string ImageId, string Path);
public sealed record ImageCropRequest(string ImageId, double X, double Y, double Width, double Height);
public sealed record ImagePixelRequest(string ImageId, double X, double Y);
public sealed record ImageCompareColorRequest(string ImageId, double X, double Y, uint Color, int Tolerance);
public sealed record ImageColorSearchRequest(
    string ImageId, uint Color, int Tolerance, int Left, int Top, int Width, int Height, int Step);
public sealed record ImageMultiColorSearchRequest(
    string ImageId, uint Color, string Pattern, int Tolerance, int Left, int Top, int Width, int Height, int Step);
public sealed record ImageTemplateSearchRequest(
    string ImageId, string TemplateId, double Similarity, int Tolerance,
    int MaxResults, int Left, int Top, int Width, int Height, int Step);
public sealed record ImageWaitImageRequest(
    string TemplateId, double Similarity, int Tolerance, int TimeoutMilliseconds, int IntervalMilliseconds);
public sealed record ImageWaitColorRequest(
    uint Color, int Tolerance, int TimeoutMilliseconds, int IntervalMilliseconds);
public sealed record ImageSizeInfo(
    int Width, int Height, bool HasScreenCoordinates, int ScreenOriginX, int ScreenOriginY);
