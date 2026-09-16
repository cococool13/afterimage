using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http;

namespace Afterimage;

static class Ffmpeg
{
    const string ReleaseZip = "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip";

    public static Process Start(string exe, IReadOnlyList<string> args, bool belowNormal, bool redirectError = true, bool raiseEvents = false)
    {
        var psi = new ProcessStartInfo
        {
            FileName = exe,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = redirectError,
        };
        foreach (var arg in args) psi.ArgumentList.Add(arg);
        var proc = new Process { StartInfo = psi, EnableRaisingEvents = raiseEvents };
        proc.Start();
        if (belowNormal)
        {
            try { proc.PriorityClass = ProcessPriorityClass.BelowNormal; } catch { }
        }
        return proc;
    }

    public static string? Find()
    {
        var beside = Path.Combine(AppContext.BaseDirectory, "ffmpeg.exe");
        if (File.Exists(beside)) return beside;

        var cached = Path.Combine(Paths.FfmpegDir, "ffmpeg.exe");
        if (File.Exists(cached)) return cached;

        var onPath = FindOnPath();
        return onPath;
    }

    public static async Task<string> EnsureAsync(CancellationToken cancel, IProgress<int>? progress = null)
    {
        var existing = Find();
        if (existing is not null)
        {
            progress?.Report(100);
            return existing;
        }

        Directory.CreateDirectory(Paths.FfmpegDir);
        var zipPath = Path.Combine(Paths.FfmpegDir, "ffmpeg.zip");
        Log.Line("downloading ffmpeg");
        progress?.Report(-1);
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Afterimage/1.2");
        using (var response = await http.GetAsync(ReleaseZip, HttpCompletionOption.ResponseHeadersRead, cancel))
        {
            response.EnsureSuccessStatusCode();
            var total = response.Content.Headers.ContentLength ?? -1;
            await using var src = await response.Content.ReadAsStreamAsync(cancel);
            await using var dst = File.Create(zipPath);
            var buf = new byte[64 * 1024];
            long read = 0;
            int n;
            var last = -1;
            while ((n = await src.ReadAsync(buf.AsMemory(), cancel)) > 0)
            {
                await dst.WriteAsync(buf.AsMemory(0, n), cancel);
                read += n;
                if (total > 0)
                {
                    var pct = (int)(read * 90 / total);
                    if (pct != last)
                    {
                        last = pct;
                        progress?.Report(pct);
                    }
                }
            }
        }

        var dest = Path.Combine(Paths.FfmpegDir, "ffmpeg.exe");
        using (var zip = ZipFile.OpenRead(zipPath))
        {
            var entry = zip.Entries.FirstOrDefault(e =>
                e.Name.Equals("ffmpeg.exe", StringComparison.OrdinalIgnoreCase)
                && e.FullName.Replace('\\', '/').Contains("/bin/", StringComparison.OrdinalIgnoreCase))
                ?? zip.Entries.FirstOrDefault(e => e.Name.Equals("ffmpeg.exe", StringComparison.OrdinalIgnoreCase));
            if (entry is null) throw new InvalidOperationException("ffmpeg.exe missing from zip");
            entry.ExtractToFile(dest, overwrite: true);
        }
        progress?.Report(95);
        try { File.Delete(zipPath); } catch { }
        Log.Line("ffmpeg ready: " + dest);
        progress?.Report(100);
        return dest;
    }

    static string? FindOnPath()
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(path)) return null;
        foreach (var dir in path.Split(Path.PathSeparator))
        {
            try
            {
                var candidate = Path.Combine(dir, "ffmpeg.exe");
                if (File.Exists(candidate)) return candidate;
            }
            catch { }
        }
        return null;
    }
}
