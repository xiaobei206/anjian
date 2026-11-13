using System.Text.Json;
using Anjian.Core.Editor;
using Anjian.Core.Permissions;
using Anjian.Core.Plugins;

namespace Anjian.App.Services;

internal sealed class WorkerHandshake
{
    public string InstanceId { get; set; } = "";
    public string WorkerAttemptId { get; set; } = "";
    public string SessionNonce { get; set; } = "";
}

internal sealed class WorkerLaunchPayload
{
    public string InstanceId { get; set; } = "";
    public string WorkerAttemptId { get; set; } = "";
    public string SessionNonce { get; set; } = "";
    public LuaDebugLaunchOptions? DebugOptions { get; set; }
    public Dictionary<string, JsonElement> Parameters { get; set; } = new(StringComparer.Ordinal);
    public string? ModuleRootPath { get; set; }
    public string? OcrModelDirectory { get; set; }
    public PluginWorkerRuntimeOptions? PluginRuntime { get; set; }
    public List<PluginApiRuntimePlan> PluginApiContributions { get; set; } = [];
    public ScriptPermissionGrant Permissions { get; set; } = ScriptPermissionGrant.Empty;
}

internal sealed record SpriteWorkerRuntimeOptions(string ModuleRootPath, string? OcrModelDirectory);

internal sealed class PluginWorkerRuntimeOptions
{
    public string PackageRootPath { get; set; } = "";
    public string EntryRelativePath { get; set; } = "";
    public string EntrySha256 { get; set; } = "";
    public string? ModulesRootRelativePath { get; set; }
    public Dictionary<string, string> ModuleHashes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
