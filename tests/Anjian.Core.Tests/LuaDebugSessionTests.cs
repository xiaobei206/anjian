using System.Globalization;
using Anjian.Core.Editor;
using NLua;
using Xunit;

namespace Anjian.Core.Tests;

public sealed class LuaDebugSessionTests
{
    [Fact]
    public void BreakpointPausesAtTheRequestedSourceLine()
    {
        var path = Path.GetFullPath("breakpoint.lua");
        var snapshots = new List<LuaDebugSnapshot>();
        var commands = new Queue<LuaDebugCommand>([
            LuaDebugCommand.Continue,
            LuaDebugCommand.Continue
        ]);

        using var lua = new NLua.Lua();
        using var debugger = CreateDebugger(lua, path, [new LuaDebugBreakpoint(path, 3)], snapshots, commands);

        lua.DoString("local first = 1\nlocal second = first + 1\nlocal third = second + 1", "@" + path);
        debugger.ThrowIfFaulted();

        Assert.Equal(new[] { 1, 3 }, snapshots.Select(snapshot => snapshot.Line));
        Assert.Equal(path, snapshots[1].Source);
    }

    [Fact]
    public void StepIntoOverAndOutFollowNestedLuaCallFrames()
    {
        var path = Path.GetFullPath("steps.lua");
        var snapshots = new List<LuaDebugSnapshot>();
        var commands = new Queue<LuaDebugCommand>([
            LuaDebugCommand.Continue,
            LuaDebugCommand.Continue,
            LuaDebugCommand.Continue,
            LuaDebugCommand.StepInto,
            LuaDebugCommand.StepOver,
            LuaDebugCommand.StepOut,
            LuaDebugCommand.Continue
        ]);
        const string source = """
            local function inner(value)
                return value + 1
            end
            local function outer(value)
                local innerResult = inner(value)
                return innerResult
            end
            local input = 21
            local result = outer(input)
            local final = result + 1
            """;

        using var lua = new NLua.Lua();
        using var debugger = CreateDebugger(lua, path, [], snapshots, commands);

        lua.DoString(source, "@" + path);
        debugger.ThrowIfFaulted();

        Assert.Equal(new[] { 1, 4, 7, 8, 5, 6, 10 }, snapshots.Select(snapshot => snapshot.Line));
        Assert.True(snapshots[4].Frames.Count >= 2, "Step-into should stop inside the outer Lua function.");
        Assert.Contains(snapshots[4].Frames, frame => frame.Name == "outer");
        Assert.DoesNotContain(snapshots, snapshot => snapshot.Line == 2);
        Assert.Equal(2, snapshots[5].Frames.Count);
        Assert.Single(snapshots[6].Frames);
    }

    [Fact]
    public void SnapshotCopiesLocalsAndReadableGlobalsWithoutExpandingTables()
    {
        var path = Path.GetFullPath("variables.lua");
        var snapshots = new List<LuaDebugSnapshot>();
        var commands = new Queue<LuaDebugCommand>([
            LuaDebugCommand.Continue,
            LuaDebugCommand.Continue
        ]);
        const string source = """
            local message = "hello debugger"
            local count = 42
            DebugGlobalValue = 99
            local settings = { enabled = true }
            local nextValue = count + 1
            """;

        using var lua = new NLua.Lua();
        using var debugger = CreateDebugger(
            lua,
            path,
            [new LuaDebugBreakpoint(path, 5)],
            snapshots,
            commands);

        lua.DoString(source, "@" + path);
        debugger.ThrowIfFaulted();

        var snapshot = Assert.Single(snapshots.Where(item => item.Line == 5));
        var locals = snapshot.Frames[0].Locals.ToDictionary(item => item.Name, StringComparer.Ordinal);
        Assert.Equal("hello debugger", locals["message"].Value);
        Assert.Equal("42", locals["count"].Value);
        Assert.Equal("table", locals["settings"].Type);
        Assert.False(locals["settings"].CanExpand);
        Assert.Contains("不展开", locals["settings"].Limitation, StringComparison.Ordinal);

        var global = Assert.Single(snapshot.Globals.Where(item => item.Name == "DebugGlobalValue"));
        Assert.Equal("99", global.Value);
    }

    [Fact]
    public void RunToCursorStopsAtTheRequestedLineAfterResume()
    {
        var path = Path.GetFullPath("cursor.lua");
        var snapshots = new List<LuaDebugSnapshot>();
        var commands = new Queue<LuaDebugCommand>([
            LuaDebugCommand.RunToCursor(path, 4),
            LuaDebugCommand.Continue
        ]);
        const string source = """
            local a = 1
            local b = a + 1
            local c = b + 1
            local d = c + 1
            """;

        using var lua = new NLua.Lua();
        using var debugger = CreateDebugger(lua, path, [], snapshots, commands);

        lua.DoString(source, "@" + path);
        debugger.ThrowIfFaulted();

        Assert.Equal(new[] { 1, 4 }, snapshots.Select(snapshot => snapshot.Line));
    }

    [Fact]
    public void DisposeRemovesTheLuaDebugHook()
    {
        var path = Path.GetFullPath("dispose.lua");
        using var lua = new NLua.Lua();
        var debugger = CreateDebugger(lua, path, [], [], new Queue<LuaDebugCommand>());
        Assert.NotEqual(KeraLua.LuaHookMask.Disabled, lua.GetHookMask());

        debugger.Dispose();

        Assert.Equal(KeraLua.LuaHookMask.Disabled, lua.GetHookMask());
    }

    private static LuaDebugSession CreateDebugger(
        NLua.Lua lua,
        string path,
        IReadOnlyList<LuaDebugBreakpoint> breakpoints,
        List<LuaDebugSnapshot> snapshots,
        Queue<LuaDebugCommand> commands)
    {
        return new LuaDebugSession(
            lua,
            path,
            new LuaDebugLaunchOptions(breakpoints),
            checkpoint: () => { },
            waitForCommand: snapshot =>
            {
                snapshots.Add(snapshot);
                return commands.Count > 0 ? commands.Dequeue() : LuaDebugCommand.Continue;
            });
    }
}
