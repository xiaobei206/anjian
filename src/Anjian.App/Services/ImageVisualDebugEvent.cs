namespace Anjian.App.Services;

internal sealed record ImageVisualDebugEvent(
    string Kind, string Label, int X, int Y, int Width, int Height,
    uint? Color, double? Similarity, long DurationMilliseconds, bool HasScreenCoordinates);
