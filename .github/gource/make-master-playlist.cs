// Writes the HLS master playlist for the qualities render.sh produced, to stdout.
//
// Usage: dotnet run --file make-master-playlist.cs -- <hls directory>
//
// Each subdirectory holding an index.m3u8 is one quality. ffmpeg's own master playlist
// does not name every codec a browser needs to decide what it can play, so this one is
// written by hand: RESOLUTION, FRAME-RATE and CODECS from ffprobe, and BANDWIDTH as the
// peak segment bitrate measured from the files on disk.
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using static Tools;

if (args.Length < 1)
{
    Fail("usage: make-master-playlist.cs <hls directory>");
}
string root = args[0];
var variants = new List<(long Bandwidth, VideoStream Stream, string Codecs, double Rate, string Name)>();
foreach (string name in Directory.GetFileSystemEntries(root).Select(Path.GetFileName).Order(StringComparer.Ordinal)!)
{
    string directory = Path.Combine(root, name!);
    if (!File.Exists(Path.Combine(directory, "index.m3u8")))
    {
        continue;
    }
    VideoStream stream = Probe(directory);
    string[] rate = stream.AverageFrameRate.Split('/');
    variants.Add((PeakBitrate(directory), stream, CodecString(stream),
        double.Parse(rate[0], CultureInfo.InvariantCulture) / double.Parse(rate[1], CultureInfo.InvariantCulture), name!));
}

// OrderByDescending is stable, as Python's sort(reverse=True) is.
var lines = new List<string> { "#EXTM3U", "#EXT-X-VERSION:7", "#EXT-X-INDEPENDENT-SEGMENTS" };
foreach (var v in variants.OrderByDescending(v => v.Bandwidth))
{
    lines.Add($"#EXT-X-STREAM-INF:BANDWIDTH={v.Bandwidth},RESOLUTION={v.Stream.Width}x{v.Stream.Height}," +
              $"FRAME-RATE={v.Rate.ToString("F3", CultureInfo.InvariantCulture)},CODECS=\"{v.Codecs}\"");
    lines.Add($"{v.Name}/index.m3u8");
}
using var output = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false)) { NewLine = "\n" };
output.Write(string.Join('\n', lines) + "\n");

internal static class Tools
{
    // The video stream of one quality, as ffprobe reports it.
    //
    // The init segment alone lacks the level and frame rate, so ffprobe reads it together
    // with the first media segment, which is what a player would read first.
    internal static VideoStream Probe(string directory)
    {
        string first = Directory.GetFiles(directory).Select(Path.GetFileName)
            .Where(f => f!.EndsWith(".m4s", StringComparison.Ordinal)).Order(StringComparer.Ordinal).First()!;
        string sample = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".mp4");
        try
        {
            using (FileStream write = File.Create(sample))
            {
                foreach (string part in new[] { "init.mp4", first })
                {
                    write.Write(File.ReadAllBytes(Path.Combine(directory, part)));
                }
            }
            var start = new ProcessStartInfo("ffprobe")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = new UTF8Encoding(false),
                UseShellExecute = false,
            };
            foreach (string arg in new[] { "-v", "error", "-select_streams", "v:0", "-show_streams", "-of", "json", sample })
            {
                start.ArgumentList.Add(arg);
            }
            using Process ffprobe = Process.Start(start)!;
            Task<string> error = ffprobe.StandardError.ReadToEndAsync();
            string text = ffprobe.StandardOutput.ReadToEnd();
            ffprobe.WaitForExit();
            if (ffprobe.ExitCode != 0)
            {
                Console.Error.Write(error.Result);
                Fail($"ffprobe exited with {ffprobe.ExitCode} on {directory}");
            }
            JsonElement json = JsonDocument.Parse(text).RootElement.GetProperty("streams")[0];
            return new VideoStream(
                json.GetProperty("codec_name").GetString()!,
                json.TryGetProperty("profile", out JsonElement profile) ? profile.GetString() : null,
                json.TryGetProperty("level", out JsonElement level) ? level.GetInt32() : -99,
                json.GetProperty("width").GetInt32(),
                json.GetProperty("height").GetInt32(),
                json.GetProperty("avg_frame_rate").GetString()!);
        }
        finally
        {
            File.Delete(sample);
        }
    }

    // The RFC 6381 codec string a browser checks before choosing a quality.
    internal static string CodecString(VideoStream stream)
    {
        int level = stream.Level;
        if (stream.CodecName == "h264")
        {
            int profile = stream.Profile switch
            {
                "Baseline" or "Constrained Baseline" => 0x42,
                "Main" => 0x4D,
                _ => 0x64,
            };
            return $"avc1.{Pad(profile, "X")}00{Pad(level, "X")}";
        }
        if (stream.CodecName == "av1")
        {
            if (level < 0)
            {
                level = stream.Height > 2160 ? 16 : stream.Height > 1080 ? 12 : 8;
            }
            return $"av01.0.{Pad(level, "D")}M.08";
        }
        Fail($"make-master-playlist: no codec string for {stream.CodecName}");
        return "";
    }

    // A number at least two characters wide, zero-padded after any sign, as Python's :02X and :02d.
    internal static string Pad(int value, string format)
    {
        string digits = Math.Abs((long)value).ToString(format, CultureInfo.InvariantCulture);
        return value < 0 ? "-" + digits : digits.PadLeft(2, '0');
    }

    // The highest bits-per-second of any one segment in a media playlist.
    internal static long PeakBitrate(string directory)
    {
        long peak = 0;
        double duration = 0;
        foreach (string line in SplitLines(File.ReadAllText(Path.Combine(directory, "index.m3u8"), new UTF8Encoding(false))))
        {
            Match match = Regex.Match(line, @"^#EXTINF:([\d.]+)");
            if (match.Success)
            {
                duration = double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            }
            else if (line.Length > 0 && !line.StartsWith('#') && duration != 0)
            {
                long size = new FileInfo(Path.Combine(directory, line)).Length;
                peak = Math.Max(peak, (long)(size * 8 / duration));
                duration = 0;
            }
        }
        return peak;
    }

    internal static void Fail(string message)
    {
        Console.Error.WriteLine(message.StartsWith("make-master-playlist", StringComparison.Ordinal) ? message : $"make-master-playlist: {message}");
        Environment.Exit(1);
    }

    // Python's str.splitlines(): every line boundary Python recognises, no trailing empty line.
    internal static List<string> SplitLines(string value)
    {
        var lines = new List<string>();
        int begin = 0;
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (c is '\n' or '\r' or '\v' or '\f' or '\x1c' or '\x1d' or '\x1e' or '\x85' or (char)0x2028 or (char)0x2029)
            {
                lines.Add(value[begin..i]);
                if (c == '\r' && i + 1 < value.Length && value[i + 1] == '\n') i++;
                begin = i + 1;
            }
        }
        if (begin < value.Length) lines.Add(value[begin..]);
        return lines;
    }
}

// The fields of ffprobe's video stream report this playlist uses.
internal sealed record VideoStream(string CodecName, string? Profile, int Level, int Width, int Height, string AverageFrameRate);
