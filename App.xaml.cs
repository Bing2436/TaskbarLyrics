using System.Windows;
using System.IO;

namespace TaskbarLyrics;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // WPF's font subsystem reads the legacy "windir" variable directly.
        // Some launchers expose only SystemRoot, which otherwise makes the first
        // Window constructor fail with UriFormatException.
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("windir")))
        {
            var systemRoot = Environment.GetEnvironmentVariable("SystemRoot");
            if (!string.IsNullOrWhiteSpace(systemRoot))
                Environment.SetEnvironmentVariable("windir", systemRoot,
                    EnvironmentVariableTarget.Process);
        }

        base.OnStartup(e);
        WriteLifecycleLog($"OnStartup; pid={Environment.ProcessId}");
        DispatcherUnhandledException += (_, args) =>
        {
            WriteCrashLog(args.Exception);
        };

        try
        {
            var window = new MainWindow();
            WriteLifecycleLog("MainWindow constructed");
            MainWindow = window;
            window.Show();
            WriteLifecycleLog($"MainWindow shown; visible={window.IsVisible}");
        }
        catch (Exception exception)
        {
            WriteCrashLog(exception);
            System.Windows.MessageBox.Show(
                $"任务栏歌词启动失败：\n{exception.Message}\n\n详细信息已写入 diagnostics.log。",
                "任务栏歌词", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    private static void WriteCrashLog(Exception exception)
    {
        try
        {
            File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "startup.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] APPLICATION ERROR{Environment.NewLine}" +
                $"{exception}{Environment.NewLine}");
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TaskbarLyrics");
            Directory.CreateDirectory(directory);
            File.AppendAllText(Path.Combine(directory, "diagnostics.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] APPLICATION ERROR{Environment.NewLine}" +
                $"{exception}{Environment.NewLine}");
        }
        catch
        {
        }
    }

    private static void WriteLifecycleLog(string message)
    {
        try
        {
            File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "startup.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] APP {message}{Environment.NewLine}");
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TaskbarLyrics");
            Directory.CreateDirectory(directory);
            File.AppendAllText(Path.Combine(directory, "diagnostics.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] APP {message}{Environment.NewLine}");
        }
        catch
        {
        }
    }
}
