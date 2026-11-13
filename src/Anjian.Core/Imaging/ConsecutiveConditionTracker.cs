namespace Anjian.Core.Imaging;

/// <summary>Tracks consecutive true observations; a false observation always resets the streak.</summary>
public sealed class ConsecutiveConditionTracker
{
    private readonly int _requiredCount;

    public ConsecutiveConditionTracker(int requiredCount)
    {
        if (requiredCount is < 1 or > 20)
            throw new ArgumentOutOfRangeException(nameof(requiredCount), "连续确认次数必须在 1 到 20 之间。");
        _requiredCount = requiredCount;
    }

    public int ConsecutiveCount { get; private set; }

    public bool Observe(bool conditionMet)
    {
        ConsecutiveCount = conditionMet ? Math.Min(ConsecutiveCount + 1, _requiredCount) : 0;
        return ConsecutiveCount >= _requiredCount;
    }
}
