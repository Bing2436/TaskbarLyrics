using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace TaskbarLyrics;

public static class TaskbarPositioner
{
    private const int AbmGetTaskbarPos = 5;
    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x00000020;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;

    public static void KeepAboveApplications(Window window)
    {
        if (!window.IsVisible)
            return;

        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
            return;

        // Reassert native Z-order even when WPF's Topmost property is already true.
        // Do not activate or show the window: user-requested hiding must be respected.
        const uint flags = 0x0001 | 0x0002 | 0x0010 | 0x0200;
        SetWindowPos(handle, new IntPtr(-1), 0, 0, 0, 0, flags);
    }

    public static void MakeClickThrough(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        var style = GetWindowLongPtr(handle, GwlExStyle).ToInt64();
        SetWindowLongPtr(handle, GwlExStyle,
            new IntPtr(style | WsExTransparent | WsExToolWindow | WsExNoActivate));
    }

    public static void Place(Window window, TaskbarPlacement placement)
    {
        var data = new AppBarData { cbSize = Marshal.SizeOf<AppBarData>() };
        if (SHAppBarMessage(AbmGetTaskbarPos, ref data) == IntPtr.Zero)
            return;

        // An auto-hidden taskbar can report a rectangle partly outside the display.
        // Keep lyrics inside the monitor even while the taskbar itself is hidden.
        var monitor = new MonitorInfo { cbSize = Marshal.SizeOf<MonitorInfo>() };
        if (GetMonitorInfo(MonitorFromRect(ref data.rc, 2), ref monitor))
        {
            var barWidth = Math.Clamp(data.rc.Right - data.rc.Left, 1,
                monitor.rcMonitor.Right - monitor.rcMonitor.Left);
            var height = Math.Clamp(data.rc.Bottom - data.rc.Top, 1,
                monitor.rcMonitor.Bottom - monitor.rcMonitor.Top);
            if (data.uEdge is 1 or 3)
                height = Math.Max(height, 40);
            data.rc.Left = Math.Clamp(data.rc.Left, monitor.rcMonitor.Left,
                monitor.rcMonitor.Right - barWidth);
            data.rc.Top = Math.Clamp(data.rc.Top, monitor.rcMonitor.Top,
                monitor.rcMonitor.Bottom - height);
            data.rc.Right = data.rc.Left + barWidth;
            data.rc.Bottom = data.rc.Top + height;
        }

        var source = PresentationSource.FromVisual(window);
        var dpi = source?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
        var topLeft = dpi.Transform(new System.Windows.Point(data.rc.Left, data.rc.Top));
        var bottomRight = dpi.Transform(new System.Windows.Point(data.rc.Right, data.rc.Bottom));

        var taskbarWidth = bottomRight.X - topLeft.X;
        var taskbarHeight = bottomRight.Y - topLeft.Y;
        var width = Math.Min(window.Width, Math.Max(260, taskbarWidth - 24));
        window.Width = width;
        window.Height = Math.Min(46, Math.Max(30, taskbarHeight - 2));

        window.Top = topLeft.Y + Math.Max(0, (taskbarHeight - window.Height) / 2);
        window.Left = placement switch
        {
            TaskbarPlacement.Left => topLeft.X + 8,
            TaskbarPlacement.Right => bottomRight.X - width - 174,
            _ => topLeft.X + (taskbarWidth - width) / 2
        };

        if (window.Left < topLeft.X + 4)
            window.Left = topLeft.X + 4;
        if (window.Left + width > bottomRight.X - 4)
            window.Left = bottomRight.X - width - 4;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AppBarData
    {
        public int cbSize;
        public IntPtr hWnd;
        public uint uCallbackMessage;
        public uint uEdge;
        public Rect rc;
        public IntPtr lParam;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int cbSize;
        public Rect rcMonitor;
        public Rect rcWork;
        public uint dwFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("shell32.dll")]
    private static extern IntPtr SHAppBarMessage(int dwMessage, ref AppBarData pData);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
        int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromRect(ref Rect rect, uint flags);

    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern IntPtr GetWindowLongPtr32(IntPtr hWnd, int nIndex);

    private static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex) =>
        IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, nIndex) : GetWindowLongPtr32(hWnd, nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern IntPtr SetWindowLongPtr32(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr value) =>
        IntPtr.Size == 8 ? SetWindowLongPtr64(hWnd, nIndex, value) : SetWindowLongPtr32(hWnd, nIndex, value);
}
