using System.Runtime.InteropServices;
using System.Text;
using Anjian.Core.Runtime;

namespace Anjian.App.Services;

internal sealed class WindowsInputController
{
    private readonly InputArbiter _arbiter;
    private readonly RunInstanceId _owner;
    private readonly object _sync = new();
    private readonly Dictionary<string, InputArbiter.InputArbiterLease> _heldKeys =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, InputArbiter.InputArbiterLease> _heldMouse =
        new(StringComparer.OrdinalIgnoreCase);

    public WindowsInputController(InputArbiter arbiter, RunInstanceId owner)
    {
        _arbiter = arbiter ?? throw new ArgumentNullException(nameof(arbiter));
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
    }

    public WindowsInputController()
        : this(new InputArbiter(), RunInstanceId.Create())
    {
    }

    public async Task MoveToAsync(int x, int y, CancellationToken token)
    {
        using var lease = await _arbiter.AcquireAsync(_owner, token).ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        MoveToCore(x, y);
    }

    public async Task ClickAsync(string button, int holdMs, CancellationToken token)
    {
        using var operation = await _arbiter.AcquireAsync(_owner, token).ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        await ClickCoreAsync(button, holdMs, token).ConfigureAwait(false);
    }

    public async Task ClickAtAsync(int x, int y, string button, int holdMs, CancellationToken token)
    {
        using var operation = await _arbiter.AcquireAsync(_owner, token).ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        MoveToCore(x, y);
        await ClickCoreAsync(button, holdMs, token).ConfigureAwait(false);
    }

    public async Task MouseDownAsync(string button, CancellationToken token)
    {
        var normalized = NormalizeMouseButton(button);
        lock (_sync)
        {
            if (_heldMouse.ContainsKey(normalized))
                return;
        }

        var lease = await _arbiter.AcquireAsync(_owner, token).ConfigureAwait(false);
        var stored = false;
        try
        {
            token.ThrowIfCancellationRequested();
            lock (_sync)
            {
                if (_heldMouse.ContainsKey(normalized))
                    return;
                try
                {
                    SendMouse(GetMouseDownFlag(normalized));
                    _heldMouse.Add(normalized, lease);
                    stored = true;
                }
                catch (Exception ex)
                {
                    _arbiter.MarkFaulted(_owner, $"按下鼠标 {normalized} 失败：{ex.Message}");
                    throw;
                }
            }
        }
        finally
        {
            if (!stored)
                lease.Dispose();
        }
    }

    public async Task MouseUpAsync(string button)
    {
        var normalized = NormalizeMouseButton(button);
        InputArbiter.InputArbiterLease? held;
        lock (_sync)
            _heldMouse.TryGetValue(normalized, out held);

        if (held is null)
        {
            using var operation = await _arbiter.AcquireAsync(_owner).ConfigureAwait(false);
            SendMouse(GetMouseUpFlag(normalized));
            return;
        }

        try
        {
            SendMouse(GetMouseUpFlag(normalized));
        }
        catch (Exception ex)
        {
            _arbiter.MarkFaulted(_owner, $"释放鼠标 {normalized} 失败：{ex.Message}");
            throw;
        }
        finally
        {
            lock (_sync)
                _heldMouse.Remove(normalized);
            held.Dispose();
        }
    }

    public async Task MouseWheelAsync(int delta, CancellationToken token)
    {
        using var lease = await _arbiter.AcquireAsync(_owner, token).ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        var input = new INPUT
        {
            Type = InputMouse,
            Union = new INPUTUNION
            {
                Mouse = new MOUSEINPUT
                {
                    MouseData = unchecked((uint)delta),
                    Flags = MouseWheelFlag
                }
            }
        };
        Send(ref input);
    }

    public async Task KeyPressAsync(string key, int holdMs, CancellationToken token)
    {
        using var operation = await _arbiter.AcquireAsync(_owner, token).ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        await KeyDownCoreAsync(key, token).ConfigureAwait(false);
        try
        {
            await Task.Delay(holdMs, token).ConfigureAwait(false);
        }
        finally
        {
            await KeyUpCoreAsync(key).ConfigureAwait(false);
        }
    }

    public async Task KeyDownAsync(string key, CancellationToken token)
    {
        var normalized = NormalizeKey(key);
        lock (_sync)
        {
            if (_heldKeys.ContainsKey(normalized))
                return;
        }

        var lease = await _arbiter.AcquireAsync(_owner, token).ConfigureAwait(false);
        var stored = false;
        try
        {
            token.ThrowIfCancellationRequested();
            lock (_sync)
            {
                if (_heldKeys.ContainsKey(normalized))
                    return;
                try
                {
                    SendKeyboard(GetVirtualKey(normalized), 0);
                    _heldKeys.Add(normalized, lease);
                    stored = true;
                }
                catch (Exception ex)
                {
                    _arbiter.MarkFaulted(_owner, $"按下按键 {normalized} 失败：{ex.Message}");
                    throw;
                }
            }
        }
        finally
        {
            if (!stored)
                lease.Dispose();
        }
    }

    public async Task KeyUpAsync(string key)
    {
        await KeyUpCoreAsync(key).ConfigureAwait(false);
    }

