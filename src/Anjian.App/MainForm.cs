using Anjian.App.Models;
using Anjian.App.Services;
using Anjian.Core;

namespace Anjian.App;

internal sealed class MainForm : Form
{
    private readonly ScriptRepository _repository = new();
    private readonly AppSettingsStore _settingsStore = new();
    private readonly GlobalRecorder _recorder = new();
    private readonly PlaybackRunner _playback = new();
    private readonly ListBox _scripts = new();
    private readonly RichTextBox _editor = new();
    private readonly RichTextBox _log = new();
    private readonly NumericUpDown _repeatCount = new();
    private readonly NumericUpDown _repeatInterval = new();
    private readonly Label _status = new();
    private readonly Label _progress = new();
    private readonly Button _recordButton = new();
    private readonly Button _stopRecordButton = new();
    private readonly Button _playButton = new();
    private readonly Button _pauseButton = new();
    private readonly Button _stopButton = new();
    private GlobalHotkeyManager? _hotkeyManager;
    private ScriptEntry? _current;
    private bool _loading;
    private bool _recording;
    private bool _dirty;
    private Task? _runTask;

    public MainForm()
    {
        Text = "按键精灵 2026 · Preview";
        MinimumSize = new Size(980, 650);
        Size = new Size(1180, 780);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Microsoft YaHei UI", 9F);
        BuildUi();

        _playback.Log += message => Ui(() =>
        {
            AppendLog(message);
            if (message.StartsWith("播放完成", StringComparison.Ordinal) ||
                message.StartsWith("播放已停止", StringComparison.Ordinal))
                SetIdle();
        });
        _playback.IterationChanged += (current, total) => Ui(() =>
            _progress.Text = total == 0 ? $"正在执行第 {current} 轮（无限）" : $"正在执行第 {current}/{total} 轮");
        _scripts.SelectedIndexChanged += (_, _) => SelectScript();
        _editor.TextChanged += (_, _) => { if (!_loading) { _dirty = true; UpdateTitle(); } };
        _repeatCount.ValueChanged += (_, _) => SaveSettings();
        _repeatInterval.ValueChanged += (_, _) => SaveSettings();

        LoadSettings();
        RefreshScripts();
        AppendLog("就绪。脚本保存在：" + _repository.RootDirectory);
        AppendLog("播放设置：次数 1=执行一次，0=无限；间隔只在每轮之间等待。");
        AppendLog("全局快捷键：F10 启动，F11 暂停/继续，F12 立即中止。");
    }

    private void BuildUi()
    {
        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 48,
            Padding = new Padding(8, 7, 8, 5),
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoScroll = true
        };

        toolbar.Controls.Add(MakeButton("新建脚本", (_, _) => CreateScript()));
        toolbar.Controls.Add(MakeButton("保存", (_, _) => SaveCurrent()));
        _recordButton.Text = "开始录制";
        _recordButton.Click += (_, _) => StartRecording();
        toolbar.Controls.Add(_recordButton);

        _stopRecordButton.Text = "结束录制";
        _stopRecordButton.Enabled = false;
        _stopRecordButton.Click += (_, _) => StopRecording();
        toolbar.Controls.Add(_stopRecordButton);

        _playButton.Text = "▶ 播放";
        _playButton.Click += async (_, _) => await StartPlaybackAsync();
        toolbar.Controls.Add(_playButton);

        _pauseButton.Text = "暂停";
        _pauseButton.Enabled = false;
        _pauseButton.Click += (_, _) => TogglePause();
        toolbar.Controls.Add(_pauseButton);

        _stopButton.Text = "■ 停止";
        _stopButton.Enabled = false;
        _stopButton.Click += async (_, _) => await StopPlaybackAsync();
        toolbar.Controls.Add(_stopButton);

        toolbar.Controls.Add(new Label { Text = "重复次数", AutoSize = true, Margin = new Padding(12, 8, 2, 0) });
        _repeatCount.Minimum = 0;
        _repeatCount.Maximum = PlaybackOptions.MaximumRepeatCount;
        _repeatCount.Width = 82;
        _repeatCount.Margin = new Padding(0, 1, 2, 0);
        toolbar.Controls.Add(_repeatCount);
        toolbar.Controls.Add(new Label { Text = "0=无限", AutoSize = true, Margin = new Padding(1, 8, 8, 0) });

