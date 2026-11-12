using Anjian.Core;

namespace Anjian.App;

internal sealed class FloatingForm : Form
{
    private readonly Label _scriptLabel = new();
    private readonly Label _statusLabel = new();
    private readonly Label _coordinatesLabel = new();
    private readonly Button _recordButton = new();
    private readonly Button _stopRecordButton = new();
    private readonly Button _playButton = new();
    private readonly Button _pauseButton = new();
    private readonly Button _stopButton = new();
    private readonly Button _topmostButton = new();
    private readonly NumericUpDown _repeatCount = new();
    private readonly NumericUpDown _repeatInterval = new();
    private readonly CheckBox _clickOnlyCheck = new() { Text = "只录点击", AutoSize = true, Checked = true };
    private readonly System.Windows.Forms.Timer _coordinateTimer = new() { Interval = 120 };
    private bool _syncing;
    private bool _dragging;
    private Point _dragCursor;
    private Point _dragForm;
    private bool _allowClose;

    public event Action? RecordToggleRequested;
    public event Action? StopRecordingRequested;
    public event Action? PlayRequested;
    public event Action? PauseRequested;
    public event Action? StopPlaybackRequested;
    public event Action? OpenMainRequested;
    public event Action? HideRequested;
    public event Action<int>? RepeatCountChanged;
    public event Action<int>? RepeatIntervalChanged;
    public event Action<bool>? ClickOnlyRecordingChanged;
    public event Action<bool>? AlwaysOnTopChanged;

