namespace Anjian.App.Models;

internal enum RecordedEventKind
{
    Move, MouseDown, MouseUp, Wheel, KeyDown, KeyUp
}

internal sealed record RecordedInputEvent(
    long Timestamp,
    RecordedEventKind Kind,
    int X = 0,
    int Y = 0,
    string? Name = null,
    int Delta = 0);
