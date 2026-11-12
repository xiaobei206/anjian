using System.Collections;
using System.Globalization;
using System.Runtime.InteropServices;
using KeraLua;
using NLua;
using NLua.Event;

namespace Anjian.Core.Editor;

/// <summary>A source location that should stop the Lua worker before the line executes.</summary>
public sealed record LuaDebugBreakpoint(string Source, int Line);

/// <summary>Debug settings copied into the worker before Lua starts running.</summary>
public sealed record LuaDebugLaunchOptions(IReadOnlyList<LuaDebugBreakpoint> Breakpoints);

/// <summary>A command returned by the UI while the worker is stopped at a Lua line hook.</summary>
public sealed record LuaDebugCommand(string Action, string? Source = null, int? Line = null)
{
    public static LuaDebugCommand Continue { get; } = new("continue");
    public static LuaDebugCommand StepInto { get; } = new("step-into");
    public static LuaDebugCommand StepOver { get; } = new("step-over");
    public static LuaDebugCommand StepOut { get; } = new("step-out");
    public static LuaDebugCommand RunToCursor(string source, int line) => new("run-to-cursor", source, line);
}

public sealed record LuaDebugVariable(
    string Name,
    string Type,
    string Value,
    bool CanExpand,
    string? Limitation = null);

public sealed record LuaDebugFrame(
    int Index,
    string Name,
    string Source,
    int Line,
    string Kind,
    IReadOnlyList<LuaDebugVariable> Locals);

public sealed record LuaDebugSnapshot(
    string Source,
    int Line,
    IReadOnlyList<LuaDebugFrame> Frames,
    IReadOnlyList<LuaDebugVariable> Globals);

/// <summary>
/// A synchronous Lua 5.4 debugger. All KeraLua/NLua stack inspection stays on the
/// thread currently executing the state. The pause callback must return a plain
/// command; snapshots contain copied strings and primitive metadata only.
/// </summary>
public sealed class LuaDebugSession : IDisposable
{
    private const int InstructionCheckpoint = 10_000;
    private const int MaximumFrames = 32;
    private const int MaximumLocalsPerFrame = 96;
    private const int MaximumGlobals = 100;
    private const int MaximumStringLength = 512;

    private readonly NLua.Lua _lua;
    private readonly string _scriptPath;
    private readonly Action _checkpoint;
    private readonly Func<LuaDebugSnapshot, LuaDebugCommand> _waitForCommand;
    private readonly HashSet<string> _breakpoints;
    private DebugMode _mode = DebugMode.Run;
    private string? _runToSource;
    private int _runToLine;
    private int _stepDepth;
    private bool _started;
    private bool _disposed;
    private Exception? _hookFailure;

    public LuaDebugSession(
        NLua.Lua lua,
        string scriptPath,
        LuaDebugLaunchOptions launchOptions,
        Action checkpoint,
        Func<LuaDebugSnapshot, LuaDebugCommand> waitForCommand)
    {
        _lua = lua ?? throw new ArgumentNullException(nameof(lua));
        ArgumentException.ThrowIfNullOrWhiteSpace(scriptPath);
        ArgumentNullException.ThrowIfNull(launchOptions);
        _checkpoint = checkpoint ?? throw new ArgumentNullException(nameof(checkpoint));
        _waitForCommand = waitForCommand ?? throw new ArgumentNullException(nameof(waitForCommand));
        _scriptPath = NormalizeSource(scriptPath);

        _breakpoints = launchOptions.Breakpoints
            .Where(item => item.Line > 0 && !string.IsNullOrWhiteSpace(item.Source))
            .Select(item => BreakpointKey(item.Source, item.Line))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (_lua.SetDebugHook(LuaHookMask.Line | LuaHookMask.Count, InstructionCheckpoint) != 0)
            throw new InvalidOperationException("Lua 调试 Hook 已被其他组件占用。");

        _lua.DebugHook += OnDebugHook;
    }

    /// <summary>Rethrows a failure captured inside NLua's native hook callback.</summary>
    public void ThrowIfFaulted()
    {
        if (_hookFailure is not null)
            throw new InvalidOperationException("Lua 调试 Hook 执行失败。", _hookFailure);
    }

