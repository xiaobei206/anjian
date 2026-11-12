using System.Runtime.InteropServices;

namespace Anjian.App.Services;

/// <summary>
/// Routes classic global playback hotkeys to the WinForms UI thread.
/// A low-level keyboard hook is used because F12 is reserved by Windows for debugger use
/// and cannot be reliably registered with RegisterHotKey.
/// </summary>
internal sealed class GlobalHotkeyManager : IDisposable
{
    public const int WindowMessage = 0x8000 + 0x42;
    public const int StartAction = 1;
    public const int PauseResumeAction = 2;
    public const int StopAction = 3;

    private const int WhKeyboardLl = 13;
    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyDown = 0x0104;
    private const int WmSysKeyUp = 0x0105;
    private const uint LlkhfInjected = 0x00000010;

    private const uint VkF10 = 0x79;
    private const uint VkF11 = 0x7A;
    private const uint VkF12 = 0x7B;

    private static readonly int[] ModifierKeys =
    [
        0x10, 0xA0, 0xA1, // Shift
        0x11, 0xA2, 0xA3, // Ctrl
        0x12, 0xA4, 0xA5, // Alt
        0x5B, 0x5C        // Windows
    ];

    private readonly nint _windowHandle;
    private readonly LowLevelKeyboardProc _callback;
    private readonly HashSet<uint> _capturedKeys = [];
    private nint _hook;

    public int LastError { get; }
    public bool IsInstalled => _hook != 0;

    public GlobalHotkeyManager(nint windowHandle)
    {
        _windowHandle = windowHandle;
        _callback = KeyboardCallback;
        _hook = SetWindowsHookEx(WhKeyboardLl, _callback, GetModuleHandle(null), 0);
        if (_hook == 0)
            LastError = Marshal.GetLastWin32Error();
    }

    private nint KeyboardCallback(int code, nint wParam, nint lParam)
    {
        if (code >= 0)
        {
            try
            {
                var message = unchecked((int)wParam);
                var data = Marshal.PtrToStructure<KbdLlHookStruct>(lParam);
                var key = data.VirtualKey;

                if (key is VkF10 or VkF11 or VkF12)
                {
                    var isDown = message is WmKeyDown or WmSysKeyDown;
                    var isUp = message is WmKeyUp or WmSysKeyUp;

                    // Consume repeats and keyup for a keydown already captured by this app.
                    if (_capturedKeys.Contains(key))
                    {
                        if (isUp)
                            _capturedKeys.Remove(key);
                        return (nint)1;
                    }

                    // Ignore injected keystrokes so a script cannot trigger its own controls.
                    if (isDown && (data.Flags & LlkhfInjected) == 0 &&
                        (data.Flags & 0x00000020) == 0 && !AnyModifierPressed())
                    {
                        var action = key switch
                        {
                            VkF10 => StartAction,
                            VkF11 => PauseResumeAction,
                            VkF12 => StopAction,
                            _ => 0
                        };

                        if (PostMessage(_windowHandle, WindowMessage, (nint)action, 0))
                        {
                            _capturedKeys.Add(key);
                            return (nint)1;
                        }
                    }
                }
            }
            catch
            {
                // A hook callback must never prevent the system input chain from continuing.
            }
        }

        return CallNextHookEx(_hook, code, wParam, lParam);
    }

    private static bool AnyModifierPressed()
    {
        foreach (var key in ModifierKeys)
        {
            if ((GetAsyncKeyState(key) & 0x8000) != 0)
                return true;
        }
        return false;
    }

    public void Dispose()
    {
        if (_hook != 0)
        {
            UnhookWindowsHookEx(_hook);
            _hook = 0;
        }
        _capturedKeys.Clear();
        GC.KeepAlive(_callback);
    }

    private delegate nint LowLevelKeyboardProc(int code, nint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct KbdLlHookStruct
    {
        public uint VirtualKey;
        public uint ScanCode;
        public uint Flags;
        public uint Time;
        public nint ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetWindowsHookEx(int idHook, LowLevelKeyboardProc callback, nint module, uint threadId);

    [DllImport("user32.dll")]
    private static extern bool UnhookWindowsHookEx(nint hook);

    [DllImport("user32.dll")]
    private static extern nint CallNextHookEx(nint hook, int code, nint wParam, nint lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostMessage(nint hwnd, int message, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    private static extern nint GetModuleHandle(string? moduleName);
}
