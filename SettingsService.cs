using System.Text.Json;
using System.IO;
using Microsoft.Win32;

namespace TaskbarLyrics;

public sealed class SettingsService
{
    private readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TaskbarLyrics", "settings.json");

    public AppSettings Load()
    {
        try
        {
            return File.Exists(_path)
                ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path)) ?? new()
                : new();
        }
        catch
        {
            return new();
        }
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(settings,
            new JsonSerializerOptions { WriteIndented = true }));
    }

    public static void SetStartWithWindows(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Run", true);
        if (enabled)
            key?.SetValue("TaskbarLyrics", $"\"{Environment.ProcessPath}\"");
        else
            key?.DeleteValue("TaskbarLyrics", false);
    }
}
