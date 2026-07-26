using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace TaskbarLyrics;

public sealed partial class LyricsService
{
    private readonly HttpClient _lrclib = new()
    {
        BaseAddress = new Uri("https://lrclib.net/"),
        Timeout = TimeSpan.FromSeconds(8)
    };
    private readonly HttpClient _qqMusic = new()
    {
        BaseAddress = new Uri("https://c.y.qq.com/"),
        Timeout = TimeSpan.FromSeconds(8)
    };
    private readonly string _cacheDirectory;
    private readonly string _logPath;

    public LyricsService()
    {
        _lrclib.DefaultRequestHeaders.UserAgent.ParseAdd("TaskbarLyrics/1.1 (Windows)");
        _qqMusic.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 TaskbarLyrics/1.1");
        _qqMusic.DefaultRequestHeaders.Referrer = new Uri("https://y.qq.com/");
        _cacheDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TaskbarLyrics", "lyrics");
        Directory.CreateDirectory(_cacheDirectory);
        _logPath = Path.Combine(Path.GetDirectoryName(_cacheDirectory)!, "diagnostics.log");
    }

    public async Task<IReadOnlyList<LyricLine>> GetLyricsAsync(
        TrackInfo track, CancellationToken cancellationToken)
    {
        var cachePath = Path.Combine(_cacheDirectory, SafeFileName($"{track.Artist} - {track.Title}.lrc"));
        if (File.Exists(cachePath))
            return ParseLrc(await File.ReadAllTextAsync(cachePath, cancellationToken));

        string? lrc = null;
        string provider = string.Empty;

        // QQ Music is the first choice. It has much better coverage for Chinese
        // releases and can distinguish live/remix versions via duration.
        try
        {
            lrc = await GetFromQqMusicAsync(track, cancellationToken);
            if (!string.IsNullOrWhiteSpace(lrc))
                provider = "QQ Music";
        }
        catch (Exception exception) when (exception is HttpRequestException
                                         or JsonException
                                         or TaskCanceledException)
        {
            Log($"QQ Music lookup failed: {exception.Message}");
        }

        try
        {
            if (string.IsNullOrWhiteSpace(lrc))
            {
                lrc = await GetFromLrclibAsync(track, cancellationToken);
                if (!string.IsNullOrWhiteSpace(lrc))
                    provider = "LRCLIB";
            }
        }
        catch (HttpRequestException exception)
        {
            Log($"LRCLIB lookup failed: {exception.Message}");
            return [];
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            Log("Lyrics lookup timed out.");
            return [];
        }

        if (string.IsNullOrWhiteSpace(lrc))
        {
            Log($"No synced lyrics: {track.Title} / {track.Artist}");
            return [];
        }

        await File.WriteAllTextAsync(cachePath, lrc, cancellationToken);
        var parsed = ParseLrc(lrc);
        Log($"{provider}: {track.Title} / {track.Artist}, {parsed.Count} lines");
        return parsed;
    }

    private async Task<string?> GetFromLrclibAsync(
        TrackInfo track, CancellationToken cancellationToken)
    {
        foreach (var title in TitleCandidates(track.Title))
        {
            var query = $"api/get?track_name={Uri.EscapeDataString(title)}" +
                        $"&artist_name={Uri.EscapeDataString(track.Artist)}";
            if (track.Duration.TotalSeconds > 1)
                query += $"&duration={(int)Math.Round(track.Duration.TotalSeconds)}";

            using var response = await _lrclib.GetAsync(query, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var result = await JsonSerializer.DeserializeAsync<LrcResult>(
                    await response.Content.ReadAsStreamAsync(cancellationToken),
                    JsonOptions, cancellationToken);
                if (!string.IsNullOrWhiteSpace(result?.SyncedLyrics))
                    return result.SyncedLyrics;
            }

            var searched = await SearchLrclibAsync(title, track.Artist, cancellationToken);
            if (!string.IsNullOrWhiteSpace(searched))
                return searched;
        }
        return null;
    }