        toolbar.Controls.Add(new Label { Text = "每轮间隔(ms)", AutoSize = true, Margin = new Padding(5, 8, 2, 0) });
        _repeatInterval.Minimum = 0;
        _repeatInterval.Maximum = PlaybackOptions.MaximumIntervalMilliseconds;
        _repeatInterval.Increment = 100;
        _repeatInterval.Width = 88;
        _repeatInterval.Margin = new Padding(0, 1, 2, 0);
        toolbar.Controls.Add(_repeatInterval);
        toolbar.Controls.Add(new Label
        {
            Text = "全局热键：F10 播放 / F11 暂停继续 / F12 中止",
            AutoSize = true,
            ForeColor = Color.FromArgb(70, 90, 110),
            Margin = new Padding(12, 8, 8, 0)
        });
        Controls.Add(toolbar);

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Size = new Size(1100, 700),
            SplitterDistance = 250,
            FixedPanel = FixedPanel.Panel1,
            Panel1MinSize = 190,
            Panel2MinSize = 400
        };
        split.Panel1.Padding = new Padding(8);
        split.Panel1.Controls.Add(new Label
        {
            Text = "我的脚本（双击打开）",
            Dock = DockStyle.Top,
            Height = 28,
            Font = new Font(Font, FontStyle.Bold)
        });

        _scripts.Dock = DockStyle.Fill;
        _scripts.IntegralHeight = false;
        split.Panel1.Controls.Add(_scripts);
        var scriptMenu = new ContextMenuStrip();
        scriptMenu.Items.Add("打开", null, (_, _) => SelectScript());
        scriptMenu.Items.Add("新建脚本", null, (_, _) => CreateScript());
        scriptMenu.Items.Add("复制脚本", null, (_, _) => DuplicateCurrent());
        scriptMenu.Items.Add("重命名", null, (_, _) => RenameCurrent());
        scriptMenu.Items.Add("删除", null, (_, _) => DeleteCurrent());
        _scripts.ContextMenuStrip = scriptMenu;
        _scripts.MouseDown += (_, e) =>
        {
            if (e.Button == MouseButtons.Right)
            {
                var index = _scripts.IndexFromPoint(e.Location);
                if (index >= 0) _scripts.SelectedIndex = index;
            }
        };

        var sideButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 38,
            FlowDirection = FlowDirection.LeftToRight
        };
        sideButtons.Controls.Add(MakeButton("重命名", (_, _) => RenameCurrent()));
        sideButtons.Controls.Add(MakeButton("删除", (_, _) => DeleteCurrent()));
        split.Panel1.Controls.Add(sideButtons);

        split.Panel2.Padding = new Padding(8, 6, 8, 6);
        var right = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 70));
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 30));

        var editorHeader = new Panel { Dock = DockStyle.Fill };
        var fileName = new Label
        {
            Text = "Lua 脚本编辑器",
            Dock = DockStyle.Left,
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold)
        };
        _status.Text = "未运行";
        _status.Dock = DockStyle.Right;
        _status.AutoSize = true;
        editorHeader.Controls.Add(_status);
        editorHeader.Controls.Add(fileName);
        right.Controls.Add(editorHeader, 0, 0);

        _editor.Dock = DockStyle.Fill;
        _editor.Font = new Font("Consolas", 10.5F);
        _editor.WordWrap = false;
        _editor.AcceptsTab = true;
        _editor.DetectUrls = false;
        _editor.HideSelection = false;
        _editor.BackColor = Color.FromArgb(250, 250, 250);
        right.Controls.Add(_editor, 0, 1);

        var logPanel = new Panel { Dock = DockStyle.Fill };
        var logHeader = new Panel { Dock = DockStyle.Top, Height = 26 };
        logHeader.Controls.Add(new Label
        {
            Text = "运行日志",
            Dock = DockStyle.Left,
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold)
        });
        _progress.Text = "";
        _progress.Dock = DockStyle.Right;
        _progress.AutoSize = true;
        logHeader.Controls.Add(_progress);
        var clearLog = MakeButton("清空", (_, _) => _log.Clear());
        clearLog.Dock = DockStyle.Right;
        clearLog.Height = 24;
        logHeader.Controls.Add(clearLog);

        _log.Dock = DockStyle.Fill;
        _log.ReadOnly = true;
        _log.Font = new Font("Consolas", 9F);
        _log.BackColor = Color.FromArgb(247, 248, 250);
        logPanel.Controls.Add(_log);
        logPanel.Controls.Add(logHeader);
        right.Controls.Add(logPanel, 0, 2);

        split.Panel2.Controls.Add(right);
        Controls.Add(split);
        KeyPreview = true;
        KeyDown += (_, e) =>
        {
            if (e.Control && e.KeyCode == Keys.S)
            {
                SaveCurrent();
                e.Handled = true;
            }
        };
    }

    private static Button MakeButton(string text, EventHandler handler)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            Height = 30,
            Margin = new Padding(2),
            Padding = new Padding(8, 0, 8, 0)
        };
        button.Click += handler;
        return button;
    }

    private void LoadSettings()
    {
        var settings = _settingsStore.Load();
        _repeatCount.Value = Math.Clamp(settings.RepeatCount, (int)_repeatCount.Minimum, (int)_repeatCount.Maximum);
        _repeatInterval.Value = Math.Clamp(settings.IntervalMilliseconds, (int)_repeatInterval.Minimum, (int)_repeatInterval.Maximum);
    }

    private void SaveSettings()
    {
        try
        {
            _settingsStore.Save(new PlaybackOptions
            {
                RepeatCount = (int)_repeatCount.Value,
                IntervalMilliseconds = (int)_repeatInterval.Value
            });
        }
        catch (Exception ex) { AppendLog("设置保存失败：" + ex.Message); }
    }

    private void RefreshScripts(string? selectPath = null)
    {
        var items = _repository.GetScripts();
        _scripts.BeginUpdate();
        _scripts.Items.Clear();
        foreach (var item in items) _scripts.Items.Add(item);
        _scripts.EndUpdate();

        if (selectPath is not null)
        {
            for (var i = 0; i < _scripts.Items.Count; i++)
            {
                if (_scripts.Items[i] is ScriptEntry entry &&
                    string.Equals(entry.FullPath, selectPath, StringComparison.OrdinalIgnoreCase))
                {
                    _scripts.SelectedIndex = i;
                    break;
                }
            }
        }

        if (_scripts.SelectedIndex < 0 && _scripts.Items.Count > 0)
            _scripts.SelectedIndex = 0;

        if (_scripts.Items.Count == 0)
        {
            _current = null;
            _loading = true;
            _editor.Clear();
            _loading = false;
            UpdateTitle();
        }
    }

    private void SelectScript()
    {
        if (_scripts.SelectedItem is not ScriptEntry selected || selected == _current)
            return;
        if (!SaveCurrent())
            return;

        try
        {
            _loading = true;
            _current = selected;
            _editor.Text = _repository.Read(selected);
            _dirty = false;
            UpdateTitle();
        }
        catch (Exception ex) { AppendLog("打开脚本失败：" + ex.Message); }
        finally { _loading = false; }
    }

    private void CreateScript()
    {
        if (!SaveCurrent()) return;
        try
        {
            var entry = _repository.Create();
            RefreshScripts(entry.FullPath);
            AppendLog("已创建脚本：" + entry.Name);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "新建失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private bool SaveCurrent()
    {
        if (_current is null || !_dirty) return true;
        try
        {
            _repository.Save(_current, _editor.Text);
            _dirty = false;
            UpdateTitle();
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "保存失败：" + ex.Message, "按键精灵 2026", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    private void RenameCurrent()
    {
        if (_current is null || !SaveCurrent()) return;
        using var prompt = new TextPromptForm("重命名脚本", "新的脚本名称：", _current.Name);
        if (prompt.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            var renamed = _repository.Rename(_current, prompt.Value);
            _current = renamed;
            RefreshScripts(renamed.FullPath);
            UpdateTitle();
            AppendLog($"脚本已重命名为：{renamed.Name}");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "重命名失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void DuplicateCurrent()
    {
        if (_current is null || !SaveCurrent()) return;
        try
        {
            var copy = _repository.Duplicate(_current);
            RefreshScripts(copy.FullPath);
            AppendLog($"已复制脚本：{copy.Name}");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "复制失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void DeleteCurrent()
    {
        if (_current is null) return;
        if (MessageBox.Show(this, $"确定删除脚本“{_current.Name}”吗？此操作不可撤销。",
            "删除脚本", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        try
        {
            _repository.Delete(_current);
            _current = null;
            _dirty = false;
            RefreshScripts();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "删除失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void StartRecording()
    {
        if (_recording || _playback.IsRunning) return;
        if (!SaveCurrent()) return;
        try
        {
            var entry = _repository.Create($"录制_{DateTime.Now:yyyyMMdd_HHmmss}");
            RefreshScripts(entry.FullPath);
            _loading = true;
            _editor.Clear();
            _dirty = true;
            _loading = false;
            _recorder.Start();
            _recording = true;
            _recordButton.Enabled = false;
            _stopRecordButton.Enabled = true;
            _playButton.Enabled = false;
            _status.Text = "正在录制…";
            AppendLog("开始全局录制。切换到目标窗口操作，完成后切回本软件点击“结束录制”。");
        }
        catch (Exception ex)
        {
            _recording = false;
            _recordButton.Enabled = true;
            _stopRecordButton.Enabled = false;
            _playButton.Enabled = true;
            AppendLog("录制启动失败：" + ex.Message);
            MessageBox.Show(this, ex.Message, "录制失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void StopRecording()
    {
        if (!_recording) return;
        try
        {
            var events = _recorder.Stop();
            _loading = true;
            _editor.Text = GlobalRecorder.GenerateLua(events);
            _dirty = true;
            _loading = false;
            SaveCurrent();
            AppendLog($"录制结束：采集 {events.Count} 个事件，已生成 Lua 脚本。");
            if (_recorder.Overflowed)
                AppendLog("警告：录制事件达到 250000 条上限，后续事件未保存。");
        }
        catch (Exception ex) { AppendLog("结束录制失败：" + ex.Message); }
        finally
        {
            _recording = false;
            _recordButton.Enabled = true;
            _stopRecordButton.Enabled = false;
            _playButton.Enabled = true;
            _status.Text = "未运行";
        }
    }

    private async Task StartPlaybackAsync()
    {
        if (_recording)
        {
            MessageBox.Show(this, "请先结束录制。");
            return;
        }
        if (_playback.IsRunning || _current is null || !SaveCurrent()) return;
        var scriptPath = _current.FullPath;
        var options = new PlaybackOptions
        {
            RepeatCount = (int)_repeatCount.Value,
            IntervalMilliseconds = (int)_repeatInterval.Value
        };
        try { options.Validate(); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message); return; }
        if (string.IsNullOrWhiteSpace(_editor.Text))
        {
            MessageBox.Show(this, "脚本内容为空。");
            return;
        }

        SaveSettings();
        _playButton.Enabled = false;
        _pauseButton.Enabled = true;
        _stopButton.Enabled = true;
        _pauseButton.Text = "暂停";
        _status.Text = "正在运行";
        AppendLog($"开始播放：{Path.GetFileNameWithoutExtension(scriptPath)}；次数={(options.IsInfinite ? "无限" : options.RepeatCount)}；每轮间隔={options.IntervalMilliseconds}ms。");

        try
        {
            _runTask = _playback.RunAsync(scriptPath, options);
            await _runTask;
        }
        catch (Exception ex)
        {
            AppendLog("脚本运行失败：" + ex);
            MessageBox.Show(this, ex.Message, "运行失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _runTask = null;
            SetIdle();
        }
    }

    private void TogglePause()
    {
        if (!_playback.IsRunning) return;
        if (_playback.IsPaused)
        {
            _playback.Resume();
            _pauseButton.Text = "暂停";
            _status.Text = "正在运行";
            AppendLog("已恢复。");
        }
        else
        {
            _playback.Pause();
            _pauseButton.Text = "继续";
            _status.Text = "已暂停";
            AppendLog("已请求暂停；当前鼠标/键盘原子操作完成后暂停生效。");
        }
    }

    private async Task StopPlaybackAsync()
    {
        if (!_playback.IsRunning) return;
        _status.Text = "正在停止";
        _playback.Stop();
        if (_runTask is not null)
        {
            try { await _runTask; } catch { }
        }
        SetIdle();
    }

    private void SetIdle()
    {
        _playButton.Enabled = !_recording;
        _pauseButton.Enabled = false;
        _stopButton.Enabled = false;
        _pauseButton.Text = "暂停";
        _status.Text = _recording ? "正在录制…" : "未运行";
        _progress.Text = "";
    }

    private void AppendLog(string text)
    {
        if (IsDisposed) return;
        _log.AppendText($"[{DateTime.Now:HH:mm:ss}] {text}{Environment.NewLine}");
        _log.SelectionStart = _log.TextLength;
        _log.ScrollToCaret();
    }

    private void UpdateTitle()
    {
        Text = $"按键精灵 2026 · {(_current?.Name ?? "未选择脚本")}{(_dirty ? " *" : "")}";
    }

    private void Ui(Action action)
    {
        if (IsDisposed || !IsHandleCreated) return;
        try
        {
            if (InvokeRequired) BeginInvoke(action);
            else action();
        }
        catch (InvalidOperationException) { }
    }

    private bool CanHandleGlobalHotkey(int action) => action switch
    {
        GlobalHotkeyManager.StartAction => !_recording && !_playback.IsRunning,
        GlobalHotkeyManager.PauseResumeAction or GlobalHotkeyManager.StopAction => !_recording && _playback.IsRunning,
        _ => false
    };

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        _hotkeyManager?.Dispose();
        _hotkeyManager = new GlobalHotkeyManager(Handle, CanHandleGlobalHotkey);
        if (_hotkeyManager.IsInstalled)
            AppendLog("全局快捷键已启用：F10 启动，F11 暂停/继续，F12 中止。");
        else
            AppendLog($"全局快捷键钩子安装失败（Win32 {_hotkeyManager.LastError}）；请使用窗口内的播放/暂停/停止按钮。");
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        _hotkeyManager?.Dispose();
        _hotkeyManager = null;
        base.OnHandleDestroyed(e);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == GlobalHotkeyManager.WindowMessage)
        {
            switch (m.WParam.ToInt32())
            {
                case GlobalHotkeyManager.StartAction:
                    if (!_recording && !_playback.IsRunning)
                        _ = StartPlaybackAsync();
                    break;
                case GlobalHotkeyManager.PauseResumeAction:
                    if (!_recording && _playback.IsRunning)
                        TogglePause();
                    break;
                case GlobalHotkeyManager.StopAction:
                    if (_playback.IsRunning)
                        _ = StopPlaybackAsync();
                    break;
            }
            return;
        }

        base.WndProc(ref m);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_recording) StopRecording();
        SaveCurrent();
        _playback.Stop();
        _playback.Dispose();
        _recorder.Dispose();
        base.OnFormClosing(e);
    }
}

internal sealed class TextPromptForm : Form
{
    private readonly TextBox _textBox = new();
    public string Value => _textBox.Text;

    public TextPromptForm(string title, string label, string initial)
    {
        Text = title;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(360, 112);
        Controls.Add(new Label { Text = label, Left = 12, Top = 10, AutoSize = true });
        _textBox.SetBounds(12, 34, 336, 24);
        _textBox.Text = initial;
        Controls.Add(_textBox);
        var ok = new Button { Text = "确定", DialogResult = DialogResult.OK, Left = 188, Top = 70, Width = 76 };
        var cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Left = 272, Top = 70, Width = 76 };
        Controls.Add(ok);
        Controls.Add(cancel);
        AcceptButton = ok;
        CancelButton = cancel;
    }
}
