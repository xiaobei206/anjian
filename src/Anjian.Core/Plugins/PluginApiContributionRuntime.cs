using System.Reflection;
using System.Text.Json;
using Anjian.Core.Api;
using NLua;

namespace Anjian.Core.Plugins;

/// <summary>
/// Exposes only manifest-declared pure Lua helpers. Each module executes lazily in a
/// restricted environment and every source read is pinned to the installed manifest hash.
/// </summary>
public static class PluginApiContributionRuntime
{
    private const string LoaderName = "__anjian_load_plugin_api_module";
    private const string RegisterName = "__anjian_register_plugin_api";

    public static void Register(Lua lua, IEnumerable<PluginApiRuntimePlan> suppliedPlans, string currentApiVersion)
    {
        ArgumentNullException.ThrowIfNull(lua);
        ArgumentNullException.ThrowIfNull(suppliedPlans);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentApiVersion);
        var plans = RevalidatePlans(suppliedPlans, currentApiVersion);
        if (plans.Count == 0)
            return;

        _ = LuaApiRegistry.Load().WithPluginContributions(plans);
        var sourceProvider = new PluginApiModuleSourceProvider(plans);
        var method = typeof(PluginApiModuleSourceProvider).GetMethod(
            nameof(PluginApiModuleSourceProvider.LoadModuleSource), BindingFlags.Instance | BindingFlags.Public)
            ?? throw new MissingMethodException(nameof(PluginApiModuleSourceProvider.LoadModuleSource));
        lua.RegisterFunction(LoaderName, sourceProvider, method);
        lua.DoString(Bootstrap, "@Anjian2026.PluginApiContributions");

        var register = lua.GetFunction(RegisterName)
            ?? throw new InvalidDataException("插件 API 注册器初始化失败。");
        foreach (var plan in plans)
        foreach (var function in plan.Functions)
        {
            var args = new List<object>
            {
                plan.PluginId, function.Module, function.Export, function.Name,
                function.Parameters.Count, function.Returns.Type, function.Signature
            };
            args.AddRange(function.Parameters.Select(parameter => (object)parameter.Type));
            _ = register.Call(args.ToArray());
        }

