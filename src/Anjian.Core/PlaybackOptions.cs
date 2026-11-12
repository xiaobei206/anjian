namespace Anjian.Core;

public sealed record PlaybackOptions
{
    public const int MaximumRepeatCount = 100_000;
    public const int MaximumIntervalMilliseconds = 600_000;
    public int RepeatCount { get; init; } = 1;
    public int IntervalMilliseconds { get; init; } = 2_000;
    public bool IsInfinite => RepeatCount == 0;

    public bool HasNextIteration(int completedIterations)
    {
        Validate();
        if (completedIterations < 1)
            throw new ArgumentOutOfRangeException(nameof(completedIterations));
        return IsInfinite || completedIterations < RepeatCount;
    }

    public void Validate()
    {
        if (RepeatCount < 0 || RepeatCount > MaximumRepeatCount)
            throw new ArgumentOutOfRangeException(nameof(RepeatCount), $"次数必须在 0 到 {MaximumRepeatCount} 之间，0 表示无限循环。");
        if (IntervalMilliseconds < 0 || IntervalMilliseconds > MaximumIntervalMilliseconds)
            throw new ArgumentOutOfRangeException(nameof(IntervalMilliseconds), $"间隔必须在 0 到 {MaximumIntervalMilliseconds} 毫秒之间。");
    }
}
