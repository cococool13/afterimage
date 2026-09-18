using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Afterimage;

sealed class App : ApplicationContext
{
    const int WhKeyboardLl = 13;
    const int WmKeydown = 0x0100;

    readonly Settings _settings = Settings.Load();
    readonly ReplayBuffer _buffer;
    readonly NotifyIcon _tray;
    readonly Icon _icon;
    readonly SynchronizationContext _ui;
    readonly Native.HookProc _hookProc;
    readonly IntPtr _hook;
    readonly ContextMenuStrip _rightMenu;
    SettingsForm? _window;
    bool _busy;
    long _lastF8;

    public App()
    {
        _buffer = new ReplayBuffer(_settings);
        Shortcuts.Write();
        _icon = LoadIcon();
        _rightMenu = new ContextMenuStrip();
        _rightMenu.Items.Add("Clip now", null, (_, _) => _ = SaveClip());
        _rightMenu.Items.Add("Open last clip", null, (_, _) => OpenLast());
        _rightMenu.Items.Add("Settings", null, (_, _) => ShowSettings());
        if (!Admin.IsElevated)
            _rightMenu.Items.Add("Run as administrator", null, (_, _) =>
            {
                if (Admin.TryRelaunchElevated()) ExitThread();
            });
        _rightMenu.Items.Add("Quit", null, (_, _) => ExitThread());

        // ContextMenuStrip installs WindowsFormsSynchronizationContext. Capturing
        // before any Control exists falls back to the default context, which posts
        // WelcomeForm onto a thread-pool thread (blank window, Not Responding).
        _ui = SynchronizationContext.Current as WindowsFormsSynchronizationContext
            ?? new WindowsFormsSynchronizationContext();
        SynchronizationContext.SetSynchronizationContext(_ui);

        _tray = new NotifyIcon
        {
            Icon = _icon,
            Text = "Afterimage",
            Visible = true,
        };
        _tray.MouseClick += OnTrayClick;

        _hookProc = OnHook;
        _hook = Native.SetWindowsHookEx(WhKeyboardLl, _hookProc, Native.GetModuleHandle(null), 0);
        if (_hook == IntPtr.Zero) Log.Line("hotkey hook failed");

        EventHandler? once = null;
        once = (_, _) =>
        {
            Application.Idle -= once;
            _ = ReadyAsync();
        };
        Application.Idle += once;
    }

    async Task ReadyAsync()
    {
        if (!_settings.Onboarded)
        {
            var welcome = new WelcomeForm(_settings, _buffer);
            welcome.FormClosed += (_, _) => SetTip();
            welcome.Show();
            return;
        }

        try
        {
            SetTip("getting FFmpeg");
            await _buffer.EnsureFfmpegAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            Log.Line(ex.ToString());
            SetTip("need FFmpeg");
            return;
        }

        try
        {
            await Task.Run(() => _buffer.Start());
            SetTip();
        }
        catch (Exception ex)
        {
            Log.Line(ex.ToString());
            SetTip("failed to start");
            if (_settings.PlaySound) Tick.Fail();
        }
    }

    void OnTrayClick(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left) ShowSettings();
        else if (e.Button == MouseButtons.Right) _rightMenu.Show(Cursor.Position);
    }

    void ShowSettings()
    {
        if (_window is { IsDisposed: false })
        {
            if (_window.WindowState == FormWindowState.Minimized)
                _window.WindowState = FormWindowState.Normal;
            _window.Show();
            _window.Activate();
            return;
        }
        _window = new SettingsForm(_settings, _buffer, ExitThread);
        _window.FormClosed += (_, _) => _window = null;
        _window.Show();
    }

    async Task SaveClip()
    {
        if (_busy) return;
        _busy = true;
        try
        {
            var path = await _buffer.SaveAsync();
            if (path is not null) SetTip();
            if (!_settings.PlaySound) return;
            if (path is null) Tick.Fail();
            else Tick.Ok();
        }
        catch (Exception ex)
        {
            Log.Line(ex.ToString());
            if (_settings.PlaySound) Tick.Fail();
        }
        finally
        {
            _busy = false;
        }
    }

    IntPtr OnHook(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0 && wParam == WmKeydown && Marshal.ReadInt32(lParam) == Hotkey.Normalize(_settings.HotkeyVk))
        {
            var now = Environment.TickCount64;
            if (now - Volatile.Read(ref _lastF8) >= 500)
            {
                Volatile.Write(ref _lastF8, now);
                _ui.Post(_ => _ = SaveClip(), null);
            }
            return (IntPtr)1;
        }
        return Native.CallNextHookEx(_hook, code, wParam, lParam);
    }

    void OpenLast()
    {
        var path = _buffer.LastPath;
        if (path is not null && File.Exists(path)) Shell.Open(path);
        else
        {
            Directory.CreateDirectory(_settings.ClipsFolder);
            Shell.Folder(_settings.ClipsFolder);
        }
    }

    void SetTip(string? status = null)
    {
        status ??= _buffer.Status;
        var text = "Afterimage — " + Hotkey.Label(_settings.HotkeyVk) + " — " + status;
        _tray.Text = text.Length <= 63 ? text : "Afterimage";
    }

    protected override void ExitThreadCore()
    {
        if (_hook != IntPtr.Zero) Native.UnhookWindowsHookEx(_hook);
        _window?.Close();
        _buffer.Dispose();
        _tray.Visible = false;
        _tray.Dispose();
        _icon.Dispose();
        _rightMenu.Dispose();
        base.ExitThreadCore();
    }

    static Icon LoadIcon()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "afterimage.ico");
        if (File.Exists(path)) return new Icon(path);
        var exe = Environment.ProcessPath;
        if (exe is not null)
        {
            var associated = Icon.ExtractAssociatedIcon(exe);
            if (associated is not null) return associated;
        }
        return SystemIcons.Application;
    }
}

static class Shell
{
    public static void Folder(string path) =>
        Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });

    public static void Open(string path) =>
        Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
}

static class Native
{
    public delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr GetModuleHandle(string? lpModuleName);
}
