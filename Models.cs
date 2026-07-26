namespace TaskbarLyrics;

public sealed record TrackInfo(
    string Title,
    string Artist,
    TimeSpan Position,
    TimeSpan Duration,
    bool IsPlaying,
    string SourceAppId);

public sealed record LyricLine(TimeSpan Time, string Text);

public enum TaskbarPlacement
{
    Left,
    Center,
    Right
}

public sealed class AppSettings
{
    public TaskbarPlacement Placement { get; set; } = TaskbarPlacement.Left;
    public bool StartWithWindows { get; set; }
    public double Width { get; set; } = 620;
}
