namespace Afterimage;

public static class StatusText
{
    public static string Line(bool running, string status, string? target = null)
    {
        if (running)
            return string.IsNullOrEmpty(target) ? "Recording" : "Recording · " + target;
        return status switch
        {
            "waiting" or "ready" or "starting" => "Waiting for a game",
            "paused" => "Paused",
            "need NVIDIA" => "Needs NVIDIA",
            "getting FFmpeg" => "Getting ready",
            "need FFmpeg" => "Setup didn't finish",
            "failed to start" => "Couldn't start",
            "no game" => "No game open",
            _ => string.IsNullOrEmpty(status) ? "Paused" : char.ToUpper(status[0]) + status[1..],
        };
    }

    public static int SelfCheck()
    {
        Check(Line(false, "waiting") == "Waiting for a game", "waiting");
        Check(Line(false, "ready") == "Waiting for a game", "ready");
        Check(Line(true, "waiting", "hl2") == "Recording · hl2", "recording");
        Check(Line(false, "no game") == "No game open", "no game");
        Check(Line(false, "need NVIDIA") == "Needs NVIDIA", "nvidia");
        Check(Line(false, "paused") == "Paused", "paused");
        Check(Line(false, "need FFmpeg") == "Setup didn't finish", "ffmpeg");
        Console.WriteLine("status-text ok");
        return 0;
    }

    static void Check(bool ok, string name)
    {
        if (!ok) throw new InvalidOperationException("fail: " + name);
    }
}
