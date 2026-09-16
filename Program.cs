namespace Afterimage;

static class Program
{
    const string MutexName = "Cohen.Afterimage.SingleInstance";

    [STAThread]
    static int Main(string[] args)
    {
        if (args.Contains("--self-check", StringComparer.OrdinalIgnoreCase))
            return SegmentPicker.SelfCheck() == 0 && ClipName.SelfCheck() == 0 && GameWindow.SelfCheck() == 0 ? 0 : 1;

        using var mutex = new Mutex(true, MutexName, out var created);
        if (!created) return 0;

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
    }
}
