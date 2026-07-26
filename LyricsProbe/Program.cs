using TaskbarLyrics;

var service = new LyricsService();
var track = new TrackInfo(
    "成都", "赵雷", TimeSpan.Zero, TimeSpan.FromSeconds(328), true, "QQMusic.exe");
var lyrics = await service.GetLyricsAsync(track, CancellationToken.None);
Console.WriteLine($"lines={lyrics.Count}");
Console.WriteLine(lyrics.Count > 0 ? $"{lyrics[0].Time}: {lyrics[0].Text}" : "NO_LYRICS");
Environment.ExitCode = lyrics.Count > 0 ? 0 : 1;
