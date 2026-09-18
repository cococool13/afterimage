using System.Diagnostics;
using Microsoft.Win32;

namespace Afterimage;

static class Install
{
    public static string Dir { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Afterimage");

    public static string Exe => Path.Combine(Dir, "Afterimage.exe");

    const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\Afterimage";

    public static bool IsInstalled
    {
        get
        {
            var src = Environment.ProcessPath;
            return src is not null && string.Equals(src, Exe, StringComparison.OrdinalIgnoreCase);
        }
    }

    public static bool TryRelocate()
    {
        var src = Environment.ProcessPath;
        if (src is null || IsInstalled) return false;
        try
        {
            Directory.CreateDirectory(Dir);
            File.Copy(src, Exe, overwrite: true);
            CopyCompanion(src, "ffmpeg.exe");
            Shortcuts.Write(Exe);
            Register(Exe);
            Process.Start(new ProcessStartInfo { FileName = Exe, UseShellExecute = true });
            Log.Line("installed to " + Exe);
            return true;
        }
        catch (Exception ex)
        {
            Log.Line("install: " + ex.Message);
            return false;
        }
    }

    static void CopyCompanion(string srcExe, string name)
    {
        var dir = Path.GetDirectoryName(srcExe);
        if (string.IsNullOrEmpty(dir)) return;
        var from = Path.Combine(dir, name);
        if (!File.Exists(from)) return;
        File.Copy(from, Path.Combine(Dir, name), overwrite: true);
    }

    public static void Register(string? exe = null)
    {
        try
        {
            exe ??= File.Exists(Exe) ? Exe : Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe) || !File.Exists(exe)) return;
            using var key = Registry.CurrentUser.CreateSubKey(UninstallKey);
            if (key is null) return;
            var ver = typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "1.5.0";
            key.SetValue("DisplayName", "Afterimage");
            key.SetValue("DisplayIcon", exe);
            key.SetValue("DisplayVersion", ver);
            key.SetValue("Publisher", "Cohen Coolidge");
            key.SetValue("InstallLocation", Path.GetDirectoryName(exe) ?? Dir);
            key.SetValue("UninstallString", $"\"{exe}\" --uninstall");
            key.SetValue("QuietUninstallString", $"\"{exe}\" --uninstall --quiet");
            key.SetValue("NoModify", 1, RegistryValueKind.DWord);
            key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            key.SetValue("EstimatedSize", (int)(new FileInfo(exe).Length / 1024), RegistryValueKind.DWord);
            key.SetValue("HelpLink", "https://afterimage-site.cohencool.workers.dev");
            key.SetValue("URLInfoAbout", "https://afterimage-site.cohencool.workers.dev");
        }
        catch (Exception ex)
        {
            Log.Line("register: " + ex.Message);
        }
    }

    public static bool Confirm()
    {
        var r = MessageBox.Show(
            "Remove Afterimage from this PC?\n\nClips in Videos stay.",
            "Afterimage",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.None,
            MessageBoxDefaultButton.Button2);
        return r == DialogResult.Yes;
    }

    public static bool Uninstall(bool quiet)
    {
        if (!quiet && !Confirm()) return false;
        Remove();
        return true;
    }

    public static void Remove()
    {
        KillOthers();
        try
        {
            using var run = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
            run?.DeleteValue("Afterimage", throwOnMissingValue: false);
        }
        catch { }
        try { Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, throwOnMissingSubKey: false); } catch { }
        try
        {
            var lnk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Afterimage.lnk");
            if (File.Exists(lnk)) File.Delete(lnk);
        }
        catch { }

        Log.Line("uninstalled");
        TryDeleteTree(Paths.Root);

        var self = Environment.ProcessPath;
        var runningFromInstall = self is not null && string.Equals(self, Exe, StringComparison.OrdinalIgnoreCase);
        if (runningFromInstall) DeleteLater(Dir);
        else TryDeleteTree(Dir);
    }

    static void KillOthers()
    {
        foreach (var p in Process.GetProcessesByName("Afterimage"))
        {
            if (p.Id == Environment.ProcessId) continue;
            try
            {
                p.Kill(entireProcessTree: true);
                p.WaitForExit(3000);
            }
            catch { }
        }
    }

    static void TryDeleteTree(string dir)
    {
        try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { }
    }

    static void DeleteLater(string dir)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c ping 127.0.0.1 -n 2 >nul & rmdir /s /q \"" + dir + "\"",
                CreateNoWindow = true,
                UseShellExecute = false,
            });
        }
        catch (Exception ex)
        {
            Log.Line("uninstall delay: " + ex.Message);
        }
    }
}
