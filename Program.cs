namespace Afterimage;

static class Program
{
    const string MutexName = @"Global\Cohen.Afterimage.SingleInstance";
    static Mutex? _instance;

    internal static void ReleaseInstance()
    {
        _instance?.Dispose();
        _instance = null;
    }

    [STAThread]
    static int Main(string[] args)
    {
        if (args.Contains("--self-check", StringComparer.OrdinalIgnoreCase))
            return SegmentPicker.SelfCheck() == 0 && ClipName.SelfCheck() == 0 && GameWindow.SelfCheck() == 0 && Hotkey.SelfCheck() == 0 && ClipCap.SelfCheck() == 0 ? 0 : 1;

        if (Install.TryRelocate()) return 0;

        try
        {
            _instance = new Mutex(true, MutexName, out var created);
            if (!created)
            {
                ReleaseInstance();
                return 0;
            }
        }
        catch (AbandonedMutexException)
        {
            // previous instance crashed; we own the mutex
        }

        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.ThreadException += (_, e) => Log.Line(e.Exception.ToString());
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log.Line(e.ExceptionObject.ToString() ?? "unhandled");

        try
        {
            Application.Run(new App());
            return 0;
        }
        catch (Exception ex)
        {
            Log.Line(ex.ToString());
            return 1;
        }
        finally
        {
            ReleaseInstance();
        }
    }
}
