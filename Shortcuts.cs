namespace Afterimage;

static class Shortcuts
{
    public static void EnsureStartMenu()
    {
        try
        {
            var exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe) || !File.Exists(exe)) return;
            var dir = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "Afterimage.lnk");
            var t = Type.GetTypeFromProgID("WScript.Shell");
            if (t is null) return;
            dynamic shell = Activator.CreateInstance(t)!;
            var shortcut = shell.CreateShortcut(path);
            shortcut.TargetPath = exe;
            shortcut.WorkingDirectory = Path.GetDirectoryName(exe);
            shortcut.IconLocation = exe;
            shortcut.Description = "Afterimage";
            shortcut.Save();
        }
        catch (Exception ex)
        {
            Log.Line("shortcut: " + ex.Message);
        }
    }
}
