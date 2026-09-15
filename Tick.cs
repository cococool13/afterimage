using System.Media;

namespace Afterimage;

static class Tick
{
    static readonly SoundPlayer OkPlayer = new(Tone(1400, 40, 1880, 55));
    static readonly SoundPlayer FailPlayer = new(Tone(220, 90));

    public static void Ok() => Play(OkPlayer);
    public static void Fail() => Play(FailPlayer);

    static void Play(SoundPlayer player)
    {
        try { player.Play(); }
        catch { }
    }

    static MemoryStream Tone(int hz1, int ms1, int hz2 = 0, int ms2 = 0)
    {
        const int sr = 22050;
        var gap = hz2 == 0 ? 0 : (int)(sr * 0.02);
        var n1 = sr * ms1 / 1000;
        var n2 = hz2 == 0 ? 0 : sr * ms2 / 1000;
        var samples = n1 + gap + n2;
        var stream = new MemoryStream(44 + samples * 2);
        using (var w = new BinaryWriter(stream, System.Text.Encoding.ASCII, leaveOpen: true))
        {
            Ascii(w, "RIFF");
            w.Write(36 + samples * 2);
            Ascii(w, "WAVEfmt ");
            w.Write(16);
            w.Write((short)1);
            w.Write((short)1);
            w.Write(sr);
            w.Write(sr * 2);
            w.Write((short)2);
            w.Write((short)16);
            Ascii(w, "data");
            w.Write(samples * 2);
            WriteTone(w, sr, hz1, n1);
            for (var i = 0; i < gap; i++) w.Write((short)0);
            if (n2 > 0) WriteTone(w, sr, hz2, n2);
        }
        stream.Position = 0;
        return stream;
    }

    static void Ascii(BinaryWriter w, string text) =>
        w.Write(System.Text.Encoding.ASCII.GetBytes(text));

    static void WriteTone(BinaryWriter w, int sr, int hz, int n)
    {
        for (var i = 0; i < n; i++)
        {
            var env = Math.Min(i, n - 1 - i);
            var amp = Math.Min(env, 400) / 400.0;
            var s = Math.Sin(2 * Math.PI * hz * i / sr) * amp * 0.28;
            w.Write((short)(s * short.MaxValue));
        }
    }
}
