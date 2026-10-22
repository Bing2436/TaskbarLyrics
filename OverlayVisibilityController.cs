using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;

namespace TaskbarLyrics;

/// <summary>Maintains the overlay independently of media/lyrics requests.</summary>
public sealed class OverlayVisibilityController : IDisposable
{
    private readonly Window _window;
    private readonly Func<TaskbarPlacement> _placement;
    private readonly DispatcherTimer _timer;
    private readonly WinEventProc _foregroundCallback;
    private IntPtr _foregroundHook;
    private bool _disposed;

    public OverlayVisibilityController(Window window, Func<TaskbarPlacement> placement)
    {
        _window = window;
        _placement = placement;
        // Keep the delegate alive for the whole lifetime of the unmanaged hook.
        _foregroundCallback = OnForegroundChanged;
        _timer = new DispatcherTimer(DispatcherPriority.Background, window.Dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _timer.Tick += OnTick;
        // OUTOFCONTEXT | SKIPOWNPROCESS: no injection into other applications.
        _foregroundHook = SetWinEventHook(3, 3, IntPtr.Zero,
            _foregroundCallback, 0, 0, 2);
        _timer.Start();
        Refresh();
    }

    public void Refresh()
    {
        if (_disposed || !_window.IsVisible)
            return;
        TaskbarPositioner.Place(_window, _placement());
        TaskbarPositioner.KeepAboveApplications(_window);
    }

    private void OnTick(object? sender, EventArgs e) => Refresh();

    private void OnForegroundChanged(IntPtr hook, uint eventType, IntPtr hwnd,
        int objectId, int childId, uint threadId, uint eventTime)
    {
        if (!_disposed && !_window.Dispatcher.HasShutdownStarted)
            _window.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(Refresh));
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _timer.Stop();
        _timer.Tick -= OnTick;
        if (_foregroundHook != IntPtr.Zero)
        {
            UnhookWinEvent(_foregroundHook);
            _foregroundHook = IntPtr.Zero;
        }
        GC.KeepAlive(_foregroundCallback);
    }

    private delegate void WinEventProc(IntPtr hook, uint eventType, IntPtr hwnd,
        int objectId, int childId, uint threadId, uint eventTime);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax,
        IntPtr module, WinEventProc callback, uint processId, uint threadId, uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWinEvent(IntPtr hook);
}
