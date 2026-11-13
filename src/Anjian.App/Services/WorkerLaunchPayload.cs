using System.Text.Json;

namespace Anjian.App.Services;

internal sealed class WorkerLaunchPayload
{
    public LuaDebugLaunchOptions? DebugOptions { get; set; }
    public Dictionary<string, JsonElement> Parameters { get; set; } = new(StringComparer.Ordinal);
}
