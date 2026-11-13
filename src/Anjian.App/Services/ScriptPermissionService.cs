using Anjian.App.Controls;
using Anjian.Core.Permissions;

namespace Anjian.App.Services;

internal static class ScriptPermissionService
{
    public static bool TryReview(
        IWin32Window owner,
        string scriptPath,
        out ScriptPermissionGrant grant)
    {
        var fullScriptPath = Path.GetFullPath(scriptPath);
        return TryReviewSidecar(owner, fullScriptPath, fullScriptPath + ".permissions.json",
            required: false, out grant);
    }

    public static bool TryReviewPackagedEntry(
        IWin32Window owner,
        string scriptPath,
        string? packagedSidecarPath,
        out ScriptPermissionGrant grant)
    {
        grant = ScriptPermissionGrant.Empty;
        var fullScriptPath = Path.GetFullPath(scriptPath);
        var expectedSidecarPath = fullScriptPath + ".permissions.json";

        if (packagedSidecarPath is null)
        {
            if (File.Exists(expectedSidecarPath))
            {
                ShowSidecarError(owner, expectedSidecarPath,
                    "包内存在未绑定到当前入口的权限 sidecar，已拒绝启动。");
                return false;
            }
            return true;
        }

        var fullSidecarPath = Path.GetFullPath(packagedSidecarPath);
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (!fullSidecarPath.Equals(expectedSidecarPath, comparison))
        {
            ShowSidecarError(owner, fullSidecarPath,
                "权限 sidecar 与当前入口不匹配，已拒绝启动。");
            return false;
        }

        return TryReviewSidecar(owner, fullScriptPath, fullSidecarPath, required: true, out grant);
    }

    private static bool TryReviewSidecar(
        IWin32Window owner,
        string fullScriptPath,
        string sidecarPath,
        bool required,
        out ScriptPermissionGrant grant)
    {
        grant = ScriptPermissionGrant.Empty;
        var scriptDirectory = Path.GetDirectoryName(fullScriptPath)
            ?? throw new InvalidDataException("无法确定脚本目录。");
        if (!File.Exists(sidecarPath))
        {
            if (!required)
                return true;

            ShowSidecarError(owner, sidecarPath, "manifest 已声明权限 sidecar，但文件缺失；脚本不会启动。");
            return false;
        }

        try
        {
            var info = new FileInfo(sidecarPath);
            if (info.Length > ScriptPermissionPolicy.MaximumManifestBytes)
                throw new InvalidDataException($"权限清单超过 {ScriptPermissionPolicy.MaximumManifestBytes} 字节上限。");
            if (HasReparsePointOnPath(scriptDirectory, Path.GetFileName(sidecarPath)))
                throw new InvalidDataException("权限清单路径不能包含符号链接或重解析点。");

            var request = ScriptPermissionPolicy.ParseRequest(File.ReadAllText(sidecarPath), scriptDirectory);
            if (!HasRequests(request))
                return true;

            using var dialog = new ScriptPermissionReviewForm(request);
            if (dialog.ShowDialog(owner) != DialogResult.OK)
                return false;

            grant = ScriptPermissionPolicy.CreateGrant(request, dialog.Approval, scriptDirectory);
            return true;
        }
        catch (Exception ex)
        {
            ShowSidecarError(owner, sidecarPath,
                "权限清单无法安全读取，脚本不会启动。" + Environment.NewLine + ex.Message);
            return false;
        }
    }

    private static bool HasReparsePointOnPath(string root, string relativePath)
    {
        var current = Path.GetFullPath(root);
        if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            return true;
        foreach (var segment in relativePath.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if (!File.Exists(current) && !Directory.Exists(current))
                return false;
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                return true;
        }
        return false;
    }

    private static void ShowSidecarError(IWin32Window owner, string sidecarPath, string message) =>
        MessageBox.Show(owner,
            message + Environment.NewLine + sidecarPath,
            "脚本权限清单错误", MessageBoxButtons.OK, MessageBoxIcon.Error);

    private static bool HasRequests(ScriptPermissionRequest request) =>
        request.FileReadRoots.Count > 0 ||
        request.FileWriteRoots.Count > 0 ||
        request.ClipboardRead ||
        request.ClipboardWrite ||
        request.Dialogs.Count > 0 ||
        request.ProcessExecutables.Count > 0 ||
        request.HttpHosts.Count > 0 ||
        request.HttpMethods.Count > 0;
}
