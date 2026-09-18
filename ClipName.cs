using System.Globalization;

namespace Afterimage;

public static class ClipName
{
    static readonly CultureInfo EnUs = CultureInfo.GetCultureInfo("en-US");

    public static string FileName(DateTime when, IEnumerable<string> existingNames)
    {
        var taken = new HashSet<string>(existingNames.Where(n => n is not null), StringComparer.OrdinalIgnoreCase);
        var stem = when.ToString("MMM d h.mm tt", EnUs);
        var name = stem + ".mp4";
        if (taken.Add(name)) return name;
        for (var n = 2; n < 1000; n++)
        {
            name = $"{stem} ({n}).mp4";
            if (taken.Add(name)) return name;
        }
        return when.ToString("MMM d h.mm.ss tt", EnUs) + ".mp4";
    }

    public static int SelfCheck()
    {
        var a = FileName(new DateTime(2026, 9, 15, 14, 41, 3), []);
        Check(a == "Sep 15 2.41 PM.mp4", "basic name");
        var b = FileName(new DateTime(2026, 9, 15, 14, 41, 3), ["Sep 15 2.41 PM.mp4"]);
        Check(b == "Sep 15 2.41 PM (2).mp4", "collision");
        var c = FileName(new DateTime(2026, 1, 2, 9, 5, 0), []);
        Check(c == "Jan 2 9.05 AM.mp4", "morning single-digit");
        var noon = FileName(new DateTime(2026, 6, 1, 12, 0, 0), []);
        Check(noon == "Jun 1 12.00 PM.mp4", "noon");
        var midnight = FileName(new DateTime(2026, 6, 1, 0, 0, 0), []);
        Check(midnight == "Jun 1 12.00 AM.mp4", "midnight");
        var chain = FileName(new DateTime(2026, 9, 15, 14, 41, 3), ["Sep 15 2.41 PM.mp4", "Sep 15 2.41 PM (2).mp4"]);
        Check(chain == "Sep 15 2.41 PM (3).mp4", "collision 3");
        Console.WriteLine("clip-name ok");
        return 0;
    }

    static void Check(bool ok, string name)
    {
        if (!ok) throw new InvalidOperationException("fail: " + name);
    }
}
