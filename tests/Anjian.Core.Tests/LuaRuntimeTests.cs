using NLua;
using KeraLua;
using Xunit;

namespace Anjian.Core.Tests;

public sealed class LuaRuntimeTests
{
    [Fact]
    public void RuntimeIsLua54()
    {
        using var lua = new NLua.Lua();
        var result = lua.DoString("return _VERSION");
        Assert.Equal("Lua 5.4", result[0]?.ToString());
    }

    [Fact]
    public void LuaCanCallRegisteredGlobalFunction()
    {
        using var lua = new NLua.Lua();
        var capture = new ApiCapture();
        lua.RegisterFunction("CaptureClick", capture, typeof(ApiCapture).GetMethod(nameof(ApiCapture.CaptureClick))!);

        lua.DoString("CaptureClick(120, 75)");

        Assert.Equal(120, capture.X);
        Assert.Equal(75, capture.HoldMilliseconds);
    }

    [Fact]
    public void InstructionCountHookIsInvoked()
    {
        using var lua = new NLua.Lua();
        var hookCalls = 0;
        Assert.Equal(0, lua.SetDebugHook(LuaHookMask.Count, 100));
        lua.DebugHook += (_, _) => Interlocked.Increment(ref hookCalls);

        lua.DoString("local total = 0; for i = 1, 10000 do total = total + i end");

        Assert.True(hookCalls > 0, "Lua instruction-count hook was not invoked.");
    }

    private sealed class ApiCapture
    {
        public double X { get; private set; }
        public double HoldMilliseconds { get; private set; }

        public void CaptureClick(double x, double holdMilliseconds)
        {
            X = x;
            HoldMilliseconds = holdMilliseconds;
        }
    }
}
