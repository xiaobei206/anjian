using System.Security.Cryptography;
using System.Text;
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
            required: false, expectedSha256: null, out grant);
    }

    public static bool TryReviewPackagedEntry(
        IWin32Window owner,
        string scriptPath,
        string? packagedSidecarPath,
        string? expectedSha256,
        out ScriptPermissionGrant grant)
    {
        grant = ScriptPermissionGrant.Empty;
        var fullScriptPath = Path.GetFullPath(scriptPath);
        var expectedSidecarPath = fullScriptPath + ".permissions.json";

        if (packagedSidecarPath is null)
        {
            if (expectedSha256 is not null)
            {
                ShowSidecarError(owner, expectedSidecarPath, "没有绑定权限 sidecar，却提供了哈希记录；已拒绝启动。");
                return false;
            }
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

        if (string.IsNullOrWhiteSpace(expectedSha256) || expectedSha256.Length != 64 ||
            expectedSha256.Any(character => !Uri.IsHexDigit(character)))
        {
            ShowSidecarError(owner, fullSidecarPath, "manifest 中缺少有效的权限 sidecar SHA-256；已拒绝启动。");
            return false;
        }

        return TryReviewSidecar(owner, fullScriptPath, fullSidecarPath,
            required: true, expectedSha256: expectedSha256, out grant);
    }

    private static bool TryReviewSidecar(
        IWin32Window owner,
        string fullScriptPath,
        string sidecarPath,
        bool required,
        string? expectedSha256,
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
            if (HasReparsePointOnPath(scriptDirectory, Path.GetFileName(sidecarPath)))
                throw new InvalidDataException("权限清单路径不能包含符号链接或重解析点。");

            var bytes = ReadSidecarBytes(sidecarPath);
            if (expectedSha256 is not null)
            {
                var actualSha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
                if (!string.Equals(actualSha256, expectedSha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("权限 sidecar SHA-256 与 manifest 不一致。");
            }

            using var reader = new StreamReader(
                new MemoryStream(bytes), new UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: true);
            var request = ScriptPermissionPolicy.ParseRequest(reader.ReadToEnd(), scriptDirectory);
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

    private static byte[] ReadSidecarBytes(string sidecarPath)
    {
        using var stream = new FileStream(sidecarPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > ScriptPermissionPolicy.MaximumManifestBytes)
            throw new InvalidDataException($"权限清单超过 {ScriptPermissionPolicy.MaximumManifestBytes} 字节上限。");

        using var output = new MemoryStream((int)stream.Length);
        var buffer = new byte[8192];
        int read;
        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            if (output.Length + read > ScriptPermissionPolicy.MaximumManifestBytes)
                throw new InvalidDataException($"权限清单超过 {ScriptPermissionPolicy.MaximumManifestBytes} 字节上限。");
            output.Write(buffer, 0, read);
        }

        return output.ToArray();
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
