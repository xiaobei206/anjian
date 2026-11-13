using System.Text;
using System.Text.Json;
using Anjian.Core;
using Anjian.Core.Api;
using Anjian.Core.Permissions;
using Anjian.Core.Plugins;

namespace Anjian.App.Services;

/// <summary>
/// Headless Windows CI smoke proving a verified local plugin executes its manifest-pinned
/// entry and require module inside a separate Worker process.
/// </summary>
internal static class PluginSmokeTestRunner
{
    public static int Run()
    {
        var logPath = Environment.GetEnvironmentVariable("ANJIAN_PLUGIN_SMOKE_LOG");
        var messages = new List<string>();
        var root = Path.Combine(Path.GetTempPath(), "Anjian2026-PluginSmoke-" + Guid.NewGuid().ToString("N"));
        try
        {
            var source = Path.Combine(root, "source");
            var storeRoot = Path.Combine(root, "installed");
            Directory.CreateDirectory(Path.Combine(source, "Modules"));
            File.WriteAllText(Path.Combine(source, "entry.lua"),
                "local module = require('smoke')\nLog('plugin-worker-ok:' .. module.value)\n",
                new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(source, "Modules", "smoke.lua"),
                "return { value = 'verified-module' }\n",
                new UTF8Encoding(false));

            var manifest = new PluginManifest
            {
                PluginId = "com.openai.ci-smoke",
                Name = "Plugin Worker CI smoke",
                Version = "1.0.0",
                Description = "Ephemeral local package used only by the automated Worker smoke test.",
                Author = "Anjian2026 CI",
                MinApiVersion = LuaApiRegistry.Load().ApiVersion,
                MaxApiVersion = LuaApiRegistry.Load().ApiVersion,
                Entry = "entry.lua",
                ModulesRoot = "Modules",
                Permissions = new PluginPermissionManifest(),
                Files =
                [
                    new PluginPackageFile
                    {
                        Path = "entry.lua",
                        Sha256 = PluginPackageValidator.ComputeSha256(Path.Combine(source, "entry.lua"))
                    },
                    new PluginPackageFile
                    {
                        Path = "Modules/smoke.lua",
                        Sha256 = PluginPackageValidator.ComputeSha256(Path.Combine(source, "Modules", "smoke.lua"))
                    }
                ]
            };
            File.WriteAllText(Path.Combine(source, PluginPackageValidator.ManifestFileName),
                JsonSerializer.Serialize(manifest, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }),
                new UTF8Encoding(false));

            var store = new PluginPackageStore(storeRoot, manifest.MinApiVersion);
            var installed = store.InstallDirectory(source);
            store.Enable(installed.PluginId, installed.Version);
            var plan = store.ResolveExecutionPlan(installed.PluginId, installed.Version, ScriptPermissionGrant.Empty);
            var worker = new PluginWorkerRuntimeOptions
            {
                PackageRootPath = plan.PackageRoot,
                EntryRelativePath = plan.EntryRelativePath,
                EntrySha256 = plan.EntrySha256,
                ModulesRootRelativePath = plan.ModulesRootRelativePath,
                ModuleHashes = plan.ModuleHashes.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase)
            };

            using var runner = new PlaybackRunner();
            runner.Log += message => messages.Add(message);
            runner.RunAsync(plan.EntryPath, new PlaybackOptions { RepeatCount = 1, IntervalMilliseconds = 0 },
                permissions: plan.Permissions, pluginRuntimeOptions: worker).GetAwaiter().GetResult();

            if (!messages.Any(message => message.Contains("plugin-worker-ok:verified-module", StringComparison.Ordinal)))
                throw new InvalidOperationException("独立插件 Worker 未返回预期的入口/模块结果。");
            WriteLog(logPath, "PASS", messages);
            return 0;
        }
        catch (Exception ex)
        {
            messages.Add(ex.ToString());
            WriteLog(logPath, "FAIL", messages);
            return 2;
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch (IOException) { }
        }
    }

    private static void WriteLog(string? path, string status, IEnumerable<string> messages)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;
        try { File.WriteAllLines(path, new[] { status }.Concat(messages)); }
        catch (IOException) { }
    }
}