    public FloatingForm(Icon icon)
    {
        Text = "按键精灵 2026";
        Icon = icon;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        ClientSize = new Size(600, 142);
        MinimumSize = new Size(600, 142);
        MaximumSize = new Size(600, 142);
        BackColor = Color.FromArgb(245, 247, 250);
        Font = new Font("Microsoft YaHei UI", 9F);
        BuildUi();

        var workArea = Screen.FromPoint(Cursor.Position).WorkingArea;
        Location = new Point(
            Math.Max(workArea.Left + 8, workArea.Right - Width - 24),
            Math.Max(workArea.Top + 8, workArea.Bottom - Height - 72));

        _coordinateTimer.Tick += (_, _) =>
        {
            var p = Cursor.Position;
            _coordinatesLabel.Text = $"X {p.X}   Y {p.Y}";
        };
        _coordinateTimer.Start();
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(9, 6, 9, 6),
            BackColor = BackColor
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 31));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 39));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var header = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        var brand = new Label
        {
            Text = "按键精灵 2026",
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold),
            ForeColor = Color.FromArgb(28, 50, 82),
            Margin = new Padding(1, 4, 12, 0)
        };
        _scriptLabel.AutoSize = false;
        _scriptLabel.Width = 165;
        _scriptLabel.Height = 26;
        _scriptLabel.AutoEllipsis = true;
        _scriptLabel.Text = "脚本：未选择";
        _scriptLabel.TextAlign = ContentAlignment.MiddleLeft;
        _scriptLabel.ForeColor = Color.FromArgb(70, 83, 101);
        _scriptLabel.Margin = new Padding(0, 1, 8, 0);
        _statusLabel.AutoSize = true;
        _statusLabel.Text = "就绪";
        _statusLabel.ForeColor = Color.FromArgb(20, 128, 104);
        _statusLabel.Font = new Font(Font, FontStyle.Bold);
        _statusLabel.Margin = new Padding(0, 5, 10, 0);

        var openMain = MakeButton("完整界面 ↗", 86);
        openMain.Click += (_, _) => OpenMainRequested?.Invoke();
        _topmostButton.Text = "置顶";
        _topmostButton.AutoSize = false;
        _topmostButton.Width = 52;
        _topmostButton.Height = 27;
        _topmostButton.Margin = new Padding(2, 0, 2, 0);
        _topmostButton.Click += (_, _) => AlwaysOnTopChanged?.Invoke(!TopMost);
        var hide = MakeButton("×", 30);
        hide.Click += (_, _) => HideRequested?.Invoke();

        header.Controls.Add(brand);
        header.Controls.Add(_scriptLabel);
        header.Controls.Add(_statusLabel);
        header.Controls.Add(openMain);
        header.Controls.Add(_topmostButton);
        header.Controls.Add(hide);
        AttachDrag(header);
        AttachDrag(brand);
        AttachDrag(_scriptLabel);
        AttachDrag(_statusLabel);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        _recordButton.Text = "● 开始录制";
        _recordButton.Click += (_, _) => RecordToggleRequested?.Invoke();
        actions.Controls.Add(_recordButton);

        _stopRecordButton.Text = "■ 结束录制";
        _stopRecordButton.Enabled = false;
        _stopRecordButton.Click += (_, _) => StopRecordingRequested?.Invoke();
        actions.Controls.Add(_stopRecordButton);

        _playButton.Text = "▶ 播放";
        _playButton.Click += (_, _) => PlayRequested?.Invoke();
        actions.Controls.Add(_playButton);

        _pauseButton.Text = "Ⅱ 暂停";
        _pauseButton.Enabled = false;
        _pauseButton.Click += (_, _) => PauseRequested?.Invoke();
        actions.Controls.Add(_pauseButton);

        _stopButton.Text = "■ 停止";
        _stopButton.Enabled = false;
        _stopButton.Click += (_, _) => StopPlaybackRequested?.Invoke();
        actions.Controls.Add(_stopButton);

        var settings = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        settings.Controls.Add(new Label { Text = "循环次数", AutoSize = true, Margin = new Padding(2, 7, 4, 0) });
        _repeatCount.Minimum = 0;
        _repeatCount.Maximum = PlaybackOptions.MaximumRepeatCount;
        _repeatCount.Value = 1;
        _repeatCount.Width = 78;
        _repeatCount.Margin = new Padding(0, 2, 2, 0);
        _repeatCount.ValueChanged += (_, _) =>
        {
            if (!_syncing) RepeatCountChanged?.Invoke((int)_repeatCount.Value);
        };
        settings.Controls.Add(_repeatCount);
        settings.Controls.Add(new Label { Text = "0=无限", AutoSize = true, ForeColor = Color.FromArgb(95, 108, 125), Margin = new Padding(2, 7, 12, 0) });

        settings.Controls.Add(new Label { Text = "轮间隔(ms)", AutoSize = true, Margin = new Padding(0, 7, 4, 0) });
        _repeatInterval.Minimum = 0;
        _repeatInterval.Maximum = PlaybackOptions.MaximumIntervalMilliseconds;
        _repeatInterval.Increment = 100;
        _repeatInterval.Value = 2_000;
        _repeatInterval.Width = 82;
        _repeatInterval.Margin = new Padding(0, 2, 8, 0);
        _repeatInterval.ValueChanged += (_, _) =>
        {
            if (!_syncing) RepeatIntervalChanged?.Invoke((int)_repeatInterval.Value);
        };
        settings.Controls.Add(_repeatInterval);

        _clickOnlyCheck.Margin = new Padding(4, 5, 0, 0);
        _clickOnlyCheck.CheckedChanged += (_, _) =>
        {
            if (!_syncing) ClickOnlyRecordingChanged?.Invoke(_clickOnlyCheck.Checked);
        };
        settings.Controls.Add(_clickOnlyCheck);

        _coordinatesLabel.AutoSize = true;
        _coordinatesLabel.Font = new Font("Consolas", 9F, FontStyle.Bold);
        _coordinatesLabel.ForeColor = Color.FromArgb(44, 82, 130);
        _coordinatesLabel.Margin = new Padding(12, 7, 0, 0);
        settings.Controls.Add(_coordinatesLabel);

        root.Controls.Add(header, 0, 0);
        root.Controls.Add(actions, 0, 1);
        root.Controls.Add(settings, 0, 2);
        Controls.Add(root);
    }

    private static Button MakeButton(string text, int width)
    {
        return new Button
        {
            Text = text,
            AutoSize = false,
            Width = width,
            Height = 30,
            Margin = new Padding(3, 2, 3, 2),
            Padding = new Padding(4, 0, 4, 0)
        };
    }

    private void AttachDrag(Control control)
    {
        control.MouseDown += (_, e) =>
        {
            if (e.Button != MouseButtons.Left) return;
            _dragging = true;
            _dragCursor = Cursor.Position;
            _dragForm = Location;
        };
        control.MouseMove += (_, _) =>
        {
            if (_dragging)
                Location = new Point(_dragForm.X + Cursor.Position.X - _dragCursor.X,
                    _dragForm.Y + Cursor.Position.Y - _dragCursor.Y);
        };
        control.MouseUp += (_, _) => _dragging = false;
    }

    public void SetSettings(int repeatCount, int repeatInterval, bool clickOnly, bool alwaysOnTop)
    {
        _syncing = true;
        _repeatCount.Value = Math.Clamp(repeatCount, (int)_repeatCount.Minimum, (int)_repeatCount.Maximum);
        _repeatInterval.Value = Math.Clamp(repeatInterval, (int)_repeatInterval.Minimum, (int)_repeatInterval.Maximum);
        _clickOnlyCheck.Checked = clickOnly;
        _syncing = false;
        SetAlwaysOnTop(alwaysOnTop);
    }

    public void SetRepeatCount(int value)
    {
        _syncing = true;
        _repeatCount.Value = Math.Clamp(value, (int)_repeatCount.Minimum, (int)_repeatCount.Maximum);
        _syncing = false;
    }

    public void SetRepeatInterval(int value)
    {
        _syncing = true;
        _repeatInterval.Value = Math.Clamp(value, (int)_repeatInterval.Minimum, (int)_repeatInterval.Maximum);
        _syncing = false;
    }

    public void SetClickOnlyRecording(bool value)
    {
        _syncing = true;
        _clickOnlyCheck.Checked = value;
        _syncing = false;
    }

    public void SetAlwaysOnTop(bool value)
    {
        TopMost = value;
        _topmostButton.Text = value ? "置顶 ✓" : "置顶";
    }

    public void UpdateState(string scriptName, bool recordingSessionActive, bool recording,
        bool recordingPaused, bool playbackRunning, bool playbackPaused)
    {
        _scriptLabel.Text = $"脚本：{scriptName}";
        _recordButton.Text = !recordingSessionActive ? "● 开始录制" : recordingPaused ? "▶ 继续录制" : "Ⅱ 暂停录制";
        _recordButton.Enabled = !playbackRunning;
        _stopRecordButton.Enabled = recordingSessionActive;
        _playButton.Enabled = !recordingSessionActive && !playbackRunning;
        _pauseButton.Enabled = playbackRunning;
        _pauseButton.Text = playbackPaused ? "▶ 继续" : "Ⅱ 暂停";
        _stopButton.Enabled = playbackRunning;

        _statusLabel.Text = recordingSessionActive
            ? recordingPaused ? "录制已暂停" : recording ? "正在录制" : "录制中"
            : playbackRunning ? playbackPaused ? "播放已暂停" : "正在播放"
            : "就绪";
    }

    public void CloseForExit()
    {
        _allowClose = true;
        _coordinateTimer.Stop();
        Close();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            HideRequested?.Invoke();
            return;
        }
        _coordinateTimer.Stop();
        base.OnFormClosing(e);
    }
}