    private async Task<string?> SearchLrclibAsync(
        string title, string artist, CancellationToken cancellationToken)
    {
        var query = $"api/search?track_name={Uri.EscapeDataString(title)}" +
                    $"&artist_name={Uri.EscapeDataString(artist)}";
        using var response = await _lrclib.GetAsync(query, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;

        var results = await JsonSerializer.DeserializeAsync<List<LrcResult>>(
            await response.Content.ReadAsStreamAsync(cancellationToken),
            JsonOptions, cancellationToken);
        return results?.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.SyncedLyrics))?.SyncedLyrics;
    }

    private async Task<string?> GetFromQqMusicAsync(
        TrackInfo track, CancellationToken cancellationToken)
    {
        QqSong? best = null;
        var bestScore = int.MinValue;

        foreach (var title in TitleCandidates(track.Title))
        {
            var keyword = string.IsNullOrWhiteSpace(track.Artist)
                ? title
                : $"{title} {track.Artist}";
            var query = "soso/fcgi-bin/client_search_cp?p=1&n=10&format=json&w=" +
                        Uri.EscapeDataString(keyword);
            using var response = await _qqMusic.GetAsync(query, cancellationToken);
            if (!response.IsSuccessStatusCode)
                continue;

            var result = await JsonSerializer.DeserializeAsync<QqSearchResult>(
                await response.Content.ReadAsStreamAsync(cancellationToken),
                JsonOptions, cancellationToken);
            foreach (var song in result?.Data?.Song?.List ?? [])
            {
                var score = ScoreSong(song, track, title);
                if (score > bestScore)
                {
                    best = song;
                    bestScore = score;
                }
            }
            if (bestScore >= 150)
                break;
        }

        if (best is null || bestScore < 65 || string.IsNullOrWhiteSpace(best.SongMid))
            return null;

        var lyricQuery = "lyric/fcgi-bin/fcg_query_lyric_new.fcg" +
                         $"?format=json&nobase64=1&g_tk=5381&songmid={Uri.EscapeDataString(best.SongMid)}";
        using var lyricResponse = await _qqMusic.GetAsync(lyricQuery, cancellationToken);
        if (!lyricResponse.IsSuccessStatusCode)
            return null;

        var lyric = await JsonSerializer.DeserializeAsync<QqLyricResult>(
            await lyricResponse.Content.ReadAsStreamAsync(cancellationToken),
            JsonOptions, cancellationToken);
        return string.IsNullOrWhiteSpace(lyric?.Lyric)
            ? null
            : System.Net.WebUtility.HtmlDecode(lyric.Lyric);
    }

    private static int ScoreSong(QqSong song, TrackInfo track, string candidateTitle)
    {
        var wantedTitle = Normalize(candidateTitle);
        var actualTitle = Normalize(song.SongName ?? string.Empty);
        var score = actualTitle == wantedTitle ? 100 :
            actualTitle.Contains(wantedTitle) || wantedTitle.Contains(actualTitle) ? 55 : 0;

        var wantedArtist = Normalize(track.Artist);
        if (!string.IsNullOrEmpty(wantedArtist))
        {
            var artists = song.Singer?.Select(x => Normalize(x.Name ?? string.Empty)).ToArray() ?? [];
            if (artists.Any(x => x == wantedArtist))
                score += 60;
            else if (artists.Any(x => x.Contains(wantedArtist) || wantedArtist.Contains(x)))
                score += 35;
        }

        if (track.Duration.TotalSeconds > 1 && song.Interval > 0)
        {
            var difference = Math.Abs(song.Interval - track.Duration.TotalSeconds);
            score += difference <= 3 ? 35 : difference <= 10 ? 15 : difference > 30 ? -20 : 0;
        }
        return score;
    }

    private static IEnumerable<string> TitleCandidates(string title)
    {
        title = title.Trim();
        yield return title;

        var cleaned = VersionSuffixRegex().Replace(title, "").Trim();
        if (!string.Equals(cleaned, title, StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(cleaned))
            yield return cleaned;
    }

    private static string Normalize(string value) =>
        NonWordRegex().Replace(value, "").ToLowerInvariant();

    private void Log(string message)
    {
        try
        {
            File.AppendAllText(_logPath,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
        }
        catch
        {
            // Diagnostics must never interrupt lyric display.
        }
    }

    public static IReadOnlyList<LyricLine> ParseLrc(string lrc)
    {
        var lines = new List<LyricLine>();
        var offset = TimeSpan.Zero;

        foreach (var rawLine in lrc.Replace("\r", "").Split('\n'))
        {
            var offsetMatch = OffsetRegex().Match(rawLine);
            if (offsetMatch.Success &&
                int.TryParse(offsetMatch.Groups[1].Value, out var milliseconds))
            {
                offset = TimeSpan.FromMilliseconds(milliseconds);
                continue;
            }

            var matches = TimestampRegex().Matches(rawLine);
            if (matches.Count == 0)
                continue;

            var text = TimestampRegex().Replace(rawLine, "").Trim();
            if (string.IsNullOrWhiteSpace(text))
                continue;

            foreach (Match match in matches)
            {
                if (!int.TryParse(match.Groups[1].Value, out var minutes) ||
                    !double.TryParse(match.Groups[2].Value, NumberStyles.Float,
                        CultureInfo.InvariantCulture, out var seconds))
                    continue;
                lines.Add(new LyricLine(TimeSpan.FromMinutes(minutes) +
                                        TimeSpan.FromSeconds(seconds) + offset, text));
            }
        }

        return lines.OrderBy(x => x.Time).ToArray();
    }

    private static string SafeFileName(string value)
    {
        foreach (var character in Path.GetInvalidFileNameChars())
            value = value.Replace(character, '_');
        return value.Length > 150 ? value[..150] + ".lrc" : value + ".lrc";
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed class LrcResult
    {
        public string? SyncedLyrics { get; set; }
    }

    private sealed class QqSearchResult
    {
        public QqData? Data { get; set; }
    }

    private sealed class QqData
    {
        public QqSongPage? Song { get; set; }
    }

    private sealed class QqSongPage
    {
        public List<QqSong>? List { get; set; }
    }

    private sealed class QqSong
    {
        public string? SongName { get; set; }
        public string? SongMid { get; set; }
        public int Interval { get; set; }
        public List<QqSinger>? Singer { get; set; }
    }

    private sealed class QqSinger
    {
        public string? Name { get; set; }
    }

    private sealed class QqLyricResult
    {
        public string? Lyric { get; set; }
    }

    [GeneratedRegex(@"\[(\d{1,3}):(\d{1,2}(?:\.\d{1,3})?)\]")]
    private static partial Regex TimestampRegex();

    [GeneratedRegex(@"\[offset:([+-]?\d+)\]", RegexOptions.IgnoreCase)]
    private static partial Regex OffsetRegex();

    [GeneratedRegex(@"\s*[\(\（\[\【].*?[\)\）\]\】]\s*$")]
    private static partial Regex VersionSuffixRegex();

    [GeneratedRegex(@"[\s\p{P}\p{S}]+")]
    private static partial Regex NonWordRegex();
}
