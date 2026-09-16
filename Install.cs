using System.Diagnostics;

namespace Afterimage;

static class Install
{
    public static string Dir { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Afterimage");

    public static string Exe => Path.Combine(Dir, "Afterimage.exe");

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
            Shortcuts.Write(Exe);
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
}
