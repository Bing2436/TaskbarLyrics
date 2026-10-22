using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using TaskbarLyrics;

internal static class OverlayProbe
{
    public static int Run()
    {
        var result = 1;
        var thread = new Thread(() =>
        {
            try { Test(); result = 0; }
            catch (Exception error) { Console.Error.WriteLine(error); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        return result;
    }

    private static void Test()
    {
        Environment.SetEnvironmentVariable("windir",
            Environment.GetEnvironmentVariable("SystemRoot"));
        var cover = new Window
        {
            Title = "Fullscreen overlay test (closes automatically)",
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            Left = 0, Top = 0,
            Width = SystemParameters.PrimaryScreenWidth,
            Height = SystemParameters.PrimaryScreenHeight,
            Topmost = true
        };
        var overlay = new Window
        {
            Title = "Lyrics overlay test", Width = 620, Height = 46,
            WindowStyle = WindowStyle.None, AllowsTransparency = true,
            Background = System.Windows.Media.Brushes.Transparent,
            ShowActivated = false, ShowInTaskbar = false, Topmost = true
        };
        try
        {
            cover.Show();
            overlay.Show();
            TaskbarPositioner.MakeClickThrough(overlay);
            using var visibility = new OverlayVisibilityController(overlay,
                () => TaskbarPlacement.Center);
            var coverHandle = new WindowInteropHelper(cover).Handle;
            var overlayHandle = new WindowInteropHelper(overlay).Handle;

            cover.Activate();
            SetWindowPos(coverHandle, new IntPtr(-1), 0, 0, 0, 0, 0x1 | 0x2 | 0x10);
            Check(IsAbove(coverHandle, overlayHandle), "fullscreen test window covers overlay before recovery");
            var foreground = GetForegroundWindow();
            Pump(TimeSpan.FromMilliseconds(1200));
            Check(IsAbove(overlayHandle, coverHandle), "timer restores lyrics above fullscreen topmost window");
            Check(GetForegroundWindow() == foreground, "recovery does not steal foreground focus");

            GetWindowRect(overlayHandle, out var rect);
            Check(rect.Top >= 0 && rect.Bottom <= SystemParameters.PrimaryScreenHeight *
                PresentationSource.FromVisual(overlay)!.CompositionTarget.TransformToDevice.M22,
                "lyrics stay inside the monitor");
            overlay.Hide();
            Pump(TimeSpan.FromMilliseconds(700));
            Check(!overlay.IsVisible && !IsWindowVisible(overlayHandle), "hidden lyrics remain hidden");
            overlay.Show();
            visibility.Refresh();
            Check(IsAbove(overlayHandle, coverHandle), "show restores overlay ordering");

            visibility.Dispose();
            SetWindowPos(coverHandle, new IntPtr(-1), 0, 0, 0, 0, 0x1 | 0x2 | 0x10);
            Pump(TimeSpan.FromMilliseconds(700));
            Check(IsAbove(coverHandle, overlayHandle), "dispose stops periodic maintenance");
        }
        finally
        {
            overlay.Close();
            cover.Close();
        }
    }

    private static bool IsAbove(IntPtr first, IntPtr second)
    {
        for (var next = GetWindow(first, 2); next != IntPtr.Zero; next = GetWindow(next, 2))
            if (next == second) return true;
        return false;
    }

    private static void Pump(TimeSpan duration)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = duration };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        Console.WriteLine($"PASS: {message}");
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr window, uint command);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out Rect rect);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr window,
        IntPtr after, int x, int y, int width, int height, uint flags);
}