    private void OnDebugHook(object? sender, DebugHookEventArgs args)
    {
        if (_hookFailure is not null || _disposed)
            return;

        try
        {
            var info = args.LuaDebug;
            if (info.Event == LuaHookEvent.Count)
            {
                _checkpoint();
                return;
            }

            if (info.Event != LuaHookEvent.Line || info.CurrentLine < 1)
                return;

            var source = NormalizeSource(info.Source);
            var line = info.CurrentLine;
            var depth = _mode is DebugMode.StepOver or DebugMode.StepOut
                ? CountFrames()
                : 0;

            var isMainScript = string.Equals(source, _scriptPath, StringComparison.OrdinalIgnoreCase);
            var shouldPause = (!_started && isMainScript) ||
                _breakpoints.Contains(BreakpointKey(source, line)) ||
                (_mode == DebugMode.StepInto) ||
                (_mode == DebugMode.StepOver && depth <= _stepDepth) ||
                (_mode == DebugMode.StepOut && depth < _stepDepth) ||
                (_mode == DebugMode.RunToCursor &&
                 string.Equals(source, _runToSource, StringComparison.OrdinalIgnoreCase) &&
                 line == _runToLine);

            if (!shouldPause)
                return;

            if (isMainScript)
                _started = true;
            var snapshot = CaptureSnapshot(source, line);
            var command = _waitForCommand(snapshot) ??
                throw new InvalidOperationException("调试器未返回下一步命令。");
            ApplyCommand(command, snapshot.Frames.Count);
        }
        catch (Exception exception)
        {
            // NLua intentionally catches exceptions from native callbacks. Store the
            // failure here and rethrow it immediately after DoString returns.
            _hookFailure = exception;
        }
    }

    private void ApplyCommand(LuaDebugCommand command, int currentDepth)
    {
        switch (command.Action)
        {
            case "continue":
                _mode = DebugMode.Run;
                _runToSource = null;
                return;
            case "step-into":
                _mode = DebugMode.StepInto;
                _runToSource = null;
                _stepDepth = currentDepth;
                return;
            case "step-over":
                _mode = DebugMode.StepOver;
                _runToSource = null;
                _stepDepth = currentDepth;
                return;
            case "step-out":
                _mode = DebugMode.StepOut;
                _runToSource = null;
                _stepDepth = currentDepth;
                return;
            case "run-to-cursor":
                if (string.IsNullOrWhiteSpace(command.Source) || command.Line is null or < 1)
                    throw new ArgumentException("运行到光标需要有效的脚本路径和行号。");
                _mode = DebugMode.RunToCursor;
                _runToSource = NormalizeSource(command.Source);
                _runToLine = command.Line.Value;
                return;
            default:
                throw new ArgumentException($"未知调试命令：{command.Action}");
        }
    }

    private int CountFrames()
    {
        var state = _lua.State;
        var pointer = AllocateDebugBuffer();
        try
        {
            var count = 0;
            while (count < MaximumFrames)
            {
                ClearDebugBuffer(pointer);
                if (state.GetStack(count, pointer) == 0)
                    break;
                count++;
            }
            return count;
        }
        finally
        {
            Marshal.FreeHGlobal(pointer);
        }
    }

    private LuaDebugSnapshot CaptureSnapshot(string source, int line)
    {
        var state = _lua.State;
        var oldTop = state.GetTop();
        var pointer = AllocateDebugBuffer();
        var frames = new List<LuaDebugFrame>();
        try
        {
            for (var level = 0; level < MaximumFrames; level++)
            {
                ClearDebugBuffer(pointer);
                if (state.GetStack(level, pointer) == 0)
                    break;
                if (!state.GetInfo("nSl", pointer))
                    break;

                var info = LuaDebug.FromIntPtr(pointer);
                var frameSource = NormalizeSource(info.Source);
                var locals = CaptureLocals(pointer);
                frames.Add(new LuaDebugFrame(
                    level,
                    string.IsNullOrWhiteSpace(info.Name) ? (level == 0 ? "(主脚本)" : "(匿名函数)") : info.Name,
                    frameSource,
                    info.CurrentLine,
                    info.What ?? string.Empty,
                    locals));
            }

            var globals = CaptureGlobals();
            return new LuaDebugSnapshot(source, line, frames, globals);
        }
        finally
        {
            state.SetTop(oldTop);
            Marshal.FreeHGlobal(pointer);
        }
    }

