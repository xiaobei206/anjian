namespace Anjian.Core.Runtime;

/// <summary>
/// Deterministic filtering/order policy for the run-instance list. Live instances are never hidden by
/// the history limit; only the most recent cleaned instances are retained in the returned view.
/// </summary>
public static class RunInstanceListQuery
{
    public const int DefaultCompletedHistoryLimit = 500;

    public static IReadOnlyList<RunInstanceDescriptor> Filter(
        IEnumerable<RunInstanceDescriptor> instances,
        string? scriptQuery = null,
        Func<RunInstanceSnapshot, bool>? snapshotPredicate = null,
        int recentCompletedLimit = DefaultCompletedHistoryLimit)
    {
        ArgumentNullException.ThrowIfNull(instances);
        if (recentCompletedLimit is < 0 or > 100_000)
            throw new ArgumentOutOfRangeException(nameof(recentCompletedLimit));

        var all = instances.ToArray();
        var recent = all
            .Where(item => !RunInstanceLifecycle.IsTerminal(item.Snapshot))
            .Concat(all
                .Where(item => RunInstanceLifecycle.IsTerminal(item.Snapshot))
                .OrderByDescending(item => item.CreatedAtUtc)
                .Take(recentCompletedLimit));

        var search = string.IsNullOrWhiteSpace(scriptQuery) ? null : scriptQuery.Trim();
        return recent
            .Where(item =>
            {
                if (search is not null && !MatchesScriptPath(item.ScriptPath, search))
                    return false;
                return snapshotPredicate is null || snapshotPredicate(item.Snapshot);
            })
            .OrderByDescending(item => item.CreatedAtUtc)
            .ToArray();
    }

    private static bool MatchesScriptPath(string path, string query)
    {
        if (path.Contains(query, StringComparison.OrdinalIgnoreCase))
            return true;
        var lastSeparator = Math.Max(path.LastIndexOf('/'), path.LastIndexOf('\\'));
        var fileName = path[(lastSeparator + 1)..];
        return fileName.Contains(query, StringComparison.OrdinalIgnoreCase);
    }
}
