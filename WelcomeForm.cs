namespace Afterimage;

sealed class WelcomeForm : Form
{
    readonly Settings _settings;
    readonly ReplayBuffer _buffer;
    readonly Label _status;
    readonly ProgressBar _bar;
    readonly Button _done;
    bool _ready;

    public WelcomeForm(Settings settings, ReplayBuffer buffer)
    {
        _settings = settings;
        _buffer = buffer;

        Text = "Afterimage";
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(380, 280);
        BackColor = Theme.Canvas;
        ForeColor = Theme.Text;
        Font = Theme.Ui;
        ShowInTaskbar = true;

        var y = 20;
        Controls.Add(new Label
        {
            Text = "Afterimage",
            Location = new Point(20, y),
            AutoSize = true,
            Font = Theme.Title,
            ForeColor = Theme.Text,
        });
        y += 36;
        Controls.Add(Line($"Press {Hotkey.Label(_settings.HotkeyVk)} in a game to keep the last {_settings.Seconds} seconds.", y));
        y += 36;
        Controls.Add(Line("Clips go to Videos → Afterimage. Then it lives by the clock.", y));
        y += 44;

        _status = new Label
        {
            Location = new Point(20, y),
            Size = new Size(340, 20),
            ForeColor = Theme.Mute,
            Text = "Getting ready…",
        };
        Controls.Add(_status);
        y += 24;
        _bar = new ProgressBar
        {
            Location = new Point(20, y),
            Size = new Size(340, 12),
            Style = ProgressBarStyle.Marquee,
            MarqueeAnimationSpeed = 30,
        };
        Controls.Add(_bar);
        y += 36;
        _done = new Button
        {
            Text = "Please wait",
            Location = new Point(20, y),
            Size = new Size(340, 36),
            FlatStyle = FlatStyle.Flat,
            BackColor = Theme.Raised,
            ForeColor = Theme.Mute,
            Enabled = false,
            Cursor = Cursors.Hand,
        };
        _done.FlatAppearance.BorderSize = 0;
        _done.Click += (_, _) => Finish();
        Controls.Add(_done);

        Shown += async (_, _) => await Setup();
        FormClosed += (_, _) =>
        {
            if (!_ready) return;
            _settings.Onboarded = true;
            _settings.Save();
        };
    }

    async Task Setup()
    {
        try
        {
            var progress = new Progress<int>(n =>
            {
                if (IsDisposed) return;
                if (n < 0)
                {
                    _bar.Style = ProgressBarStyle.Marquee;
                    _status.Text = "Downloading (one time)…";
                    return;
                }
                _bar.Style = ProgressBarStyle.Continuous;
                _bar.Value = Math.Clamp(n, 0, 100);
                _status.Text = n >= 100 ? "Ready." : $"Downloading… {n}%";
            });
            await _buffer.EnsureFfmpegAsync(CancellationToken.None, progress);
            if (!Gpu.HasNvidia())
            {
                if (IsDisposed) return;
                _status.Text = "Needs an NVIDIA graphics card.";
                ReadyButton("Got it");
                return;
            }
            await Task.Run(() => _buffer.Start());
            if (IsDisposed) return;
            _status.Text = "Ready. Start a game, then press " + Hotkey.Label(_settings.HotkeyVk) + ".";
            ReadyButton("Got it");
        }
        catch (Exception ex)
        {
            Log.Line(ex.ToString());
            if (IsDisposed) return;
            _status.Text = "Couldn't finish setup. Check the network and try again.";
            _done.Enabled = true;
            _done.Text = "Close";
        }
    }

    void ReadyButton(string text)
    {
        _bar.Style = ProgressBarStyle.Continuous;
        _bar.Value = 100;
        _done.Enabled = true;
        _done.Text = text;
        _done.BackColor = Theme.Ash;
        _done.ForeColor = Theme.AshText;
        _ready = true;
    }

    void Finish() => Close();

    static Label Line(string text, int y) => new()
    {
        Text = text,
        Location = new Point(20, y),
        Size = new Size(340, 36),
        ForeColor = Theme.Mute,
    };
}
