using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Anjian.Core.Permissions;
using Anjian.Core.Plugins;
using Xunit;

namespace Anjian.Core.Tests;

public sealed class PluginPackageStoreTests : IDisposable
{
    private readonly string _temporaryRoot = Path.Combine(Path.GetTempPath(), "anjian-plugin-tests-" + Guid.NewGuid().ToString("N"));
    private readonly string _storeRoot;
    private readonly PluginPackageStore _store;

    public PluginPackageStoreTests()
    {
        Directory.CreateDirectory(_temporaryRoot);
        _storeRoot = Path.Combine(_temporaryRoot, "installed");
        _store = new PluginPackageStore(_storeRoot, "1.2");
    }


    [Fact]
    public void PublishedRuntimeBuildsAndRunsAnIndependentPluginWorker()
    {
        var runtime = Environment.GetEnvironmentVariable("ANJIAN_PUBLISHED_RUNTIME");
        if (string.IsNullOrWhiteSpace(runtime))
            return; // Repeated by Windows CI after the actual self-contained publish.
        Assert.True(Directory.Exists(runtime), "Published runtime directory does not exist: " + runtime);

        var executable = Path.Combine(runtime, "Anjian.App.exe");
        Assert.True(File.Exists(executable), "Published apphost is missing: " + executable);
        var logPath = Path.Combine(_temporaryRoot, "plugin-worker-smoke.log");
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = executable,
            WorkingDirectory = runtime,
            UseShellExecute = false,
            CreateNoWindow = true,
            Environment =
            {
                ["ANJIAN_PLUGIN_SMOKE_LOG"] = logPath
            },
            ArgumentList = { "--plugin-smoke-test" }
        });
        Assert.NotNull(process);
        if (!process!.WaitForExit(90_000))
        {
            process.Kill(entireProcessTree: true);
            Assert.Fail("Published plugin Worker smoke test did not finish within 90 seconds.");
        }

        var log = File.Exists(logPath) ? File.ReadAllText(logPath) : "";
        Assert.True(process.ExitCode == 0, $"Published plugin Worker smoke failed ({process.ExitCode}). {log}");
        Assert.Contains("plugin-worker-ok:verified-module", log, StringComparison.Ordinal);
    }

    [Fact]
    public void InstallDirectoryIsClosedAndDisabledUntilExplicitEnable()
    {
        var source = CreatePackage(Path.Combine(_temporaryRoot, "source"), manifestReadRoot: "data/sub");
        var installed = _store.InstallDirectory(source);

        Assert.False(installed.Enabled);
        Assert.Null(installed.ValidationError);
        Assert.Single(_store.ListInstalled());
        Assert.Throws<InvalidOperationException>(() =>
            _store.ResolveExecutionPlan(installed.PluginId, installed.Version, ScriptPermissionGrant.Empty));

        _store.Enable(installed.PluginId, installed.Version);
        Assert.True(Assert.Single(_store.ListInstalled()).Enabled);
        var plan = _store.ResolveExecutionPlan(installed.PluginId, installed.Version, GetApprovedScriptGrant(installed));
        Assert.Equal("entry.lua", plan.EntryRelativePath);
        Assert.Single(plan.ModuleHashes);
        Assert.True(plan.Permissions.AllowsFileRead(Path.Combine(installed.PackageDirectory, "data", "sub", "input.txt")));
        Assert.False(plan.Permissions.AllowsFileRead(Path.Combine(installed.PackageDirectory, "data", "outside.txt")));
        Assert.False(plan.Permissions.AllowsClipboardWrite);
        Assert.True(plan.Permissions.AllowsClipboardRead);
        Assert.True(plan.Permissions.AllowsHttp(new Uri("https://api.example.com/items"), "GET"));
        Assert.False(plan.Permissions.AllowsHttp(new Uri("https://api.example.com/items"), "POST"));
    }

    [Fact]
    public void MissingScriptPermissionSidecarMeansNoEffectivePluginCapabilities()
    {
        var source = CreatePackage(Path.Combine(_temporaryRoot, "no-sidecar"), includeSidecar: false);
        var installed = _store.InstallDirectory(source);
        _store.Enable(installed.PluginId, installed.Version);

        var broadGrant = new ScriptPermissionGrant(
            [Path.Combine(installed.PackageDirectory, "data")],
            [Path.Combine(installed.PackageDirectory, "output")],
            true, true, ["message"], [], ["api.example.com"], ["GET"], true);
        var plan = _store.ResolveExecutionPlan(installed.PluginId, installed.Version, broadGrant);

        Assert.Empty(plan.Permissions.FileReadRoots);
        Assert.False(plan.Permissions.ClipboardRead);
        Assert.False(plan.Permissions.ClipboardWrite);
        Assert.Empty(plan.Permissions.HttpHosts);
        Assert.False(plan.Permissions.AllowInsecureHttp);
    }

    [Fact]
    public void ValidatorRejectsUnlistedFilesTraversalNativePayloadAndBadHashes()
    {
        var source = CreatePackage(Path.Combine(_temporaryRoot, "closed"));
        File.WriteAllText(Path.Combine(source, "unlisted.txt"), "not in manifest");
        Assert.Throws<InvalidDataException>(() => PluginPackageValidator.Inspect(source, "1.2"));
        File.Delete(Path.Combine(source, "unlisted.txt"));

        var unsafePermissionPackage = CreatePackage(Path.Combine(_temporaryRoot, "unsafe-process-permission"));
        var unsafePermissionManifest = ReadManifest(unsafePermissionPackage);
        unsafePermissionManifest.Permissions.ProcessExecutables.Add("../outside.exe");
        WriteManifest(unsafePermissionPackage, unsafePermissionManifest);
        Assert.Throws<InvalidDataException>(() => PluginPackageValidator.Inspect(unsafePermissionPackage, "1.2"));

        var absolutePermissionPackage = CreatePackage(Path.Combine(_temporaryRoot, "absolute-process-permission"));
        var absolutePermissionManifest = ReadManifest(absolutePermissionPackage);
        absolutePermissionManifest.Permissions.ProcessExecutables.Add(
            Path.GetFullPath(Path.Combine(_temporaryRoot, "trusted-tool.exe")));
        WriteManifest(absolutePermissionPackage, absolutePermissionManifest);
        Assert.Single(PluginPackageValidator.Inspect(absolutePermissionPackage, "1.2").ManifestPermissions.ProcessExecutables);

        var nativePath = Path.Combine(source, "helper.dll");
        File.WriteAllText(nativePath, "not a native library");
        var manifest = ReadManifest(source);
        manifest.Files.Add(new PluginPackageFile
        {
            Path = "helper.dll",
            Sha256 = PluginPackageValidator.ComputeSha256(nativePath)
        });
        WriteManifest(source, manifest);
        Assert.Throws<InvalidDataException>(() => PluginPackageValidator.Inspect(source, "1.2"));
        File.Delete(nativePath);

        manifest = ReadManifest(source);
        manifest.Files.Single(item => item.Path == "entry.lua").Path = "../entry.lua";
        WriteManifest(source, manifest);
        Assert.Throws<InvalidDataException>(() => PluginPackageValidator.Inspect(source, "1.2"));

        var badHashSource = CreatePackage(Path.Combine(_temporaryRoot, "bad-hash"));
        manifest = ReadManifest(badHashSource);
        manifest.Files.Single(item => item.Path == "entry.lua").Sha256 = new string('0', 64);
        WriteManifest(badHashSource, manifest);
        Assert.Throws<InvalidDataException>(() => PluginPackageValidator.Inspect(badHashSource, "1.2"));
    }


    [Fact]
    public void ValidatorRequiresExplicitTopLevelAndPermissionSchemaVersions()
    {
        var manifestlessVersion = CreatePackage(Path.Combine(_temporaryRoot, "missing-top-schema"));
        var topLevel = JsonNode.Parse(File.ReadAllText(Path.Combine(manifestlessVersion, PluginPackageValidator.ManifestFileName)))!.AsObject();
        topLevel.Remove("schemaVersion");
        WriteRawManifest(manifestlessVersion, topLevel);
        Assert.Throws<InvalidDataException>(() => PluginPackageValidator.Inspect(manifestlessVersion, "1.2"));

        var entrylessPackage = CreatePackage(Path.Combine(_temporaryRoot, "missing-entry"));
        var entryless = JsonNode.Parse(File.ReadAllText(Path.Combine(entrylessPackage, PluginPackageValidator.ManifestFileName)))!.AsObject();
        entryless.Remove("entry");
        WriteRawManifest(entrylessPackage, entryless);
        Assert.Throws<InvalidDataException>(() => PluginPackageValidator.Inspect(entrylessPackage, "1.2"));

        var permissionlessVersion = CreatePackage(Path.Combine(_temporaryRoot, "missing-permission-schema"));
        var nested = JsonNode.Parse(File.ReadAllText(Path.Combine(permissionlessVersion, PluginPackageValidator.ManifestFileName)))!.AsObject();
        nested["permissions"]!.AsObject().Remove("schemaVersion");
        WriteRawManifest(permissionlessVersion, nested);
        Assert.Throws<InvalidDataException>(() => PluginPackageValidator.Inspect(permissionlessVersion, "1.2"));
    }

    private static void WriteRawManifest(string root, JsonNode node) =>
        File.WriteAllText(Path.Combine(root, PluginPackageValidator.ManifestFileName),
            node.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }),
            new UTF8Encoding(false));

    [Fact]
    public void ApiCompatibilityIsCheckedBeforeInstalling()
    {
        var source = CreatePackage(Path.Combine(_temporaryRoot, "future-api"), minimumApiVersion: "1.3");

        Assert.Throws<InvalidDataException>(() => _store.InstallDirectory(source));
        Assert.Empty(_store.ListInstalled());
    }

    [Fact]
    public void ZipInstallIsLocalAndSafeAndRejectsTraversal()
    {
        var source = CreatePackage(Path.Combine(_temporaryRoot, "zip-source"));
        var zipPath = Path.Combine(_temporaryRoot, "plugin.zip");
        CreateZip(source, zipPath);
        var installed = _store.InstallZip(zipPath);

        Assert.False(installed.Enabled);
        Assert.True(File.Exists(Path.Combine(installed.PackageDirectory, "entry.lua")));

        var maliciousZip = Path.Combine(_temporaryRoot, "malicious.zip");
        using (var file = new FileStream(maliciousZip, FileMode.CreateNew, FileAccess.ReadWrite))
        using (var archive = new ZipArchive(file, ZipArchiveMode.Create))
        using (var writer = new StreamWriter(archive.CreateEntry("../escape.txt").Open(), new UTF8Encoding(false)))
            writer.Write("should not escape");
        Assert.Throws<InvalidDataException>(() => _store.InstallZip(maliciousZip));
        Assert.False(File.Exists(Path.Combine(_temporaryRoot, "escape.txt")));
    }

    [Fact]
    public void ModifiedInstalledFilesAreRejectedAtLaunchTime()
    {
        var source = CreatePackage(Path.Combine(_temporaryRoot, "tamper-source"));
        var installed = _store.InstallDirectory(source);
        _store.Enable(installed.PluginId, installed.Version);
        File.AppendAllText(installed.EntryPath!, "\n-- modified after install");

        Assert.Throws<InvalidDataException>(() =>
            _store.ResolveExecutionPlan(installed.PluginId, installed.Version, ScriptPermissionGrant.Empty));
        var listed = Assert.Single(_store.ListInstalled());
        Assert.NotNull(listed.ValidationError);
    }

    [Fact]
    public void LifecycleAllowsUpgradeButOnlyOneEnabledVersionAndUninstallRequiresDisabled()
    {
        var firstSource = CreatePackage(Path.Combine(_temporaryRoot, "version1"), version: "1.0.0");
        var secondSource = CreatePackage(Path.Combine(_temporaryRoot, "version2"), version: "1.1.0");
        var first = _store.InstallDirectory(firstSource);
        var second = _store.InstallDirectory(secondSource);

        _store.Enable(first.PluginId, first.Version);
        _store.Enable(second.PluginId, second.Version);
        var listed = _store.ListInstalled();
        Assert.False(Assert.Single(listed, item => item.Version == "1.0.0").Enabled);
        Assert.True(Assert.Single(listed, item => item.Version == "1.1.0").Enabled);
        Assert.Throws<InvalidOperationException>(() => _store.Uninstall(second.PluginId, second.Version));

        _store.Disable(second.PluginId, second.Version);
        _store.Uninstall(second.PluginId, second.Version);
        Assert.DoesNotContain(_store.ListInstalled(), item => item.Version == "1.1.0");
        Assert.True(Directory.Exists(first.PackageDirectory));
    }

    [Fact]
    public void CorruptOrFutureCatalogIsNotSilentlyRewritten()
    {
        var source = CreatePackage(Path.Combine(_temporaryRoot, "catalog-source"));
        _store.InstallDirectory(source);
        File.WriteAllText(_store.CatalogPath, "{\"schemaVersion\":99,\"plugins\":[]}");

        Assert.Throws<InvalidDataException>(() => new PluginPackageStore(_storeRoot, "1.2"));
        Assert.Equal("{\"schemaVersion\":99,\"plugins\":[]}", File.ReadAllText(_store.CatalogPath));
    }

    private static ScriptPermissionGrant GetApprovedScriptGrant(PluginPackageSummary installed)
    {
        var sidecar = installed.EntryPath + ".permissions.json";
        var request = ScriptPermissionPolicy.ParseRequest(File.ReadAllText(sidecar), installed.PackageDirectory);
        return ScriptPermissionPolicy.CreateGrant(request, new ScriptPermissionApproval
        {
            FileReadRoots = ["data"],
            ClipboardRead = true,
            Dialogs = ["message"],
            HttpHosts = ["api.example.com"],
            HttpMethods = ["GET"]
        }, installed.PackageDirectory);
    }

    private static string CreatePackage(
        string root,
        string pluginId = "com.example.sample",
        string version = "1.0.0",
        string minimumApiVersion = "1.2",
        bool includeSidecar = true,
        bool includeModule = true,
        string manifestReadRoot = "data")
    {
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "entry.lua"), "local helper = require('sample')\nLog('plugin ' .. helper.value)\n", new UTF8Encoding(false));
        if (includeModule)
        {
            Directory.CreateDirectory(Path.Combine(root, "Modules"));
            File.WriteAllText(Path.Combine(root, "Modules", "sample.lua"), "return { value = 'ok' }\n", new UTF8Encoding(false));
        }
        if (includeSidecar)
        {
            File.WriteAllText(Path.Combine(root, "entry.lua.permissions.json"),
                "{\"schemaVersion\":1,\"fileReadRoots\":[\"data\"],\"clipboardRead\":true,\"dialogs\":[\"message\"],\"httpHosts\":[\"api.example.com\"],\"httpMethods\":[\"GET\"]}",
                new UTF8Encoding(false));
        }

        var manifest = new PluginManifest
        {
            PluginId = pluginId,
            Name = "示例插件",
            Version = version,
            Description = "Core test package",
            Author = "test",
            MinApiVersion = minimumApiVersion,
            MaxApiVersion = "1.2",
            Entry = "entry.lua",
            ModulesRoot = includeModule ? "Modules" : null,
            Permissions = new PluginPermissionManifest
            {
                FileReadRoots = [manifestReadRoot],
                ClipboardRead = true,
                Dialogs = ["message"],
                HttpHosts = ["api.example.com"],
                HttpMethods = ["GET"]
            },
            Files = []
        };
        foreach (var path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/');
            manifest.Files.Add(new PluginPackageFile { Path = relative, Sha256 = PluginPackageValidator.ComputeSha256(path) });
        }
        WriteManifest(root, manifest);
        return root;
    }

    private static PluginManifest ReadManifest(string root) =>
        JsonSerializer.Deserialize<PluginManifest>(File.ReadAllText(Path.Combine(root, PluginPackageValidator.ManifestFileName)),
            new JsonSerializerOptions(JsonSerializerDefaults.Web))
        ?? throw new InvalidDataException("manifest missing");

    private static void WriteManifest(string root, PluginManifest manifest) =>
        File.WriteAllText(Path.Combine(root, PluginPackageValidator.ManifestFileName),
            JsonSerializer.Serialize(manifest, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }),
            new UTF8Encoding(false));

    private static void CreateZip(string sourceDirectory, string zipPath)
    {
        using var file = new FileStream(zipPath, FileMode.CreateNew, FileAccess.ReadWrite);
        using var archive = new ZipArchive(file, ZipArchiveMode.Create);
        foreach (var path in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDirectory, path).Replace(Path.DirectorySeparatorChar, '/');
            using var input = File.OpenRead(path);
            using var output = archive.CreateEntry(relative).Open();
            input.CopyTo(output);
        }
    }

    public void Dispose()
    {
        try { Directory.Delete(_temporaryRoot, recursive: true); } catch { }
    }
}