        lua[RegisterName] = null;
        lua[LoaderName] = null;
    }

    private static List<PluginApiRuntimePlan> RevalidatePlans(
        IEnumerable<PluginApiRuntimePlan> suppliedPlans, string currentApiVersion)
    {
        var result = new List<PluginApiRuntimePlan>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var supplied in suppliedPlans)
        {
            if (supplied is null || string.IsNullOrWhiteSpace(supplied.PackageRoot))
                throw new InvalidDataException("插件 API Worker plan 缺少 package root。");
            var root = Path.GetFullPath(supplied.PackageRoot);
            var storeRoot = Directory.GetParent(root)?.Parent?.FullName
                ?? throw new InvalidDataException("无法从插件包目录确定本地插件 catalog。");
            var store = new PluginPackageStore(storeRoot, currentApiVersion);
            var installed = store.ListInstalled().SingleOrDefault(item =>
                item.PluginId.Equals(supplied.PluginId, StringComparison.Ordinal) &&
                item.Version.Equals(supplied.Version, StringComparison.Ordinal));
            if (installed is null || !installed.Enabled || installed.ValidationError is not null ||
                !Path.GetFullPath(installed.PackageDirectory).Equals(root,
                    OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                throw new InvalidDataException("插件 API 来自未安装、已停用或损坏的插件版本。");

            var inspection = PluginPackageValidator.Inspect(root, currentApiVersion);
            if (inspection.Manifest.Contributions is not { Functions.Count: > 0 } contributions)
                throw new InvalidDataException("插件 API contributions 缺失。");
            if (!inspection.Manifest.PluginId.Equals(supplied.PluginId, StringComparison.Ordinal) ||
                !inspection.Manifest.Version.Equals(supplied.Version, StringComparison.Ordinal) ||
                !string.Equals(inspection.Manifest.ModulesRoot, supplied.ModulesRootRelativePath, StringComparison.Ordinal))
                throw new InvalidDataException("插件 API Worker plan 与已验证 manifest 不一致。");
            var expectedFunctions = JsonSerializer.Serialize(
                contributions.Functions, PluginPackageValidator.JsonOptions);
            var suppliedFunctions = JsonSerializer.Serialize(
                supplied.Functions, PluginPackageValidator.JsonOptions);
            if (!string.Equals(expectedFunctions, suppliedFunctions, StringComparison.Ordinal))
                throw new InvalidDataException("插件 API Worker plan 函数契约与已验证 manifest 不一致。");

            var moduleRoot = inspection.Manifest.ModulesRoot
                ?? throw new InvalidDataException("插件 API contributions 缺少 modulesRoot。");
            var prefix = moduleRoot + "/";
            var hashes = inspection.FileHashes
                .Where(pair => pair.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                    Path.GetExtension(pair.Key).Equals(".lua", StringComparison.OrdinalIgnoreCase))
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
            if (hashes.Count != supplied.ModuleHashes.Count ||
                hashes.Any(pair => !supplied.ModuleHashes.TryGetValue(pair.Key, out var value) ||
                    !value.Equals(pair.Value, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException("插件 API Worker 模块 hash 清单与已验证 manifest 不一致。");
            if (!seen.Add(inspection.Manifest.PluginId))
                throw new InvalidDataException("插件 API Worker plan 重复包含 pluginId。");

            result.Add(new PluginApiRuntimePlan
            {
                PluginId = inspection.Manifest.PluginId,
                Version = inspection.Manifest.Version,
                PackageRoot = inspection.PackageRoot,
                ModulesRootRelativePath = moduleRoot,
                ModuleHashes = hashes,
                Functions = contributions.Functions
            });
        }
        return result;
    }

    private const string Bootstrap = """
        local hostGlobal = _G
        local sourceLoader = __anjian_load_plugin_api_module
        local originalLoad = load
        __anjian_load_plugin_api_module = nil

        local safeBase = {
            assert = assert, error = error, ipairs = ipairs, next = next, pairs = pairs,
            pcall = pcall, select = select, tonumber = tonumber, tostring = tostring,
            type = type, xpcall = xpcall
        }
        local function copyLibrary(source)
            local target = {}
            for key, value in pairs(source) do target[key] = value end
            return target
        end

        local moduleCache = {}
        local moduleLoading = {}
        local loadModule
        local function makeEnvironment(pluginId)
            local environment = {}
            local library = {}
            for key, value in pairs(safeBase) do library[key] = value end
            library.math = copyLibrary(math)
            library.string = copyLibrary(string)
            library.table = copyLibrary(table)
            library.utf8 = copyLibrary(utf8)
            setmetatable(environment, { __index = library })
            environment.require = function(name) return loadModule(pluginId, name) end
            return environment
        end

        loadModule = function(pluginId, moduleName)
            if type(pluginId) ~= "string" or type(moduleName) ~= "string" or moduleName == "" then
                error("invalid plugin API module request", 2)
            end
            local key = pluginId .. "\\0" .. moduleName
            if moduleCache[key] ~= nil then return moduleCache[key] end
            if moduleLoading[key] then error("circular plugin API require: " .. moduleName, 2) end
            moduleLoading[key] = true
            local ok, result = pcall(function()
                local source = sourceLoader(pluginId, moduleName)
                local environment = makeEnvironment(pluginId)
                local chunk, compileError = originalLoad(
                    source, "@plugin-api/" .. pluginId .. "/" .. moduleName, "t", environment)
                if not chunk then error(compileError, 0) end
                local value = chunk()
                if value == nil then value = true end
                return value
            end)
            moduleLoading[key] = nil
            if not ok then error(result, 2) end
            moduleCache[key] = result
            return result
        end

        local function hasType(value, expected)
            if expected == "nil" then return value == nil end
            if expected == "integer" then return type(value) == "number" and math.type(value) == "integer" end
            return type(value) == expected
        end

        local function registerPluginApi(pluginId, moduleName, exportName, publicName,
                parameterCount, returnType, signature, ...)
            if type(parameterCount) ~= "number" or parameterCount < 0 or parameterCount > 16 then
                error("invalid plugin API parameter count", 2)
            end
            local parameterTypes = {...}
            if #parameterTypes ~= parameterCount then error("invalid plugin API parameter schema", 2) end
            hostGlobal[publicName] = function(...)
                if select("#", ...) ~= parameterCount then
                    error(publicName .. " expects " .. parameterCount .. " arguments (" .. signature .. ")", 2)
                end
                for index = 1, parameterCount do
                    if not hasType(select(index, ...), parameterTypes[index]) then
                        error(publicName .. " argument " .. index .. " must be " .. parameterTypes[index], 2)
                    end
                end
                local moduleValue = loadModule(pluginId, moduleName)
                if type(moduleValue) ~= "table" then
                    error("plugin API module must return a table: " .. moduleName, 2)
                end
                local implementation = moduleValue[exportName]
                if type(implementation) ~= "function" then
                    error("plugin API export must be a function: " .. exportName, 2)
                end
                local ok, result = pcall(implementation, ...)
                if not ok then
                    error("plugin API " .. publicName .. " failed: " .. tostring(result), 2)
                end
                if not hasType(result, returnType) then
                    error("plugin API " .. publicName .. " returned an unexpected type; expected " .. returnType, 2)
                end
                return result
            end
        end
        __anjian_register_plugin_api = registerPluginApi
        """;

    public sealed class PluginApiModuleSourceProvider
    {
        private readonly Dictionary<string, PluginApiRuntimePlan> _plans = new(StringComparer.Ordinal);

        public PluginApiModuleSourceProvider(IEnumerable<PluginApiRuntimePlan> plans)
        {
            foreach (var plan in plans)
                if (!_plans.TryAdd(plan.PluginId, plan))
                    throw new InvalidDataException("插件 API module provider 中 pluginId 重复。");
        }

        public string LoadModuleSource(string pluginId, string moduleName)
        {
            if (!_plans.TryGetValue(pluginId, out var plan) ||
                string.IsNullOrWhiteSpace(plan.ModulesRootRelativePath))
                throw new InvalidDataException("未知插件或未声明模块根目录。");
            var relative = PluginPackageValidator.ResolveContributionModulePath(
                plan.ModulesRootRelativePath, moduleName, plan.ModuleHashes.Keys);
            if (!plan.ModuleHashes.TryGetValue(relative, out var expectedHash))
                throw new FileNotFoundException("插件 API require 引用了未登记模块：" + moduleName);
            return PluginPackageValidator.ReadVerifiedText(plan.PackageRoot, relative, expectedHash);
        }
    }
}
