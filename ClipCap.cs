namespace Afterimage;

public static class ClipCap
{
    public const int DefaultMaxFiles = 50;
    public const int DefaultMaxGb = 5;

    public static int Prune(string folder, int maxFiles, long maxBytes)
    {
        if (maxFiles < 1 || maxBytes < 1 || !Directory.Exists(folder)) return 0;
        var files = new List<FileInfo>();
        long total = 0;
        try
        {
            foreach (var f in new DirectoryInfo(folder).EnumerateFiles("*.mp4"))
            {
                try
                {
                    var len = f.Length;
                    _ = f.LastWriteTimeUtc;
                    total += len;
                    files.Add(f);
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            files.Sort((a, b) =>
            {
                var c = a.LastWriteTimeUtc.CompareTo(b.LastWriteTimeUtc);
                return c != 0 ? c : StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name);
            });
        }
        catch (IOException) { return 0; }
        catch (UnauthorizedAccessException) { return 0; }

        var deleted = 0;
        var i = 0;
        while (i < files.Count && (files.Count > maxFiles || total > maxBytes))
        {
            var f = files[i];
            try
            {
                var len = f.Length;
                f.Delete();
                total -= len;
                files.RemoveAt(i);
                deleted++;
            }
            catch
            {
                i++;
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
            if (Prune(Path.Combine(dir.FullName, "missing"), 50, 1000) != 0)
                throw new InvalidOperationException("fail: missing folder");
            Make("keep.txt", -4, 10);
            Make("a.mp4", -3, 10);
            Make("b.mp4", -2, 10);
            Make("c.mp4", -1, 10);
            if (Prune(dir.FullName, maxFiles: 0, maxBytes: 10) != 0)
                throw new InvalidOperationException("fail: invalid cap");
            if (!File.Exists(Path.Combine(dir.FullName, "a.mp4")))
                throw new InvalidOperationException("fail: invalid cap deleted");
            var n = Prune(dir.FullName, maxFiles: 2, maxBytes: 10_000);
            if (n != 1) throw new InvalidOperationException("fail: file cap");
            if (File.Exists(Path.Combine(dir.FullName, "a.mp4"))) throw new InvalidOperationException("fail: oldest remains");
            if (!File.Exists(Path.Combine(dir.FullName, "c.mp4"))) throw new InvalidOperationException("fail: newest gone");
            if (!File.Exists(Path.Combine(dir.FullName, "keep.txt"))) throw new InvalidOperationException("fail: non-mp4");

            Make("d.mp4", 0, 50);
            n = Prune(dir.FullName, maxFiles: 50, maxBytes: 55);
            if (n < 1) throw new InvalidOperationException("fail: size cap");
            n = Prune(dir.FullName, maxFiles: 50, maxBytes: long.MaxValue);
            if (n != 0) throw new InvalidOperationException("fail: under cap");
            Console.WriteLine("clip-cap ok");
            return 0;
        }
        finally
        {
            try { dir.Delete(recursive: true); } catch { }
        }
    }
}
