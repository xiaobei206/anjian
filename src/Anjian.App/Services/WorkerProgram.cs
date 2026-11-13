using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Anjian.Core.Api;
using Anjian.Core.Editor;
using Anjian.Core.Parameters;
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
            var launchPayload = client.Request("worker-ready");
            var launch = string.IsNullOrWhiteSpace(launchPayload)
                ? new WorkerLaunchPayload()
                : JsonSerializer.Deserialize<WorkerLaunchPayload>(launchPayload)
                    ?? throw new InvalidDataException("主进程返回了无效的启动参数。");
            var debugOptions = launch.DebugOptions;

            try
            {
                using var automation = new WindowsControlAutomationProvider(client);
                using var ocr = new PaddleOcrRecognitionService(launch.OcrModelDirectory);
                using var lua = new NLua.Lua(openLibs: true);
                var api = new WorkerLuaApi(client, lua, automation, ocr, launch.Permissions);
                LuaApiRuntime.Register(lua, api);
                if (!string.IsNullOrWhiteSpace(launch.ModuleRootPath))
                    SpriteLuaModuleLoader.Register(lua, launch.ModuleRootPath);
                LuaParameterInjector.Inject(lua, launch.Parameters);
                var fullPath = Path.GetFullPath(scriptPath);
                if (debugOptions is null)
                {
                    lua.SetDebugHook(LuaHookMask.Count, 10_000);
                    lua.DebugHook += (_, _) => client.Send("checkpoint");
                    lua.DoString(File.ReadAllText(fullPath), "@" + fullPath);
                }
                else
                {
                    using var debugger = new LuaDebugSession(
                        lua,
                        fullPath,
                        debugOptions,
                        checkpoint: () => client.Send("checkpoint"),
                        waitForCommand: snapshot =>
                        {
                            var commandPayload = client.Request(
                                "debug-break",
                                text: JsonSerializer.Serialize(snapshot));
                            if (string.IsNullOrWhiteSpace(commandPayload))
                                throw new IOException("调试器未返回控制命令。");
                            return JsonSerializer.Deserialize<LuaDebugCommand>(commandPayload)
                                ?? throw new InvalidDataException("调试器返回了无效控制命令。");
                        });
                    lua.DoString(File.ReadAllText(fullPath), "@" + fullPath);
                    debugger.ThrowIfFaulted();
                }

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
    public void Send(string type, string? name = null, double x = 0, double y = 0, int milliseconds = 0, string? text = null) =>
        _ = Request(type, name, x, y, milliseconds, text);

    public string? Request(string type, string? name = null, double x = 0, double y = 0, int milliseconds = 0, string? text = null)
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
        var root = document.RootElement;
        if (!root.GetProperty("ok").GetBoolean())
            throw new InvalidOperationException(root.TryGetProperty("error", out var error)
                ? error.GetString()
                : "自动化命令失败。");

        return root.TryGetProperty("payload", out var payload) && payload.ValueKind == JsonValueKind.String
            ? payload.GetString()
            : null;
    }
}
