using System.Text.Json;
using Anjian.Core.Api;
using Anjian.Core.Windows;
using NLua;
using Xunit;
namespace Anjian.Core.Tests;
public sealed class LuaApiRegistryTests
{
 private static readonly string[] Expected=["MoveTo","LeftClick","RightClick","MiddleClick","LeftDoubleClick","RightDoubleClick","MiddleDoubleClick","LeftDown","LeftUp","RightDown","RightUp","MiddleDown","MiddleUp","MouseWheel","KeyPress","KeyDown","KeyUp","TypeText","Hotkey","Delay","RandomDelay","Log","WindowFind","WindowEnumerate","WindowEnumerateChildren","WindowGetInfo","WindowActivate","WindowShow","WindowHide","WindowMinimize","WindowMaximize","WindowRestore","WindowSetTopmost","WindowMove","WindowResize","WindowAlign","WindowWait","WindowWaitClosed","CoordinateToScreen","CoordinateFromScreen","WindowBind","WindowUnbind","WindowGetCapabilities","ControlEnumerate","ControlGetInfo","ControlGetValue","ControlSetValue","ControlGetText","ControlGetSelection","ControlSelect","ControlGetToggleState","ControlToggle","ControlInvoke","WindowInput"];
 [Fact] public void RegistryContainsOnlyImplementedPublicFunctions(){var r=LuaApiRegistry.Load();Assert.Equal(1,r.SchemaVersion);Assert.Equal("0.4",r.ApiVersion);Assert.Equal(Expected.Order(StringComparer.Ordinal),r.PublicFunctionNames.Order(StringComparer.Ordinal));Assert.All(r.Apis,a=>{Assert.Equal("implemented",a.ImplementationStatus);Assert.NotEmpty(a.Id);Assert.NotEmpty(a.Signature);Assert.NotEmpty(a.Example);Assert.NotEmpty(a.Errors);Assert.NotEmpty(a.Behavior.PauseBehavior);Assert.NotEmpty(a.Behavior.CancellationBehavior);Assert.NotEmpty(a.Behavior.CoordinateSpace);Assert.NotEmpty(a.Behavior.PermissionNotes);});Assert.DoesNotContain(r.Apis,a=>a.Name is "FindImage" or "OCR" or "CaptureScreen" or "MoveToWindow");var keys=r.Apis.Single(a=>a.Name=="KeyPress").Parameters.Single(p=>p.Name=="key").AllowedValues;Assert.Contains("NUMPAD0",keys);Assert.Contains("NUMPAD9",keys);Assert.DoesNotContain("NUMPAD0-9",keys);}
 [Fact] public void RuntimeCreatesCatalogGlobalsAndHidesBridgeNames(){var r=LuaApiRegistry.Load();using var lua=new Lua(openLibs:true);LuaApiRuntime.Register(lua,new WorkerLuaApi(new Recorder()));foreach(var a in r.Apis)Assert.Equal("function",lua.DoString($"return type({a.Name})")[0]?.ToString());foreach(var b in r.Apis.SelectMany(a=>a.Runtime.Bindings))Assert.Equal("nil",lua.DoString($"return type({b.Name})")[0]?.ToString());Assert.Equal("nil",lua.DoString("return type(FindWindow)")[0]?.ToString());Assert.Equal("nil",lua.DoString("return type(FindImage)")[0]?.ToString());Assert.Equal("nil",lua.DoString("return type(OCR)")[0]?.ToString());Assert.Equal("nil",lua.DoString("return type(os)")[0]?.ToString());Assert.Equal("nil",lua.DoString("return type(io)")[0]?.ToString());}
 [Fact]
 public void LuaCallsAndDefaultsMatchCatalog()
 {
  var pipe=new Recorder();
  using var lua=new Lua(openLibs:true);
  LuaApiRuntime.Register(lua,new WorkerLuaApi(pipe));
  lua.DoString("""
  MoveTo(-120,450);LeftClick();RightClick(0);MiddleClick(12)
  LeftDoubleClick();RightDoubleClick(1,2);MiddleDoubleClick(0,0)
  LeftDown();LeftUp();RightDown();RightUp();MiddleDown();MiddleUp()
  MouseWheel(-120);KeyPress("ENTER");KeyPress("F24",600000);KeyDown("CTRL");KeyUp("CTRL")
  TypeText("按键精灵 2026");Log("before-hotkey");Hotkey("CTRL","SHIFT","S");Log("after-hotkey")
  Delay(0);RandomDelay(0,0);Log("测试完成")
  """);
  Assert.True(pipe.Items.Any(m=>m.Type=="move"&&m.X==-120&&m.Y==450),"MoveTo should pass absolute coordinates.");
  Assert.True(pipe.Items.Any(m=>m.Type=="click"&&m.Name=="left"&&m.Ms==50),"LeftClick default duration should be 50ms.");
  Assert.True(pipe.Items.Any(m=>m.Type=="click"&&m.Name=="right"&&m.Ms==0),"RightClick(0) should preserve zero.");
  Assert.True(pipe.Items.Any(m=>m.Type=="click"&&m.Name=="middle"&&m.Ms==12),"MiddleClick should preserve explicit duration.");
  Assert.True(pipe.Items.Any(m=>m.Type=="click"&&m.Name=="right"&&m.Ms==1),"RightDoubleClick should preserve explicit duration.");
  Assert.True(pipe.Items.Any(m=>m.Type=="delay"&&m.Ms==100),"Default double-click gap expected Delay(100); actual commands: "+string.Join(" | ",pipe.Items.Select(m=>$"{m.Type}:{m.Name}:{m.Ms}:{m.Text}")));
  Assert.True(pipe.Items.Any(m=>m.Type=="key-press"&&m.Name=="ENTER"&&m.Ms==50),"KeyPress default duration should be 50ms.");
  Assert.True(pipe.Items.Any(m=>m.Type=="key-press"&&m.Name=="F24"&&m.Ms==600000),"KeyPress should accept the documented maximum duration.");
  Assert.True(pipe.Items.Any(m=>m.Type=="type-text"&&m.Text=="按键精灵 2026"),"TypeText should preserve Unicode text. Actual: "+string.Join(" | ",pipe.Items.Select(m=>$"Type={m.Type}; Name={m.Name}; Text={m.Text}")));
  Assert.True(pipe.Items.Any(m=>m.Type=="log"&&m.Text=="测试完成"),"Log should reach the runtime pipe.");
  var i=pipe.Items.FindIndex(m=>m.Type=="log"&&m.Text=="before-hotkey");
  Assert.True(i>=0,"Hotkey diagnostic marker was not recorded.");
  Assert.Equal(new (string Type,string? Name)[]{("key-down","CTRL"),("key-down","SHIFT"),("key-down","S"),("key-up","S"),("key-up","SHIFT"),("key-up","CTRL")},pipe.Items.Skip(i+1).Take(6).Select(m=>(m.Type,m.Name)).ToArray());
 }
 [Fact] public void InvalidRangeFractionalWheelAndEmptyHotkeyFail(){using var lua=new Lua(openLibs:true);LuaApiRuntime.Register(lua,new WorkerLuaApi(new Recorder()));var r=lua.DoString("""local a=pcall(LeftClick,600001);local b=pcall(KeyPress,"ENTER",600001);local c=pcall(MouseWheel,1.5);local d=pcall(Hotkey);local e=pcall(RandomDelay,300,100);return a,b,c,d,e""");Assert.Equal(5,r.Length);Assert.All(r,x=>Assert.False(Convert.ToBoolean(x)));}
 [Fact]
 public void WindowApiReturnsLuaTablesAndPreservesOpaqueReferenceId()
 {
  var pipe = new Recorder
  {
   Response = """{"id":"window-opaque-1","title":"目标窗口","className":"TestClass","processId":42,"processName":"test","visible":true,"minimized":false,"maximized":false,"foreground":true,"focused":true,"isChild":false,"windowRect":{"left":-100,"top":20,"right":500,"bottom":420,"width":600,"height":400,"hasArea":true},"clientRect":{"left":-92,"top":48,"right":492,"bottom":412,"width":584,"height":364,"hasArea":true},"destroyTracking":true}"""
  };
  using var lua = new Lua(openLibs:true);
  LuaApiRuntime.Register(lua, new WorkerLuaApi(pipe, lua));
  var values = lua.DoString("""
    local w = WindowFind("目标", "", "test", 42, true)
    local info = WindowGetInfo(w.id)
    return w.id, w.title, info.focused, info.isChild
    """);
  Assert.Equal("window-opaque-1", values[0]?.ToString());
  Assert.Equal("目标窗口", values[1]?.ToString());
  Assert.True(Convert.ToBoolean(values[2]));
  Assert.False(Convert.ToBoolean(values[3]));
  Assert.Contains(pipe.Items, item => item.Type == "window-find");
  Assert.Contains(pipe.Items, item => item.Type == "window-get-info");
 }

