namespace Afterimage;

static class Hotkey
{
    public const int DefaultVk = SettingsLogic.DefaultHotkeyVk;

    public static int Normalize(int vk) => SettingsLogic.ClampHotkey(vk);

    public static string Label(int vk)
    {
        vk = Normalize(vk);
        try
        {
            var text = new KeysConverter().ConvertToString((Keys)vk);
            return string.IsNullOrWhiteSpace(text) ? "F8" : text;
        }
        catch
        {
            return "F8";
        }
    }

    public static bool IsModifier(Keys key) => SettingsLogic.IsModifierVk((int)key);

    public static int SelfCheck()
    {
        if (Normalize(0) != DefaultVk) throw new InvalidOperationException("fail: default");
        if (Normalize(0x78) != 0x78) throw new InvalidOperationException("fail: f9");
        if (Normalize(0x12) != DefaultVk) throw new InvalidOperationException("fail: alt vk");
        if (IsModifier(Keys.F8)) throw new InvalidOperationException("fail: f8");
        if (!IsModifier(Keys.LMenu) || !IsModifier(Keys.RMenu)) throw new InvalidOperationException("fail: alt");
        Console.WriteLine("hotkey ok");
        return 0;
    }
}
