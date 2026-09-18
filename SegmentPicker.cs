namespace Afterimage;

public static class SegmentPicker
{
    public static FileInfo[] Pick(IEnumerable<FileInfo> files, int seconds)
    {
        var ordered = files
            .Where(f => f.Exists && f.Length > 0)
            .OrderBy(f => f.LastWriteTimeUtc)
            .ThenBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (ordered.Length <= 1) return [];
        return ordered.Take(ordered.Length - 1).TakeLast(Math.Max(1, seconds)).ToArray();
    }

    public static string ConcatLine(string path)
    {
        if (string.IsNullOrEmpty(path)) throw new ArgumentException("path");
        return "file '" + path.Replace('\\', '/').Replace("'", @"'\''") + "'";
    }

    public static string[] CopyForConcat(IEnumerable<FileInfo> files, string destDir)
    {
        Directory.CreateDirectory(destDir);
        var lines = new List<string>();
        var i = 0;
        foreach (var f in files)
        {
            var copy = Path.Combine(destDir, $"{i:000}.ts");
            f.CopyTo(copy, overwrite: true);
            lines.Add(ConcatLine(copy));
            i++;
        }
        return lines.ToArray();
    }

    public static int SelfCheck()
    {
        var dir = Directory.CreateTempSubdirectory("clip-test-");
        try
        {
            FileInfo Make(string name, int ageSeconds, int bytes = 16)
            {
                var path = Path.Combine(dir.FullName, name);
                File.WriteAllBytes(path, new byte[bytes]);
                File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddSeconds(ageSeconds));
                return new FileInfo(path);
            }

            var empty = Pick([], 20);
            Check(empty.Length == 0, "empty input");

            var one = Pick([Make("a.ts", 0)], 20);
            Check(one.Length == 0, "single in-progress file");

            var files = new[]
            {
                Make("0.ts", -5),
                Make("1.ts", -4),
                Make("2.ts", -3),
                Make("3.ts", -2),
                Make("4.ts", -1),
            };
            var picked = Pick(files, 3);
            Check(picked.Length == 3, "take 3 of 4 complete");
            Check(picked[0].Name == "1.ts" && picked[2].Name == "3.ts", "skip newest, keep latest complete");

            var zero = Make("z.ts", -10, bytes: 0);
            var withZero = Pick([zero, files[0], files[1], files[2]], 2);
            Check(withZero.All(f => f.Length > 0), "skip zero-length");
            Check(withZero.Length == 2, "two complete after skipping empty and newest");

            Check(ConcatLine("/tmp/a.ts") == "file '/tmp/a.ts'", "concat simple");
            Check(
                ConcatLine(@"C:\x\O'Brien\a.ts") == @"file 'C:/x/O'\''Brien/a.ts'",
                "concat quote");
            var stage = Path.Combine(dir.FullName, "mux");
            var lines = CopyForConcat([files[1], files[2]], stage);
            Check(lines.Length == 2, "stage count");
            Check(File.Exists(Path.Combine(stage, "000.ts")), "stage first");
            Check(File.Exists(Path.Combine(stage, "001.ts")), "stage second");
            Check(lines[0].Contains("000.ts", StringComparison.Ordinal), "stage line");
            Check(File.ReadAllBytes(Path.Combine(stage, "000.ts")).Length == files[1].Length, "stage bytes");

            Console.WriteLine("self-check ok");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
        finally
        {
            try { dir.Delete(recursive: true); } catch { }
        }
    }

    static void Check(bool ok, string name)
    {
        if (!ok) throw new InvalidOperationException("fail: " + name);
    }
}
