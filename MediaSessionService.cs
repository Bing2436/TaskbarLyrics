using Windows.Media.Control;

namespace TaskbarLyrics;

public sealed class MediaSessionService
{
    private GlobalSystemMediaTransportControlsSessionManager? _manager;

    public async Task InitializeAsync()
    {
        _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
    }

    public async Task<TrackInfo?> GetQqMusicAsync()
    {
        if (_manager is null)
            return null;

        var sessions = _manager.GetSessions();
        var session = sessions.FirstOrDefault(IsQqMusicSession);
        if (session is null)
            return null;

        var properties = await session.TryGetMediaPropertiesAsync();
        if (string.IsNullOrWhiteSpace(properties.Title))
            return null;

        var timeline = session.GetTimelineProperties();
        var playback = session.GetPlaybackInfo();
        var position = timeline.Position;
        var isPlaying = playback.PlaybackStatus ==
                        GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;

        if (isPlaying)
        {
            var elapsed = DateTimeOffset.UtcNow - timeline.LastUpdatedTime;
            if (elapsed > TimeSpan.Zero && elapsed < TimeSpan.FromSeconds(10))
                position += elapsed;
        }

        if (timeline.EndTime > TimeSpan.Zero && position > timeline.EndTime)
            position = timeline.EndTime;

        return new TrackInfo(
            properties.Title.Trim(),
            properties.Artist?.Trim() ?? string.Empty,
            position,
            timeline.EndTime,
            isPlaying,
            session.SourceAppUserModelId ?? string.Empty);
    }

    private static bool IsQqMusicSession(GlobalSystemMediaTransportControlsSession session)
    {
        var id = session.SourceAppUserModelId ?? string.Empty;
        return id.Contains("qqmusic", StringComparison.OrdinalIgnoreCase) ||
               id.Contains("tencent", StringComparison.OrdinalIgnoreCase);
    }
}
