using Microsoft.Win32;

namespace Afterimage;

sealed class Settings
{
    public int Seconds { get; set; } = SettingsLogic.DefaultSeconds;
    public int HotkeyVk { get; set; } = Hotkey.DefaultVk;
    public bool StartWithWindows { get; set; } = SettingsLogic.DefaultStartWithWindows;
    public bool PlaySound { get; set; } = true;
    public bool Mic { get; set; }
    public string Quality { get; set; } = SettingsLogic.DefaultQuality;
    public bool Onboarded { get; set; } = SettingsLogic.DefaultOnboarded;
    public bool CapClips { get; set; } = SettingsLogic.DefaultCapClips;
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
                s.Seconds = SettingsLogic.ClampSeconds(s.Seconds);
                s.HotkeyVk = Hotkey.Normalize(s.HotkeyVk);
                s.Quality = SettingsLogic.ClampQuality(s.Quality);
                s.ClipsFolder = SettingsLogic.ClampFolder(s.ClipsFolder, DefaultClipsFolder());
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
