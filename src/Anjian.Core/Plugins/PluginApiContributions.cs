using Anjian.Core.Api;

namespace Anjian.Core.Plugins;

/// <summary>Optional, manifest-declared metadata for pure Lua helper functions.</summary>
public sealed class PluginApiContributionSet
{
    public int SchemaVersion { get; set; } = 1;
    public List<PluginApiFunctionContribution> Functions { get; set; } = [];
}

public sealed class PluginApiFunctionContribution
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Signature { get; set; } = "";
    public string Module { get; set; } = "";
    public string Export { get; set; } = "";
    public List<LuaApiParameter> Parameters { get; set; } = [];
    public LuaApiReturn Returns { get; set; } = new();
    public List<string> Errors { get; set; } = [];
    public LuaApiBehavior Behavior { get; set; } = new();
    public string Example { get; set; } = "";
}

/// <summary>Serialized worker plan; the worker revalidates the installed catalogue and hashes.</summary>
public sealed class PluginApiRuntimePlan
{
    public string PluginId { get; set; } = "";
    public string Version { get; set; } = "";
    public string PackageRoot { get; set; } = "";
    public string? ModulesRootRelativePath { get; set; }
    public Dictionary<string, string> ModuleHashes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<PluginApiFunctionContribution> Functions { get; set; } = [];
}