    private IReadOnlyList<LuaDebugVariable> CaptureLocals(IntPtr debugPointer)
    {
        var variables = new List<LuaDebugVariable>();
        for (var index = 1; index <= MaximumLocalsPerFrame; index++)
        {
            var name = _lua.State.GetLocal(debugPointer, index);
            if (string.IsNullOrEmpty(name))
                break;

            var value = _lua.Pop();
            try
            {
                variables.Add(FormatValue(name, value));
            }
            finally
            {
                DisposeLuaValue(value);
            }
        }

        return variables;
    }

    private IReadOnlyList<LuaDebugVariable> CaptureGlobals()
    {
        var variables = new List<LuaDebugVariable>();
        using var globalTable = _lua.GetTable("_G");
        var entries = _lua.GetTableDict(globalTable);
        try
        {
            foreach (DictionaryEntry entry in (IDictionary)entries)
            {
                if (entry.Key is not string name ||
                    name is "_G" or "luanet" ||
                    name.StartsWith("__", StringComparison.Ordinal))
                    continue;

                variables.Add(FormatValue(name, entry.Value));
                if (variables.Count >= MaximumGlobals)
                    break;
            }
        }
        finally
        {
            foreach (var entry in entries)
            {
                DisposeLuaValue(entry.Key);
                DisposeLuaValue(entry.Value);
            }
        }

        variables.Sort((left, right) => StringComparer.OrdinalIgnoreCase.Compare(left.Name, right.Name));
        if (entries.Count > MaximumGlobals)
        {
            variables.Add(new LuaDebugVariable(
                "…", "提示", $"全局变量较多，仅显示前 {MaximumGlobals} 项。", false, "已达到显示上限。"));
        }

        return variables;
    }

    private static LuaDebugVariable FormatValue(string name, object? value)
    {
        if (value is null)
            return new LuaDebugVariable(name, "nil", "nil", false);

        if (value is string text)
        {
            var rendered = text
                .Replace("\r", "\\r", StringComparison.Ordinal)
                .Replace("\n", "\\n", StringComparison.Ordinal);
            var truncated = rendered.Length > MaximumStringLength;
            if (truncated)
                rendered = rendered[..MaximumStringLength] + "…";
            return new LuaDebugVariable(name, "string", rendered, false,
                truncated ? $"字符串仅显示前 {MaximumStringLength} 个字符。" : null);
        }

        if (value is bool boolean)
            return new LuaDebugVariable(name, "boolean", boolean ? "true" : "false", false);

        if (value is double number)
            return new LuaDebugVariable(name, "number", number.ToString("G", CultureInfo.InvariantCulture), false);
        if (value is float single)
            return new LuaDebugVariable(name, "number", single.ToString("G", CultureInfo.InvariantCulture), false);
        if (value is IFormattable formattable &&
            (value.GetType().IsPrimitive || value is decimal or long or ulong))
            return new LuaDebugVariable(name, "number", formattable.ToString(null, CultureInfo.InvariantCulture), false);

        var type = value is LuaTable ? "table"
            : value is NLua.LuaFunction ? "function"
            : value is LuaThread ? "thread"
            : "userdata";
        return new LuaDebugVariable(
            name,
            type,
            $"({type})",
            false,
            "为避免观察变量时触发脚本逻辑，当前版本不展开此类值。");
    }

    private static void DisposeLuaValue(object? value)
    {
        if (value is LuaBase luaBase)
            luaBase.Dispose();
    }

    private static IntPtr AllocateDebugBuffer()
    {
        var size = Marshal.SizeOf<LuaDebug>();
        var pointer = Marshal.AllocHGlobal(size);
        ClearDebugBuffer(pointer);
        return pointer;
    }

    private static void ClearDebugBuffer(IntPtr pointer)
    {
        var size = Marshal.SizeOf<LuaDebug>();
        var zeroes = new byte[size];
        Marshal.Copy(zeroes, 0, pointer, size);
    }

    private static string BreakpointKey(string source, int line) =>
        $"{NormalizeSource(source)}|{line.ToString(CultureInfo.InvariantCulture)}";

    private static string NormalizeSource(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
            return string.Empty;

        var value = source.StartsWith('@') ? source[1..] : source;
        if (value.StartsWith('=') || value.StartsWith("[", StringComparison.Ordinal))
            return value;

        try
        {
            return Path.GetFullPath(value);
        }
        catch
        {
            return value;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _lua.DebugHook -= OnDebugHook;
        _lua.RemoveDebugHook();
    }

    private enum DebugMode
    {
        Run,
        StepInto,
        StepOver,
        StepOut,
        RunToCursor
    }
}
