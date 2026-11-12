namespace Anjian.Core.Recorder;

public enum RecorderRawEventKind
{
    Move,
    MouseDown,
    MouseUp,
    Wheel,
    KeyDown,
    KeyUp
}

public enum RecorderActionKind
{
    MouseMove,
    MouseClick,
    MouseDown,
    MouseUp,
    MouseWheel,
    KeyPress,
    KeyDown,
    KeyUp,
    TypeText
}

public enum RecorderSynchronizationState
{
    Synchronized,
    Pending,
    Detached,
    Missing
}

public sealed record RecorderEventInput(
    long TimestampTicks,
    RecorderRawEventKind Kind,
    int? X = null,
    int? Y = null,
    string? Name = null,
    int Delta = 0,
    string Source = "LowLevelHook",
    uint Flags = 0);

public sealed record RecorderRawEvent(
    string EventId,
    long TimestampTicks,
    RecorderRawEventKind Kind,
    int? X,
    int? Y,
    string? Name,
    int Delta,
    string Source,
    uint Flags);

public sealed record RecorderAction(
    string ActionId,
    RecorderActionKind Kind,
    int X = 0,
    int Y = 0,
    string? Name = null,
    int Delta = 0,
    long HoldMs = 0,
    long DelayMs = 0,
    string? Text = null,
    bool Enabled = true,
    string? Group = null,
    string? Note = null,
    string[]? RawEventIds = null,
    long StartTimestampTicks = 0,
    long EndTimestampTicks = 0,
    string? Source = null);

public sealed record RecorderDocument
{
    public const int CurrentSchemaVersion = 1;
    public const long MaximumActionDelayMilliseconds = 604_800_000;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public int RecorderVersion { get; init; } = 1;
    public string ScriptId { get; init; } = Guid.NewGuid().ToString("N");
    public string RecordingId { get; init; } = Guid.NewGuid().ToString("N");
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public string CoordinateSpace { get; init; } = "virtual-desktop-physical-pixels";
    public long TimestampFrequency { get; init; } = System.Diagnostics.Stopwatch.Frequency;
    public bool ClickOnly { get; init; }
    public long DroppedEventCount { get; init; }
    public List<RecorderRawEvent> Events { get; init; } = [];
    public List<RecorderAction> Actions { get; init; } = [];
    public string GeneratedBlockId { get; init; } = Guid.NewGuid().ToString("N");
    public string? GeneratedBlockHash { get; init; }
    public RecorderSynchronizationState SynchronizationState { get; init; } = RecorderSynchronizationState.Synchronized;

    public void Validate()
    {
        if (SchemaVersion != CurrentSchemaVersion)
            throw new NotSupportedException($"不支持录制数据 schemaVersion={SchemaVersion}；当前支持版本为 {CurrentSchemaVersion}。原文件未修改。");
        if (!IsSafeId(ScriptId))
            throw new InvalidDataException("scriptId 缺失或包含不允许的字符。");
        if (!IsSafeId(RecordingId))
            throw new InvalidDataException("recordingId 缺失或包含不允许的字符。");
        if (!IsSafeId(GeneratedBlockId))
            throw new InvalidDataException("generatedBlockId 缺失或包含不允许的字符。");
        if (DroppedEventCount < 0)
            throw new InvalidDataException("录制文档的 DroppedEventCount 不能为负数。");
        if (TimestampFrequency <= 0)
            throw new InvalidDataException("录制文档的 TimestampFrequency 无效。");
        if (!string.Equals(CoordinateSpace, "virtual-desktop-physical-pixels", StringComparison.Ordinal))
            throw new NotSupportedException($"不支持坐标空间：{CoordinateSpace}");
        if (Events is null || Actions is null)
            throw new InvalidDataException("录制文档缺少 events 或 actions 数组。");
        if (Events.Count > 250_000)
            throw new InvalidDataException("录制文档原始事件超过 250000 条上限。");
        if (Actions.Count > 250_000)
            throw new InvalidDataException("录制文档动作超过 250000 条上限。");
        if (Events.Any(item => item is null || string.IsNullOrWhiteSpace(item.EventId) || !Enum.IsDefined(item.Kind)))
            throw new InvalidDataException("录制文档包含空原始事件或缺少 eventId。");
        if (Events.Select(item => item.EventId).Distinct(StringComparer.Ordinal).Count() != Events.Count)
            throw new InvalidDataException("录制文档包含重复 eventId。");
        if (Events.Any(item => !IsSafeId(item.EventId)))
            throw new InvalidDataException("原始事件 eventId 包含不允许的字符。");
        if (Actions.Any(action => action is null || !IsSafeId(action.ActionId)))
            throw new InvalidDataException("录制文档包含空动作或不安全的 actionId。");
        if (Actions.Select(action => action.ActionId).Distinct(StringComparer.Ordinal).Count() != Actions.Count)
            throw new InvalidDataException("录制文档包含重复 actionId。");

        var knownEventIds = Events.Select(item => item.EventId).ToHashSet(StringComparer.Ordinal);
        foreach (var action in Actions)
        {
            if (action.DelayMs < 0 || action.HoldMs < 0)
                throw new InvalidDataException($"动作 {action.ActionId} 的时间参数不能为负数。");
            if (action.DelayMs > MaximumActionDelayMilliseconds)
                throw new InvalidDataException($"动作 {action.ActionId} 的动作间隔不能超过 7 天。");
            if (!Enum.IsDefined(action.Kind))
                throw new InvalidDataException($"动作 {action.ActionId} 的类型无效。");
            if ((action.Kind is RecorderActionKind.MouseClick or RecorderActionKind.KeyPress) &&
                action.HoldMs > 600_000)
                throw new InvalidDataException($"动作 {action.ActionId} 的按住时长不能超过 600000ms。");
            if (action.RawEventIds is not null &&
                action.RawEventIds.Any(id => !knownEventIds.Contains(id)))
                throw new InvalidDataException($"动作 {action.ActionId} 引用了不存在的原始事件。");
        }
    }

    private static bool IsSafeId(string value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length <= 128 &&
        value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_');
}