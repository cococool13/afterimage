namespace Afterimage;

sealed class SettingsForm : Form
{
    readonly Settings _settings;
    readonly ReplayBuffer _buffer;
    readonly Action _quit;
    readonly Label _status;
    readonly Panel _dot;
    readonly Button _pause;
    readonly Button[] _length;
    readonly Button _hotkey;
    readonly Button[] _quality;
    readonly CheckBox _boot;
    readonly CheckBox _sound;
    readonly CheckBox _mic;
    readonly CheckBox _cap;
    readonly Button _admin;
    readonly Label _folder;
    readonly Label[] _recent;
    readonly System.Windows.Forms.Timer _tick;
    bool _listen;

    public SettingsForm(Settings settings, ReplayBuffer buffer, Action quit)
    {
        _settings = settings;
        _buffer = buffer;
        _quit = quit;

        Text = "Afterimage";
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(380, 760);
        BackColor = Theme.Canvas;
        ForeColor = Theme.Text;
        Font = Theme.Ui;
        KeyPreview = true;

        var y = 18;
        Controls.Add(LabelAt("Afterimage", 20, y, Theme.Title, Theme.Text));
        y += 28;

        _dot = new Panel { Location = new Point(22, y + 6), Size = new Size(8, 8), BackColor = Theme.Mint };
        _status = new Label { Location = new Point(36, y), Size = new Size(200, 22), ForeColor = Theme.Mute, Text = "Starting" };
        _pause = Pill("Pause", 248, y - 4, 112, false);
        _pause.Click += async (_, _) => await ToggleCapture();
        Controls.Add(_dot);
        Controls.Add(_status);
        Controls.Add(_pause);
        y += 36;
        Controls.Add(Rule(y));
        y += 16;

        Controls.Add(LabelAt("CLIP LENGTH", 20, y, Theme.UiSmall, Theme.Mute));
        y += 22;
        _length = new Button[3];
        var seconds = new[] { 15, 20, 30 };
        for (var i = 0; i < 3; i++)
        {
            var n = seconds[i];
            var btn = Pill(n + " s", 20 + i * 114, y, 108, n == _settings.Seconds);
            btn.Click += (_, _) =>
            {
                _settings.Seconds = n;
                _settings.Save();
                RestartBuffer();
                PaintLength();
            };
            _length[i] = btn;
            Controls.Add(btn);
        }
        y += 44;

        Controls.Add(LabelAt("QUALITY", 20, y, Theme.UiSmall, Theme.Mute));
        y += 22;
        _quality = new Button[2];
        _quality[0] = Pill("Fast", 20, y, 166, _settings.Quality != "quality");
        _quality[1] = Pill("Quality", 194, y, 166, _settings.Quality == "quality");
        _quality[0].Click += (_, _) => SetQuality("fast");
        _quality[1].Click += (_, _) => SetQuality("quality");
        Controls.Add(_quality[0]);
        Controls.Add(_quality[1]);
        y += 44;

        Controls.Add(LabelAt("HOTKEY", 20, y, Theme.UiSmall, Theme.Mute));
        y += 22;
        _hotkey = Pill(Hotkey.Label(_settings.HotkeyVk), 20, y, 100, true);
        _hotkey.Click += (_, _) =>
        {
            _listen = true;
            _hotkey.Text = "...";
        };
        Controls.Add(_hotkey);
        Controls.Add(LabelAt("Click, then press a key.", 130, y + 4, Theme.UiSmall, Theme.Mute));
        y += 44;
        Controls.Add(Rule(y));
        y += 14;

        _boot = Check("Start with Windows", 20, y, _settings.StartWithWindows);
        _boot.CheckedChanged += (_, _) => { _settings.StartWithWindows = _boot.Checked; _settings.Save(); };
        Controls.Add(_boot);
        y += 26;
        _sound = Check("Play a sound", 20, y, _settings.PlaySound);
        _sound.CheckedChanged += (_, _) => { _settings.PlaySound = _sound.Checked; _settings.Save(); };
        Controls.Add(_sound);
        y += 26;
        _mic = Check("Record microphone", 20, y, _settings.Mic);
        _mic.CheckedChanged += (_, _) =>
        {
            _settings.Mic = _mic.Checked;
            _settings.Save();
            RestartBuffer();
        };
        Controls.Add(_mic);
        y += 26;
        _cap = Check("Delete old clips (50 / 5 GB)", 20, y, _settings.CapClips);
        _cap.CheckedChanged += (_, _) => { _settings.CapClips = _cap.Checked; _settings.Save(); };
        Controls.Add(_cap);
        y += 34;
        Controls.Add(Rule(y));
        y += 12;

        Controls.Add(LabelAt("CLIPS FOLDER", 20, y, Theme.UiSmall, Theme.Mute));
        y += 18;
        _folder = new Label { Location = new Point(20, y), Size = new Size(250, 32), ForeColor = Theme.Mute, Font = Theme.UiSmall };
        var change = Ghost("Change", 276, y, 84);
        change.Click += (_, _) => PickFolder();
        Controls.Add(_folder);
        Controls.Add(change);
        y += 40;

        var view = Ash("View clips", 20, y, 166);
        view.Click += (_, _) => OpenClips();
        var last = Ghost("Open last", 194, y, 166);
        last.Click += (_, _) => OpenLast();
        Controls.Add(view);
        Controls.Add(last);
        y += 44;

        Controls.Add(LabelAt("RECENT", 20, y, Theme.UiSmall, Theme.Mute));
        y += 18;
        _recent = new Label[3];
        for (var i = 0; i < 3; i++)
        {
            var row = new Label
            {
                Location = new Point(20, y),
                Size = new Size(340, 20),
                ForeColor = Theme.Mute,
                Font = Theme.UiSmall,
                Cursor = Cursors.Hand,
            };
            row.Click += (_, _) =>
            {
                if (row.Tag is string path && File.Exists(path)) Shell.Open(path);
            };
            _recent[i] = row;
            Controls.Add(row);
            y += 20;
        }
        y += 10;
        _admin = Ghost("Run as administrator", 20, y, 340);
        _admin.Click += (_, _) =>
        {
            if (Admin.TryRelaunchElevated()) _quit();
        };
        Controls.Add(_admin);
        y += 36;
        var quitBtn = Ghost("Quit Afterimage", 20, y, 340);
        quitBtn.Click += (_, _) => _quit();
        Controls.Add(quitBtn);

        KeyDown += OnKeyDown;
        _tick = new System.Windows.Forms.Timer { Interval = 2000 };
        _tick.Tick += (_, _) => RefreshState();
        Load += (_, _) =>
        {
            RefreshState();
            PaintLength();
            PaintQuality();
            _tick.Start();
        };
        FormClosed += (_, _) => _tick.Stop();
    }

