using System.IO.Pipes;
using NAudio.Wave;

namespace Afterimage;

sealed class LoopbackPipe : IDisposable
{
    readonly string _pipeName;
    NamedPipeServerStream? _pipe;
    WasapiLoopbackCapture? _capture;
    int _busy;

    public LoopbackPipe(string pipeName) => _pipeName = pipeName;

    public int SampleRate { get; private set; } = 48000;
    public int Channels { get; private set; } = 2;
    public bool Connected => _pipe is { IsConnected: true };

    public void Start()
    {
        _pipe = new NamedPipeServerStream(
            _pipeName,
            PipeDirection.Out,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous,
            inBufferSize: 0,
            outBufferSize: 256 * 1024);

        _capture = new WasapiLoopbackCapture();
        var format = _capture.WaveFormat;
        if (format.Encoding != WaveFormatEncoding.IeeeFloat || format.BitsPerSample != 32)
            throw new InvalidOperationException("loopback is not 32-bit float");
        SampleRate = format.SampleRate;
        Channels = format.Channels;
        _capture.DataAvailable += OnData;
        _capture.RecordingStopped += (_, e) =>
        {
            if (e.Exception is not null) Log.Line("loopback stopped: " + e.Exception.Message);
        };

        _ = ConnectAsync();
    }

    async Task ConnectAsync()
    {
        try
        {
            if (_pipe is null || _capture is null) return;
            await _pipe.WaitForConnectionAsync().ConfigureAwait(false);
            _capture.StartRecording();
        }
        catch (Exception ex)
        {
            Log.Line("loopback connect failed: " + ex.Message);
        }
    }

    void OnData(object? sender, WaveInEventArgs e)
    {
        var pipe = _pipe;
        if (pipe is not { IsConnected: true } || e.BytesRecorded <= 0) return;
        if (Interlocked.Exchange(ref _busy, 1) == 1) return;
        try
        {
            pipe.Write(e.Buffer, 0, e.BytesRecorded);
        }
        catch (IOException)
        {
            // ffmpeg exited
        }
        catch (ObjectDisposedException) { }
        finally
        {
            Interlocked.Exchange(ref _busy, 0);
        }
    }

    public void Dispose()
    {
        try { _capture?.StopRecording(); } catch { }
        _capture?.Dispose();
        _capture = null;
        try { _pipe?.Dispose(); } catch { }
        _pipe = null;
    }
}
