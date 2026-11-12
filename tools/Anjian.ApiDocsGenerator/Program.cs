using System.Text;
using Anjian.Core.Api;
var write=args.Contains("--write",StringComparer.OrdinalIgnoreCase);var check=args.Contains("--check",StringComparer.OrdinalIgnoreCase);
if(write==check){Console.Error.WriteLine("用法：dotnet run --project tools/Anjian.ApiDocsGenerator -- --write|--check");return 2;}
var root=FindRoot();var path=Path.Combine(root,"docs","FUNCTION-REFERENCE-CN.md");var original=File.ReadAllText(path);var registry=LuaApiRegistry.Load();var updated=registry.UpdateGeneratedReferenceSection(original);
if(check){if(!string.Equals(LuaApiRegistry.NormalizeNewlines(original),LuaApiRegistry.NormalizeNewlines(updated),StringComparison.Ordinal)){Console.Error.WriteLine("函数手册生成区与 API 注册表不一致，请运行 --write。");return 1;}Console.WriteLine($"API 文档生成区一致：{registry.Apis.Count} 个当前公开函数。");return 0;}
File.WriteAllText(path,updated,new UTF8Encoding(false));Console.WriteLine($"已生成函数手册：{registry.Apis.Count} 个当前公开函数。");return 0;
static string FindRoot(){foreach(var start in new[]{Environment.CurrentDirectory,AppContext.BaseDirectory}){var d=new DirectoryInfo(start);while(d is not null){if(File.Exists(Path.Combine(d.FullName,"docs","FUNCTION-REFERENCE-CN.md")))return d.FullName;d=d.Parent;}}throw new DirectoryNotFoundException("未找到 docs/FUNCTION-REFERENCE-CN.md。");}
