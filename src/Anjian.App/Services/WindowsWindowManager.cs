using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Anjian.Core.Windows;

namespace Anjian.App.Services;

/// <summary>
/// Main-process window access. Scripts only receive opaque reference IDs, never raw HWNDs.
/// References are tombstoned by WinEvent destroy notifications and revalidated before every use.
/// </summary>
internal sealed class WindowsWindowManager : IDisposable
{
    private const uint EventObjectDestroy = 0x8001;
    private const uint WineventOutOfContext = 0x0000;
    private const int ObjIdWindow = 0;
    private const int ChildIdSelf = 0;
    private const uint MonitorDefaultToNearest = 2;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;
    private const int SwHide = 0;
    private const int SwShow = 5;
    private const int SwMinimize = 6;
    private const int SwMaximize = 3;
    private const int SwRestore = 9;

    private readonly ConcurrentDictionary<string, WindowReference> _references = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<nint, ConcurrentBag<WindowReference>> _referencesByHandle = new();
    private readonly WinEventProc _eventCallback;
    private readonly nint _destroyHook;
    private bool _disposed;

    public WindowsWindowManager()
    {
        _eventCallback = OnWinEvent;
        _destroyHook = SetWinEventHook(
            EventObjectDestroy, EventObjectDestroy, 0, _eventCallback, 0, 0, WineventOutOfContext);
    }

    public IReadOnlyList<WindowInfo> Enumerate(WindowSelector selector)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(selector);
        var results = new List<WindowInfo>();
        EnumWindows((handle, _) =>
        {
            if (results.Count >= 512)
                return false;

            try
            {
                if (!IsWindow(handle) || (selector.VisibleOnly && !IsWindowVisible(handle)))
                    return true;

                var title = ReadWindowText(handle);
                var className = ReadClassName(handle);
                if (!MatchesText(selector.TitleContains, title, contains: true)
                    || !MatchesText(selector.ClassName, className, contains: false))
                    return true;

                var threadId = GetWindowThreadProcessId(handle, out var processId);
                if (threadId == 0 || processId == 0 || processId > int.MaxValue)
                    return true;

                var processName = ReadProcessName(processId, out var processStartTicks);
                if (selector.ProcessId > 0 && selector.ProcessId != (int)processId)
                    return true;
                if (!MatchesProcessName(selector.ProcessName, processName))
                    return true;

                var windowRect = ReadWindowRect(handle);
                var clientRect = ReadClientRect(handle);
                var reference = RegisterReference(handle, processId, threadId, className, processStartTicks);
                results.Add(CreateInfo(reference, title, processName, windowRect, clientRect));
            }
            catch (Win32Exception)
            {
                // A window can disappear while it is being enumerated. Skip that snapshot.
            }
            catch (ArgumentException)
            {
                // The owning process may exit between enumeration and its metadata lookup.
            }

            return true;
        }, 0);

