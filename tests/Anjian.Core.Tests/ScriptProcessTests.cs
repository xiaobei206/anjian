using System.Diagnostics;
using System.Text.Json;
using Anjian.Core.Api;
using Anjian.Core.Permissions;
using NLua;
using Xunit;

namespace Anjian.Core.Tests;

public sealed class ScriptProcessTests
{
    [Fact]
    public void ExactExecutableApprovalIsRequiredAndRelativePathsUseScriptDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "AnjianProcess-" + Guid.NewGuid().ToString("N"));
        var requested = ScriptPermissionPolicy.ParseRequest(
            """{"schemaVersion":1,"processExecutables":["tools/runner.exe"]}""", root);
        var approved = ScriptPermissionPolicy.CreateGrant(requested,
            new ScriptPermissionApproval { ProcessExecutables = ["tools/runner.exe"] }, root);
        var fullPath = Path.GetFullPath(Path.Combine(root, "tools", "runner.exe"));

        var request = ScriptProcessPolicy.CreateStartRequest(approved, "tools/runner.exe", [], root);
        Assert.Equal(fullPath, request.ExecutablePath);
        Assert.True(approved.AllowsProcessStart(fullPath));
        Assert.Throws<UnauthorizedAccessException>(() =>
            ScriptProcessPolicy.CreateStartRequest(ScriptPermissionGrant.Empty, fullPath, [], root));
        Assert.Throws<UnauthorizedAccessException>(() =>
            ScriptProcessPolicy.CreateStartRequest(approved, Path.Combine(root, "other.exe"), [], root));
    }

    [Fact]
    public void ProcessRequestsRejectNonExePathsInvalidJsonAndUnsafeArguments()
    {
        var root = Path.GetTempPath();
        var executable = Path.Combine(root, "app.exe");
        var grant = new ScriptPermissionGrant([], [], false, false, [], [executable], [], [], false);

        Assert.Throws<InvalidDataException>(() =>
            ScriptProcessPolicy.CreateStartRequest(grant, Path.Combine(root, "script.bat"), [], root));
        Assert.Throws<InvalidDataException>(() =>
            ScriptProcessPolicy.CreateStartRequest(grant, executable, ["bad\0arg"], root));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ScriptProcessPolicy.CreateStartRequest(grant, executable,
                Enumerable.Repeat("x", ScriptProcessPolicy.MaximumArgumentCount + 1), root));
        Assert.Throws<InvalidDataException>(() =>
            ScriptProcessPolicy.ParseStartRequest(grant, "{", root));
        Assert.Throws<InvalidDataException>(() =>
            ScriptProcessPolicy.NormalizeHandle("123"));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ScriptProcessPolicy.CreateWaitRequest(Guid.NewGuid().ToString("N"), 600001));
    }

    [Fact]
    public void WorkerApisPassArgumentArrayAsStructuredJsonAndReturnTypedStatus()
    {
        var root = Path.Combine(Path.GetTempPath(), "AnjianProcessWorker-" + Guid.NewGuid().ToString("N"));
        var executable = Path.GetFullPath(Path.Combine(root, "tools", "runner.exe"));
        var handle = Guid.NewGuid().ToString("N");
        var grant = new ScriptPermissionGrant([], [], false, false, [], [executable], [], [], false);
        var pipe = new ProcessApiRecorder([
            JsonSerializer.Serialize(handle),
            "true",
            """{"processHandle":"__HANDLE__","processId":123,"hasExited":true,"exitCode":7}""".Replace("__HANDLE__", handle, StringComparison.Ordinal)
        ]);
        using var lua = new Lua(openLibs: true);
        LuaApiRuntime.Register(lua, new WorkerLuaApi(pipe, lua, permissions: grant, scriptDirectory: root));
        var result = lua.DoString("""
            local handle = StartProcess("tools/runner.exe", {"--label", "a&b|c", "", "日本語"})
            local exited = WaitProcess(handle, 5000)
            local info = GetProcessInfo(handle)
            return handle, exited, info.processId, info.hasExited, info.exitCode
            """);

        Assert.Equal(handle, result[0]?.ToString());
        Assert.True(Convert.ToBoolean(result[1]));
        Assert.Equal(123, Convert.ToInt32(result[2]));
        Assert.True(Convert.ToBoolean(result[3]));
        Assert.Equal(7, Convert.ToInt32(result[4]));
        var start = pipe.Messages.Single(x => x.Type == "process-start");
        using var payload = JsonDocument.Parse(start.Text!);
        Assert.Equal(executable, payload.RootElement.GetProperty("executablePath").GetString());
        Assert.Equal(new[] { "--label", "a&b|c", "", "日本語" },
            payload.RootElement.GetProperty("arguments").EnumerateArray().Select(x => x.GetString()).ToArray());
        Assert.Equal("process-wait", pipe.Messages[1].Type);
        Assert.Equal("process-info", pipe.Messages[2].Type);
    }

    [Fact]
    public void WorkerRejectsUnapprovedExecutableAndMalformedArgumentTableBeforeIpc()
    {
        var root = Path.GetTempPath();
        var executable = Path.Combine(root, "approved.exe");
        var grant = new ScriptPermissionGrant([], [], false, false, [], [executable], [], [], false);
        var pipe = new ProcessApiRecorder([]);
        using var lua = new Lua(openLibs: true);
        LuaApiRuntime.Register(lua, new WorkerLuaApi(pipe, lua, permissions: grant, scriptDirectory: root));
        var result = lua.DoString("""
            local denied = pcall(function() StartProcess("other.exe") end)
            local hole = pcall(function() StartProcess("approved.exe", {[1]="a", [3]="c"}) end)
            local named = pcall(function() StartProcess("approved.exe", {value="x"}) end)
            local nonString = pcall(function() StartProcess("approved.exe", {1}) end)
            return denied, hole, named, nonString
            """);
        Assert.All(result, value => Assert.False(Convert.ToBoolean(value)));
        Assert.Empty(pipe.Messages);
    }

    [Fact]
    public async Task WindowsSessionStartsOnlyApprovedExeAndCancellationDoesNotKillIt()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var ping = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "ping.exe");
        Assert.True(File.Exists(ping), "Windows ping.exe must be available on the CI runner.");
        var grant = new ScriptPermissionGrant([], [], false, false, [], [Path.GetFullPath(ping)], [], [], false);
        using var session = new ScriptProcessSession();
        using var otherInstanceSession = new ScriptProcessSession();
        var start = new ScriptProcessStartRequest(Path.GetFullPath(ping), ["-n", "12", "127.0.0.1"]);
        var payload = JsonSerializer.Serialize(start);
        var handle = JsonSerializer.Deserialize<string>(session.Start(grant, Path.GetTempPath(), payload));
        Assert.False(string.IsNullOrWhiteSpace(handle));
        var handlePayload = JsonSerializer.Serialize(new ScriptProcessHandleRequest(handle!));
        Assert.Throws<UnauthorizedAccessException>(() => otherInstanceSession.GetInfo(grant, handlePayload));

        using var initial = JsonDocument.Parse(session.GetInfo(grant,
            JsonSerializer.Serialize(new ScriptProcessHandleRequest(handle!))));
        Assert.False(initial.RootElement.GetProperty("hasExited").GetBoolean());

        var pauseEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resumeWait = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pauseTimer = Stopwatch.StartNew();
        var pauseAwareWait = session.WaitAsync(grant,
            JsonSerializer.Serialize(new ScriptProcessWaitRequest(handle!, 100)),
            async (ms, token) =>
            {
                pauseEntered.TrySetResult();
                await resumeWait.Task.WaitAsync(token);
                await Task.Delay(ms, token);
            },
            CancellationToken.None);
        await pauseEntered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await Task.Delay(150);
        Assert.False(pauseAwareWait.IsCompleted);
        resumeWait.TrySetResult();
        Assert.False(await pauseAwareWait);
        Assert.True(pauseTimer.ElapsedMilliseconds >= 230,
            "Time spent paused must not consume the process wait timeout.");

        using (var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50)))
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                session.WaitAsync(grant,
                    JsonSerializer.Serialize(new ScriptProcessWaitRequest(handle!, 10000)),
                    (ms, token) => Task.Delay(ms, token), cts.Token));
        }

        using var afterCancel = JsonDocument.Parse(session.GetInfo(grant,
            JsonSerializer.Serialize(new ScriptProcessHandleRequest(handle!))));
        Assert.False(afterCancel.RootElement.GetProperty("hasExited").GetBoolean());

        var exited = await session.WaitAsync(grant,
            JsonSerializer.Serialize(new ScriptProcessWaitRequest(handle!, 15000)),
            (ms, token) => Task.Delay(ms, token), CancellationToken.None);
        Assert.True(exited);
        using var final = JsonDocument.Parse(session.GetInfo(grant,
            JsonSerializer.Serialize(new ScriptProcessHandleRequest(handle!))));
        Assert.True(final.RootElement.GetProperty("hasExited").GetBoolean());
        Assert.Equal(0, final.RootElement.GetProperty("exitCode").GetInt32());
    }

    private sealed class ProcessApiRecorder(IEnumerable<string?> responses) : ILuaCommandPipe
    {
        private readonly Queue<string?> _responses = new(responses);
        public List<Message> Messages { get; } = [];
        public void Send(string type, string? name = null, double x = 0, double y = 0,
            int milliseconds = 0, string? text = null) =>
            _ = Request(type, name, x, y, milliseconds, text);
        public string? Request(string type, string? name = null, double x = 0, double y = 0,
            int milliseconds = 0, string? text = null)
        {
            Messages.Add(new Message(type, name, text));
            return _responses.Count > 0 ? _responses.Dequeue() : null;
        }
    }

    private sealed record Message(string Type, string? Name, string? Text);
}