 [Fact]
 public void ControlAutomationApisUseWorkerLocalServiceAndKeepHwndOutOfLua()
 {
  var target = new WindowAutomationTarget("window-token", 123456, 42, 7, "ProbeWindow", 1234, false);
  var pipe = new Recorder { ResponseQueue = new Queue<string?>([JsonSerializer.Serialize(target, WindowApiJson.Options), "true"]) };
  var automation = new AutomationRecorder();
  using var lua = new Lua(openLibs:true);
  LuaApiRuntime.Register(lua, new WorkerLuaApi(pipe, lua, automation));
  var result = lua.DoString("""
    local binding = WindowBind("window-token")
    local info = ControlGetInfo(binding, "control-token")
    local value = ControlGetValue(binding, "control-token")
    ControlSetValue(binding, "control-token", "hello")
    ControlInvoke(binding, "control-token")
    WindowInput(binding, "auto", "click", "left", -40, 100, "", 25)
    WindowUnbind(binding)
    return value, info.controlId
    """);
  Assert.Equal("uia-value", result[0]?.ToString());
  Assert.Equal("control-token", result[1]?.ToString());
  Assert.Equal(target, automation.BoundTarget);
  Assert.Equal("hello", automation.LastSetValue);
  Assert.True(automation.WasInvoked);
  Assert.True(automation.WasUnbound);
  Assert.Contains(pipe.Items, item => item.Type == "window-automation-target");
  var input = pipe.Items.Single(item => item.Type == "window-input");
  Assert.Contains("\"windowId\":\"window-token\"", input.Text, StringComparison.Ordinal);
  Assert.Contains("\"x\":-40", input.Text, StringComparison.Ordinal);
 }