    async Task ToggleCapture()
    {
        if (_buffer.Armed) _buffer.Stop();
        else
        {
            try
            {
                await _buffer.EnsureFfmpegAsync(CancellationToken.None);
                await Task.Run(() => _buffer.Start());
            }
            catch (Exception ex)
            {
                Log.Line(ex.ToString());
            }
        }
        RefreshState();
    }

    void RestartBuffer()
    {
        if (!_buffer.IsRunning) return;
        _ = RestartBufferAsync();
    }

    async Task RestartBufferAsync()
    {
        try { await Task.Run(() => _buffer.Start()); }
        catch (Exception ex) { Log.Line(ex.ToString()); }
        if (!IsDisposed) RefreshState();
    }

    void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (!_listen) return;
        e.Handled = true;
        e.SuppressKeyPress = true;
        if (e.KeyCode == Keys.Escape)
        {
            _listen = false;
            _hotkey.Text = Hotkey.Label(_settings.HotkeyVk);
            return;
        }
        if (Hotkey.IsModifier(e.KeyCode)) return;
        _settings.HotkeyVk = (int)e.KeyCode;
        _settings.Save();
        _listen = false;
        _hotkey.Text = Hotkey.Label(_settings.HotkeyVk);
    }

    void SetQuality(string quality)
    {
        _settings.Quality = quality;
        _settings.Save();
        RestartBuffer();
        PaintQuality();
    }

    void RefreshState()
    {
        var on = _buffer.IsRunning;
        _status.Text = on
            ? (string.IsNullOrEmpty(_buffer.TargetName) ? "Recording" : "Recording · " + _buffer.TargetName)
            : Title(_buffer.Status);
        _dot.BackColor = on ? Theme.Mint : _buffer.Armed ? Theme.Ash : Theme.Ember;
        _pause.Text = _buffer.Armed ? "Pause" : "Resume";
        _admin.Visible = !Admin.IsElevated;
        _admin.Text = _buffer.NeedsAdmin ? "Run as administrator (needed)" : "Run as administrator";
        _folder.Text = _settings.ClipsFolder;
        if (!_listen) _hotkey.Text = Hotkey.Label(_settings.HotkeyVk);
        PaintRecent();
    }

    void PaintLength()
    {
        var seconds = new[] { 15, 20, 30 };
        for (var i = 0; i < _length.Length; i++)
            Style(_length[i], seconds[i] == _settings.Seconds);
    }

    void PaintQuality()
    {
        Style(_quality[0], _settings.Quality != "quality");
        Style(_quality[1], _settings.Quality == "quality");
    }

    void PaintRecent()
    {
        string[] files = [];
        try
        {
            if (Directory.Exists(_settings.ClipsFolder))
                files = Directory.GetFiles(_settings.ClipsFolder, "*.mp4")
                    .OrderByDescending(File.GetLastWriteTimeUtc)
                    .Take(3)
                    .ToArray();
        }
        catch { }
        for (var i = 0; i < _recent.Length; i++)
        {
            if (i < files.Length)
            {
                _recent[i].Text = Path.GetFileNameWithoutExtension(files[i]);
                _recent[i].Tag = files[i];
                _recent[i].ForeColor = Theme.Text;
            }
            else
            {
                _recent[i].Text = i == 0 ? "No clips yet" : "";
                _recent[i].Tag = null;
                _recent[i].ForeColor = Theme.Mute;
            }
        }
    }

    void PickFolder()
    {
        using var d = new FolderBrowserDialog
        {
            Description = "Clips folder",
            SelectedPath = Directory.Exists(_settings.ClipsFolder) ? _settings.ClipsFolder : Settings.DefaultClipsFolder(),
        };
        if (d.ShowDialog(this) != DialogResult.OK) return;
        _settings.ClipsFolder = d.SelectedPath;
        _settings.Save();
        RefreshState();
    }

    void OpenClips()
    {
        Directory.CreateDirectory(_settings.ClipsFolder);
        Shell.Folder(_settings.ClipsFolder);
    }

    void OpenLast()
    {
        var path = _buffer.LastPath;
        if (path is not null && File.Exists(path)) Shell.Open(path);
        else OpenClips();
    }

    static string Title(string status) =>
        string.IsNullOrEmpty(status) ? "Paused" : char.ToUpper(status[0]) + status[1..];

    static Label LabelAt(string text, int x, int y, Font font, Color color) => new()
    {
        Text = text,
        Location = new Point(x, y),
        AutoSize = true,
        Font = font,
        ForeColor = color,
        BackColor = Color.Transparent,
    };

    static Panel Rule(int y) => new()
    {
        Location = new Point(20, y),
        Size = new Size(340, 1),
        BackColor = Theme.Line,
    };

    static CheckBox Check(string text, int x, int y, bool on) => new()
    {
        Text = text,
        Location = new Point(x, y),
        AutoSize = true,
        ForeColor = Theme.Text,
        BackColor = Theme.Canvas,
        Checked = on,
        FlatStyle = FlatStyle.Flat,
    };

    Button Pill(string text, int x, int y, int w, bool on)
    {
        var b = new Button
        {
            Text = text,
            Location = new Point(x, y),
            Size = new Size(w, 30),
            FlatStyle = FlatStyle.Flat,
            Font = Theme.Ui,
            Cursor = Cursors.Hand,
        };
        b.FlatAppearance.BorderSize = 1;
        Style(b, on);
        return b;
    }

    static Button Ash(string text, int x, int y, int w)
    {
        var b = new Button
        {
            Text = text,
            Location = new Point(x, y),
            Size = new Size(w, 36),
            FlatStyle = FlatStyle.Flat,
            BackColor = Theme.Ash,
            ForeColor = Theme.AshText,
            Font = Theme.Ui,
            Cursor = Cursors.Hand,
        };
        b.FlatAppearance.BorderSize = 0;
        b.FlatAppearance.MouseOverBackColor = Color.White;
        return b;
    }

    static Button Ghost(string text, int x, int y, int w)
    {
        var b = new Button
        {
            Text = text,
            Location = new Point(x, y),
            Size = new Size(w, 30),
            FlatStyle = FlatStyle.Flat,
            BackColor = Theme.Canvas,
            ForeColor = Theme.Mute,
            Font = Theme.Ui,
            Cursor = Cursors.Hand,
        };
        b.FlatAppearance.BorderColor = Theme.Line;
        b.FlatAppearance.BorderSize = 1;
        b.FlatAppearance.MouseOverBackColor = Theme.Raised;
        return b;
    }

    static void Style(Button b, bool on)
    {
        b.BackColor = on ? Theme.Ash : Theme.Raised;
        b.ForeColor = on ? Theme.AshText : Theme.Text;
        b.FlatAppearance.BorderColor = on ? Theme.Ash : Theme.Line;
    }
}
