namespace Afterimage;

static class Gpu
{
    public static bool HasNvidia()
    {
        try
        {
            var sys = Environment.SystemDirectory;
            return File.Exists(Path.Combine(sys, "nvapi64.dll"))
                || File.Exists(Path.Combine(sys, "nvEncodeAPI64.dll"));
        }
        catch
        {
            return true;
        }
    }
}
