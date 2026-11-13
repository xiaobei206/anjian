using Anjian.Core.Permissions;

namespace Anjian.Core.Tests;

public sealed class ScriptPermissionPolicyTests
{
    [Fact]
    public void MissingApprovalDeniesEveryCapability()
    {
        var request = ScriptPermissionPolicy.ParseRequest(
            """{"schemaVersion":1,"fileReadRoots":["data"],"clipboardRead":true,"dialogs":["message"],"httpHosts":["api.example.com"],"httpMethods":["GET"]}""",
            Path.GetTempPath());

        var grant = ScriptPermissionPolicy.CreateGrant(request, null, Path.GetTempPath());

        Assert.Empty(grant.FileReadRoots);
        Assert.False(grant.AllowsClipboardRead);
        Assert.False(grant.AllowsDialog("message"));
        Assert.False(grant.AllowsHttp(new Uri("https://api.example.com/data"), "GET"));
    }

    [Fact]
    public void RelativeRootsAreResolvedAgainstScriptDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "anjian-permissions", Guid.NewGuid().ToString("N"));
        var request = ScriptPermissionPolicy.ParseRequest(
            """{"schemaVersion":1,"fileReadRoots":["data"],"fileWriteRoots":["output"]}""", root);

        Assert.Equal(Path.GetFullPath(Path.Combine(root, "data")), request.FileReadRoots.Single());
        Assert.Equal(Path.GetFullPath(Path.Combine(root, "output")), request.FileWriteRoots.Single());
    }

    [Fact]
    public void FileRootsUsePathBoundariesNotStringPrefixes()
    {
        var root = Path.Combine(Path.GetTempPath(), "anjian-permissions", "allowed");
        var grant = new ScriptPermissionGrant(
            [root], [], false, false, [], [], [], [], false);

        Assert.True(grant.AllowsFileRead(Path.Combine(root, "nested", "file.txt")));
        Assert.True(grant.AllowsFileRead(root));
        Assert.False(grant.AllowsFileRead(root + "-sibling" + Path.DirectorySeparatorChar + "file.txt"));
        Assert.False(grant.AllowsFileRead(Path.Combine(root, "..", "outside.txt")));
    }

    [Fact]
    public void ApprovalMustBeStrictSubsetOfRequestedCapabilities()
    {
        var basePath = Path.Combine(Path.GetTempPath(), "anjian-permissions");
        var requested = ScriptPermissionPolicy.ParseRequest(
            """{"schemaVersion":1,"fileReadRoots":["data"],"clipboardRead":true,"dialogs":["message"]}""", basePath);

        var grant = ScriptPermissionPolicy.CreateGrant(requested,
            new ScriptPermissionApproval
            {
                FileReadRoots = ["data"],
                ClipboardRead = true,
                Dialogs = ["message"]
            }, basePath);

        Assert.True(grant.AllowsFileRead(Path.Combine(basePath, "data", "a.txt")));
        Assert.True(grant.AllowsClipboardRead);
        Assert.True(grant.AllowsDialog("message"));
        Assert.False(grant.AllowsDialog("confirm"));

        Assert.Throws<InvalidDataException>(() => ScriptPermissionPolicy.CreateGrant(requested,
            new ScriptPermissionApproval { ClipboardWrite = true }, basePath));
        Assert.Throws<InvalidDataException>(() => ScriptPermissionPolicy.CreateGrant(requested,
            new ScriptPermissionApproval { FileReadRoots = ["other"] }, basePath));
    }

    [Fact]
    public void ClipboardAndDialogsAreDefaultDeny()
    {
        var grant = ScriptPermissionGrant.Empty;
        Assert.False(grant.AllowsClipboardRead);
        Assert.False(grant.AllowsClipboardWrite);
        Assert.False(grant.AllowsDialog("message"));
        Assert.False(grant.AllowsDialog("input"));
        Assert.False(grant.AllowsDialog("confirm"));
    }

    [Fact]
    public void ProcessStartRequiresAnExactExecutablePath()
    {
        var executable = Path.Combine(Path.GetTempPath(), "tools", "worker.exe");
        var grant = new ScriptPermissionGrant(
            [], [], false, false, [], [Path.GetFullPath(executable)], [], [], false);

        Assert.True(grant.AllowsProcessStart(executable));
        Assert.False(grant.AllowsProcessStart(Path.Combine(Path.GetTempPath(), "tools", "other.exe")));
        Assert.False(grant.AllowsProcessStart(Path.Combine(Path.GetTempPath(), "tools", "worker.bat")));
    }

    [Fact]
    public void HttpRequiresExactHostMethodAndHttpsUnlessExplicitlyGranted()
    {
        var request = ScriptPermissionPolicy.ParseRequest(
            """{"schemaVersion":1,"httpHosts":["api.example.com"],"httpMethods":["get","post"]}""",
            Path.GetTempPath());
        var grant = ScriptPermissionPolicy.CreateGrant(request,
            new ScriptPermissionApproval { HttpHosts = ["api.example.com"], HttpMethods = ["GET"] },
            Path.GetTempPath());

        Assert.True(grant.AllowsHttp(new Uri("https://api.example.com/v1"), "GET"));
        Assert.False(grant.AllowsHttp(new Uri("https://api.example.com/v1"), "POST"));
        Assert.False(grant.AllowsHttp(new Uri("https://sub.api.example.com/v1"), "GET"));
        Assert.False(grant.AllowsHttp(new Uri("http://api.example.com/v1"), "GET"));
        Assert.False(grant.AllowsHttp(new Uri("https://evil.example.com/v1"), "GET"));
    }

    [Fact]
    public void HttpHostAllowsIdnNormalizationButRejectsWildcards()
    {
        Assert.Equal("xn--bcher-kva.example", ScriptPermissionPolicy.NormalizeHost("bücher.example"));
        Assert.Throws<ArgumentException>(() => ScriptPermissionPolicy.NormalizeHost("*.example.com"));
    }

    [Fact]
    public void ExplicitInsecureHttpApprovalIsRequired()
    {
        var request = ScriptPermissionPolicy.ParseRequest(
            """{"schemaVersion":1,"httpHosts":["api.example.com"],"httpMethods":["GET"],"allowInsecureHttp":true}""",
            Path.GetTempPath());
        var grant = ScriptPermissionPolicy.CreateGrant(request,
            new ScriptPermissionApproval
            {
                HttpHosts = ["api.example.com"],
                HttpMethods = ["GET"],
                AllowInsecureHttp = true
            }, Path.GetTempPath());

        Assert.True(grant.AllowsHttp(new Uri("http://api.example.com/v1"), "GET"));
    }

    [Fact]
    public void HttpMethodsMustBePairedWithAtLeastOneExactHost()
    {
        Assert.Throws<InvalidDataException>(() => ScriptPermissionPolicy.ParseRequest(
            """{"schemaVersion":1,"httpMethods":["GET"]}""", Path.GetTempPath()));
        Assert.Throws<InvalidDataException>(() => ScriptPermissionPolicy.ParseRequest(
            """{"schemaVersion":1,"httpHosts":["api.example.com"]}""", Path.GetTempPath()));
    }

    [Fact]
    public void UnsupportedCapabilitiesAndInvalidExecutablesAreRejected()
    {
        Assert.Throws<InvalidDataException>(() => ScriptPermissionPolicy.ParseRequest(
            """{"schemaVersion":1,"dialogs":["shell"]}""", Path.GetTempPath()));
        Assert.Throws<InvalidDataException>(() => ScriptPermissionPolicy.ParseRequest(
            """{"schemaVersion":1,"processExecutables":["script.bat"]}""", Path.GetTempPath()));
    }

    [Fact]
    public void FutureSchemaAndOversizedManifestAreRejected()
    {
        Assert.Throws<InvalidDataException>(() => ScriptPermissionPolicy.ParseRequest(
            """{"schemaVersion":2}""", Path.GetTempPath()));
        Assert.Throws<InvalidDataException>(() => ScriptPermissionPolicy.ParseRequest(
            new string(' ', ScriptPermissionPolicy.MaximumManifestBytes + 1), Path.GetTempPath()));
    }
}
