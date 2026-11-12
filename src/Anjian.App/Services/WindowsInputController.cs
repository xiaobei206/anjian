using System.Runtime.InteropServices;
using System.Text;

namespace Anjian.App.Services;

internal sealed class WindowsInputController
{
    private readonly object _sync = new();
    private readonly HashSet<string> _heldKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _heldMouse = new(StringComparer.OrdinalIgnoreCase);

    public void MoveTo(int x, int y)
    {
        if (!SetCursorPos(x, y))
            throw new InvalidOperationException($"SetCursorPos 失败：{Marshal.GetLastWin32Error()}");
    }

    public async Task ClickAsync(string button, int holdMs, CancellationToken token)
    {
        MouseDown(button);
        try { await Task.Delay(holdMs, token).ConfigureAwait(false); }
        finally { MouseUp(button); }
    }

    public void MouseDown(string button)
    {
        var flag = button.ToLowerInvariant() switch
        {
            "left" => MouseLeftDown,
            "right" => MouseRightDown,
            "middle" => MouseMiddleDown,
            _ => throw new ArgumentException($"不支持的鼠标按钮：{button}")
        };
        SendMouse(flag);
        lock (_sync) _heldMouse.Add(button);
    }

    public void MouseUp(string button)
    {
        var flag = button.ToLowerInvariant() switch
        {
            "left" => MouseLeftUp,
            "right" => MouseRightUp,
            "middle" => MouseMiddleUp,
            _ => throw new ArgumentException($"不支持的鼠标按钮：{button}")
        };
        SendMouse(flag);
        lock (_sync) _heldMouse.Remove(button);
    }

    public void MouseWheel(int delta)
    {
        var input = new INPUT { Type = InputMouse, Union = new INPUTUNION { Mouse = new MOUSEINPUT { MouseData = unchecked((uint)delta), Flags = MouseWheelFlag } } };
        Send(ref input);
    }

    public async Task KeyPressAsync(string key, int holdMs, CancellationToken token)
    {
        KeyDown(key);
        try { await Task.Delay(holdMs, token).ConfigureAwait(false); }
        finally { KeyUp(key); }
    }

    public void KeyDown(string key)
    {
        var vk = GetVirtualKey(key);
        SendKeyboard(vk, 0);
        lock (_sync) _heldKeys.Add(key);
    }

    public void KeyUp(string key)
    {
        var vk = GetVirtualKey(key);
        SendKeyboard(vk, KeyUpFlag);
        lock (_sync) _heldKeys.Remove(key);
    }

    public void TypeText(string text)
    {
        foreach (var rune in text.EnumerateRunes())
        {
            var value = rune.Value;
            if (value <= 0xFFFF)
            {
                SendUnicode((ushort)value, false);
                SendUnicode((ushort)value, true);
            }
            else
            {
                var scalar = value - 0x10000;
                var high = (ushort)(0xD800 + (scalar >> 10));
                var low = (ushort)(0xDC00 + (scalar & 0x3FF));
                SendUnicode(high, false); SendUnicode(high, true);
                SendUnicode(low, false); SendUnicode(low, true);
            }
        }
    }

    public void ReleaseAll()
    {
        string[] keys, mouse;
        lock (_sync)
        {
            keys = _heldKeys.ToArray();
            mouse = _heldMouse.ToArray();
            _heldKeys.Clear();
            _heldMouse.Clear();
        }
        foreach (var key in keys)
        {
            try { SendKeyboard(GetVirtualKey(key), KeyUpFlag); } catch { }
        }
        foreach (var button in mouse)
        {
            try
            {
                var flag = button.ToLowerInvariant() switch
                {
                    "left" => MouseLeftUp,
                    "right" => MouseRightUp,
                    "middle" => MouseMiddleUp,
                    _ => 0u
                };
                if (flag != 0) SendMouse(flag);
            }
            catch { }
        }
    }

