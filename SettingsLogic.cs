namespace Afterimage;

public static class SettingsLogic
{
    public const bool DefaultOnboarded = false;
    public const bool DefaultCapClips = true;
    public const bool DefaultStartWithWindows = true;
    public const int DefaultSeconds = 20;
    public const string DefaultQuality = "fast";

    public static int ClampSeconds(int n) => n is 15 or 20 or 30 ? n : DefaultSeconds;

    public static string ClampQuality(string? q) => q is "fast" or "quality" ? q : DefaultQuality;

    public static string ClampFolder(string? folder, string fallback) =>
        string.IsNullOrWhiteSpace(folder) ? fallback : folder;

    public static bool PersistOnboarded(bool setupOk) => setupOk;

    public static int SelfCheck()
    {
        Check(ClampSeconds(1) == 20, "seconds clamp");
        Check(ClampSeconds(15) == 15, "seconds 15");
        Check(ClampSeconds(20) == 20, "seconds 20");
        Check(ClampSeconds(30) == 30, "seconds 30");
        Check(ClampQuality("slow") == "fast", "quality clamp");
        Check(ClampQuality("quality") == "quality", "quality keep");
        Check(ClampFolder(" ", "Videos/Afterimage") == "Videos/Afterimage", "folder");
        Check(!DefaultOnboarded, "onboarded off");
        Check(DefaultCapClips, "cap on");
        Check(DefaultStartWithWindows, "start with windows");
        Check(!PersistOnboarded(false), "setup fail stays off");
        Check(PersistOnboarded(true), "setup ok persists");
        Console.WriteLine("settings-logic ok");
        return 0;
    }

    static void Check(bool ok, string name)
    {
        if (!ok) throw new InvalidOperationException("fail: " + name);
    }
}
