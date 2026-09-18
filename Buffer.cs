using System.Diagnostics;

namespace Afterimage;

sealed class ReplayBuffer : IDisposable
{
    readonly Settings _settings;
    readonly object _gate = new();
    Process? _ffmpeg;
    PcmPipe? _audio;
    PcmPipe? _mic;
    System.Threading.Timer? _follow;
    string? _ffmpegPath;
    nint _hwnd;
    bool _armed;
    int _saving;
    int _generation;

    public ReplayBuffer(Settings settings) => _settings = settings;

    string _status = "starting";

    public string TargetName { get; private set; } = "";
    public int TargetPid { get; private set; }
    public string? LastPath { get; private set; }

    public bool NeedsAdmin => Admin.NeedsAdminFor(TargetPid);

    public bool IsRunning
    {
        get
        {
            try
            {
                if (_ffmpeg is { HasExited: false }) return true;
            }
            catch { }
            if (_status == "recording") _status = _armed ? "waiting" : "paused";
            return false;
        }
    }

    public bool Armed => _armed;
    public string Status => IsRunning ? "recording" : _status;

    public async Task EnsureFfmpegAsync(CancellationToken cancel, IProgress<int>? progress = null)
    {
        _status = "getting FFmpeg";
        _ffmpegPath = await Ffmpeg.EnsureAsync(cancel, progress);
        _status = "ready";
    }

    public void Start()
    {
        lock (_gate)
        {
            _armed = true;
            StartCore();
        }
    }

