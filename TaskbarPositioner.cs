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
        public int lParam;
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
