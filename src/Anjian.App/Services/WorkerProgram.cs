using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using KeraLua;
using NLua;

namespace Anjian.App.Services;

internal static class WorkerProgram
{
    public static int Run(string[] args)
    {
        if (args.Length < 3)
            return 64;

        var pipeName = args[1];
        var scriptPath = args[2];
        try
        {
            using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.None);
            pipe.Connect(10_000);
            using var reader = new StreamReader(pipe, new UTF8Encoding(false), false, 4096, leaveOpen: true);
            using var writer = new StreamWriter(pipe, new UTF8Encoding(false), 4096, leaveOpen: true) { AutoFlush = true };
            var client = new WorkerPipeClient(reader, writer);

            try
            {
                using var lua = new NLua.Lua(openLibs: true);
                var api = new WorkerLuaApi(client);
                Register(lua, api);
                lua.SetDebugHook(LuaHookMask.Count, 10_000);
                lua.DebugHook += (_, _) => client.Send("checkpoint");
                lua.DoString(File.ReadAllText(scriptPath), "@" + Path.GetFileName(scriptPath));
                return 0;
            }
            catch (Exception ex)
            {
                try { client.Send("error", text: ex.ToString()); } catch { }
                return 1;
            }
        }
        catch
        {
            return 70;
        }
    }

    private static void Register(NLua.Lua lua, WorkerLuaApi api)
    {
        void Reg(string luaName, string methodName) =>
            lua.RegisterFunction(luaName, api, typeof(WorkerLuaApi).GetMethod(methodName)!);

        Reg("__MoveTo", nameof(WorkerLuaApi.MoveTo));
        Reg("__LeftClick", nameof(WorkerLuaApi.LeftClick));
        Reg("__RightClick", nameof(WorkerLuaApi.RightClick));
        Reg("__MiddleClick", nameof(WorkerLuaApi.MiddleClick));
        Reg("__LeftDown", nameof(WorkerLuaApi.LeftDown));
        Reg("__LeftUp", nameof(WorkerLuaApi.LeftUp));
        Reg("__RightDown", nameof(WorkerLuaApi.RightDown));
        Reg("__RightUp", nameof(WorkerLuaApi.RightUp));
        Reg("__MiddleDown", nameof(WorkerLuaApi.MiddleDown));
        Reg("__MiddleUp", nameof(WorkerLuaApi.MiddleUp));
        Reg("__MouseWheel", nameof(WorkerLuaApi.MouseWheel));
        Reg("__KeyPress", nameof(WorkerLuaApi.KeyPress));
        Reg("__KeyDown", nameof(WorkerLuaApi.KeyDown));
        Reg("__KeyUp", nameof(WorkerLuaApi.KeyUp));
        Reg("__TypeText", nameof(WorkerLuaApi.TypeText));
        Reg("__Delay", nameof(WorkerLuaApi.Delay));
        Reg("__Log", nameof(WorkerLuaApi.Log));
        Reg("__RandomDelay", nameof(WorkerLuaApi.RandomDelay));

        lua.DoString("""
            MoveTo = function(x, y) __MoveTo(x, y) end
            LeftClick = function(ms) __LeftClick(ms == nil and 50 or ms) end
            RightClick = function(ms) __RightClick(ms == nil and 50 or ms) end
            MiddleClick = function(ms) __MiddleClick(ms == nil and 50 or ms) end
            LeftDown = function() __LeftDown() end
            LeftUp = function() __LeftUp() end
            RightDown = function() __RightDown() end
            RightUp = function() __RightUp() end
            MiddleDown = function() __MiddleDown() end
            MiddleUp = function() __MiddleUp() end
            MouseWheel = function(delta) __MouseWheel(delta) end
            KeyPress = function(key, ms) __KeyPress(key, ms == nil and 50 or ms) end
            KeyDown = function(key) __KeyDown(key) end
            KeyUp = function(key) __KeyUp(key) end
            TypeText = function(text) __TypeText(text) end
            Delay = function(ms) __Delay(ms) end
            Log = function(text) __Log(tostring(text)) end
            RandomDelay = function(a, b) __RandomDelay(a, b) end
            Hotkey = function(...)
                local keys = {...}
                for i = 1, #keys do KeyDown(keys[i]) end
                for i = #keys, 1, -1 do KeyUp(keys[i]) end
            end
            os = nil; io = nil; package = nil; debug = nil
            require = nil; dofile = nil; loadfile = nil
            """);
        lua["luanet"] = null;
    }
}

internal sealed class WorkerPipeClient(StreamReader reader, StreamWriter writer)
{
    public void Send(string type, string? name = null, double x = 0, double y = 0, int milliseconds = 0, string? text = null)
    {
        var message = JsonSerializer.Serialize(new IpcMessage
        {
            Type = type, Name = name, X = x, Y = y, Milliseconds = milliseconds, Text = text
        });
        writer.WriteLine(message);
        var response = reader.ReadLine();
        if (response is null)
            throw new IOException("脚本执行主进程已断开。");
        using var document = JsonDocument.Parse(response);
        if (!document.RootElement.GetProperty("ok").GetBoolean())
            throw new InvalidOperationException(document.RootElement.TryGetProperty("error", out var error) ? error.GetString() : "自动化命令失败。");
    }
}

internal sealed class WorkerLuaApi(WorkerPipeClient pipe)
{
    public void MoveTo(double x, double y) => pipe.Send("move", x: Coordinate(x), y: Coordinate(y));
    public void LeftClick(double ms) => pipe.Send("click", "left", milliseconds: Duration(ms));
    public void RightClick(double ms) => pipe.Send("click", "right", milliseconds: Duration(ms));
    public void MiddleClick(double ms) => pipe.Send("click", "middle", milliseconds: Duration(ms));
    public void LeftDown() => pipe.Send("mouse-down", "left");
    public void LeftUp() => pipe.Send("mouse-up", "left");
    public void RightDown() => pipe.Send("mouse-down", "right");
    public void RightUp() => pipe.Send("mouse-up", "right");
    public void MiddleDown() => pipe.Send("mouse-down", "middle");
    public void MiddleUp() => pipe.Send("mouse-up", "middle");
    public void MouseWheel(double delta) => pipe.Send("wheel", milliseconds: checked((int)delta));
    public void KeyPress(string key, double ms) => pipe.Send("key-press", key, milliseconds: Duration(ms));
    public void KeyDown(string key) => pipe.Send("key-down", key);
    public void KeyUp(string key) => pipe.Send("key-up", key);
    public void TypeText(string text) => pipe.Send("type-text", text: text);
    public void Delay(double ms) => pipe.Send("delay", milliseconds: Duration(ms));
    public void Log(string text) => pipe.Send("log", text: text);

    public void RandomDelay(double minimum, double maximum)
    {
        var min = Duration(minimum);
        var max = Duration(maximum);
        if (max < min) throw new ArgumentException("RandomDelay 的最大值不能小于最小值。");
        pipe.Send("delay", milliseconds: Random.Shared.Next(min, checked(max + 1)));
    }

    private static int Duration(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value < 0 || value > 600_000 || value != Math.Truncate(value))
            throw new ArgumentOutOfRangeException(nameof(value), "时间必须是 0 到 600000 之间的整数毫秒。");
        return checked((int)value);
    }

    private static int Coordinate(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value < int.MinValue || value > int.MaxValue || value != Math.Truncate(value))
            throw new ArgumentOutOfRangeException(nameof(value), "坐标必须是整数像素。");
        return checked((int)value);
    }
}
