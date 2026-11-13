using System.Reflection;
using Anjian.Core.Plugins;
using NLua;

namespace Anjian.App.Services;

/// <summary>
/// Loads only manifest-listed Lua modules from a verified package, then removes Lua's
/// unrestricted file/native-module loading surfaces for this plugin Worker.
/// </summary>
internal sealed class PluginLuaModuleLoader
{
    private readonly string _packageRoot;
    private readonly string? _modulesRoot;
    private readonly Dictionary<string, string> _moduleHashes;

    private PluginLuaModuleLoader(
        string packageRoot, string? modulesRootRelativePath, IReadOnlyDictionary<string, string> moduleHashes)
    {
        _packageRoot = Path.GetFullPath(packageRoot);
        if (!Directory.Exists(_packageRoot))
            throw new DirectoryNotFoundException("插件包目录不存在：" + _packageRoot);

        _modulesRoot = string.IsNullOrWhiteSpace(modulesRootRelativePath)
            ? null
            : PluginPackageValidator.NormalizeRelativePath(modulesRootRelativePath);
        _moduleHashes = new Dictionary<string, string>(moduleHashes, StringComparer.OrdinalIgnoreCase);
        if (_modulesRoot is null && _moduleHashes.Count != 0)
            throw new InvalidDataException("插件模块 hash 清单与 modulesRoot 不一致。");
        foreach (var pair in _moduleHashes)
        {
            var normalized = PluginPackageValidator.NormalizeRelativePath(pair.Key);
            if (_modulesRoot is null ||
                !normalized.StartsWith(_modulesRoot + "/", StringComparison.OrdinalIgnoreCase) ||
                !Path.GetExtension(normalized).Equals(".lua", StringComparison.OrdinalIgnoreCase) ||
                pair.Value.Length != 64 || pair.Value.Any(c => !Uri.IsHexDigit(c)))
                throw new InvalidDataException("插件 Worker 模块清单无效：" + pair.Key);
        }
    }

    public static void Register(
        Lua lua,
        string packageRoot,
        string? modulesRootRelativePath,
        IReadOnlyDictionary<string, string> moduleHashes)
    {
        ArgumentNullException.ThrowIfNull(lua);
        ArgumentNullException.ThrowIfNull(moduleHashes);
        var loader = new PluginLuaModuleLoader(packageRoot, modulesRootRelativePath, moduleHashes);
        var method = typeof(PluginLuaModuleLoader).GetMethod(
            nameof(LoadModuleSource), BindingFlags.Instance | BindingFlags.Public)
            ?? throw new MissingMethodException(nameof(LoadModuleSource));
        lua.RegisterFunction("__anjian_load_plugin_module", loader, method);
        lua.DoString("""
            local sourceLoader = __anjian_load_plugin_module
            __anjian_load_plugin_module = nil
            local loaded = {}
            local loading = {}

            function require(name)
                if type(name) ~= "string" or name == "" then
                    error("require expects a non-empty module name", 2)
                end
                if loaded[name] ~= nil then
                    return loaded[name]
                end
                if loading[name] then
                    error("circular require is not supported: " .. name, 2)
                end

                loading[name] = true
                local ok, value = pcall(function()
                    local source = sourceLoader(name)
                    local chunk, compileError = load(source, "@plugin-module/" .. name)
                    if not chunk then error(compileError, 0) end
                    local result = chunk()
                    if result == nil then result = true end
                    return result
                end)
                loading[name] = nil
                if not ok then error(value, 2) end
                loaded[name] = value
                return value
            end

            local originalOs = os
            os = {}
            if type(originalOs) == "table" then
                os.clock = originalOs.clock
                os.date = originalOs.date
                os.difftime = originalOs.difftime
                os.time = originalOs.time
            end
            io = nil
            debug = nil
            dofile = nil
            loadfile = nil
            package = nil
            """);
    }

    public string LoadModuleSource(string moduleName)
    {
        if (_modulesRoot is null || string.IsNullOrWhiteSpace(moduleName) || moduleName.Length > 180 ||
            moduleName.StartsWith('.') || moduleName.EndsWith('.') ||
            moduleName.Split('.').Any(segment => segment.Length == 0 ||
                segment.Any(c => !((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') ||
                    (c >= '0' && c <= '9') || c is '_' or '-'))))
            throw new InvalidDataException("插件 require 模块名无效或未声明：" + moduleName);

        var relative = moduleName.Replace('.', '/');
        var candidates = new[]
        {
            _modulesRoot + "/" + relative + ".lua",
            _modulesRoot + "/" + relative + "/init.lua"
        };
        foreach (var candidate in candidates)
        {
            if (!_moduleHashes.TryGetValue(candidate, out var expectedHash))
                continue;
            return PluginPackageValidator.ReadVerifiedText(_packageRoot, candidate, expectedHash);
        }
        throw new FileNotFoundException("插件尝试加载 manifest 未登记的 Lua 模块：" + moduleName);
    }
}
