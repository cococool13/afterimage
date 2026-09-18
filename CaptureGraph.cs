namespace Afterimage;

public static class CaptureGraph
{
    public readonly record struct Result(string Filter, string[] Maps);

    public static Result Build(string video, int pcmInputs)
    {
        if (string.IsNullOrWhiteSpace(video)) throw new ArgumentException("video");
        var labeled = video.EndsWith("[v]", StringComparison.Ordinal) ? video : video + "[v]";
        return pcmInputs switch
        {
            <= 0 => new(labeled, ["[v]"]),
            1 => new(labeled, ["[v]", "0:a:0"]),
            _ => new(
                labeled + ";[0:a][1:a]amix=inputs=2:duration=first:dropout_transition=0[a]",
                ["[v]", "[a]"]),
        };
    }

    public static int SelfCheck()
    {
        const string video = "gfxcapture=hwnd=1";
        var none = Build(video, 0);
        Check(none.Filter == "gfxcapture=hwnd=1[v]", "label v");
        Check(none.Maps is ["[v]"], "video-only maps");

        var loopback = Build(video, 1);
        Check(loopback.Filter == "gfxcapture=hwnd=1[v]", "loopback filter");
        Check(loopback.Maps is ["[v]", "0:a:0"], "loopback maps");
        Check(!loopback.Maps.Contains("0:v:0"), "video is not input 0");
        Check(!loopback.Maps.Contains("1:a:0"), "audio is not input 1");

        var mic = Build(video, 2);
        Check(mic.Filter.Contains("amix=inputs=2", StringComparison.Ordinal), "amix");
        Check(mic.Maps is ["[v]", "[a]"], "mic maps");

        var already = Build("gfxcapture=hwnd=1[v]", 0);
        Check(already.Filter == "gfxcapture=hwnd=1[v]", "no double label");

        Console.WriteLine("capture-graph ok");
        return 0;
    }

    static void Check(bool ok, string name)
    {
        if (!ok) throw new InvalidOperationException("fail: " + name);
    }
}
