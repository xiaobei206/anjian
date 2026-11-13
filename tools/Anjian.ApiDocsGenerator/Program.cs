using System.Text;
using Anjian.Core.Api;
using Anjian.Core.Plugins;
if (args.Length == 3 && args[0].Equals("--write-plugin-reference", StringComparison.OrdinalIgnoreCase))
{
    var builtIn = LuaApiRegistry.Load();
    var store = new PluginPackageStore(args[1], builtIn.ApiVersion);
    var merged = builtIn.WithPluginContributions(store.GetEnabledApiContributionPlans());
    var output = Path.GetFullPath(args[2]);
    Directory.CreateDirectory(Path.GetDirectoryName(output)!);
    var document = "# 当前启用插件 API 参考\\n\\n> 由本机已启用插件清单生成；仅包含经过兼容性、文件清单与 SHA-256 校验的纯 Lua helper。\\n\\n" +
        merged.RenderGeneratedReferenceSection();
    File.WriteAllText(output, document, new UTF8Encoding(false));
    Console.WriteLine($"已写入插件 API 参考：{output}（{merged.Apis.Count} 项，其中内置 {builtIn.Apis.Count} 项）。");
    return 0;
}
var write=args.Contains("--write",StringComparer.OrdinalIgnoreCase);var check=args.Contains("--check",StringComparer.OrdinalIgnoreCase);
if(write==check){Console.Error.WriteLine("用法：dotnet run --project tools/Anjian.ApiDocsGenerator -- --write|--check；或 --write-plugin-reference <plugin-root> <output.md>");return 2;}
var root=FindRoot();var path=Path.Combine(root,"docs","FUNCTION-REFERENCE-CN.md");var original=File.ReadAllText(path);var registry=LuaApiRegistry.Load();var updated=registry.UpdateGeneratedReferenceSection(original);
if(check){if(!string.Equals(LuaApiRegistry.NormalizeNewlines(original),LuaApiRegistry.NormalizeNewlines(updated),StringComparison.Ordinal)){Console.Error.WriteLine("函数手册生成区与 API 注册表不一致，请运行 --write。");return 1;}Console.WriteLine($"API 文档生成区一致：{registry.Apis.Count} 个当前公开函数。");return 0;}
File.WriteAllText(path,updated,new UTF8Encoding(false));Console.WriteLine($"已生成函数手册：{registry.Apis.Count} 个当前公开函数。");return 0;
static string FindRoot(){foreach(var start in new[]{Environment.CurrentDirectory,AppContext.BaseDirectory}){var d=new DirectoryInfo(start);while(d is not null){if(File.Exists(Path.Combine(d.FullName,"docs","FUNCTION-REFERENCE-CN.md")))return d.FullName;d=d.Parent;}}throw new DirectoryNotFoundException("未找到 docs/FUNCTION-REFERENCE-CN.md。");}
