using System.Text.Json;

namespace Afterimage;

static class Paths
{
    public static readonly string Root =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Afterimage");

    public static string Buffer => Path.Combine(Root, "buffer");
    public static string FfmpegDir => Path.Combine(Root, "ffmpeg");
    public static string LogFile => Path.Combine(Root, "afterimage.log");

    public static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
}

static class Log
{
    static readonly object Gate = new();

    public static void Line(string message)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Paths.Root);
                File.AppendAllText(Paths.LogFile, $"{DateTime.Now:u} {message}{Environment.NewLine}");
            }
        }
        catch { /* logging must never throw */ }
    }
}
