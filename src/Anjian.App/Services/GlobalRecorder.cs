using System.Diagnostics;
using System.Runtime.InteropServices;
using Anjian.App.Models;

namespace Anjian.App.Services;

internal sealed class GlobalRecorder : IDisposable
{
    private const int WhKeyboardLl = 13, WhMouseLl = 14;
    private const int WmMouseMove = 0x0200, WmLeftDown = 0x0201, WmLeftUp = 0x0202;
    private const int WmRightDown = 0x0204, WmRightUp = 0x0205, WmMiddleDown = 0x0207, WmMiddleUp = 0x0208;
    private const int WmMouseWheel = 0x020A;
    private const int WmKeyDown = 0x0100, WmKeyUp = 0x0101, WmSysKeyDown = 0x0104, WmSysKeyUp = 0x0105;
    private const uint InjectedFlag = 0x00000001;
    private const int MaximumEvents = 250_000;

    private readonly List<RecordedInputEvent> _events = [];
    private readonly LowLevelProc _mouseProc;
    private readonly LowLevelProc _keyboardProc;
    private nint _mouseHook, _keyboardHook;
    private bool _recording;

    public bool Overflowed { get; private set; }

    public GlobalRecorder()
    {
        _mouseProc = MouseCallback;
        _keyboardProc = KeyboardCallback;
    }

    public void Start()
    {
        if (_recording) throw new InvalidOperationException("正在录制。");
        _events.Clear();
        Overflowed = false;
        _mouseHook = SetWindowsHookEx(WhMouseLl, _mouseProc, GetModuleHandle(null), 0);
        if (_mouseHook == 0) throw new InvalidOperationException($"鼠标录制钩子安装失败：{Marshal.GetLastWin32Error()}");
        _keyboardHook = SetWindowsHookEx(WhKeyboardLl, _keyboardProc, GetModuleHandle(null), 0);
        if (_keyboardHook == 0)
        {
            UnhookWindowsHookEx(_mouseHook);
            _mouseHook = 0;
            throw new InvalidOperationException($"键盘录制钩子安装失败：{Marshal.GetLastWin32Error()}");
        }
        _recording = true;
    }

    public IReadOnlyList<RecordedInputEvent> Stop()
    {
        _recording = false;
        if (_mouseHook != 0) { UnhookWindowsHookEx(_mouseHook); _mouseHook = 0; }
        if (_keyboardHook != 0) { UnhookWindowsHookEx(_keyboardHook); _keyboardHook = 0; }
        return _events.ToArray();
    }

    private nint MouseCallback(int code, nint wParam, nint lParam)
    {
        if (code >= 0 && _recording)
        {
            try
            {
                var data = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                if ((data.Flags & InjectedFlag) == 0 && !IsOurApplicationForeground())
                {
                    var kind = unchecked((int)wParam);
                    var timestamp = Stopwatch.GetTimestamp();
                    switch (kind)
                    {
                        case WmMouseMove:
                            AddMove(timestamp, data.Point.X, data.Point.Y);
                            break;
                        case WmLeftDown: Add(new(timestamp, RecordedEventKind.MouseDown, data.Point.X, data.Point.Y, "left")); break;
                        case WmLeftUp: Add(new(timestamp, RecordedEventKind.MouseUp, data.Point.X, data.Point.Y, "left")); break;
                        case WmRightDown: Add(new(timestamp, RecordedEventKind.MouseDown, data.Point.X, data.Point.Y, "right")); break;
                        case WmRightUp: Add(new(timestamp, RecordedEventKind.MouseUp, data.Point.X, data.Point.Y, "right")); break;
                        case WmMiddleDown: Add(new(timestamp, RecordedEventKind.MouseDown, data.Point.X, data.Point.Y, "middle")); break;
                        case WmMiddleUp: Add(new(timestamp, RecordedEventKind.MouseUp, data.Point.X, data.Point.Y, "middle")); break;
                        case WmMouseWheel:
                            Add(new(timestamp, RecordedEventKind.Wheel, data.Point.X, data.Point.Y, Delta: unchecked((short)(data.MouseData >> 16))));
                            break;
                    }
                }
            }
            catch { }
        }
        return CallNextHookEx(_mouseHook, code, wParam, lParam);
    }

    private nint KeyboardCallback(int code, nint wParam, nint lParam)
    {
        if (code >= 0 && _recording)
        {
            try
            {
                var data = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                if ((data.Flags & InjectedFlag) == 0 && !IsOurApplicationForeground())
                {
                    var kind = unchecked((int)wParam);
                    var name = KeyName(data.VirtualKey);
                    if (name is not null)
                    {
                        var timestamp = Stopwatch.GetTimestamp();
                        if (kind is WmKeyDown or WmSysKeyDown)
                            Add(new(timestamp, RecordedEventKind.KeyDown, Name: name));
                        else if (kind is WmKeyUp or WmSysKeyUp)
                            Add(new(timestamp, RecordedEventKind.KeyUp, Name: name));
                    }
                }
            }
            catch { }
        }
        return CallNextHookEx(_keyboardHook, code, wParam, lParam);
    }

    private void AddMove(long timestamp, int x, int y)
    {
        if (_events.Count > 0 && _events[^1].Kind == RecordedEventKind.Move)
        {
            var previous = _events[^1];
            var distance = Math.Abs(previous.X - x) + Math.Abs(previous.Y - y);
            var elapsedMs = (timestamp - previous.Timestamp) * 1000.0 / Stopwatch.Frequency;
            if (distance < 10 && elapsedMs < 50)
            {
                _events[^1] = new RecordedInputEvent(timestamp, RecordedEventKind.Move, x, y);
                return;
            }
        }
        Add(new(timestamp, RecordedEventKind.Move, x, y));
    }

