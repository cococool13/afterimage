using System.Diagnostics;

namespace Afterimage;

sealed class ReplayBuffer : IDisposable
{
    readonly Settings _settings;
    readonly object _gate = new();
    Process? _ffmpeg;
    LoopbackPipe? _audio;
    System.Threading.Timer? _follow;
    string? _ffmpegPath;
    nint _hwnd;
    int _saving;
    int _generation;

    public ReplayBuffer(Settings settings) => _settings = settings;

    string _status = "starting";

    public string TargetName { get; private set; } = "";

    public bool IsRunning
    {
        get
        {
            try
            {
                if (_ffmpeg is { HasExited: false }) return true;
            }
            catch { }
            if (_status == "recording") _status = "paused";
            return false;
        }
    }

    public string Status => IsRunning ? "recording" : _status;

    public async Task EnsureFfmpegAsync(CancellationToken cancel)
    {
        _status = "getting FFmpeg";
        _ffmpegPath = await Ffmpeg.EnsureAsync(cancel);
        _status = "ready";
    }

    public void Start()
    {
        lock (_gate) StartCore();
    }

    void StartCore()
    {
        StopCore();
        if (_ffmpegPath is null) throw new InvalidOperationException("ffmpeg missing");

        Directory.CreateDirectory(Paths.Buffer);
        foreach (var leftover in Directory.EnumerateFiles(Paths.Buffer, "seg_*.ts"))
        {
            try { File.Delete(leftover); } catch { }
        }
        Directory.CreateDirectory(_settings.ClipsFolder);

        var target = GameWindow.Pick(_hwnd);
        _hwnd = target.Hwnd;
        TargetName = target.Name;
        var video = target.Hwnd != 0
            ? $"gfxcapture=hwnd={(ulong)target.Hwnd}:width=1920:height=1080:resize_mode=scale:scale_mode=bilinear:max_framerate=60:capture_cursor=0:display_border=0"
            : "gfxcapture=monitor_idx=0:width=1920:height=1080:resize_mode=scale:scale_mode=bilinear:max_framerate=60:capture_cursor=0:display_border=0";

        var pipeName = "ClipAudio-" + Environment.ProcessId;
        var wrap = _settings.Seconds + 4;
        var args = new List<string>
        {
            "-hide_banner", "-loglevel", "quiet", "-nostdin", "-nostats", "-y",
            "-threads", "1", "-filter_threads", "1",
            "-fflags", "+genpts",
            "-init_hw_device", "d3d11va=hw",
            "-filter_hw_device", "hw",
            "-filter_complex", video,
        };

        try
        {
            _audio = new LoopbackPipe(pipeName);
            _audio.Start();
            args.AddRange([
                "-thread_queue_size", "32",
                "-probesize", "32",
                "-analyzeduration", "0",
                "-f", "f32le",
                "-ar", _audio.SampleRate.ToString(),
                "-ac", _audio.Channels.ToString(),
                "-i", @"\\.\pipe\" + pipeName,
                "-map", "0:v:0",
                "-map", "1:a:0",
            ]);
        }
        catch (Exception ex)
        {
            Log.Line("audio skipped: " + ex.Message);
            _audio?.Dispose();
            _audio = null;
        }

        args.AddRange([
            "-c:v", "h264_nvenc",
            "-preset", "p1",
            "-tune", "ull",
            "-rc", "constqp",
            "-qp", "23",
            "-bf", "0",
            "-g", "60",
            "-rc-lookahead", "0",
            "-delay", "0",
            "-allow_sw", "0",
        ]);
        if (_audio is not null)
            args.AddRange(["-c:a", "aac", "-b:a", "128k", "-ac", "2"]);

        args.AddRange([
            "-f", "segment",
            "-segment_time", "1",
            "-segment_wrap", wrap.ToString(),
            "-reset_timestamps", "1",
            "-segment_format", "mpegts",
            Path.Combine(Paths.Buffer, "seg_%03d.ts"),
        ]);

        var proc = Ffmpeg.Start(_ffmpegPath, args, belowNormal: true, redirectError: false, raiseEvents: true);
        Interlocked.Increment(ref _generation);
        var gen = _generation;
        var started = DateTime.UtcNow;
        void Fallback(object? o, EventArgs? e)
        {
            if (gen != Volatile.Read(ref _generation)) return;
            if (DateTime.UtcNow - started < TimeSpan.FromSeconds(3) && _hwnd != 0)
            {
                Log.Line("window capture died; falling back to monitor");
                lock (_gate)
                {
                    if (gen != Volatile.Read(ref _generation)) return;
                    _hwnd = 0;
                    TargetName = "";
                    StartCore();
                }
            }
        }
        proc.Exited += Fallback;
        if (proc.HasExited) Fallback(null, null);
        _ffmpeg = proc;
        _status = "recording";
        _follow = new System.Threading.Timer(Follow, null, 1000, 1000);
        Log.Line($"buffer start {_settings.Seconds}s 1080p {(target.Hwnd == 0 ? "monitor" : target.Name)}");
    }

