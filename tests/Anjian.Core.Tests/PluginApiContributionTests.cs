using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Anjian.Core.Api;
using Anjian.Core.Editor;
using Anjian.Core.Plugins;
using NLua;
using Xunit;

namespace Anjian.Core.Tests;

public sealed class PluginApiContributionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "anjian-plugin-api-" + Guid.NewGuid().ToString("N"));
    private readonly string _storeRoot;
    private readonly PluginPackageStore _store;

    public PluginApiContributionTests()
    {
        _storeRoot = Path.Combine(_root, "Plugins");
        _store = new PluginPackageStore(_storeRoot, "1.2");
    }

    [Fact]
    public void EnabledPluginContributionIsMergedDiscoverableAndCallableInsideWorkerLua()
    {
        var installed = Install("com.example.math", "PluginAdd");
        _store.Enable(installed.PluginId, installed.Version);

        var plans = _store.GetEnabledApiContributionPlans();
        var merged = LuaApiRegistry.Load().WithPluginContributions(plans);
        Assert.Equal(104, merged.Apis.Count);
        Assert.Contains(LuaEditorService.GetCompletions(merged, "PluginA"), item => item.Api.Name == "PluginAdd");

        using var lua = new Lua(openLibs: true);
        PluginApiContributionRuntime.Register(lua, plans, "1.2");
        Assert.Equal(5d, Convert.ToDouble(lua.DoString("return PluginAdd(2, 3)")[0]));
        Assert.Throws<NLua.Exceptions.LuaException>(() => lua.DoString("return PluginAdd('2', 3)"));
        Assert.Throws<NLua.Exceptions.LuaException>(() => lua.DoString("return PluginAdd(1)"));

        _store.Disable(installed.PluginId, installed.Version);
        Assert.Empty(_store.GetEnabledApiContributionPlans());
    }

    [Fact]
    public void SandboxCannotResolveHostAutomationGlobalsAndPluginErrorsStayInWorkerLua()
    {
        var installed = Install("com.example.sandbox", "PluginHostGlobal");
        _store.Enable(installed.PluginId, installed.Version);
        var plans = _store.GetEnabledApiContributionPlans();
        using var lua = new Lua(openLibs: true);
        PluginApiContributionRuntime.Register(lua, plans, "1.2");
        Assert.Null(lua.DoString("return PluginHostGlobal()")[0]);
        Assert.Throws<NLua.Exceptions.LuaException>(() => lua.DoString("return PluginAlwaysFails()"));
    }

    [Fact]
    public void BuiltInNameAndEnabledPluginNameCollisionsAreRejectedAtomically()
    {
        var builtIn = CreateSourcePackage("com.example.collision", "LeftClick");
        Assert.Throws<InvalidDataException>(() => _store.InstallDirectory(builtIn));

        var first = Install("com.example.first", "PluginCollision");
        var second = Install("com.example.second", "PluginCollision");
        _store.Enable(first.PluginId, first.Version);
        Assert.Throws<InvalidDataException>(() => _store.Enable(second.PluginId, second.Version));
        Assert.False(Assert.Single(_store.ListInstalled(), item => item.PluginId == second.PluginId).Enabled);
        Assert.Single(_store.GetEnabledApiContributionPlans());
    }

    [Fact]
    public void FutureContributionSchemaUnknownFieldsAndChangedModuleAreRejected()
    {
        var future = CreateSourcePackage("com.example.future", "PluginFuture");
        var path = Path.Combine(future, PluginPackageValidator.ManifestFileName);
        var json = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        json["contributions"]!["schemaVersion"] = 99;
        File.WriteAllText(path, json.ToJsonString(new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
        Assert.Throws<InvalidDataException>(() => PluginPackageValidator.Inspect(future, "1.2"));

        var unknown = CreateSourcePackage("com.example.unknown", "PluginUnknown");
        path = Path.Combine(unknown, PluginPackageValidator.ManifestFileName);
        json = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        json["contributions"]!["untrustedField"] = true;
        File.WriteAllText(path, json.ToJsonString(new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
        Assert.Throws<InvalidDataException>(() => PluginPackageValidator.Inspect(unknown, "1.2"));

        var installed = Install("com.example.tamper", "PluginTamper");
        _store.Enable(installed.PluginId, installed.Version);
        File.AppendAllText(Path.Combine(installed.PackageDirectory, "Modules", "api.lua"), "-- tampered");
        Assert.Throws<InvalidDataException>(() => _store.GetEnabledApiContributionPlans());
    }

    private PluginPackageSummary Install(string pluginId, string functionName) =>
        _store.InstallDirectory(CreateSourcePackage(pluginId, functionName));

    private string CreateSourcePackage(string pluginId, string functionName)
    {
        var root = Path.Combine(_root, "src-" + pluginId.Replace('.', '-'));
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(Path.Combine(root, "Modules"));
        File.WriteAllText(Path.Combine(root, "entry.lua"), "Log('entry')\n", new UTF8Encoding(false));
        var isHostProbe = functionName == "PluginHostGlobal";
        var implementation = isHostProbe
            ? "return { probe = function() return MoveTo end, fail = function() error('plugin-local-error') end }\n"
            : "return { add = function(a, b) return a + b end }\n";
        File.WriteAllText(Path.Combine(root, "Modules", "api.lua"), implementation, new UTF8Encoding(false));

        var usesNumbers = functionName is "PluginAdd" or "PluginCollision" or "PluginTamper" or "PluginFuture" or "PluginUnknown";
        var parameters = usesNumbers
            ? new List<LuaApiParameter>
            {
                new() { Name = "a", Type = "number", Required = true, Description = "第一个数" },
                new() { Name = "b", Type = "number", Required = true, Description = "第二个数" }
            }
            : new List<LuaApiParameter>();
        var functions = new List<PluginApiFunctionContribution>
        {
            MakeFunction(functionName, isHostProbe ? "probe" : "add", parameters,
                isHostProbe ? "nil" : "number", isHostProbe)
        };
        if (isHostProbe)
            functions.Add(MakeFunction("PluginAlwaysFails", "fail", [], "nil", true));

        var manifest = new PluginManifest
        {
            PluginId = pluginId,
            Name = pluginId,
            Version = "1.0.0",
            Description = "API contribution test",
            Author = "tests",
            MinApiVersion = "1.2",
            MaxApiVersion = "1.2",
            Entry = "entry.lua",
            ModulesRoot = "Modules",
            Permissions = new PluginPermissionManifest { SchemaVersion = 1 },
            Contributions = new PluginApiContributionSet { Functions = functions },
            Files = []
        };
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/');
            manifest.Files.Add(new PluginPackageFile { Path = relative, Sha256 = PluginPackageValidator.ComputeSha256(file) });
        }
        File.WriteAllText(Path.Combine(root, PluginPackageValidator.ManifestFileName),
            JsonSerializer.Serialize(manifest, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }),
            new UTF8Encoding(false));
        return root;
    }

    private static PluginApiFunctionContribution MakeFunction(
        string name, string export, List<LuaApiParameter> parameters, string returnType, bool noArguments)
    {
        return new PluginApiFunctionContribution
        {
            Id = name == "PluginAlwaysFails" ? "fail" : "probe",
            Name = name,
            Title = "插件测试函数",
            Description = "用于契约测试的纯 Lua 插件 helper。",
            Signature = name + "(" + string.Join(", ", parameters.Select(item => item.Name)) + ")",
            Module = "api",
            Export = export,
            Parameters = parameters,
            Returns = new LuaApiReturn
            {
                Type = returnType,
                Description = returnType == "nil" ? "无返回值或宿主能力不可见。" : "返回两个数的和。"
            },
            Errors = ["参数类型错误", "插件实现抛出异常"],
            Behavior = new LuaApiBehavior
            {
                PauseBehavior = "沿用当前 Lua Worker 的指令检查点。",
                CancellationBehavior = "Stop 终止本次 Worker。",
                CoordinateSpace = "不适用。",
                PermissionNotes = "纯 Lua helper，不获得文件、输入、进程、剪贴板、对话框或网络权限。",
                SideEffects = []
            },
            Example = noArguments ? name + "()" : name + "(2, 3)"
        };
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); } catch { }
    }
}
