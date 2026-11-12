namespace Anjian.Core.Recorder;

/// <summary>
/// Converts ordered low-level input events into editable actions.
/// Only adjacent matching Down/Up events are collapsed, so drags and overlapping
/// key/button sequences retain their original ordering and are never silently lost.
/// </summary>
public static class RecorderDocumentBuilder
{
    public const long MaximumClickHoldMilliseconds = 600_000;

    public static RecorderDocument Create(
        IEnumerable<RecorderEventInput> sourceEvents,
        bool clickOnly,
        long timestampFrequency,
        string? scriptId = null,
        string? recordingId = null,
        DateTimeOffset? createdAtUtc = null,
        long droppedEventCount = 0)
    {
        ArgumentNullException.ThrowIfNull(sourceEvents);
        if (droppedEventCount < 0)
            throw new ArgumentOutOfRangeException(nameof(droppedEventCount));
        if (timestampFrequency <= 0)
            throw new ArgumentOutOfRangeException(nameof(timestampFrequency));

        var ordered = sourceEvents
            .Select((item, index) => (item, index))
            .OrderBy(pair => pair.item.TimestampTicks)
            .ThenBy(pair => pair.index)
            .Select(pair => pair.item)
            .ToArray();

        if (ordered.Length > 250_000)
            throw new ArgumentOutOfRangeException(nameof(sourceEvents), "录制事件超过 250000 条上限。");

        var events = ordered.Select((item, index) => new RecorderRawEvent(
            EventId: $"event-{index + 1:D6}",
            TimestampTicks: item.TimestampTicks,
            Kind: item.Kind,
            X: item.X,
            Y: item.Y,
            Name: item.Name,
            Delta: item.Delta,
            Source: item.Source,
            Flags: item.Flags)).ToList();

        var actions = new List<RecorderAction>(events.Count);
        long previousActionEnd = 0;
        var hasPreviousAction = false;

        for (var index = 0; index < events.Count;)
        {
            var current = events[index];
            var consumed = 1;
            var actionKind = MapKind(current.Kind);
            var holdMs = 0L;
            var endTimestamp = current.TimestampTicks;
            string[] rawEventIds = [current.EventId];

            if (index + 1 < events.Count && IsMatchingPair(current, events[index + 1]))
            {
                var release = events[index + 1];
                var candidateHold = ToMilliseconds(release.TimestampTicks - current.TimestampTicks, timestampFrequency);
                if (candidateHold <= MaximumClickHoldMilliseconds)
                {
                    actionKind = current.Kind == RecorderRawEventKind.MouseDown
                        ? RecorderActionKind.MouseClick
                        : RecorderActionKind.KeyPress;
                    holdMs = candidateHold;
                    endTimestamp = release.TimestampTicks;
                    consumed = 2;
                    rawEventIds = [current.EventId, release.EventId];
                }
            }

            var delayMs = hasPreviousAction
                ? ToMilliseconds(Math.Max(0, current.TimestampTicks - previousActionEnd), timestampFrequency)
                : 0;

            actions.Add(new RecorderAction(
                ActionId: $"action-{actions.Count + 1:D6}",
                Kind: actionKind,
                X: current.X ?? 0,
                Y: current.Y ?? 0,
                Name: current.Name,
                Delta: current.Delta,
                HoldMs: holdMs,
                DelayMs: delayMs,
                RawEventIds: rawEventIds,
                StartTimestampTicks: current.TimestampTicks,
                EndTimestampTicks: endTimestamp,
                Source: current.Source));

            previousActionEnd = endTimestamp;
            hasPreviousAction = true;
            index += consumed;
        }

        var document = new RecorderDocument
        {
            ScriptId = string.IsNullOrWhiteSpace(scriptId) ? Guid.NewGuid().ToString("N") : scriptId,
            RecordingId = string.IsNullOrWhiteSpace(recordingId) ? Guid.NewGuid().ToString("N") : recordingId,
            CreatedAtUtc = createdAtUtc ?? DateTimeOffset.UtcNow,
            TimestampFrequency = timestampFrequency,
            ClickOnly = clickOnly,
            DroppedEventCount = droppedEventCount,
            Events = events,
            Actions = actions,
            GeneratedBlockId = Guid.NewGuid().ToString("N"),
            SynchronizationState = RecorderSynchronizationState.Pending
        };
        document.Validate();
        return document;
    }

    public static long ToMilliseconds(long ticks, long timestampFrequency)
    {
        if (timestampFrequency <= 0)
            throw new ArgumentOutOfRangeException(nameof(timestampFrequency));
        if (ticks <= 0)
            return 0;

        var milliseconds = (double)ticks * 1000d / timestampFrequency;
        if (double.IsNaN(milliseconds) || milliseconds <= 0)
            return 0;
        if (milliseconds >= long.MaxValue)
            return long.MaxValue;
        return (long)Math.Round(milliseconds, MidpointRounding.AwayFromZero);
    }

    private static bool IsMatchingPair(RecorderRawEvent down, RecorderRawEvent up)
    {
        var isMousePair = down.Kind == RecorderRawEventKind.MouseDown && up.Kind == RecorderRawEventKind.MouseUp;
        var isKeyPair = down.Kind == RecorderRawEventKind.KeyDown && up.Kind == RecorderRawEventKind.KeyUp;
        return (isMousePair || isKeyPair) &&
               !string.IsNullOrWhiteSpace(down.Name) &&
               string.Equals(down.Name, up.Name, StringComparison.OrdinalIgnoreCase);
    }

    private static RecorderActionKind MapKind(RecorderRawEventKind kind) => kind switch
    {
        RecorderRawEventKind.Move => RecorderActionKind.MouseMove,
        RecorderRawEventKind.MouseDown => RecorderActionKind.MouseDown,
        RecorderRawEventKind.MouseUp => RecorderActionKind.MouseUp,
        RecorderRawEventKind.Wheel => RecorderActionKind.MouseWheel,
        RecorderRawEventKind.KeyDown => RecorderActionKind.KeyDown,
        RecorderRawEventKind.KeyUp => RecorderActionKind.KeyUp,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "未知的原始录制事件类型。")
    };
}