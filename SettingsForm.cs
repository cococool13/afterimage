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
    readonly CheckBox _boot;
    readonly CheckBox _sound;
    readonly Label _folder;
    readonly System.Windows.Forms.Timer _tick;

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
        ClientSize = new Size(380, 540);
        BackColor = Theme.Canvas;
        ForeColor = Theme.Text;
        Font = Theme.Ui;
        Padding = new Padding(20);

        var y = 18;
        Controls.Add(LabelAt("Afterimage", 20, y, Theme.Title, Theme.Text));
        y += 28;

        _dot = new Panel
        {
            Location = new Point(22, y + 6),
            Size = new Size(8, 8),
            BackColor = Theme.Mint,
        };
        _status = new Label
        {
            Location = new Point(36, y),
            Size = new Size(200, 22),
            ForeColor = Theme.Mute,
            Text = "Starting",
        };
        _pause = Pill("Pause", 248, y - 4, 112, false);
        _pause.Click += (_, _) =>
        {
            if (_buffer.IsRunning) _buffer.Stop();
            else
            {
                try { _buffer.Start(); }
                catch (Exception ex) { Log.Line(ex.ToString()); }
            }
            RefreshState();
        };
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
                if (_buffer.IsRunning) _buffer.Start();
                PaintLength();
                RefreshState();
            };
            _length[i] = btn;
            Controls.Add(btn);
        }
        y += 44;

        Controls.Add(LabelAt("HOTKEY", 20, y, Theme.UiSmall, Theme.Mute));
        y += 22;
        var key = Pill("F8", 20, y, 64, true);
        key.Enabled = false;
        Controls.Add(key);
        Controls.Add(LabelAt("Saves the last seconds.", 96, y + 4, Theme.UiSmall, Theme.Mute));
        y += 44;
        Controls.Add(Rule(y));
        y += 16;

        _boot = Check("Start with Windows", 20, y, _settings.StartWithWindows);
        _boot.CheckedChanged += (_, _) =>
        {
            _settings.StartWithWindows = _boot.Checked;
            _settings.Save();
        };
        Controls.Add(_boot);
        y += 28;
        _sound = Check("Play a sound", 20, y, _settings.PlaySound);
        _sound.CheckedChanged += (_, _) =>
        {
            _settings.PlaySound = _sound.Checked;
            _settings.Save();
        };
        Controls.Add(_sound);
        y += 36;
        Controls.Add(Rule(y));
        y += 14;

        Controls.Add(LabelAt("CLIPS FOLDER", 20, y, Theme.UiSmall, Theme.Mute));
        y += 20;
        _folder = new Label
        {
            Location = new Point(20, y),
            Size = new Size(250, 36),
            ForeColor = Theme.Mute,
            Font = Theme.UiSmall,
        };
        var change = Ghost("Change", 276, y, 84);
        change.Click += (_, _) => PickFolder();
        Controls.Add(_folder);
        Controls.Add(change);
        y += 44;

        var view = Ash("View clips", 20, y, 340);
        view.Click += (_, _) => OpenClips();
        Controls.Add(view);
        y += 44;
        var quitBtn = Ghost("Quit Afterimage", 20, y, 340);
        quitBtn.Click += (_, _) => _quit();
        Controls.Add(quitBtn);

        _tick = new System.Windows.Forms.Timer { Interval = 800 };
        _tick.Tick += (_, _) => RefreshState();
        Load += (_, _) =>
        {
            RefreshState();
            PaintLength();
            _tick.Start();
        };
        FormClosed += (_, _) => _tick.Stop();
    }

    void RefreshState()
    {
        var on = _buffer.IsRunning;
        _status.Text = on ? "Recording" : Title(_buffer.Status);
        _dot.BackColor = on ? Theme.Mint : Theme.Ember;
        _pause.Text = on ? "Pause" : "Resume";
        _folder.Text = _settings.ClipsFolder;
    }

    void PaintLength()
    {
        var seconds = new[] { 15, 20, 30 };
        for (var i = 0; i < _length.Length; i++)
            Style(_length[i], seconds[i] == _settings.Seconds);
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
