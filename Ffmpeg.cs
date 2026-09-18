using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http;

namespace Afterimage;

static class Ffmpeg
{
    static readonly string[] Zips =
    [
        "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip",
        "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip",
    ];

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
        foreach (var dir in CandidateDirs())
        {
            var path = Path.Combine(dir, "ffmpeg.exe");
            if (File.Exists(path) && new FileInfo(path).Length > 0) return path;
        }
        return null;
    }

    static IEnumerable<string> CandidateDirs()
    {
        yield return AppContext.BaseDirectory;
        var proc = Environment.ProcessPath;
        if (proc is not null)
        {
            var dir = Path.GetDirectoryName(proc);
            if (!string.IsNullOrEmpty(dir)) yield return dir;
        }
        yield return Install.Dir;
        yield return Paths.FfmpegDir;
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
        var dest = Path.Combine(Paths.FfmpegDir, "ffmpeg.exe");
        Exception? last = null;
        foreach (var url in Zips)
        {
            try
            {
                Log.Line("downloading ffmpeg " + url);
                await DownloadAsync(url, zipPath, progress, cancel);
                Extract(zipPath, dest);
                try { File.Delete(zipPath); } catch { }
                Log.Line("ffmpeg ready: " + dest);
                progress?.Report(100);
                return dest;
            }
            catch (Exception ex)
            {
                last = ex;
                Log.Line("ffmpeg download failed: " + ex.Message);
                try { File.Delete(dest); } catch { }
            }
        }
        throw last ?? new InvalidOperationException("ffmpeg download failed");
    }

    static async Task DownloadAsync(string url, string zipPath, IProgress<int>? progress, CancellationToken cancel)
    {
        progress?.Report(-1);
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Afterimage/1.5");
        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancel);
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

    static void Extract(string zipPath, string dest)
    {
        using var zip = ZipFile.OpenRead(zipPath);
        var entry = zip.Entries.FirstOrDefault(e =>
                e.Name.Equals("ffmpeg.exe", StringComparison.OrdinalIgnoreCase)
                && e.FullName.Replace('\\', '/').Contains("/bin/", StringComparison.OrdinalIgnoreCase))
            ?? zip.Entries.FirstOrDefault(e => e.Name.Equals("ffmpeg.exe", StringComparison.OrdinalIgnoreCase));
        if (entry is null) throw new InvalidOperationException("ffmpeg.exe missing from zip");
        entry.ExtractToFile(dest, overwrite: true);
    }
}
