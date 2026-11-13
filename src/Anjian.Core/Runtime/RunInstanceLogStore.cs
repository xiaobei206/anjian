using System.Text.RegularExpressions;

namespace Anjian.Core.Runtime;

public enum RunInstanceLogLevel
{
    Information,
    Warning,
    Error
}

/// <summary>
/// An allowlisted, payload-free event record used by the run-instance monitor.
/// Message text is sanitized before it enters this store.
/// </summary>
public sealed record RunInstanceLogEntry(
    DateTimeOffset TimestampUtc,
    RunInstanceLogLevel Level,
    string EventCode,
    RunInstanceId? InstanceId,
    string? WorkerAttemptId,
    int? Iteration,
    string Message);

/// <summary>
/// Bounded in-memory history. This deliberately does not persist logs or collect request/response bodies.
/// </summary>
public sealed class RunInstanceLogStore
{
    public const int DefaultCapacity = 10_000;
    public const int MaximumMessageLength = 4_096;

    private readonly object _sync = new();
    private readonly List<RunInstanceLogEntry> _entries = [];
    private readonly int _capacity;

    public RunInstanceLogStore(int capacity = DefaultCapacity)
    {
        if (capacity is < 1 or > 1_000_000)
            throw new ArgumentOutOfRangeException(nameof(capacity), "日志缓存容量必须为 1 至 1,000,000 条。");
        _capacity = capacity;
    }

    public int Count
    {
        get { lock (_sync) return _entries.Count; }
    }

    public void Add(RunInstanceLogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var normalized = entry with
        {
            TimestampUtc = entry.TimestampUtc.ToUniversalTime(),
            EventCode = RunInstanceLogSanitizer.Sanitize(entry.EventCode, maximumLength: 128),
            WorkerAttemptId = entry.WorkerAttemptId is null
                ? null
                : RunInstanceLogSanitizer.Sanitize(entry.WorkerAttemptId, maximumLength: 64),
            Message = RunInstanceLogSanitizer.Sanitize(entry.Message, maximumLength: MaximumMessageLength)
        };

        lock (_sync)
        {
            _entries.Add(normalized);
            if (_entries.Count > _capacity)
                _entries.RemoveRange(0, _entries.Count - _capacity);
        }
    }

    /// <summary>
    /// Returns a stable snapshot in timestamp order. When an instance is selected,
    /// global events may optionally be included; events from other instances never are.
    /// </summary>
    public IReadOnlyList<RunInstanceLogEntry> Query(
        RunInstanceId? instanceId = null,
        RunInstanceLogLevel? level = null,
        string? text = null,
        int limit = 1_000,
        bool includeGlobal = true)
    {
        if (limit is < 1 or > DefaultCapacity)
            throw new ArgumentOutOfRangeException(nameof(limit), $"查询条数必须为 1 至 {DefaultCapacity}。");

        RunInstanceLogEntry[] snapshot;
        lock (_sync)
            snapshot = _entries.ToArray();

        var search = string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        var filtered = snapshot.Where(entry =>
            (instanceId is null ||
             entry.InstanceId == instanceId ||
             (includeGlobal && entry.InstanceId is null)) &&
            (level is null || entry.Level == level) &&
            (search is null ||
             entry.Message.Contains(search, StringComparison.OrdinalIgnoreCase) ||
             entry.EventCode.Contains(search, StringComparison.OrdinalIgnoreCase) ||
             (entry.WorkerAttemptId?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)));

        return filtered.OrderBy(entry => entry.TimestampUtc).TakeLast(limit).ToArray();
    }
}

/// <summary>
/// Removes sensitive values and terminal control characters before a log is surfaced to any UI/export.
/// Short values are replaced only as stand-alone tokens to avoid corrupting every occurrence of common letters.
/// </summary>
public static class RunInstanceLogSanitizer
{
    public const string RedactionMarker = "[敏感值已隐藏]";

    public static string Sanitize(
        string? message,
        IEnumerable<string>? sensitiveValues = null,
        int maximumLength = RunInstanceLogStore.MaximumMessageLength)
    {
        if (maximumLength < 1)
            throw new ArgumentOutOfRangeException(nameof(maximumLength));

        var result = message ?? string.Empty;
        if (sensitiveValues is not null)
        {
            foreach (var secret in sensitiveValues
                         .Where(value => !string.IsNullOrEmpty(value))
                         .Distinct(StringComparer.Ordinal)
                         .OrderByDescending(value => value.Length))
            {
                if (secret.Length >= 3)
                {
                    result = result.Replace(secret, RedactionMarker, StringComparison.Ordinal);
                }
                else
                {
                    result = Regex.Replace(
                        result,
                        $@"(?<![\p{{L}}\p{{N}}_]){Regex.Escape(secret)}(?![\p{{L}}\p{{N}}_])",
                        RedactionMarker);
                }
            }
        }

        result = new string(result
            .Where(character => !char.IsControl(character) || character is '\t' or '\r' or '\n')
            .ToArray());

        if (result.Length > maximumLength)
            result = result[..maximumLength] + "…";
        return result;
    }
}
