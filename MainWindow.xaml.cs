using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace TaskbarLyrics;

public partial class MainWindow : Window
{
    private readonly MediaSessionService _media = new();
    private readonly LyricsService _lyrics = new();
    private readonly SettingsService _settingsService = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Forms.NotifyIcon _trayIcon;
    private readonly System.Drawing.Icon _appIcon;
    private AppSettings _settings;
    private IReadOnlyList<LyricLine> _currentLyrics = [];
    private string _trackKey = string.Empty;
    private bool _allowClose;
    private bool _loadingLyrics;
    private DateTime _lastPositionUpdate = DateTime.MinValue;

    public MainWindow()
    {
        InitializeComponent();
        _settings = _settingsService.Load();
        Width = _settings.Width;

        _appIcon = LoadAppIcon();
        _trayIcon = CreateTrayIcon();
        SourceInitialized += OnSourceInitialized;
        Loaded += OnLoaded;
        Closing += OnClosing;
        _timer.Tick += OnTick;
        TraceWindow("constructed");
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        TaskbarPositioner.Place(this, _settings.Placement);
        TraceWindow($"loaded; visible={IsVisible}; left={Left}; top={Top}; width={Width}; height={Height}");
        try
        {
            await _media.InitializeAsync();
            _timer.Start();
        }
        catch
        {
            CurrentLine.Text = "无法读取系统媒体信息，请重启本程序";
        }
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        TaskbarPositioner.MakeClickThrough(this);
        TraceWindow($"source initialized; handle={new WindowInteropHelper(this).Handle}");
        if (PresentationSource.FromVisual(this) is HwndSource source)
            source.AddHook(WindowProc);
    }

    private IntPtr WindowProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int wmDisplayChange = 0x007E;
        const int wmSettingChange = 0x001A;
        if (message is wmDisplayChange or wmSettingChange)
            Dispatcher.BeginInvoke(() => TaskbarPositioner.Place(this, _settings.Placement));
        return IntPtr.Zero;
    }

    private async void OnTick(object? sender, EventArgs e)
    {
        if (DateTime.UtcNow - _lastPositionUpdate < TimeSpan.FromMilliseconds(700))
            return;
        _lastPositionUpdate = DateTime.UtcNow;

        try
        {
            var track = await _media.GetQqMusicAsync();
            if (track is null)
            {
                CurrentLine.Text = "QQ 音乐未播放";
                NextLine.Text = string.Empty;
                return;
            }

            var key = $"{track.Artist}\n{track.Title}";
            if (key != _trackKey && !_loadingLyrics)
            {
                _trackKey = key;
                _currentLyrics = [];
                CurrentLine.Text = $"{track.Title}  ·  {track.Artist}";
                NextLine.Text = "正在匹配同步歌词…";
                _loadingLyrics = true;
                try
                {
                    _currentLyrics = await _lyrics.GetLyricsAsync(track, _shutdown.Token);
                    if (_currentLyrics.Count == 0)
                        NextLine.Text = "暂未找到同步歌词";
                }
                finally
                {
                    _loadingLyrics = false;
                }
            }

            if (_currentLyrics.Count > 0)
                ShowLyricAt(track.Position);
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            CurrentLine.Text = "读取 QQ 音乐状态时出现问题";
            NextLine.Text = "程序会自动重试";
        }
    }

    private void ShowLyricAt(TimeSpan position)
    {
        var index = FindCurrentLine(position);
        if (index < 0)
        {
            CurrentLine.Text = "♪";
            NextLine.Text = _currentLyrics[0].Text;
            return;
        }

        CurrentLine.Text = _currentLyrics[index].Text;
        NextLine.Text = index + 1 < _currentLyrics.Count ? _currentLyrics[index + 1].Text : string.Empty;
    }

    private int FindCurrentLine(TimeSpan position)
    {
        var low = 0;
        var high = _currentLyrics.Count - 1;
        var result = -1;
        while (low <= high)
        {
            var middle = low + (high - low) / 2;
            if (_currentLyrics[middle].Time <= position)
            {
                result = middle;
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }
        return result;
    }

    public void Reveal()
    {
        Show();
        TaskbarPositioner.Place(this, _settings.Placement);
        TraceWindow("revealed by second launch");
    }

    private static void TraceWindow(string message)
    {
        try
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TaskbarLyrics");
            Directory.CreateDirectory(directory);
            File.AppendAllText(Path.Combine(directory, "diagnostics.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] WINDOW {message}{Environment.NewLine}");
        }
        catch
        {
        }
    }

    private Forms.NotifyIcon CreateTrayIcon()
    {
        var menu = new Forms.ContextMenuStrip();
        var toggle = new Forms.ToolStripMenuItem("隐藏歌词");
        toggle.Click += (_, _) =>
        {
            if (IsVisible)
            {
                Hide();
                toggle.Text = "显示歌词";
            }
            else
            {
                Show();
                TaskbarPositioner.Place(this, _settings.Placement);
                toggle.Text = "隐藏歌词";
            }
        };
        menu.Items.Add(toggle);

        var placement = new Forms.ToolStripMenuItem("显示位置");
        AddPlacementItem(placement, "左侧", TaskbarPlacement.Left);
        AddPlacementItem(placement, "居中", TaskbarPlacement.Center);
        AddPlacementItem(placement, "右侧", TaskbarPlacement.Right);
        menu.Items.Add(placement);

        var startup = new Forms.ToolStripMenuItem("开机启动")
        {
            Checked = _settings.StartWithWindows,
            CheckOnClick = true
        };
        startup.Click += (_, _) =>
        {
            _settings.StartWithWindows = startup.Checked;
            SettingsService.SetStartWithWindows(startup.Checked);
            _settingsService.Save(_settings);
        };
        menu.Items.Add(startup);
        menu.Items.Add(new Forms.ToolStripSeparator());

        var exit = new Forms.ToolStripMenuItem("退出");
        exit.Click += (_, _) =>
        {
            _allowClose = true;
            Close();
        };
        menu.Items.Add(exit);

        return new Forms.NotifyIcon
        {
            Text = "任务栏歌词",
            Icon = _appIcon,
            ContextMenuStrip = menu,
            Visible = true
        };
    }

    private void AddPlacementItem(Forms.ToolStripMenuItem parent, string text, TaskbarPlacement value)
    {
        var item = new Forms.ToolStripMenuItem(text) { Checked = _settings.Placement == value };
        item.Click += (_, _) =>
        {
            _settings.Placement = value;
            foreach (Forms.ToolStripMenuItem sibling in parent.DropDownItems)
                sibling.Checked = ReferenceEquals(sibling, item);
            _settingsService.Save(_settings);
            TaskbarPositioner.Place(this, value);
        };
        parent.DropDownItems.Add(item);
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        _timer.Stop();
        _shutdown.Cancel();
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _appIcon.Dispose();
        System.Windows.Application.Current.Shutdown();
    }

    private static System.Drawing.Icon LoadAppIcon()
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(Environment.ProcessPath))
                return System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath)
                       ?? (System.Drawing.Icon)SystemIcons.Application.Clone();
        }
        catch
        {
        }
        return (System.Drawing.Icon)SystemIcons.Application.Clone();
    }
}
