using Anjian.Core.Permissions;

namespace Anjian.Core.Plugins;

/// <summary>Builds an effective plugin grant as a subset of both declared request and approved grant.</summary>
public static class PluginPermissionIntersection
{
    public static ScriptPermissionGrant Intersect(
        ScriptPermissionRequest request,
        ScriptPermissionGrant approved)
    {
        ArgumentNullException.ThrowIfNull(request);
        approved ??= ScriptPermissionGrant.Empty;
        return new ScriptPermissionGrant(
            IntersectPathRoots(request.FileReadRoots, approved.FileReadRoots),
            IntersectPathRoots(request.FileWriteRoots, approved.FileWriteRoots),
            request.ClipboardRead && approved.ClipboardRead,
            request.ClipboardWrite && approved.ClipboardWrite,
            IntersectTokens(request.Dialogs, approved.Dialogs, StringComparer.OrdinalIgnoreCase),
            IntersectPaths(request.ProcessExecutables, approved.ProcessExecutables),
            IntersectTokens(request.HttpHosts, approved.HttpHosts, StringComparer.OrdinalIgnoreCase),
            IntersectTokens(request.HttpMethods, approved.HttpMethods, StringComparer.OrdinalIgnoreCase),
            request.AllowInsecureHttp && approved.AllowInsecureHttp);
    }

    private static IReadOnlyList<string> IntersectPathRoots(
        IEnumerable<string> requestedRoots, IEnumerable<string> approvedRoots)
    {
        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var result = new HashSet<string>(comparer);
        foreach (var requested in requestedRoots)
        foreach (var approved in approvedRoots)
        {
            var requestedFull = Path.GetFullPath(requested);
            var approvedFull = Path.GetFullPath(approved);
            if (IsSameOrWithin(requestedFull, approvedFull))
                result.Add(approvedFull);
            else if (IsSameOrWithin(approvedFull, requestedFull))
                result.Add(requestedFull);
        }
        return result.OrderBy(path => path, comparer).ToArray();
    }

    private static bool IsSameOrWithin(string root, string candidate)
    {
        try
        {
            var relative = Path.GetRelativePath(root, candidate);
            var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            return relative == "." ||
                   (!Path.IsPathRooted(relative) && relative != ".." &&
                    !relative.StartsWith(".." + Path.DirectorySeparatorChar, comparison) &&
                    !relative.StartsWith(".." + Path.AltDirectorySeparatorChar, comparison));
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static IReadOnlyList<string> IntersectPaths(IEnumerable<string> requested, IEnumerable<string> approved)
    {
        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var allowed = new HashSet<string>(approved.Select(Path.GetFullPath), comparer);
        return requested.Select(Path.GetFullPath).Where(allowed.Contains).Distinct(comparer).OrderBy(x => x, comparer).ToArray();
    }

    private static IReadOnlyList<string> IntersectTokens(
        IEnumerable<string> requested, IEnumerable<string> approved, StringComparer comparer)
    {
        var allowed = new HashSet<string>(approved, comparer);
        return requested.Where(allowed.Contains).Distinct(comparer).OrderBy(x => x, comparer).ToArray();
    }
}
