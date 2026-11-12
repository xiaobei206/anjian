using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Anjian.Core.Api;
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
                LuaApiRuntime.Register(lua, api);
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

}

internal sealed class WorkerPipeClient(StreamReader reader, StreamWriter writer) : ILuaCommandPipe
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