    void Follow(object? _)
    {
        if (!Monitor.TryEnter(_gate)) return;
        try
        {
            if (_ffmpegPath is null || Volatile.Read(ref _saving) != 0) return;
            if (_ffmpeg is null || _ffmpeg.HasExited) return;
            var next = GameWindow.Pick(_hwnd);
            if (next.Hwnd == _hwnd) return;
            if (next.Hwnd == 0 && _hwnd != 0 && GameWindow.IsAlive(_hwnd)) return;
            Log.Line($"retarget {(next.Hwnd == 0 ? "monitor" : next.Name)}");
            StartCore();
        }
        catch (Exception ex)
        {
            Log.Line("follow: " + ex.Message);
        }
        finally
        {
            Monitor.Exit(_gate);
        }
    }

    public void Stop()
    {
        lock (_gate) StopCore();
    }

    void StopCore()
    {
        Interlocked.Increment(ref _generation);
        _follow?.Dispose();
        _follow = null;
        var proc = _ffmpeg;
        _ffmpeg = null;
        if (proc is not null)
        {
            try
            {
                if (!proc.HasExited)
                {
                    proc.Kill(entireProcessTree: true);
                    proc.WaitForExit(1500);
                }
            }
            catch { }
            proc.Dispose();
        }
        _audio?.Dispose();
        _audio = null;
        _status = "paused";
    }

    public async Task<string?> SaveAsync()
    {
        if (Interlocked.Exchange(ref _saving, 1) == 1) return null;
        try
        {
            if (_ffmpegPath is null) return null;
            if (IsRunning) await WaitForSegmentClose().ConfigureAwait(false);
            else return null;

            if (!Directory.Exists(Paths.Buffer)) return null;
            var picked = SegmentPicker.Pick(new DirectoryInfo(Paths.Buffer).EnumerateFiles("seg_*.ts"), _settings.Seconds);
            if (picked.Length == 0) return null;

            Directory.CreateDirectory(_settings.ClipsFolder);
            var existing = Directory.EnumerateFiles(_settings.ClipsFolder).Select(p => Path.GetFileName(p)!);
            var dest = Path.Combine(_settings.ClipsFolder, ClipName.FileName(DateTime.Now, existing!));
            Directory.CreateDirectory(Paths.Root);
            var listPath = Path.Combine(Paths.Root, "concat.txt");
            await File.WriteAllLinesAsync(listPath, picked.Select(static f =>
                "file '" + f.FullName.Replace('\\', '/').Replace("'", @"'\''") + "'")).ConfigureAwait(false);

            using var mux = Ffmpeg.Start(_ffmpegPath, [
                "-hide_banner", "-loglevel", "error", "-nostdin", "-threads", "1", "-y",
                "-fflags", "+genpts",
                "-f", "concat", "-safe", "0", "-i", listPath,
                "-c", "copy", dest,
            ], belowNormal: true, redirectError: true);
            var errTask = mux.StandardError.ReadToEndAsync();
            await mux.WaitForExitAsync().ConfigureAwait(false);
            var err = await errTask.ConfigureAwait(false);
            if (mux.ExitCode != 0 || !File.Exists(dest))
            {
                Log.Line("save failed: " + err);
                return null;
            }
            Log.Line("saved " + dest);
            return dest;
        }
        finally
        {
            Interlocked.Exchange(ref _saving, 0);
        }
    }

    static async Task WaitForSegmentClose()
    {
        var current = NewestName();
        if (current is null) return;
        var limit = DateTime.UtcNow.AddMilliseconds(1100);
        while (DateTime.UtcNow < limit)
        {
            await Task.Delay(30).ConfigureAwait(false);
            var name = NewestName();
            if (name is not null && name != current) return;
        }
    }

    static string? NewestName()
    {
        if (!Directory.Exists(Paths.Buffer)) return null;
        FileInfo? best = null;
        foreach (var f in new DirectoryInfo(Paths.Buffer).EnumerateFiles("seg_*.ts"))
        {
            if (best is null || f.LastWriteTimeUtc > best.LastWriteTimeUtc) best = f;
        }
        return best?.Name;
    }

    public void Dispose() => Stop();
}
