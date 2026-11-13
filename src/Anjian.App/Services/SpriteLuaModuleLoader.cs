using System.Reflection;
using NLua;

namespace Anjian.App.Services;

/// <summary>
/// A package-scoped Lua require implementation. Module names are mapped only to files
/// underneath the package's Modules directory; no arbitrary path or network access is exposed.
/// </summary>
internal sealed class SpriteLuaModuleLoader
{
    private const long MaximumModuleBytes = 2L * 1024 * 1024;
    private readonly string _moduleRoot;

    private SpriteLuaModuleLoader(string moduleRoot)
    {
        _moduleRoot = Path.GetFullPath(moduleRoot);
        if (!Directory.Exists(_moduleRoot))
            throw new DirectoryNotFoundException("小精灵模块目录不存在：" + _moduleRoot);
    }

    public static void Register(Lua lua, string moduleRoot)
    {
        ArgumentNullException.ThrowIfNull(lua);
        var loader = new SpriteLuaModuleLoader(moduleRoot);
        var method = typeof(SpriteLuaModuleLoader).GetMethod(
            nameof(LoadModuleSource), BindingFlags.Instance | BindingFlags.Public)
            ?? throw new MissingMethodException(nameof(LoadModuleSource));
        lua.RegisterFunction("__anjian_load_sprite_module", loader, method);
        lua.DoString("""
            local sourceLoader = __anjian_load_sprite_module
            __anjian_load_sprite_module = nil
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
                    local chunk, compileError = load(source, "@sprite-module/" .. name)
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
            """);
    }

    public string LoadModuleSource(string moduleName)
    {
        if (string.IsNullOrWhiteSpace(moduleName) || moduleName.Length > 180 ||
            moduleName.Contains('/') || moduleName.Contains('\\') || moduleName.Contains(':') ||
            moduleName.StartsWith('.') || moduleName.EndsWith('.') ||
            moduleName.Split('.').Any(part => part.Length == 0 || part is "." or ".." ||
                part.Any(character => !(char.IsLetterOrDigit(character) || character == '_' || character == '-'))))
            throw new InvalidDataException("Lua 模块名无效：" + moduleName);

        var relative = moduleName.Replace('.', Path.DirectorySeparatorChar);
        var candidates = new[]
        {
            Path.Combine(_moduleRoot, relative + ".lua"),
            Path.Combine(_moduleRoot, relative, "init.lua")
        };
        foreach (var candidate in candidates)
        {
            var fullPath = Path.GetFullPath(candidate);
            var prefix = _moduleRoot.EndsWith(Path.DirectorySeparatorChar)
                ? _moduleRoot
                : _moduleRoot + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Lua 模块路径越过小精灵模块目录。");
            if (!File.Exists(fullPath))
                continue;

            if ((File.GetAttributes(fullPath) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("不允许通过符号链接加载 Lua 模块。");
            var info = new FileInfo(fullPath);
            if (info.Length > MaximumModuleBytes)
                throw new InvalidDataException("Lua 模块超过 2 MiB 限制：" + moduleName);
            return File.ReadAllText(fullPath);
        }

        throw new FileNotFoundException("小精灵模块不存在：" + moduleName);
    }
}