    void StartCore()
    {
        StopFfmpeg();
        if (_ffmpegPath is null) throw new InvalidOperationException("ffmpeg missing");
        if (!Gpu.HasNvidia())
        {
            _hwnd = 0;
            TargetName = "";
            TargetPid = 0;
            _status = "need NVIDIA";
            TryTrim();
            return;
        }
        EnsureFollow();

        Directory.CreateDirectory(Paths.Buffer);
        foreach (var leftover in Directory.EnumerateFiles(Paths.Buffer, "seg_*.ts"))
        {
            try { File.Delete(leftover); } catch { }
        }
        Directory.CreateDirectory(_settings.ClipsFolder);

        var target = GameWindow.Pick(_hwnd);
        if (target.Hwnd == 0)
        {
            _hwnd = 0;
            TargetName = "";
            TargetPid = 0;
            _status = "waiting";
            TryTrim();
            return;
        }

        _hwnd = target.Hwnd;
        TargetName = target.Name;
        TargetPid = target.Pid;
        var video = VideoFilter(target.Hwnd);

        var pipeName = "ClipAudio-" + Environment.ProcessId;
        var micName = "ClipMic-" + Environment.ProcessId;
        var wrap = _settings.Seconds + 4;
        var args = new List<string>
        {
            "-hide_banner", "-loglevel", "error", "-nostdin", "-nostats", "-y",
            "-threads", "1", "-filter_threads", "1",
            "-fflags", "+genpts",
            "-init_hw_device", "d3d11va=hw",
            "-filter_hw_device", "hw",
        };

        var pcm = 0;
        try
        {
            _audio = PcmPipe.StartLoopback(pipeName);
            pcm = 1;
            if (_settings.Mic)
            {
                try
                {
                    _mic = PcmPipe.StartMic(micName);
                    pcm = 2;
                }
                catch (Exception ex)
                {
                    Log.Line("mic skipped: " + ex.Message);
                    _mic?.Dispose();
                    _mic = null;
                }
            }
            if (_audio is not null) AddPcmInput(args, _audio, pipeName);
            if (_mic is not null) AddPcmInput(args, _mic, micName);
        }
        catch (Exception ex)
        {
            Log.Line("audio skipped: " + ex.Message);
            _audio?.Dispose();
            _audio = null;
            _mic?.Dispose();
            _mic = null;
            pcm = 0;
        }

        var graph = CaptureGraph.Build(video, pcm);
        args.Add("-filter_complex");
        args.Add(graph.Filter);
        foreach (var map in graph.Maps)
        {
            args.Add("-map");
            args.Add(map);
        }

        args.AddRange([
            "-c:v", "h264_nvenc",
            "-preset", _settings.NvencPreset,
            "-tune", "ull",
            "-rc", "constqp",
            "-qp", _settings.Quality == "quality" ? "21" : "26",
            "-bf", "0",
            "-g", "60",
            "-gpu", "0",
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

        var proc = Ffmpeg.Start(_ffmpegPath, args, belowNormal: true, redirectError: true, raiseEvents: true);
        var errTask = proc.StandardError.ReadToEndAsync();
        Interlocked.Increment(ref _generation);
        var gen = _generation;
        var started = DateTime.UtcNow;
        void Fallback(object? o, EventArgs? e)
        {
            if (gen != Volatile.Read(ref _generation)) return;
            _ = LogFfmpegError(errTask);
            if (DateTime.UtcNow - started < TimeSpan.FromSeconds(3) && _hwnd != 0)
            {
                Log.Line("window capture died; waiting for a game");
                lock (_gate)
                {
                    if (gen != Volatile.Read(ref _generation) || !_armed) return;
                    _hwnd = 0;
                    TargetName = "";
                    TargetPid = 0;
                    _status = "waiting";
                }
            }
        }
        proc.Exited += Fallback;
        if (proc.HasExited) Fallback(null, null);
        _ffmpeg = proc;
        _status = "recording";
        TryLowLatency(true);
        Log.Line($"buffer start {_settings.Seconds}s {_settings.NvencPreset} {target.Name}");
    }

    void Follow(object? _)
    {
        if (!Monitor.TryEnter(_gate)) return;
        try
        {
            if (!_armed || _ffmpegPath is null || Volatile.Read(ref _saving) != 0) return;
            var next = GameWindow.Pick(_hwnd);
            var live = _ffmpeg is { HasExited: false };
            if (live)
            {
                if (next.Hwnd == _hwnd) return;
                if (next.Hwnd == 0 && GameWindow.IsAlive(_hwnd)) return;
                if (next.Hwnd == 0)
                {
                    Log.Line("game gone; idle");
                    StopFfmpeg();
                    _hwnd = 0;
                    TargetName = "";
                    TargetPid = 0;
                    _status = "waiting";
                    TryTrim();
                    return;
                }
                Log.Line("retarget " + next.Name);
                StartCore();
                return;
            }
            if (next.Hwnd != 0) StartCore();
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
        lock (_gate)
        {
            _armed = false;
            StopFollow();
            StopFfmpeg();
            _status = "paused";
            TryLowLatency(false);
            TryTrim();
        }
    }

    void EnsureFollow() =>
        _follow ??= new System.Threading.Timer(Follow, null, 750, 750);

    void StopFollow()
    {
        _follow?.Dispose();
        _follow = null;
    }

    void StopFfmpeg()
    {
        Interlocked.Increment(ref _generation);
        var proc = _ffmpeg;
        _ffmpeg = null;
        if (proc is not null)
        {
            try
            {
                if (!proc.HasExited)
                {
                    proc.Kill(entireProcessTree: true);
                    if (!proc.WaitForExit(1500))
                        proc.WaitForExit(5000);
                }
            }
            catch { }
            proc.Dispose();
        }
        _audio?.Dispose();
        _audio = null;
        _mic?.Dispose();
        _mic = null;
    }

    static string VideoFilter(nint hwnd)
    {
        var id = $"hwnd={(ulong)hwnd}";
        var scale = GameWindow.BiggerThan1080(hwnd)
            ? ":width=1920:height=1080:resize_mode=scale:scale_mode=bilinear"
            : "";
        return $"gfxcapture={id}{scale}:max_framerate=60:capture_cursor=0:display_border=0";
    }

    static void TryLowLatency(bool on)
    {
        try
        {
            System.Runtime.GCSettings.LatencyMode = on
                ? System.Runtime.GCLatencyMode.SustainedLowLatency
                : System.Runtime.GCLatencyMode.Interactive;
        }
        catch { }
    }

    static void TryTrim()
    {
        try
        {
            using var p = Process.GetCurrentProcess();
            EmptyWorkingSet(p.Handle);
        }
        catch { }
    }

    [System.Runtime.InteropServices.DllImport("psapi.dll")]
    static extern bool EmptyWorkingSet(nint hProcess);

    public async Task<string?> SaveAsync()
    {
        if (Interlocked.Exchange(ref _saving, 1) == 1) return null;
        try
        {
            if (_ffmpegPath is null) return null;
            if (IsRunning) await WaitForSegmentClose().ConfigureAwait(false);
            else return null;

            if (!Directory.Exists(Paths.Buffer)) return null;
            var seconds = _settings.Seconds;
            var picked = SegmentPicker.Pick(new DirectoryInfo(Paths.Buffer).EnumerateFiles("seg_*.ts"), seconds);
            if (picked.Length == 0) return null;

            Directory.CreateDirectory(_settings.ClipsFolder);
            var existing = Directory.EnumerateFiles(_settings.ClipsFolder).Select(p => Path.GetFileName(p)!);
            var dest = Path.Combine(_settings.ClipsFolder, ClipName.FileName(DateTime.Now, existing!));
            var workDir = Path.Combine(Paths.Root, "mux");
            try { if (Directory.Exists(workDir)) Directory.Delete(workDir, true); } catch { }
            Directory.CreateDirectory(workDir);
            try
            {
                string[] lines;
                try
                {
                    lines = SegmentPicker.CopyForConcat(picked, workDir);
                }
                catch (Exception ex)
                {
                    Log.Line("stage failed: " + ex.Message);
                    return null;
                }
                var listPath = Path.Combine(workDir, "concat.txt");
                await File.WriteAllLinesAsync(listPath, lines).ConfigureAwait(false);

                using var mux = Ffmpeg.Start(_ffmpegPath, [
                    "-hide_banner", "-loglevel", "error", "-nostdin", "-threads", "1", "-y",
                    "-fflags", "+genpts",
                    "-f", "concat", "-safe", "0", "-i", listPath,
                    "-c", "copy", dest,
                ], belowNormal: true, redirectError: true);
                var errTask = mux.StandardError.ReadToEndAsync();
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                try
                {
                    await mux.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    try { mux.Kill(entireProcessTree: true); } catch { }
                    try { mux.WaitForExit(3000); } catch { }
                    Log.Line("save timed out");
                    TryDelete(dest);
                    return null;
                }
                var err = await errTask.ConfigureAwait(false);
                var len = 0L;
                try { if (File.Exists(dest)) len = new FileInfo(dest).Length; } catch { }
                if (mux.ExitCode != 0 || len == 0)
                {
                    Log.Line("save failed: " + err);
                    TryDelete(dest);
                    return null;
                }
            }
            finally
            {
                try { Directory.Delete(workDir, true); } catch { }
            }
            LastPath = dest;
            if (_settings.CapClips)
            {
                try
                {
                    var n = ClipCap.Prune(
                        _settings.ClipsFolder,
                        ClipCap.DefaultMaxFiles,
                        (long)ClipCap.DefaultMaxGb << 30);
                    if (n > 0) Log.Line("pruned " + n);
                }
                catch (Exception ex)
                {
                    Log.Line("prune: " + ex.Message);
                }
            }
            Log.Line("saved " + dest);
            return dest;
        }
        finally
        {
            Interlocked.Exchange(ref _saving, 0);
        }
    }

    static async Task LogFfmpegError(Task<string> errTask)
    {
        try
        {
            var err = (await errTask.ConfigureAwait(false)).Trim();
            if (err.Length > 0) Log.Line("ffmpeg: " + err);
        }
        catch { }
    }

    static void AddPcmInput(List<string> args, PcmPipe pipe, string pipeName) =>
        args.AddRange([
            "-thread_queue_size", "32",
            "-probesize", "32",
            "-analyzeduration", "0",
            "-f", "f32le",
            "-ar", pipe.SampleRate.ToString(),
            "-ac", pipe.Channels.ToString(),
            "-i", @"\\.\pipe\" + pipeName,
        ]);

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

    static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }

    public void Dispose() => Stop();
}
