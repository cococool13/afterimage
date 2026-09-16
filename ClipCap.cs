namespace Afterimage;

public static class ClipCap
{
    public const int DefaultMaxFiles = 50;
    public const int DefaultMaxGb = 5;

    public static int Prune(string folder, int maxFiles, long maxBytes)
    {
        if (maxFiles < 1 || maxBytes < 1 || !Directory.Exists(folder)) return 0;
        var files = new DirectoryInfo(folder).EnumerateFiles("*.mp4")
            .OrderBy(f => f.LastWriteTimeUtc)
            .ThenBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var total = files.Sum(f => f.Length);
        var deleted = 0;
        while (files.Count > 0 && (files.Count > maxFiles || total > maxBytes))
        {
            var oldest = files[0];
            files.RemoveAt(0);
            total -= oldest.Length;
            try
            {
                oldest.Delete();
                deleted++;
            }
            catch
            {
                total += oldest.Length;
            }
        }
        return deleted;
    }

    public static int SelfCheck()
    {
        var dir = Directory.CreateTempSubdirectory("afterimage-cap-");
        try
        {
            FileInfo Make(string name, int age, int bytes)
            {
                var path = Path.Combine(dir.FullName, name);
                File.WriteAllBytes(path, new byte[bytes]);
                File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(age));
                return new FileInfo(path);
            }
            Make("a.mp4", -3, 10);
            Make("b.mp4", -2, 10);
            Make("c.mp4", -1, 10);
            var n = Prune(dir.FullName, maxFiles: 2, maxBytes: 10_000);
            if (n != 1) throw new InvalidOperationException("fail: file cap");
            if (File.Exists(Path.Combine(dir.FullName, "a.mp4"))) throw new InvalidOperationException("fail: oldest remains");
            if (!File.Exists(Path.Combine(dir.FullName, "c.mp4"))) throw new InvalidOperationException("fail: newest gone");

            Make("d.mp4", 0, 50);
            n = Prune(dir.FullName, maxFiles: 50, maxBytes: 55);
            if (n < 1) throw new InvalidOperationException("fail: size cap");
            Console.WriteLine("clip-cap ok");
            return 0;
        }
        finally
        {
            try { dir.Delete(recursive: true); } catch { }
        }
    }
}