    public async Task TypeTextAsync(string text, CancellationToken token)
    {
        using var lease = await _arbiter.AcquireAsync(_owner, token).ConfigureAwait(false);
        try
        {
            foreach (var rune in text.EnumerateRunes())
            {
                token.ThrowIfCancellationRequested();
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
                    SendUnicode(high, false);
                    SendUnicode(low, false);
                    SendUnicode(high, true);
                    SendUnicode(low, true);
                }
            }
            token.ThrowIfCancellationRequested();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _arbiter.MarkFaulted(_owner, "Unicode 文本输入失败，键盘状态可能不完整：" + ex.Message);
            throw;
        }
    }

    public Task<bool> ReleaseAllAsync()
    {
        KeyValuePair<string, InputArbiter.InputArbiterLease>[] keys;
        KeyValuePair<string, InputArbiter.InputArbiterLease>[] mouse;
        lock (_sync)
        {
            keys = _heldKeys.ToArray();
            mouse = _heldMouse.ToArray();
        }

        var succeeded = true;
        var failures = new List<string>();
        foreach (var pair in keys)
        {
            try
            {
                SendKeyboard(GetVirtualKey(pair.Key), KeyUpFlag);
            }
            catch (Exception ex)
            {
                succeeded = false;
                failures.Add($"key {pair.Key}: {ex.Message}");
            }
        }

        foreach (var pair in mouse)
        {
            try
            {
                SendMouse(GetMouseUpFlag(pair.Key));
            }
            catch (Exception ex)
            {
                succeeded = false;
                failures.Add($"mouse {pair.Key}: {ex.Message}");
            }
        }

        if (!succeeded)
            _arbiter.MarkFaulted(_owner, "实例输入清理失败；全局输入已锁定。 " + string.Join("; ", failures));

        lock (_sync)
        {
            _heldKeys.Clear();
            _heldMouse.Clear();
        }
        foreach (var pair in keys)
            pair.Value.Dispose();
        foreach (var pair in mouse)
            pair.Value.Dispose();

        return Task.FromResult(succeeded);
    }

    private async Task ClickCoreAsync(string button, int holdMs, CancellationToken token)
    {
        await MouseDownCoreAsync(button, token).ConfigureAwait(false);
        try
        {
            await Task.Delay(holdMs, token).ConfigureAwait(false);
        }
        finally
        {
            await MouseUpAsync(button).ConfigureAwait(false);
        }
    }

    private async Task MouseDownCoreAsync(string button, CancellationToken token)
    {
        var normalized = NormalizeMouseButton(button);
        var lease = await _arbiter.AcquireAsync(_owner, token).ConfigureAwait(false);
        var stored = false;
        try
        {
            token.ThrowIfCancellationRequested();
            lock (_sync)
            {
                if (_heldMouse.ContainsKey(normalized))
                    return;
                try
                {
                    SendMouse(GetMouseDownFlag(normalized));
                    _heldMouse.Add(normalized, lease);
                    stored = true;
                }
                catch (Exception ex)
                {
                    _arbiter.MarkFaulted(_owner, $"按下鼠标 {normalized} 失败：{ex.Message}");
                    throw;
                }
            }
        }
        finally
        {
            if (!stored)
                lease.Dispose();
        }
    }

    private async Task KeyDownCoreAsync(string key, CancellationToken token)
    {
        var normalized = NormalizeKey(key);
        var lease = await _arbiter.AcquireAsync(_owner, token).ConfigureAwait(false);
        var stored = false;
        try
        {
            token.ThrowIfCancellationRequested();
            lock (_sync)
            {
                if (_heldKeys.ContainsKey(normalized))
                    return;
                try
                {
                    SendKeyboard(GetVirtualKey(normalized), 0);
                    _heldKeys.Add(normalized, lease);
                    stored = true;
                }
                catch (Exception ex)
                {
                    _arbiter.MarkFaulted(_owner, $"按下按键 {normalized} 失败：{ex.Message}");
                    throw;
                }
            }
        }
        finally
        {
            if (!stored)
                lease.Dispose();
        }
    }

    private async Task KeyUpCoreAsync(string key)
    {
        var normalized = NormalizeKey(key);
        InputArbiter.InputArbiterLease? held;
        lock (_sync)
            _heldKeys.TryGetValue(normalized, out held);

        if (held is null)
        {
            using var operation = await _arbiter.AcquireAsync(_owner).ConfigureAwait(false);
            SendKeyboard(GetVirtualKey(normalized), KeyUpFlag);
            return;
        }

        try
        {
            SendKeyboard(GetVirtualKey(normalized), KeyUpFlag);
        }
        catch (Exception ex)
        {
            _arbiter.MarkFaulted(_owner, $"释放按键 {normalized} 失败：{ex.Message}");
            throw;
        }
        finally
        {
            lock (_sync)
                _heldKeys.Remove(normalized);
            held.Dispose();
        }
    }

    private static void MoveToCore(int x, int y)
    {
        if (!SetCursorPos(x, y))
            throw new InvalidOperationException($"SetCursorPos 失败：{Marshal.GetLastWin32Error()}");
    }

    private static string NormalizeMouseButton(string button) =>
        button.Trim().ToLowerInvariant() is "left" or "right" or "middle"
            ? button.Trim().ToLowerInvariant()
            : throw new ArgumentException($"不支持的鼠标按钮：{button}", nameof(button));

    private static string NormalizeKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("按键名称不能为空。", nameof(key));
        var normalized = key.Trim().ToUpperInvariant();
        _ = GetVirtualKey(normalized);
        return normalized;
    }

    private static uint GetMouseDownFlag(string button) => button switch
    {
        "left" => MouseLeftDown,
        "right" => MouseRightDown,
        "middle" => MouseMiddleDown,
        _ => throw new ArgumentException($"不支持的鼠标按钮：{button}")
    };

    private static uint GetMouseUpFlag(string button) => button switch
    {
        "left" => MouseLeftUp,
        "right" => MouseRightUp,
        "middle" => MouseMiddleUp,
        _ => throw new ArgumentException($"不支持的鼠标按钮：{button}")
    };

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
