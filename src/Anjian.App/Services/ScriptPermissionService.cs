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
        grant = ScriptPermissionGrant.Empty;
        var fullScriptPath = Path.GetFullPath(scriptPath);
        var scriptDirectory = Path.GetDirectoryName(fullScriptPath)
            ?? throw new InvalidDataException("无法确定脚本目录。");
        var sidecarPath = fullScriptPath + ".permissions.json";
        if (!File.Exists(sidecarPath))
            return true;

        try
        {
            var info = new FileInfo(sidecarPath);
            if (info.Length > ScriptPermissionPolicy.MaximumManifestBytes)
                throw new InvalidDataException($"权限清单超过 {ScriptPermissionPolicy.MaximumManifestBytes} 字节上限。");
            if ((File.GetAttributes(sidecarPath) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("权限清单不能是符号链接或重解析点。");

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
            MessageBox.Show(owner,
                "权限清单无法安全读取，脚本不会启动。" + Environment.NewLine +
                sidecarPath + Environment.NewLine + ex.Message,
                "脚本权限清单错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

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