    private static ushort GetVirtualKey(string key)
    {
        var normalized = key.Trim().ToUpperInvariant();
        if (normalized.Length == 1 && ((normalized[0] >= 'A' && normalized[0] <= 'Z') || (normalized[0] >= '0' && normalized[0] <= '9')))
            return normalized[0];
        if (normalized.StartsWith("F", StringComparison.Ordinal) && int.TryParse(normalized[1..], out var f) && f is >= 1 and <= 24)
            return (ushort)(0x70 + f - 1);
        return normalized switch
        {
            "ENTER" => 0x0D, "ESC" => 0x1B, "TAB" => 0x09, "SPACE" => 0x20,
            "BACKSPACE" => 0x08, "DELETE" => 0x2E, "INSERT" => 0x2D,
            "HOME" => 0x24, "END" => 0x23, "PAGEUP" => 0x21, "PAGEDOWN" => 0x22,
            "UP" => 0x26, "DOWN" => 0x28, "LEFT" => 0x25, "RIGHT" => 0x27,
            "CTRL" or "CONTROL" => 0x11, "SHIFT" => 0x10, "ALT" => 0x12, "WIN" => 0x5B,
            "CAPSLOCK" => 0x14, "PRINTSCREEN" => 0x2C, "PAUSE" => 0x13,
            "NUMLOCK" => 0x90, "SCROLLLOCK" => 0x91,
            "NUMPAD0" => 0x60, "NUMPAD1" => 0x61, "NUMPAD2" => 0x62, "NUMPAD3" => 0x63,
            "NUMPAD4" => 0x64, "NUMPAD5" => 0x65, "NUMPAD6" => 0x66, "NUMPAD7" => 0x67,
            "NUMPAD8" => 0x68, "NUMPAD9" => 0x69,
            "NUMPADMULTIPLY" => 0x6A, "NUMPADADD" => 0x6B, "NUMPADSUBTRACT" => 0x6D,
            "NUMPADDECIMAL" => 0x6E, "NUMPADDIVIDE" => 0x6F,
            "SEMICOLON" => 0xBA, "EQUALS" => 0xBB, "COMMA" => 0xBC, "MINUS" => 0xBD,
            "PERIOD" => 0xBE, "SLASH" => 0xBF, "BACKTICK" => 0xC0,
            "LEFTBRACKET" => 0xDB, "BACKSLASH" => 0xDC, "RIGHTBRACKET" => 0xDD, "QUOTE" => 0xDE,
            _ => throw new ArgumentException($"不支持的按键：{key}")
        };
    }

    private static void SendUnicode(ushort value, bool keyUp)
    {
        var input = new INPUT { Type = InputKeyboard, Union = new INPUTUNION { Keyboard = new KEYBDINPUT { VirtualKey = 0, ScanCode = value, Flags = KeyUnicode | (keyUp ? KeyUpFlag : 0) } } };
        Send(ref input);
    }

    private static void SendKeyboard(ushort vk, uint flags)
    {
        var input = new INPUT { Type = InputKeyboard, Union = new INPUTUNION { Keyboard = new KEYBDINPUT { VirtualKey = vk, Flags = flags } } };
        Send(ref input);
    }

    private static void SendMouse(uint flags)
    {
        var input = new INPUT { Type = InputMouse, Union = new INPUTUNION { Mouse = new MOUSEINPUT { Flags = flags } } };
        Send(ref input);
    }

    private static void Send(ref INPUT input)
    {
        if (SendInput(1, ref input, Marshal.SizeOf<INPUT>()) != 1)
            throw new InvalidOperationException($"SendInput 失败：{Marshal.GetLastWin32Error()}");
    }

    private const uint InputMouse = 0, InputKeyboard = 1;
    private const uint MouseLeftDown = 0x0002, MouseLeftUp = 0x0004;
    private const uint MouseRightDown = 0x0008, MouseRightUp = 0x0010;
    private const uint MouseMiddleDown = 0x0020, MouseMiddleUp = 0x0040;
    private const uint MouseWheelFlag = 0x0800;
    private const uint KeyUpFlag = 0x0002, KeyUnicode = 0x0004;

    [DllImport("user32.dll", SetLastError = true)] private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, ref INPUT inputs, int size);

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT { public uint Type; public INPUTUNION Union; }
    [StructLayout(LayoutKind.Explicit)]
    private struct INPUTUNION
    {
        [FieldOffset(0)] public MOUSEINPUT Mouse;
        [FieldOffset(0)] public KEYBDINPUT Keyboard;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int X, Y;
        public uint MouseData, Flags, Time;
        public nint ExtraInfo;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort VirtualKey, ScanCode;
        public uint Flags, Time;
        public nint ExtraInfo;
    }
}
