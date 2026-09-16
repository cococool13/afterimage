namespace Afterimage;

static class Hotkey
{
    public const int DefaultVk = 0x77; // F8

    public static int Normalize(int vk) => vk is > 0 and < 256 ? vk : DefaultVk;

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

    public static bool IsModifier(Keys key) =>
        key is Keys.ShiftKey or Keys.ControlKey or Keys.Menu or Keys.LWin or Keys.RWin
            or Keys.LShiftKey or Keys.RShiftKey or Keys.LControlKey or Keys.RControlKey;

    public static int SelfCheck()
    {
        if (Normalize(0) != DefaultVk) throw new InvalidOperationException("fail: default");
        if (Normalize(0x78) != 0x78) throw new InvalidOperationException("fail: f9");
        if (IsModifier(Keys.F8)) throw new InvalidOperationException("fail: f8");
        Console.WriteLine("hotkey ok");
        return 0;
    }
}
