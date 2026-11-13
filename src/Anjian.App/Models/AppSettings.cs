namespace Anjian.App.Models;

public sealed record AppSettings
{
    public int RepeatCount { get; init; } = 1;
    public int IntervalMilliseconds { get; init; } = 2_000;
    public int RuntimeTimeoutMinutes { get; init; }
    public bool ClickOnlyRecording { get; init; } = true;
    public bool AlwaysOnTop { get; init; }
}
