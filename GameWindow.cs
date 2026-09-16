using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Afterimage;

static class GameWindow
{
    public readonly record struct Target(nint Hwnd, int Pid, string Name);

    static readonly HashSet<string> Skip = new(StringComparer.OrdinalIgnoreCase)
    {
        "explorer", "searchhost", "searchapp", "shellexperiencehost",
        "startmenuexperiencehost", "textinputhost", "lockapp", "dwm",
        "afterimage", "gamebar", "gamebarftw", "xboxgamebarwidgets",
        "nvcontainer", "nvsphelper64", "nvidia share", "nvidia overlay",
        "logonui", "csrss", "conhost", "runtimebroker",
        "systemsettings", "taskmgr",
    };

    public static bool IsSkippedProcess(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return true;
        return Skip.Contains(Path.GetFileNameWithoutExtension(name));
    }

    public static bool IsAlive(nint hwnd) => hwnd != 0 && IsWindow(hwnd);

    public static Target Pick(nint sticky)
    {
        var fg = Root(GetForegroundWindow());
        if (IsGame(fg)) return Describe(fg);

        if (IsAlive(sticky) && IsGame(sticky)) return Describe(sticky);

        var full = FindFullscreen();
        if (full != 0) return Describe(full);

        return default;
    }

    public static int SelfCheck()
    {
        Check(IsSkippedProcess("explorer"), "explorer");
        Check(IsSkippedProcess("EXPLORER.EXE"), "exe suffix");
        Check(IsSkippedProcess("Afterimage"), "self");
        Check(!IsSkippedProcess("hl2"), "source game");
        Check(!IsSkippedProcess("FortniteClient-Win64-Shipping"), "fortnite");
        Check(!IsSkippedProcess("r5apex"), "apex");
        Console.WriteLine("game-window ok");
        return 0;
    }

    static nint FindFullscreen()
    {
        nint best = 0;
        var bestArea = 0L;
        EnumWindows((hwnd, _) =>
        {
            if (!IsGame(hwnd) || !CoversPrimary(hwnd)) return true;
            var area = Area(hwnd);
            if (area > bestArea)
            {
                bestArea = area;
                best = hwnd;
            }
            return true;
        }, 0);
        return best;
    }

    static bool IsGame(nint hwnd)
    {
        if (hwnd == 0 || !IsWindow(hwnd) || !IsWindowVisible(hwnd)) return false;
        if (GetWindow(hwnd, GwOwner) != 0) return false;
        if (Cloaked(hwnd)) return false;

        GetWindowThreadProcessId(hwnd, out var pid);
        if (pid == 0 || pid == Environment.ProcessId) return false;
        if (IsSkippedProcess(ProcessName((int)pid))) return false;

        var area = Area(hwnd);
        if (area < 800L * 600) return false;
        return CoversPrimary(hwnd) || area >= PrimaryArea() / 2;
    }

    static Target Describe(nint hwnd)
    {
        GetWindowThreadProcessId(hwnd, out var pid);
        return new Target(hwnd, (int)pid, ProcessName((int)pid));
    }

    static nint Root(nint hwnd) => hwnd == 0 ? 0 : GetAncestor(hwnd, GaRoot);

    static string ProcessName(int pid)
    {
        try
        {
            using var p = Process.GetProcessById(pid);
            return p.ProcessName;
        }
        catch
        {
            return "";
        }
    }

    static bool CoversPrimary(nint hwnd)
    {
        if (!GetWindowRect(hwnd, out var wr)) return false;
        var monitor = MonitorFromWindow(hwnd, MonitorDefaultToPrimary);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref info)) return false;
        var mr = info.Monitor;
        var interW = Math.Max(0, Math.Min(wr.Right, mr.Right) - Math.Max(wr.Left, mr.Left));
        var interH = Math.Max(0, Math.Min(wr.Bottom, mr.Bottom) - Math.Max(wr.Top, mr.Top));
        var monitorArea = Math.Max(1L, (long)(mr.Right - mr.Left) * (mr.Bottom - mr.Top));
        return (long)interW * interH >= monitorArea * 9 / 10;
    }

    static long Area(nint hwnd)
    {
        if (!GetWindowRect(hwnd, out var r)) return 0;
        return Math.Max(0, (long)(r.Right - r.Left) * (r.Bottom - r.Top));
    }

    static long PrimaryArea()
    {
        var w = GetSystemMetrics(SmCxScreen);
        var h = GetSystemMetrics(SmCyScreen);
        return Math.Max(1L, (long)w * h);
    }

    static bool Cloaked(nint hwnd) =>
        DwmGetWindowAttribute(hwnd, DwmwaCloaked, out var cloaked, sizeof(int)) == 0 && cloaked != 0;

    static void Check(bool ok, string name)
    {
        if (!ok) throw new InvalidOperationException("fail: " + name);
    }

    const int GwOwner = 4;
    const int GaRoot = 2;
    const int SmCxScreen = 0;
    const int SmCyScreen = 1;
    const int DwmwaCloaked = 14;
    const int MonitorDefaultToPrimary = 1;

    delegate bool EnumProc(nint hwnd, nint lParam);

    [DllImport("user32.dll")]
    static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    static extern nint GetAncestor(nint hwnd, int flags);

    [DllImport("user32.dll")]
    static extern nint GetWindow(nint hwnd, int cmd);

    [DllImport("user32.dll")]
    static extern bool IsWindow(nint hwnd);

    [DllImport("user32.dll")]
    static extern bool IsWindowVisible(nint hwnd);

    [DllImport("user32.dll")]
    static extern bool GetWindowRect(nint hwnd, out Rect rect);

    [DllImport("user32.dll")]
    static extern uint GetWindowThreadProcessId(nint hwnd, out uint pid);

    [DllImport("user32.dll")]
    static extern bool EnumWindows(EnumProc lpEnumFunc, nint lParam);

    [DllImport("user32.dll")]
    static extern nint MonitorFromWindow(nint hwnd, int flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);

    [DllImport("user32.dll")]
    static extern int GetSystemMetrics(int index);

    [DllImport("dwmapi.dll")]
    static extern int DwmGetWindowAttribute(nint hwnd, int attr, out int value, int size);

    [StructLayout(LayoutKind.Sequential)]
    struct Rect
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct MonitorInfo
    {
        public int Size;
        public Rect Monitor;
        public Rect Work;
        public int Flags;
    }
}