 [Fact]
 public void StringControlApiValuesAreReturnedWithoutJsonQuotes()
 {
  var automation = new AutomationRecorder();
  using var lua = new Lua(openLibs:true);
  LuaApiRuntime.Register(lua, new WorkerLuaApi(new Recorder(), lua, automation));
  var result = lua.DoString("return ControlGetText('binding-token', 'control-token')");
  Assert.Equal("uia-text", result[0]?.ToString());
 }

 [Fact]
 public void WindowInputRejectsMissingActionSpecificArgumentsBeforeSending()
 {
  using var lua = new Lua(openLibs:true);
  LuaApiRuntime.Register(lua, new WorkerLuaApi(new Recorder(), lua, new AutomationRecorder()));
  var result = lua.DoString("""
    local a = pcall(function() WindowInput("binding", "auto", "move") end)
    local b = pcall(function() WindowInput("binding", "auto", "click", "left") end)
    local c = pcall(function() WindowInput("binding", "auto", "text") end)
    return a, b, c
    """);
  Assert.All(result, item => Assert.False(Convert.ToBoolean(item)));
 }
 [Fact] public void ManualGeneratedRegionMatchesCatalog(){var root=FindRoot();var m=File.ReadAllText(Path.Combine(root,"docs","FUNCTION-REFERENCE-CN.md"));Assert.Equal(LuaApiRegistry.NormalizeNewlines(m),LuaApiRegistry.NormalizeNewlines(LuaApiRegistry.Load().UpdateGeneratedReferenceSection(m)));Assert.Contains("MoveTo(x, y)",m,StringComparison.Ordinal);Assert.Contains("600000",m,StringComparison.Ordinal);Assert.Contains("完整产品目标中的函数类别",m,StringComparison.Ordinal);Assert.Contains("目标设计",m,StringComparison.Ordinal);}
 private static string FindRoot(){foreach(var s in new[]{Environment.CurrentDirectory,AppContext.BaseDirectory}){var d=new DirectoryInfo(s);while(d is not null){if(File.Exists(Path.Combine(d.FullName,"docs","FUNCTION-REFERENCE-CN.md")))return d.FullName;d=d.Parent;}}throw new DirectoryNotFoundException();}
 private sealed class Recorder:ILuaCommandPipe
 {
  public List<Item> Items{get;}=[];
  public string? Response{get;set;}
  public Queue<string?> ResponseQueue{get;set;}=[];
  public void Send(string type,string? name=null,double x=0,double y=0,int milliseconds=0,string? text=null) =>
    Items.Add(new Item(type,name,x,y,milliseconds,text));
  public string? Request(string type,string? name=null,double x=0,double y=0,int milliseconds=0,string? text=null)
  {
   Items.Add(new Item(type,name,x,y,milliseconds,text));
   return ResponseQueue.Count>0?ResponseQueue.Dequeue():Response;
  }
 }
 private sealed record Item(string Type,string? Name,double X,double Y,int Ms,string? Text);

 private sealed class AutomationRecorder : IWindowAutomationService
 {
  public WindowAutomationTarget? BoundTarget { get; private set; }
  public string? LastSetValue { get; private set; }
  public bool WasInvoked { get; private set; }
  public bool WasUnbound { get; private set; }
  public string Bind(WindowAutomationTarget target) { BoundTarget=target; return "binding-token"; }
  public string GetWindowId(string bindingId) => "window-token";
  public bool Unbind(string bindingId) { WasUnbound=true; return true; }
  public void ClearBindings() { }
  public WindowAutomationCapabilities GetCapabilities(string bindingId) =>
    new(bindingId,"test-provider",42,true,Array.Empty<AutomationCapability>());
  public IReadOnlyList<ControlElementInfo> Enumerate(string bindingId,int maxResults) => Array.Empty<ControlElementInfo>();
  public ControlElementInfo GetInfo(string bindingId,string controlId) =>
    new(controlId,"Probe","ProbeAutomationId","Edit","ControlType.Edit",
      new PixelRect(1,2,3,4),true,false,true,false,Array.Empty<string>());
  public string GetValue(string bindingId,string controlId) => "uia-value";
  public void SetValue(string bindingId,string controlId,string value) => LastSetValue=value;
  public string GetText(string bindingId,string controlId) => "uia-text";
  public IReadOnlyList<string> GetSelection(string bindingId,string controlId) => ["Selected"];
  public void Select(string bindingId,string controlId) { }
  public string GetToggleState(string bindingId,string controlId) => "On";
  public void Toggle(string bindingId,string controlId) { }
  public void Invoke(string bindingId,string controlId) => WasInvoked=true;
  public void Dispose() { }
 }

}