    private void Add(RecordedInputEvent value)
    {
        if (_events.Count < MaximumEvents)
            _events.Add(value);
        else
            Overflowed = true;
    }

    public static string GenerateLua(IReadOnlyList<RecordedInputEvent> events)
    {
        var output = new System.Text.StringBuilder();
        output.AppendLine("-- 按键精灵 2026 自动录制脚本");
        output.AppendLine("-- 鼠标移动已合并；时间单位：毫秒");
        long lastEnd = events.Count > 0 ? events[0].Timestamp : 0;
        var index = 0;
        while (index < events.Count)
        {
            var current = events[index];
            var endTime = current.Timestamp;
            var consumed = 1;
            string statement;

            if (index + 1 < events.Count && IsPaired(current, events[index + 1]))
            {
                var release = events[index + 1];
                var holdMs = Math.Max(0, (int)Math.Round((release.Timestamp - current.Timestamp) * 1000.0 / Stopwatch.Frequency));
                statement = current.Kind == RecordedEventKind.MouseDown
                    ? $"{ClickName(current.Name!)}({holdMs})"
                    : $"KeyPress(\"{current.Name}\", {holdMs})";
                endTime = release.Timestamp;
                consumed = 2;
            }
            else
            {
                statement = current.Kind switch
                {
                    RecordedEventKind.Move => $"MoveTo({current.X}, {current.Y})",
                    RecordedEventKind.MouseDown => $"{ButtonName(current.Name!, true)}()",
                    RecordedEventKind.MouseUp => $"{ButtonName(current.Name!, false)}()",
                    RecordedEventKind.Wheel => $"MouseWheel({current.Delta})",
                    RecordedEventKind.KeyDown => $"KeyDown(\"{current.Name}\")",
                    RecordedEventKind.KeyUp => $"KeyUp(\"{current.Name}\")",
                    _ => ""
                };
            }

            if (index > 0)
            {
                var gapMs = Math.Max(0, (int)Math.Round((current.Timestamp - lastEnd) * 1000.0 / Stopwatch.Frequency));
                if (gapMs > 0) output.Append("Delay(").Append(gapMs).AppendLine(")");
            }

            if (current.Kind is RecordedEventKind.MouseDown or RecordedEventKind.Wheel)
                output.Append("MoveTo(").Append(current.X).Append(", ").Append(current.Y).AppendLine(")");
            if (!string.IsNullOrWhiteSpace(statement))
                output.AppendLine(statement);

            lastEnd = endTime;
            index += consumed;
        }
        return output.ToString();
    }

    private static bool IsPaired(RecordedInputEvent down, RecordedInputEvent up) =>
        (down.Kind == RecordedEventKind.MouseDown && up.Kind == RecordedEventKind.MouseUp ||
         down.Kind == RecordedEventKind.KeyDown && up.Kind == RecordedEventKind.KeyUp) &&
        string.Equals(down.Name, up.Name, StringComparison.OrdinalIgnoreCase);

    private static string ClickName(string button) => button switch
    {
        "left" => "LeftClick", "right" => "RightClick", "middle" => "MiddleClick", _ => "LeftClick"
    };

    private static string ButtonName(string button, bool down) => button switch
    {
        "left" => down ? "LeftDown" : "LeftUp",
        "right" => down ? "RightDown" : "RightUp",
        "middle" => down ? "MiddleDown" : "MiddleUp",
        _ => down ? "LeftDown" : "LeftUp"
    };

    private static bool IsOurApplicationForeground()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == 0) return false;
        GetWindowThreadProcessId(hwnd, out var processId);
        return processId == (uint)Environment.ProcessId;
    }

    private static string? KeyName(uint vk)
    {
        if (vk is >= 0x41 and <= 0x5A || vk is >= 0x30 and <= 0x39)
            return ((char)vk).ToString();
        if (vk is >= 0x70 and <= 0x87)
            return "F" + (vk - 0x6F);
        return vk switch
        {
            0x08 => "BACKSPACE", 0x09 => "TAB", 0x0D => "ENTER", 0x10 or 0xA0 or 0xA1 => "SHIFT",
            0x11 or 0xA2 or 0xA3 => "CTRL", 0x12 or 0xA4 or 0xA5 => "ALT",
            0x1B => "ESC", 0x20 => "SPACE", 0x21 => "PAGEUP", 0x22 => "PAGEDOWN",
            0x23 => "END", 0x24 => "HOME", 0x25 => "LEFT", 0x26 => "UP", 0x27 => "RIGHT", 0x28 => "DOWN",
            0x2E => "DELETE", 0x5B or 0x5C => "WIN", 0x14 => "CAPSLOCK",
            _ => null
        };
    }

    public void Dispose() => Stop();

    private delegate nint LowLevelProc(int code, nint wParam, nint lParam);
    [StructLayout(LayoutKind.Sequential)] private struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct MSLLHOOKSTRUCT { public POINT Point; public uint MouseData, Flags, Time; public nint ExtraInfo; }
    [StructLayout(LayoutKind.Sequential)] private struct KBDLLHOOKSTRUCT { public uint VirtualKey, ScanCode, Flags, Time; public nint ExtraInfo; }

    [DllImport("user32.dll", SetLastError = true)] private static extern nint SetWindowsHookEx(int idHook, LowLevelProc callback, nint module, uint threadId);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("user32.dll")] private static extern nint CallNextHookEx(nint hook, int code, nint wParam, nint lParam);
    [DllImport("kernel32.dll", CharSet = CharSet.Auto)] private static extern nint GetModuleHandle(string? moduleName);
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint hwnd, out uint processId);
}
