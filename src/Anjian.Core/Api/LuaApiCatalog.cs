using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using NLua;
using Anjian.Core.Windows;
using Anjian.Core.Ocr;
using Anjian.Core.Permissions;
using Anjian.Core.Plugins;

namespace Anjian.Core.Api;

public sealed class LuaApiRegistry
{
    private const string ResourceName="Anjian.Core.Api.lua-api-registry.json";
    public const string GeneratedStartMarker="<!-- BEGIN GENERATED API REFERENCE -->";
    public const string GeneratedEndMarker="<!-- END GENERATED API REFERENCE -->";
    private const char Tick=(char)96;
    public int SchemaVersion{get;init;}
    public string ApiVersion{get;init;}="";
    public string Product{get;init;}="";
    public List<LuaApiCategory> Categories{get;init;}=[];
    public List<LuaApiDefinition> Apis{get;init;}=[];
    public LuaRuntimeDefinition Runtime{get;init;}=new();
    public IReadOnlyList<string> PublicFunctionNames=>Apis.Select(x=>x.Name).ToArray();
    public static LuaApiRegistry Load()
    {
        using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)??throw new InvalidOperationException($"Missing embedded registry: {ResourceName}");
        var registry=JsonSerializer.Deserialize<LuaApiRegistry>(stream,new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??throw new InvalidDataException("API registry JSON is empty.");
        registry.Validate();return registry;
    }
    public void Validate()
    {
        if(SchemaVersion!=1||string.IsNullOrWhiteSpace(ApiVersion)||string.IsNullOrWhiteSpace(Product)||Apis.Count==0||Categories.Count==0)throw new InvalidDataException("API registry root fields are invalid.");
        Unique(Categories.Select(x=>x.Id),"category ID");Unique(Apis.Select(x=>x.Id),"API ID");Unique(Apis.Select(x=>x.Name),"public function name");
        var categoryIds=Categories.Select(x=>x.Id).ToHashSet(StringComparer.Ordinal);var bridgeNames=new HashSet<string>(StringComparer.Ordinal);var localNames=new HashSet<string>(StringComparer.Ordinal);
        foreach(var a in Apis)
        {
            if(string.IsNullOrWhiteSpace(a.Id)||string.IsNullOrWhiteSpace(a.Name)||string.IsNullOrWhiteSpace(a.Signature)||string.IsNullOrWhiteSpace(a.Title)||string.IsNullOrWhiteSpace(a.Description)||string.IsNullOrWhiteSpace(a.Example))throw new InvalidDataException("Each API needs an ID, name, signature, title, description, and example.");
            if(!categoryIds.Contains(a.Category)||a.ImplementationStatus!="implemented")throw new InvalidDataException($"Invalid category or unimplemented API: {a.Name}");
            if(string.IsNullOrWhiteSpace(a.IntroducedIn)||string.IsNullOrWhiteSpace(a.Returns.Type)||string.IsNullOrWhiteSpace(a.Returns.Description)||a.Errors.Count==0)throw new InvalidDataException($"Missing version/return/error contract: {a.Name}");
            if(string.IsNullOrWhiteSpace(a.Behavior.PauseBehavior)||string.IsNullOrWhiteSpace(a.Behavior.CancellationBehavior)||string.IsNullOrWhiteSpace(a.Behavior.CoordinateSpace)||string.IsNullOrWhiteSpace(a.Behavior.PermissionNotes))throw new InvalidDataException($"Missing runtime semantics: {a.Name}");
            if(string.IsNullOrWhiteSpace(a.Runtime.Definition))throw new InvalidDataException($"Missing Lua wrapper: {a.Name}");
            Unique(a.Parameters.Select(p=>p.Name),$"parameter name {a.Name}");
            foreach(var p in a.Parameters)
            {
                if(string.IsNullOrWhiteSpace(p.Name)||string.IsNullOrWhiteSpace(p.Type)||string.IsNullOrWhiteSpace(p.Description))throw new InvalidDataException($"Incomplete parameter in {a.Name}.");
                if(!p.Required&&(!p.DefaultValue.HasValue||p.DefaultValue.Value.ValueKind==JsonValueKind.Null))throw new InvalidDataException($"Optional parameter needs default: {a.Name}.{p.Name}");
                if(p.Minimum.HasValue&&p.Maximum.HasValue&&p.Minimum.Value>p.Maximum.Value)throw new InvalidDataException($"Invalid range: {a.Name}.{p.Name}");
                if(p.Variadic&&p.MinimumCount.GetValueOrDefault()<1)throw new InvalidDataException($"Variadic parameter minimum missing: {a.Name}.{p.Name}");
            }
            foreach(var b in a.Runtime.Bindings)
            {
                if(!b.Name.StartsWith("__",StringComparison.Ordinal)||!b.LocalName.StartsWith("__AnjianBridge_",StringComparison.Ordinal)||string.IsNullOrWhiteSpace(b.Method))throw new InvalidDataException($"Invalid internal binding for {a.Name}.");
                if(!bridgeNames.Add(b.Name)||!localNames.Add(b.LocalName))throw new InvalidDataException("Duplicate internal bridge name.");
            }
        }
        Unique(Runtime.DisabledGlobals,"hidden global");
    }
    public LuaApiRegistry WithPluginContributions(IEnumerable<PluginApiRuntimePlan> plans)
    {
        ArgumentNullException.ThrowIfNull(plans);
        var entries = plans.SelectMany(plan => (plan.Functions ?? [])
            .Select(function => (Plan: plan, Function: function))).ToArray();
        if (entries.Length == 0)
            return this;

        var categories = Categories.ToList();
        if (categories.Any(category => category.Id.Equals("plugins", StringComparison.Ordinal)))
            throw new InvalidDataException("内置 API registry 已占用 plugins 分类。");
        categories.Add(new LuaApiCategory
        {
            Id = "plugins",
            Title = "本地插件函数",
            Description = "来自已启用本地插件的纯 Lua helper；不扩展宿主权限。"
        });

        var apis = Apis.ToList();
        var contributedNames = new HashSet<string>(Apis.Select(api => api.Name), StringComparer.OrdinalIgnoreCase);
        foreach (var (plan, function) in entries)
        {
            if (!contributedNames.Add(function.Name))
                throw new InvalidDataException("插件 API 名称与内置函数或其他启用插件冲突：" + function.Name);
            apis.Add(new LuaApiDefinition
            {
                Id = $"plugin:{plan.PluginId}:{function.Id}",
                Name = function.Name,
                Category = "plugins",
                Title = function.Title,
                Description = $"[{plan.PluginId} {plan.Version}] {function.Description}",
                Signature = function.Signature,
                Parameters = function.Parameters,
                Returns = function.Returns,
                Errors = function.Errors,
                Behavior = function.Behavior,
                Example = function.Example,
                ImplementationStatus = "implemented",
                VerificationStatus = "plugin-declared-runtime-contract",
                IntroducedIn = $"plugin:{plan.PluginId}@{plan.Version}",
                Runtime = new LuaApiRuntimeDefinition { Definition = "verified-plugin-lua-helper", Bindings = [] }
            });
        }

        var merged = new LuaApiRegistry
        {
            SchemaVersion = SchemaVersion,
            ApiVersion = ApiVersion,
            Product = Product,
            Categories = categories,
            Apis = apis,
            Runtime = Runtime
        };
        merged.Validate();
        if (merged.Apis.Select(api => api.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != merged.Apis.Count)
            throw new InvalidDataException("插件 API 名称存在大小写冲突。");
        return merged;
    }

    public string RenderGeneratedReferenceSection()
    {
        var lines=new List<string>();var group=1;
        foreach(var c in Categories)
        {
            var items=Apis.Where(a=>a.Category==c.Id).ToArray();if(items.Length==0)continue;
            lines.Add($"### 2.{group} {c.Title}");lines.Add("");
            foreach(var a in items)
            {
                lines.Add($"#### {Tick}{a.Signature}{Tick} — {a.Title}");lines.Add("");
                lines.Add($"- **稳定 ID：** {Tick}{a.Id}{Tick}");
                lines.Add("- **状态：** 当前已实现；自动化 API 契约测试覆盖，Windows 桌面实机验收待完成。");
                lines.Add($"- **说明：** {a.Description}");lines.Add("");
                if(a.Parameters.Count>0)
                {
                    lines.Add("| 参数 | Lua 类型 | 必填 | 默认值 | 范围/单位/选项 | 说明 |");lines.Add("|---|---|---|---|---|---|");
                    foreach(var p in a.Parameters)lines.Add($"| {Tick}{Escape(p.Name)}{Tick} | {Tick}{Escape(p.Type)}{Tick} | {(p.Required?"是":"否")} | {Default(p)} | {Range(p)} | {Escape(p.Description)} |");
                    lines.Add("");
                }else{lines.Add("**参数：** 无。");lines.Add("");}
                lines.Add($"**返回值：** {Tick}{Escape(a.Returns.Type)}{Tick} — {a.Returns.Description}");
                lines.Add($"**错误：** {string.Join("；",a.Errors.Select(Escape))}");
                lines.Add($"**执行/取消语义：** {a.Behavior.PauseBehavior} {a.Behavior.CancellationBehavior}");
                lines.Add($"**坐标/权限：** 坐标空间：{a.Behavior.CoordinateSpace}。{a.Behavior.PermissionNotes}");
                lines.Add("");lines.Add("**示例：**");lines.Add(new string(Tick,3)+"lua");lines.Add(a.Example);lines.Add(new string(Tick,3));lines.Add("");
            }group++;
        }return string.Join("\n",lines).TrimEnd()+"\n";
    }
    public string UpdateGeneratedReferenceSection(string manual)
    {
        ArgumentNullException.ThrowIfNull(manual);var start=manual.IndexOf(GeneratedStartMarker,StringComparison.Ordinal);var end=manual.IndexOf(GeneratedEndMarker,StringComparison.Ordinal);
        if(start<0||end<0||end<=start)throw new InvalidDataException("Function reference generated markers are missing.");
        return manual[..(start+GeneratedStartMarker.Length)]+"\n\n"+RenderGeneratedReferenceSection().TrimEnd()+"\n\n"+manual[end..];
    }
    public static string NormalizeNewlines(string s)=>s.Replace("\r\n","\n",StringComparison.Ordinal).Replace('\r','\n');
    private static void Unique(IEnumerable<string> values,string label){var used=new HashSet<string>(StringComparer.Ordinal);foreach(var v in values)if(string.IsNullOrWhiteSpace(v)||!used.Add(v))throw new InvalidDataException($"Empty or duplicate {label}: {v}");}
    private static string Escape(string s)=>s.Replace("|","\\|",StringComparison.Ordinal).Replace("\r"," ",StringComparison.Ordinal).Replace("\n"," ",StringComparison.Ordinal);
    private static string Default(LuaApiParameter p){if(!p.DefaultValue.HasValue||p.DefaultValue.Value.ValueKind==JsonValueKind.Null)return "—";var v=p.DefaultValue.Value;return v.ValueKind switch{JsonValueKind.String=>Tick+Escape(v.GetString()??"")+Tick,JsonValueKind.True=>Tick+"true"+Tick,JsonValueKind.False=>Tick+"false"+Tick,_=>Tick+v.ToString()+Tick};}
    private static string Range(LuaApiParameter p)
    {
        var result=new List<string>();
        if(p.Minimum.HasValue||p.Maximum.HasValue){var min=p.Minimum?.ToString(CultureInfo.InvariantCulture)??"−∞";var max=p.Maximum?.ToString(CultureInfo.InvariantCulture)??"∞";result.Add($"{min}–{max}{(string.IsNullOrWhiteSpace(p.Unit)?"":" "+p.Unit)}");}
        else if(p.AllowedValues.Count>0||p.ValuePatterns.Count>0){if(p.AllowedValues.Count>0)result.Add("固定值："+string.Join("、",p.AllowedValues));if(p.ValuePatterns.Count>0)result.Add("规则："+string.Join("、",p.ValuePatterns));}
        else if(p.MinimumCount.HasValue)result.Add($"至少 {p.MinimumCount.Value} 项");
        if(p.Variadic)result.Add("可变参数");return Escape(result.Count==0?"—":string.Join("；",result));
    }
}
public sealed class LuaApiCategory{public string Id{get;init;}="";public string Title{get;init;}="";public string Description{get;init;}="";}
public sealed class LuaApiDefinition
{
 public string Id{get;init;}="";public string Name{get;init;}="";public string Category{get;init;}="";public string Title{get;init;}="";public string Description{get;init;}="";public string Signature{get;init;}="";
 public List<LuaApiParameter> Parameters{get;init;}=[];public LuaApiReturn Returns{get;init;}=new();public List<string> Errors{get;init;}=[];public LuaApiBehavior Behavior{get;init;}=new();
 public string Example{get;init;}="";public string ImplementationStatus{get;init;}="";public string VerificationStatus{get;init;}="";public string IntroducedIn{get;init;}="";public LuaApiRuntimeDefinition Runtime{get;init;}=new();
}
public sealed class LuaApiParameter{public string Name{get;init;}="";public string Type{get;init;}="";public bool Required{get;init;}public JsonElement? DefaultValue{get;init;}public double? Minimum{get;init;}public double? Maximum{get;init;}public string? Unit{get;init;}public List<string> AllowedValues{get;init;}=[];public List<string> ValuePatterns{get;init;}=[];public int? MinimumCount{get;init;}public bool Variadic{get;init;}public string Description{get;init;}="";}
public sealed class LuaApiReturn{public string Type{get;init;}="";public string Description{get;init;}="";}
public sealed class LuaApiBehavior{public string PauseBehavior{get;init;}="";public string CancellationBehavior{get;init;}="";public List<string> SideEffects{get;init;}=[];public string CoordinateSpace{get;init;}="";public string PermissionNotes{get;init;}="";}
public sealed class LuaApiRuntimeDefinition{public string Definition{get;init;}="";public List<LuaApiBridgeBinding> Bindings{get;init;}=[];}
public sealed class LuaApiBridgeBinding{public string Name{get;init;}="";public string Method{get;init;}="";public string LocalName{get;init;}="";}
public sealed class LuaRuntimeDefinition{public string Prelude{get;init;}="";public List<string> DisabledGlobals{get;init;}=[];}

public static class LuaApiRuntime
{
 public static void Register(Lua lua,object bridge)
 {
  ArgumentNullException.ThrowIfNull(lua);ArgumentNullException.ThrowIfNull(bridge);lua.State.Encoding=Encoding.UTF8;var r=LuaApiRegistry.Load();var bindings=r.Apis.SelectMany(a=>a.Runtime.Bindings).ToArray();var names=new HashSet<string>(StringComparer.Ordinal);
  foreach(var b in bindings){if(!names.Add(b.Name))throw new InvalidDataException($"Duplicate bridge: {b.Name}");var method=bridge.GetType().GetMethod(b.Method,BindingFlags.Instance|BindingFlags.Public)??throw new InvalidDataException($"Missing bridge method: {b.Method}");lua.RegisterFunction(b.Name,bridge,method);}
  var source=new StringBuilder();foreach(var b in bindings)source.Append("local ").Append(b.LocalName).Append(" = ").Append(b.Name).Append('\n');if(!string.IsNullOrWhiteSpace(r.Runtime.Prelude))source.AppendLine(r.Runtime.Prelude);foreach(var a in r.Apis)source.Append(a.Name).Append(" = ").Append(a.Runtime.Definition).Append('\n');
  lua.DoString(source.ToString(),"@Anjian2026.ApiRegistry");foreach(var n in bindings.Select(b=>b.Name).Concat(r.Runtime.DisabledGlobals).Distinct(StringComparer.Ordinal))lua[n]=null;lua["luanet"]=null;
 }
}
public interface ILuaCommandPipe{void Send(string type,string? name=null,double x=0,double y=0,int milliseconds=0,string? text=null);string? Request(string type,string? name=null,double x=0,double y=0,int milliseconds=0,string? text=null);}
public sealed partial class WorkerLuaApi(ILuaCommandPipe pipe, Lua? lua = null, IWindowAutomationService? automation = null, IOcrRecognitionService? ocrService = null, ScriptPermissionGrant? permissions = null, string? scriptDirectory = null)
{
  internal ScriptPermissionGrant Permissions { get; } = permissions ?? ScriptPermissionGrant.Empty;
  private string ScriptDirectory { get; } = Path.GetFullPath(scriptDirectory ?? Environment.CurrentDirectory);
 public void MoveTo(double x,double y)=>pipe.Send("move",x:Coord(x),y:Coord(y));
 public void LeftClick(double ms)=>pipe.Send("click","left",milliseconds:Duration(ms));public void RightClick(double ms)=>pipe.Send("click","right",milliseconds:Duration(ms));public void MiddleClick(double ms)=>pipe.Send("click","middle",milliseconds:Duration(ms));
 public void LeftDown()=>pipe.Send("mouse-down","left");public void LeftUp()=>pipe.Send("mouse-up","left");public void RightDown()=>pipe.Send("mouse-down","right");public void RightUp()=>pipe.Send("mouse-up","right");public void MiddleDown()=>pipe.Send("mouse-down","middle");public void MiddleUp()=>pipe.Send("mouse-up","middle");
 public void MouseWheel(double delta)=>pipe.Send("wheel",milliseconds:Integer(delta,int.MinValue,int.MaxValue,"滚轮增量"));
 public void KeyPress(string key,double ms)=>pipe.Send("key-press",KeyName(key),milliseconds:Duration(ms));public void KeyDown(string key)=>pipe.Send("key-down",KeyName(key));public void KeyUp(string key)=>pipe.Send("key-up",KeyName(key));
 public void TypeText(string text){ArgumentNullException.ThrowIfNull(text);pipe.Send("type-text",text:text);}public void Delay(double ms)=>pipe.Send("delay",milliseconds:Duration(ms));public void Log(string text)=>pipe.Send("log",text:text);
 public void RandomDelay(double minimum,double maximum){var min=Duration(minimum);var max=Duration(maximum);if(max<min)throw new ArgumentException("RandomDelay 的最大值不能小于最小值。");pipe.Send("delay",milliseconds:Random.Shared.Next(min,checked(max+1)));}
 private static int Duration(double x)=>Integer(x,0,600000,"时间");private static int Coord(double x)=>Integer(x,int.MinValue,int.MaxValue,"坐标");
 private static int Integer(double x,int min,int max,string label){if(double.IsNaN(x)||double.IsInfinity(x)||x<min||x>max||x!=Math.Truncate(x))throw new ArgumentOutOfRangeException(nameof(x),$"{label}必须是 {min} 到 {max} 之间的整数。");return checked((int)x);}
 private static string KeyName(string? key){if(string.IsNullOrWhiteSpace(key))throw new ArgumentException("按键名称不能为空。",nameof(key));return key.Trim();}
}