        return results;
    }

    public IReadOnlyList<WindowInfo> EnumerateChildren(string parentId)
    {
        var parent = Resolve(parentId);
        var results = new List<WindowInfo>();
        EnumChildWindows(parent.Handle, (handle, _) =>
        {
            if (results.Count >= 512)
                return false;

            try
            {
                if (!IsWindow(handle))
                    return true;

                var threadId = GetWindowThreadProcessId(handle, out var processId);
                if (threadId == 0 || processId == 0 || processId > int.MaxValue)
                    return true;

                var className = ReadClassName(handle);
                var processName = ReadProcessName(processId, out var processStartTicks);
                var reference = RegisterReference(handle, processId, threadId, className, processStartTicks, isChild: true);
                results.Add(CreateInfo(
                    reference,
                    ReadWindowText(handle),
                    processName,
                    ReadWindowRect(handle),
                    ReadClientRect(handle)));
            }
            catch (Win32Exception)
            {
                // Child windows can be destroyed while the hierarchy is being enumerated.
            }
            catch (ArgumentException)
            {
                // The owning process may exit during enumeration.
            }

            return true;
        }, 0);

        EnsureCurrent(parent);
        return results;
    }

    public WindowInfo? Find(WindowSelector selector) => Enumerate(selector).FirstOrDefault();

    public WindowInfo GetInfo(string id)
    {
        var reference = Resolve(id);
        return CreateInfo(
            reference,
            ReadWindowText(reference.Handle),
            ReadProcessName(reference.ProcessId, out _),
            ReadWindowRect(reference.Handle),
            ReadClientRect(reference.Handle));
    }

    public CoordinatePoint ConvertCoordinate(WindowCoordinateRequest request, bool toScreen)
    {
        ArgumentNullException.ThrowIfNull(request);
        var reference = Resolve(request.Id);
        if (!Enum.TryParse<CoordinateSpace>(request.Space, ignoreCase: true, out var space))
            throw new ArgumentException("坐标空间必须是 screen、window、client、anchor 或 ratio。", nameof(request));

        var anchor = ParseAnchor(request.Anchor);
        var geometry = new WindowGeometry(
            ReadWindowRect(reference.Handle),
            ReadClientRect(reference.Handle));
        var point = new CoordinatePoint(request.X, request.Y);
        return toScreen
            ? CoordinateConverter.ToScreen(point, space, geometry, anchor)
            : CoordinateConverter.FromScreen(point, space, geometry, anchor);
    }

    public bool Activate(string id)
    {
        var reference = Resolve(id);
        EnsureCurrent(reference);
        _ = ShowWindow(reference.Handle, SwRestore);
        return SetForegroundWindow(reference.Handle);
    }

    public bool Show(string id) => ShowState(id, SwShow);
    public bool Hide(string id) => ShowState(id, SwHide);
    public bool Minimize(string id) => ShowState(id, SwMinimize);
    public bool Maximize(string id) => ShowState(id, SwMaximize);
    public bool Restore(string id) => ShowState(id, SwRestore);

    public bool SetTopmost(string id, bool enabled)
    {
        var reference = Resolve(id);
        EnsureCurrent(reference);
        var insertAfter = enabled ? new nint(-1) : new nint(-2);
        SetPosition(reference.Handle, insertAfter, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate);
        return true;
    }

    public bool Move(string id, int x, int y)
    {
        var reference = Resolve(id);
        var bounds = ReadWindowRect(reference.Handle);
        EnsureCurrent(reference);
        var position = ToWindowPosition(reference, x, y);
        SetPosition(reference.Handle, 0, position.X, position.Y, bounds.Width, bounds.Height, SwpNoZOrder | SwpNoActivate);
        return true;
    }

    public bool Resize(string id, int width, int height)
    {
        if (width is < 1 or > 32768 || height is < 1 or > 32768)
            throw new ArgumentOutOfRangeException(nameof(width), "窗口宽高必须在 1 到 32768 个物理像素之间。");

        var reference = Resolve(id);
        var bounds = ReadWindowRect(reference.Handle);
        EnsureCurrent(reference);
        var position = ToWindowPosition(reference, bounds.Left, bounds.Top);
        SetPosition(reference.Handle, 0, position.X, position.Y, width, height, SwpNoZOrder | SwpNoActivate);
        return true;
    }

    public bool Align(string id, WindowAnchor anchor, int offsetX, int offsetY)
    {
        var reference = Resolve(id);
        var bounds = ReadWindowRect(reference.Handle);
        EnsureCurrent(reference);
        var monitor = MonitorFromWindow(reference.Handle, MonitorDefaultToNearest);
        var monitorInfo = new MonitorInfo { Size = (uint)Marshal.SizeOf<MonitorInfo>() };
        if (monitor == 0 || !GetMonitorInfo(monitor, ref monitorInfo))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "无法读取目标窗口所在显示器的工作区。");

        var work = ToPixelRect(monitorInfo.Work);
        var location = CoordinateConverter.AlignWindow(bounds, work, anchor, offsetX, offsetY);
        var position = ToWindowPosition(reference, location.X, location.Y);
        SetPosition(reference.Handle, 0, position.X, position.Y, bounds.Width, bounds.Height, SwpNoZOrder | SwpNoActivate);
        return true;
    }

    public async Task<WindowInfo?> WaitForWindowAsync(
        WindowSelector selector,
        int timeoutMilliseconds,
        CancellationToken cancellationToken)
    {
        ValidateTimeout(timeoutMilliseconds);
        var stopwatch = Stopwatch.StartNew();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var match = Find(selector);
            if (match is not null)
                return match;

            var remaining = timeoutMilliseconds - (int)Math.Min(int.MaxValue, stopwatch.ElapsedMilliseconds);
            if (remaining <= 0)
                return null;

            await Task.Delay(Math.Min(100, remaining), cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<bool> WaitForClosedAsync(
        string id,
        int timeoutMilliseconds,
        CancellationToken cancellationToken)
    {
        ValidateTimeout(timeoutMilliseconds);
        var reference = GetReference(id);
        if (!IsCurrent(reference))
        {
            Invalidate(reference);
            return true;
        }

        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.ElapsedMilliseconds < timeoutMilliseconds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay((int)Math.Min(100, timeoutMilliseconds - stopwatch.ElapsedMilliseconds), cancellationToken)
                .ConfigureAwait(false);
            if (!IsCurrent(reference))
            {
                Invalidate(reference);
                return true;
            }
        }

        return !IsCurrent(reference);
    }

    public void ClearReferences()
    {
        _references.Clear();
        _referencesByHandle.Clear();
    }

    private bool ShowState(string id, int command)
    {
        var reference = Resolve(id);
        EnsureCurrent(reference);
        _ = ShowWindow(reference.Handle, command);
        return true;
    }

    private WindowReference RegisterReference(
        nint handle,
        uint processId,
        uint threadId,
        string className,
        long processStartTicks,
        bool isChild = false)
    {
        if (_destroyHook == 0)
            throw new InvalidOperationException(
                "无法安装 Windows 窗口销毁事件跟踪；为避免 HWND 销毁后复用导致误操作，本次拒绝创建窗口引用。");

        var id = Guid.NewGuid().ToString("N");
        var reference = new WindowReference(id, handle, processId, threadId, className, processStartTicks, isChild);
        _references[id] = reference;
        _referencesByHandle.AddOrUpdate(
            handle,
            _ => new ConcurrentBag<WindowReference>([reference]),
            (_, existing) =>
            {
                existing.Add(reference);
                return existing;
            });
        return reference;
    }

    private WindowReference Resolve(string id)
    {
        var reference = GetReference(id);
        EnsureCurrent(reference);
        return reference;
    }

    private WindowReference GetReference(string id)
    {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(id) || !_references.TryGetValue(id, out var reference))
            throw new InvalidOperationException("窗口引用不存在或已过期；请重新执行 WindowFind/WindowEnumerate。");
        return reference;
    }

    private void EnsureCurrent(WindowReference reference)
    {
        if (!IsCurrent(reference))
        {
            Invalidate(reference);
            throw new InvalidOperationException("目标窗口已关闭或窗口句柄已失效；请重新查找窗口，不会继续使用旧引用。");
        }
    }

    private bool IsCurrent(WindowReference reference)
    {
        try
        {
            if (Volatile.Read(ref reference.Destroyed) != 0 || !IsWindow(reference.Handle))
                return false;

            var threadId = GetWindowThreadProcessId(reference.Handle, out var processId);
            if (threadId != reference.ThreadId || processId != reference.ProcessId)
                return false;

            if (!string.Equals(ReadClassName(reference.Handle), reference.ClassName, StringComparison.Ordinal))
                return false;

            if (reference.ProcessStartTicks == 0)
                return true;

            using var process = Process.GetProcessById(checked((int)reference.ProcessId));
            return process.StartTime.ToUniversalTime().Ticks == reference.ProcessStartTicks;
        }
        catch (Win32Exception)
        {
            // The HWND may be destroyed between IsWindow and one of the metadata calls.
            return false;
        }
        catch (ArgumentException)
        {
            // The owning process may exit while validating its reference.
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private void Invalidate(WindowReference reference)
    {
        Interlocked.Exchange(ref reference.Destroyed, 1);
        _references.TryRemove(reference.Id, out _);
    }

    private WindowInfo CreateInfo(
        WindowReference reference,
        string title,
        string processName,
        PixelRect windowRect,
        PixelRect clientRect) =>
        new(
            reference.Id,
            title,
            reference.ClassName,
            checked((int)reference.ProcessId),
            processName,
            IsWindowVisible(reference.Handle),
            IsIconic(reference.Handle),
            IsZoomed(reference.Handle),
            GetForegroundWindow() == reference.Handle,
            IsFocused(reference.Handle, reference.ThreadId),
            reference.IsChild,
            windowRect,
            clientRect,
            DestroyTracking: _destroyHook != 0);

    private static string ReadWindowText(nint handle)
    {
        var length = Math.Clamp(GetWindowTextLength(handle), 0, 32767);
        var builder = new StringBuilder(Math.Max(2, length + 1));
        _ = GetWindowText(handle, builder, builder.Capacity);
        return builder.ToString();
    }

    private static string ReadClassName(nint handle)
    {
        var builder = new StringBuilder(512);
        if (GetClassName(handle, builder, builder.Capacity) == 0)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "读取窗口类名失败。");
        return builder.ToString();
    }

    private static string ReadProcessName(uint processId, out long processStartTicks)
    {
        processStartTicks = 0;
        try
        {
            using var process = Process.GetProcessById(checked((int)processId));
            processStartTicks = process.StartTime.ToUniversalTime().Ticks;
            return process.ProcessName;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static bool IsFocused(nint handle, uint threadId)
    {
        var info = new GuiThreadInfo { Size = (uint)Marshal.SizeOf<GuiThreadInfo>() };
        if (!GetGUIThreadInfo(threadId, ref info) || info.Focus == 0)
            return false;
        return info.Focus == handle || IsChild(handle, info.Focus);
    }

    private static PixelRect ReadWindowRect(nint handle)
    {
        if (!GetWindowRectNative(handle, out var rect))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "读取窗口外框矩形失败。");
        return ToPixelRect(rect);
    }

    private static PixelRect ReadClientRect(nint handle)
    {
        if (!GetClientRectNative(handle, out var client))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "读取窗口客户区矩形失败。");

        var origin = new Point { X = 0, Y = 0 };
        if (!ClientToScreen(handle, ref origin))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "客户区到屏幕坐标转换失败。");

        return new PixelRect(
            origin.X,
            origin.Y,
            checked(origin.X + client.Right),
            checked(origin.Y + client.Bottom));
    }

    private static PixelRect ToPixelRect(Rect rect) =>
        new(rect.Left, rect.Top, rect.Right, rect.Bottom);

    private static bool MatchesText(string filter, string value, bool contains) =>
        string.IsNullOrWhiteSpace(filter)
        || (contains
            ? value.Contains(filter.Trim(), StringComparison.OrdinalIgnoreCase)
            : string.Equals(value, filter.Trim(), StringComparison.OrdinalIgnoreCase));

    private static bool MatchesProcessName(string filter, string value)
    {
        if (string.IsNullOrWhiteSpace(filter))
            return true;
        var requested = Path.GetFileNameWithoutExtension(filter.Trim());
        return string.Equals(value, requested, StringComparison.OrdinalIgnoreCase);
    }

    private static WindowAnchor ParseAnchor(string value) =>
        (value ?? string.Empty).Trim().ToLowerInvariant().Replace('_', '-') switch
        {
            "top-left" => WindowAnchor.TopLeft,
            "top-center" or "top-middle" => WindowAnchor.TopCenter,
            "top-right" => WindowAnchor.TopRight,
            "center-left" or "middle-left" => WindowAnchor.CenterLeft,
            "center" or "middle" => WindowAnchor.Center,
            "center-right" or "middle-right" => WindowAnchor.CenterRight,
            "bottom-left" => WindowAnchor.BottomLeft,
            "bottom-center" or "bottom-middle" => WindowAnchor.BottomCenter,
            "bottom-right" => WindowAnchor.BottomRight,
            _ => throw new ArgumentException("锚点必须是 top-left、top-center、top-right、center-left、center、center-right、bottom-left、bottom-center 或 bottom-right。", nameof(value))
        };

    private static void ValidateTimeout(int timeoutMilliseconds)
    {
        if (timeoutMilliseconds is < 0 or > 600000)
            throw new ArgumentOutOfRangeException(nameof(timeoutMilliseconds), "窗口等待超时必须在 0 到 600000 毫秒之间。");
    }

    /// <summary>
    /// Public Move/Align coordinates are always virtual-desktop screen pixels.
    /// SetWindowPos expects parent-client coordinates for child HWNDs, so convert
    /// only at the Win32 boundary and keep the public coordinate contract stable.
    /// </summary>
    private static (int X, int Y) ToWindowPosition(WindowReference reference, int screenX, int screenY)
    {
        if (!reference.IsChild)
            return (screenX, screenY);

        var parent = GetParent(reference.Handle);
        if (parent == 0 || !IsWindow(parent))
            throw new InvalidOperationException("目标子窗口的父窗口已关闭，无法按屏幕坐标定位。");

        var point = new Point { X = screenX, Y = screenY };
        if (!ScreenToClient(parent, ref point))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "无法将屏幕坐标转换为父窗口客户区坐标。");

        return (point.X, point.Y);
    }

    private static void SetPosition(
        nint handle,
        nint insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags)
    {
        if (!SetWindowPos(handle, insertAfter, x, y, width, height, flags))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "SetWindowPos 失败；目标窗口权限或状态可能不允许此操作。");
    }

    private void OnWinEvent(
        nint hook,
        uint eventType,
        nint handle,
        int objectId,
        int childId,
        uint eventThread,
        uint eventTime)
    {
        if (eventType != EventObjectDestroy || handle == 0 || objectId != ObjIdWindow || childId != ChildIdSelf)
            return;

        if (_referencesByHandle.TryGetValue(handle, out var references))
        {
            foreach (var reference in references)
                Interlocked.Exchange(ref reference.Destroyed, 1);
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(WindowsWindowManager));
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        ClearReferences();
        if (_destroyHook != 0)
            _ = UnhookWinEvent(_destroyHook);
        GC.KeepAlive(_eventCallback);
    }

    private sealed class WindowReference(
        string id,
        nint handle,
        uint processId,
        uint threadId,
        string className,
        long processStartTicks,
        bool isChild)
    {
        public string Id { get; } = id;
        public nint Handle { get; } = handle;
        public uint ProcessId { get; } = processId;
        public uint ThreadId { get; } = threadId;
        public string ClassName { get; } = className;
        public long ProcessStartTicks { get; } = processStartTicks;
        public bool IsChild { get; } = isChild;
        public int Destroyed;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X, Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct GuiThreadInfo
    {
        public uint Size;
        public uint Flags;
        public nint Active;
        public nint Focus;
        public nint Capture;
        public nint MenuOwner;
        public nint MoveSize;
        public nint Caret;
        public Rect CaretRect;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public uint Size;
        public Rect Monitor;
        public Rect Work;
        public uint Flags;
    }

    private delegate bool EnumWindowsProc(nint handle, nint lParam);
    private delegate void WinEventProc(
        nint hook,
        uint eventType,
        nint handle,
        int objectId,
        int childId,
        uint eventThread,
        uint eventTime);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc callback, nint lParam);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumChildWindows(nint parent, EnumWindowsProc callback, nint lParam);

    [DllImport("user32.dll", EntryPoint = "GetWindowTextLengthW", SetLastError = true)]
    private static extern int GetWindowTextLength(nint handle);

    [DllImport("user32.dll", EntryPoint = "GetWindowTextW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetWindowText(nint handle, StringBuilder text, int maxCount);

    [DllImport("user32.dll", EntryPoint = "GetClassNameW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetClassName(nint handle, StringBuilder className, int maxCount);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(nint handle);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(nint handle);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(nint handle);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsZoomed(nint handle);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(nint handle, out uint processId);

    [DllImport("user32.dll", EntryPoint = "GetWindowRect", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRectNative(nint handle, out Rect rect);

    [DllImport("user32.dll", EntryPoint = "GetClientRect", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetClientRectNative(nint handle, out Rect rect);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ClientToScreen(nint handle, ref Point point);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsChild(nint parent, nint child);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint GetParent(nint handle);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ScreenToClient(nint handle, ref Point point);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetGUIThreadInfo(uint threadId, ref GuiThreadInfo info);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(nint handle);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(nint handle, int command);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(nint handle, nint insertAfter, int x, int y, int width, int height, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint MonitorFromWindow(nint handle, uint flags);

    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetWinEventHook(
        uint eventMin,
        uint eventMax,
        nint module,
        WinEventProc callback,
        uint processId,
        uint threadId,
        uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWinEvent(nint hook);
}
