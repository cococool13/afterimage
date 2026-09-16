using Microsoft.Win32;

namespace Afterimage;

sealed class Settings
{
    public int Seconds { get; set; } = 20;
    public int HotkeyVk { get; set; } = Hotkey.DefaultVk;
    public bool StartWithWindows { get; set; }
    public bool PlaySound { get; set; } = true;
    public bool Mic { get; set; }
    public string Quality { get; set; } = "fast";
    public bool Onboarded { get; set; }
    public string ClipsFolder { get; set; } = DefaultClipsFolder();

    static string FilePath => Path.Combine(Paths.Root, "settings.json");

    public static string DefaultClipsFolder() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "Afterimage");

    public string NvencPreset => Quality == "quality" ? "p4" : "p1";

    public static Settings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var s = System.Text.Json.JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath))
                    ?? new Settings();
                if (s.Seconds is not (15 or 20 or 30)) s.Seconds = 20;
                s.HotkeyVk = Hotkey.Normalize(s.HotkeyVk);
                if (s.Quality is not ("fast" or "quality")) s.Quality = "fast";
                if (string.IsNullOrWhiteSpace(s.ClipsFolder)) s.ClipsFolder = DefaultClipsFolder();
                return s;
            }
        }
        catch { /* keep defaults */ }
        return new Settings();
    }

    public void Save()
    {
        Directory.CreateDirectory(Paths.Root);
        File.WriteAllText(FilePath, System.Text.Json.JsonSerializer.Serialize(this, Paths.Json));
        ApplyRunKey();
    }

    void ApplyRunKey()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
        if (key is null) return;
        if (StartWithWindows)
            key.SetValue("Afterimage", $"\"{Environment.ProcessPath}\"");
        else
            key.DeleteValue("Afterimage", throwOnMissingValue: false);
    }
}
