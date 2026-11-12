using Anjian.App.Models;
using Anjian.App.Services;
using Anjian.App.Controls;
using Anjian.Core;
using Anjian.Core.Api;
using Anjian.Core.Editor;

namespace Anjian.App;

internal sealed class MainForm : Form
{
    private readonly ScriptRepository _repository = new();
    private readonly AppSettingsStore _settingsStore = new();
    private readonly GlobalRecorder _recorder = new();
    private readonly PlaybackRunner _playback = new();
    private readonly ListBox _scripts = new();
    private readonly LuaApiRegistry _apiRegistry = LuaApiRegistry.Load();
    private readonly TabControl _editorTabs = new() { Dock = DockStyle.Fill };
    private readonly RichTextBox _emptyEditor = new();
    private readonly Dictionary<string, ScriptTab> _openTabs = new(StringComparer.OrdinalIgnoreCase);
    private ScriptTab? _activeTab;
    private RichTextBox _editor => _activeTab?.Editor.TextBox ?? _emptyEditor;
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
    private readonly Button _debugStartButton = new();
    private readonly Button _debugContinueButton = new();
    private readonly Button _debugStepIntoButton = new();
    private readonly Button _debugStepOverButton = new();
    private readonly Button _debugStepOutButton = new();
    private readonly Button _debugRunToCursorButton = new();
    private readonly ListBox _debugCallStack = new();
    private readonly DataGridView _debugVariables = new();
    private readonly Label _debugSummary = new();
    private LuaDebugSnapshot? _debugSnapshot;
    private readonly Panel _fullModePanel = new() { Dock = DockStyle.Fill };
    private readonly CheckBox _clickOnlyCheck = new() { Text = "只记录点击坐标", AutoSize = true, Checked = true };
    private readonly CheckBox _alwaysOnTopCheck = new() { Text = "悬浮窗始终置顶", AutoSize = true };
    private bool _recordSessionActive;
    private bool _recordingPaused;
    private bool _clickOnlyForSession;
    private bool _loadingSettings;
    private GlobalHotkeyManager? _hotkeyManager;
    private ScriptEntry? _current;
    private bool _recording;
    private bool _dirty;
    private Task? _runTask;
    private FloatingForm? _floating;
    private NotifyIcon? _trayIcon;
    private Icon? _applicationIcon;
    private bool _allowExit;
    private bool _refreshingScripts;
    private bool _suppressTabSelection;

    private sealed class ScriptTab(ScriptEntry entry, TabPage page, LuaEditorControl editor)
    {
        public ScriptEntry Entry { get; set; } = entry;
        public TabPage Page { get; } = page;
        public LuaEditorControl Editor { get; } = editor;
        public bool Dirty { get; set; }
        public HashSet<int> Breakpoints { get; } = [];
    }

    public MainForm()
    {
        Text = "按键精灵 2026";
        MinimumSize = new Size(980, 650);
        Size = new Size(1180, 780);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Microsoft YaHei UI", 9F);
        ShowInTaskbar = false;
        Opacity = 0;
        _applicationIcon = LoadApplicationIcon();
        Icon = (Icon)_applicationIcon.Clone();
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
        _playback.DebugPaused += snapshot => Ui(() => ShowDebugSnapshot(snapshot));
        _playback.DebugSessionCompleted += () => Ui(ClearDebuggerView);
        _scripts.SelectedIndexChanged += (_, _) => SelectScript();
        _editorTabs.SelectedIndexChanged += (_, _) => HandleEditorTabSelectionChanged();
        _repeatCount.ValueChanged += (_, _) =>
        {
            _floating?.SetRepeatCount((int)_repeatCount.Value);
            SaveSettings();
        };
        _repeatInterval.ValueChanged += (_, _) =>
        {
            _floating?.SetRepeatInterval((int)_repeatInterval.Value);
            SaveSettings();
        };
        _clickOnlyCheck.CheckedChanged += (_, _) =>
        {
            _floating?.SetClickOnlyRecording(_clickOnlyCheck.Checked);
            SaveSettings();
        };
        _alwaysOnTopCheck.CheckedChanged += (_, _) => SetAlwaysOnTop(_alwaysOnTopCheck.Checked);

        LoadSettings();
        RefreshScripts();
        InitializeFloatingAndTray();
        UpdateFloatingState();
        Shown += (_, _) =>
        {
            Hide();
            Opacity = 1;
            ShowFloatingWindow();
        };
        AppendLog("就绪。脚本保存在：" + _repository.RootDirectory);
        AppendLog("播放设置：次数 1=执行一次，0=无限；间隔只在每轮之间等待。");
        AppendLog("全局快捷键：F10 启动，F11 暂停/继续，F12 立即中止。");
        UpdateDebuggerButtons(isPaused: false);
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
        _recordButton.Click += (_, _) => ToggleRecording();
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
        toolbar.Controls.Add(MakeButton("查找/替换", (_, _) => ShowSearchReplace()));
        toolbar.Controls.Add(MakeButton("检查语法", async (_, _) => await CheckCurrentSyntaxAsync()));
        toolbar.Controls.Add(MakeButton("关闭标签", (_, _) => CloseCurrentTab()));

        _debugStartButton.Text = "启动调试";
        _debugStartButton.Click += async (_, _) => await StartDebugAsync();
        toolbar.Controls.Add(_debugStartButton);
        _debugContinueButton.Text = "继续";
        _debugContinueButton.Enabled = false;
        _debugContinueButton.Click += (_, _) => SubmitDebugCommand(LuaDebugCommand.Continue);
        toolbar.Controls.Add(_debugContinueButton);
        _debugStepIntoButton.Text = "单步进入";
        _debugStepIntoButton.Enabled = false;
        _debugStepIntoButton.Click += (_, _) => SubmitDebugCommand(LuaDebugCommand.StepInto);
        toolbar.Controls.Add(_debugStepIntoButton);
        _debugStepOverButton.Text = "单步越过";
        _debugStepOverButton.Enabled = false;
        _debugStepOverButton.Click += (_, _) => SubmitDebugCommand(LuaDebugCommand.StepOver);
        toolbar.Controls.Add(_debugStepOverButton);
        _debugStepOutButton.Text = "单步跳出";
        _debugStepOutButton.Enabled = false;
        _debugStepOutButton.Click += (_, _) => SubmitDebugCommand(LuaDebugCommand.StepOut);
        toolbar.Controls.Add(_debugStepOutButton);
        _debugRunToCursorButton.Text = "运行到光标";
        _debugRunToCursorButton.Enabled = false;
        _debugRunToCursorButton.Click += (_, _) => RunToCursor();
        toolbar.Controls.Add(_debugRunToCursorButton);

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
        _clickOnlyCheck.Margin = new Padding(10, 7, 4, 0);
        toolbar.Controls.Add(_clickOnlyCheck);
        _alwaysOnTopCheck.Margin = new Padding(4, 7, 4, 0);
        toolbar.Controls.Add(_alwaysOnTopCheck);
        toolbar.Controls.Add(MakeButton("返回悬浮窗", (_, _) => HideMainWindow(showFloatingWindow: true)));
        _fullModePanel.Controls.Add(toolbar);

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
        var right = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 57));
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 18));

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

        _editorTabs.Dock = DockStyle.Fill;
        _editorTabs.Padding = new Point(12, 4);
        _editorTabs.ShowToolTips = true;
        right.Controls.Add(_editorTabs, 0, 1);

        var debugPanel = new Panel { Dock = DockStyle.Fill };
        var debugHeader = new Panel { Dock = DockStyle.Top, Height = 26 };
        debugHeader.Controls.Add(new Label
        {
            Text = "调试器：单击编辑器行号槽左侧圆点设置/取消断点",
            Dock = DockStyle.Left,
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold)
        });
        _debugSummary.Dock = DockStyle.Right;
        _debugSummary.AutoSize = true;
        debugHeader.Controls.Add(_debugSummary);

        var debugSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Size = new Size(900, 160),
            SplitterDistance = 270,
            Panel1MinSize = 180,
            Panel2MinSize = 300
        };
        _debugCallStack.Dock = DockStyle.Fill;
        _debugCallStack.IntegralHeight = false;
        _debugCallStack.SelectedIndexChanged += (_, _) => RefreshDebugVariables();
        debugSplit.Panel1.Controls.Add(_debugCallStack);

        _debugVariables.Dock = DockStyle.Fill;
        _debugVariables.ReadOnly = true;
        _debugVariables.AllowUserToAddRows = false;
        _debugVariables.AllowUserToDeleteRows = false;
        _debugVariables.AllowUserToResizeRows = false;
        _debugVariables.RowHeadersVisible = false;
        _debugVariables.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _debugVariables.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _debugVariables.Columns.Add("Scope", "范围");
        _debugVariables.Columns.Add("Name", "变量");
        _debugVariables.Columns.Add("Type", "类型");
        _debugVariables.Columns.Add("Value", "值");
        _debugVariables.Columns.Add("Limitation", "说明");
        debugSplit.Panel2.Controls.Add(_debugVariables);
        debugPanel.Controls.Add(debugSplit);
        debugPanel.Controls.Add(debugHeader);
        right.Controls.Add(debugPanel, 0, 2);

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
        right.Controls.Add(logPanel, 0, 3);

        split.Panel2.Controls.Add(right);
        _fullModePanel.Controls.Add(split);
        Controls.Add(_fullModePanel);
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

    private static Icon LoadApplicationIcon()
    {
        try
        {
            return Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? (Icon)SystemIcons.Application.Clone();
        }
        catch
        {
            return (Icon)SystemIcons.Application.Clone();
        }
    }

    private void InitializeFloatingAndTray()
    {
        _floating = new FloatingForm((Icon)_applicationIcon!.Clone());
        _floating.RecordToggleRequested += ToggleRecording;
        _floating.StopRecordingRequested += StopRecording;
        _floating.PlayRequested += () => _ = StartPlaybackAsync();
        _floating.PauseRequested += TogglePause;
        _floating.StopPlaybackRequested += () => _ = StopPlaybackAsync();
        _floating.OpenMainRequested += ShowMainWindow;
        _floating.HideRequested += HideFloatingWindow;
        _floating.RepeatCountChanged += value =>
        {
            if (_repeatCount.Value != value) _repeatCount.Value = Math.Clamp(value, (int)_repeatCount.Minimum, (int)_repeatCount.Maximum);
        };
        _floating.RepeatIntervalChanged += value =>
        {
            if (_repeatInterval.Value != value) _repeatInterval.Value = Math.Clamp(value, (int)_repeatInterval.Minimum, (int)_repeatInterval.Maximum);
        };
        _floating.ClickOnlyRecordingChanged += value =>
        {
            if (_clickOnlyCheck.Checked != value) _clickOnlyCheck.Checked = value;
        };
        _floating.AlwaysOnTopChanged += value =>
        {
            if (_alwaysOnTopCheck.Checked != value) _alwaysOnTopCheck.Checked = value;
        };
        _floating.SetSettings((int)_repeatCount.Value, (int)_repeatInterval.Value,
            _clickOnlyCheck.Checked, _alwaysOnTopCheck.Checked);

        var trayMenu = new ContextMenuStrip();
        trayMenu.Items.Add("打开主窗口", null, (_, _) => ShowMainWindow());
        trayMenu.Items.Add("显示悬浮窗", null, (_, _) => ShowFloatingWindow());
        trayMenu.Items.Add("隐藏悬浮窗", null, (_, _) => HideFloatingWindow());
        trayMenu.Items.Add(new ToolStripSeparator());
        trayMenu.Items.Add("退出按键精灵 2026", null, (_, _) => ExitApplication());

        _trayIcon = new NotifyIcon
        {
            Icon = (Icon)_applicationIcon.Clone(),
            Text = "按键精灵 2026",
            ContextMenuStrip = trayMenu,
            Visible = true
        };
        _trayIcon.DoubleClick += (_, _) => ShowMainWindow();
    }

    private void ShowMainWindow()
    {
        ShowInTaskbar = true;
        if (!Visible) Show();
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
        BringToFront();
        Activate();
    }

    private void HideMainWindow(bool showFloatingWindow)
    {
        if (!SaveCurrent()) return;
        SaveSettings();
        ShowInTaskbar = false;
        Hide();
        if (showFloatingWindow) ShowFloatingWindow();
    }

    private void ShowFloatingWindow()
    {
        if (_floating is null || _floating.IsDisposed) return;
        if (!_floating.Visible) _floating.Show();
        _floating.UpdateState(_current?.Name ?? "未选择", _recordSessionActive, _recording,
            _recordingPaused, _playback.IsRunning, _playback.IsPaused);
    }

    private void HideFloatingWindow()
    {
        if (_floating is { IsDisposed: false }) _floating.Hide();
    }

    private void ExitApplication()
    {
        _allowExit = true;
        Close();
    }

    private void UpdateFloatingState()
    {
        _floating?.UpdateState(_current?.Name ?? "未选择", _recordSessionActive, _recording,
            _recordingPaused, _playback.IsRunning, _playback.IsPaused);
    }

    private void SetAlwaysOnTop(bool enabled)
    {
        if (_alwaysOnTopCheck.Checked != enabled) _alwaysOnTopCheck.Checked = enabled;
        _floating?.SetAlwaysOnTop(enabled);
        SaveSettings();
    }

    private void LoadSettings()
    {
        _loadingSettings = true;
        var settings = _settingsStore.Load();
        _repeatCount.Value = Math.Clamp(settings.RepeatCount, (int)_repeatCount.Minimum, (int)_repeatCount.Maximum);
        _repeatInterval.Value = Math.Clamp(settings.IntervalMilliseconds, (int)_repeatInterval.Minimum, (int)_repeatInterval.Maximum);
        _clickOnlyCheck.Checked = settings.ClickOnlyRecording;
        _alwaysOnTopCheck.Checked = settings.AlwaysOnTop;
        _loadingSettings = false;
    }

    private void SaveSettings()
    {
        if (_loadingSettings) return;
        try
        {
            _settingsStore.Save(new AppSettings
            {
                RepeatCount = (int)_repeatCount.Value,
                IntervalMilliseconds = (int)_repeatInterval.Value,
                ClickOnlyRecording = _clickOnlyCheck.Checked,
                AlwaysOnTop = _alwaysOnTopCheck.Checked
            });
        }
        catch (Exception ex) { AppendLog("设置保存失败：" + ex.Message); }
    }

    private void RefreshScripts(string? selectPath = null)
    {
        var items = _repository.GetScripts();
        var wantedPath = selectPath ?? _current?.FullPath;
        _refreshingScripts = true;
        try
        {
            _scripts.BeginUpdate();
            _scripts.Items.Clear();
            foreach (var item in items) _scripts.Items.Add(item);
            _scripts.SelectedIndex = -1;

            if (wantedPath is not null)
            {
                for (var i = 0; i < _scripts.Items.Count; i++)
                {
                    if (_scripts.Items[i] is ScriptEntry entry &&
                        string.Equals(entry.FullPath, wantedPath, StringComparison.OrdinalIgnoreCase))
                    {
                        _scripts.SelectedIndex = i;
                        break;
                    }
                }
            }

            if (_scripts.SelectedIndex < 0 && _scripts.Items.Count > 0)
                _scripts.SelectedIndex = 0;
        }
        finally
        {
            _scripts.EndUpdate();
            _refreshingScripts = false;
        }

        if (_scripts.SelectedItem is ScriptEntry selected)
        {
            if (_activeTab is null || !string.Equals(_activeTab.Entry.FullPath, selected.FullPath, StringComparison.OrdinalIgnoreCase))
                SelectScript();
        }
        else if (_activeTab is null)
        {
            _emptyEditor.Clear();
            _current = null;
            _dirty = false;
            UpdateTitle();
        }
    }

    private void SelectScript()
    {
        if (_refreshingScripts || _scripts.SelectedItem is not ScriptEntry selected)
            return;

        if (_recordSessionActive)
        {
            SetScriptListSelection(_current);
            AppendLog("录制期间不能切换脚本，请先结束录制。");
            return;
        }

        if (_activeTab is not null &&
            string.Equals(_activeTab.Entry.FullPath, selected.FullPath, StringComparison.OrdinalIgnoreCase))
            return;

        OpenScriptTab(selected);
    }

    private void OpenScriptTab(ScriptEntry entry)
    {
        if (_activeTab is not null && !SaveCurrent())
        {
            SetScriptListSelection(_current);
            return;
        }

        if (_openTabs.TryGetValue(entry.FullPath, out var existing))
        {
            _editorTabs.SelectedTab = existing.Page;
            ActivateTab(existing);
            return;
        }

        try
        {
            var editor = new LuaEditorControl(_apiRegistry) { Dock = DockStyle.Fill };
            editor.TextBox.Text = _repository.Read(entry);
            var page = new TabPage(entry.Name) { Tag = null, ToolTipText = entry.FullPath };
            var tab = new ScriptTab(entry, page, editor);
            editor.SetBreakpoints(tab.Breakpoints);
            editor.BreakpointToggled += line =>
            {
                if (!tab.Breakpoints.Add(line))
                    tab.Breakpoints.Remove(line);
            };
            page.Tag = tab;
            page.Controls.Add(editor);
            editor.ContentChanged += (_, _) =>
            {
                tab.Dirty = true;
                tab.Page.Text = tab.Entry.Name + " *";
                if (ReferenceEquals(_activeTab, tab))
                {
                    _dirty = true;
                    UpdateTitle();
                }
            };

            _openTabs.Add(entry.FullPath, tab);
            _editorTabs.TabPages.Add(page);
            _editorTabs.SelectedTab = page;
            ActivateTab(tab);
        }
        catch (Exception ex)
        {
            AppendLog("打开脚本失败：" + ex.Message);
            MessageBox.Show(this, ex.Message, "打开脚本失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void HandleEditorTabSelectionChanged()
    {
        if (_suppressTabSelection)
            return;

        if (_recordSessionActive && _activeTab is not null &&
            _editorTabs.SelectedTab?.Tag is ScriptTab recordingAlternative &&
            !ReferenceEquals(recordingAlternative, _activeTab))
        {
            _suppressTabSelection = true;
            _editorTabs.SelectedTab = _activeTab.Page;
            _suppressTabSelection = false;
            AppendLog("录制期间不能切换脚本标签，请先结束录制。");
            return;
        }

        if (_editorTabs.SelectedTab?.Tag is ScriptTab selected)
        {
            ActivateTab(selected);
            return;
        }

        if (_activeTab is not null && SaveCurrent())
        {
            _activeTab = null;
            _current = null;
            _dirty = false;
            UpdateTitle();
        }
    }

    private void ActivateTab(ScriptTab tab)
    {
        if (!ReferenceEquals(_activeTab, tab) && !SaveCurrent())
        {
            _suppressTabSelection = true;
            _editorTabs.SelectedTab = _activeTab?.Page;
            _suppressTabSelection = false;
            return;
        }

        _activeTab = tab;
        _current = tab.Entry;
        _dirty = tab.Dirty;
        SetScriptListSelection(tab.Entry);
        UpdateTitle();
        if (_debugSnapshot is not null &&
            string.Equals(NormalizeScriptPath(_debugSnapshot.Source), NormalizeScriptPath(tab.Entry.FullPath),
                StringComparison.OrdinalIgnoreCase))
            tab.Editor.SetCurrentExecutionLine(_debugSnapshot.Line);
        else
            tab.Editor.SetCurrentExecutionLine(null);
        UpdateDebuggerButtons(isPaused: _debugSnapshot is not null);
    }

    private void SetScriptListSelection(ScriptEntry? entry)
    {
        _refreshingScripts = true;
        try
        {
            var index = -1;
            if (entry is not null)
            {
                for (var i = 0; i < _scripts.Items.Count; i++)
                {
                    if (_scripts.Items[i] is ScriptEntry candidate &&
                        string.Equals(candidate.FullPath, entry.FullPath, StringComparison.OrdinalIgnoreCase))
                    {
                        index = i;
                        break;
                    }
                }
            }

            _scripts.SelectedIndex = index;
        }
        finally
        {
            _refreshingScripts = false;
        }
    }

    private void CloseCurrentTab()
    {
        if (_recordSessionActive)
        {
            AppendLog("录制期间不能关闭脚本标签，请先结束录制。");
            return;
        }

        if (_activeTab is null || !SaveCurrent())
            return;

        var closing = _activeTab;
        _suppressTabSelection = true;
        _openTabs.Remove(closing.Entry.FullPath);
        _editorTabs.TabPages.Remove(closing.Page);
        _activeTab = null;
        _current = null;
        _dirty = false;
        _suppressTabSelection = false;
        closing.Page.Dispose();

        if (_editorTabs.SelectedTab?.Tag is ScriptTab next)
            ActivateTab(next);
        else
        {
            SetScriptListSelection(null);
            UpdateTitle();
        }
    }

    private void ShowSearchReplace()
    {
        if (_activeTab is null)
        {
            MessageBox.Show(this, "请先打开一个脚本。", "查找与替换", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new ScriptSearchReplaceForm(_editor);
        dialog.ShowDialog(this);
    }

    private async Task CheckCurrentSyntaxAsync()
    {
        if (_recordSessionActive || _current is null || !SaveCurrent())
            return;

        var path = _current.FullPath;
        var source = _editor.Text;
        _status.Text = "正在检查语法…";
        try
        {
            var diagnostics = await Task.Run(() =>
                LuaEditorService.AnalyzeSyntax(source, Path.GetFileName(path)));

            if (!string.Equals(_current?.FullPath, path, StringComparison.OrdinalIgnoreCase))
                return;

            if (diagnostics.Count == 0)
            {
                AppendLog($"语法检查通过：{Path.GetFileName(path)}（仅编译检查，未执行脚本）。");
                MessageBox.Show(this, "Lua 语法检查通过。脚本没有被执行。", "语法检查", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            foreach (var diagnostic in diagnostics)
                AppendLog($"语法错误：{Path.GetFileName(path)} 第 {diagnostic.Line} 行，第 {diagnostic.Column} 列：{diagnostic.Message}");

            var first = diagnostics[0];
            NavigateToLine(first.Line, first.Column);
            MessageBox.Show(this,
                $"{Path.GetFileName(path)}：第 {first.Line} 行，第 {first.Column} 列\n{first.Message}",
                "语法检查失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (Exception ex)
        {
            AppendLog("语法检查失败：" + ex.Message);
            MessageBox.Show(this, ex.Message, "语法检查失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _status.Text = _playback.IsRunning ? "正在运行" : "未运行";
        }
    }

    private void NavigateToLine(int line, int column)
    {
        var index = _editor.GetFirstCharIndexFromLine(Math.Max(0, line - 1));
        if (index < 0)
            index = Math.Max(0, _editor.TextLength - 1);

        var position = Math.Clamp(index + Math.Max(0, column - 1), 0, _editor.TextLength);
        _editor.Select(position, position < _editor.TextLength ? 1 : 0);
        _editor.ScrollToCaret();
        _editor.Focus();
    }

    private void CreateScript()
    {
        if (_recordSessionActive) { AppendLog("请先结束录制，再新建脚本。"); return; }
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
        if (_recordSessionActive) return true;
        if (_current is null || (!_dirty && _activeTab?.Dirty != true)) return true;
        try
        {
            _repository.Save(_current, _editor.Text);
            _dirty = false;
            if (_activeTab is not null)
            {
                _activeTab.Dirty = false;
                _activeTab.Page.Text = _activeTab.Entry.Name;
            }
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
        if (_recordSessionActive) { AppendLog("请先结束录制，再重命名脚本。"); return; }
        if (_current is null || !SaveCurrent()) return;
        using var prompt = new TextPromptForm("重命名脚本", "新的脚本名称：", _current.Name);
        if (prompt.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            var oldPath = _current.FullPath;
            var renamed = _repository.Rename(_current, prompt.Value);
            if (_activeTab is not null)
            {
                _openTabs.Remove(oldPath);
                _activeTab.Entry = renamed;
                _openTabs[renamed.FullPath] = _activeTab;
                _activeTab.Page.Text = renamed.Name + (_activeTab.Dirty ? " *" : string.Empty);
            }
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
        if (_recordSessionActive) { AppendLog("请先结束录制，再复制脚本。"); return; }
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
        if (_recordSessionActive) { AppendLog("请先结束录制，再删除脚本。"); return; }
        if (_current is null) return;
        if (MessageBox.Show(this, $"确定删除脚本“{_current.Name}”吗？此操作不可撤销。",
            "删除脚本", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        try
        {
            var deletedPath = _current.FullPath;
            _repository.Delete(_current);
            if (_activeTab is not null &&
                string.Equals(_activeTab.Entry.FullPath, deletedPath, StringComparison.OrdinalIgnoreCase))
            {
                var closing = _activeTab;
                _suppressTabSelection = true;
                _openTabs.Remove(deletedPath);
                _editorTabs.TabPages.Remove(closing.Page);
                _activeTab = null;
                _current = null;
                _dirty = false;
                _suppressTabSelection = false;
                closing.Page.Dispose();
            }
            RefreshScripts();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "删除失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ToggleRecording()
    {
        if (!_recordSessionActive) StartRecording();
        else if (_recording) PauseRecording();
        else ResumeRecording();
    }

    private void StartRecording()
    {
        if (_recordSessionActive || _playback.IsRunning) return;
        if (!SaveCurrent()) return;
        try
        {
            var entry = _repository.Create($"录制_{DateTime.Now:yyyyMMdd_HHmmss}");
            RefreshScripts(entry.FullPath);
            _editor.Clear();
            _dirty = true;
            _clickOnlyForSession = _clickOnlyCheck.Checked;
            _recorder.Start(_clickOnlyForSession);
            _recordSessionActive = true;
            _recording = true;
            _recordingPaused = false;
            _editor.ReadOnly = true;
            UpdateRecordingUi();
            AppendLog(_clickOnlyForSession
                ? "开始录制（只记录点击坐标）：每次点击只生成点击位置，不记录鼠标轨迹。"
                : "开始全局录制：已启用鼠标移动采集。");
        }
        catch (Exception ex)
        {
            _recordSessionActive = false;
            _recording = false;
            _recordingPaused = false;
            _editor.ReadOnly = false;
            UpdateRecordingUi();
            AppendLog("录制启动失败：" + ex.Message);
            MessageBox.Show(this, ex.Message, "录制失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void PauseRecording()
    {
        if (!_recordSessionActive || !_recording) return;
        try
        {
            _recorder.Pause();
            _recording = false;
            _recordingPaused = true;
            UpdateRecordingPreview();
            UpdateRecordingUi();
            AppendLog("录制已暂停；之前采集的动作已保留。点击“继续录制”可从当前位置接着录。");
        }
        catch (Exception ex) { AppendLog("暂停录制失败：" + ex.Message); }
    }

    private void ResumeRecording()
    {
        if (!_recordSessionActive || _recording) return;
        try
        {
            _recorder.Resume();
            _recording = true;
            _recordingPaused = false;
            UpdateRecordingUi();
            AppendLog("继续录制；暂停期间的等待时间不会写入脚本。");
        }
        catch (Exception ex)
        {
            AppendLog("继续录制失败：" + ex.Message);
            MessageBox.Show(this, ex.Message, "继续录制失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void UpdateRecordingPreview()
    {
        _editor.Text = GlobalRecorder.GenerateLua(_recorder.GetEventsSnapshot(), _clickOnlyForSession);
        _dirty = true;
    }

    private void StopRecording()
    {
        if (!_recordSessionActive) return;
        try
        {
            var events = _recorder.Stop();
            _editor.ReadOnly = false;
            _editor.Text = GlobalRecorder.GenerateLua(events, _clickOnlyForSession);
            _dirty = true;
            _recordSessionActive = false;
            _recording = false;
            _recordingPaused = false;
            SaveCurrent();
            AppendLog($"录制结束：采集 {events.Count} 个事件，已生成 Lua 脚本。");
            if (_recorder.Overflowed)
                AppendLog("警告：录制事件达到 250000 条上限，后续事件未保存。");
        }
        catch (Exception ex) { AppendLog("结束录制失败：" + ex.Message); }
        finally
        {
            _recordSessionActive = false;
            _recording = false;
            _recordingPaused = false;
            _editor.ReadOnly = false;
            UpdateRecordingUi();
        }
    }

    private void UpdateRecordingUi()
    {
        var buttonText = !_recordSessionActive ? "● 开始录制" : _recordingPaused ? "▶ 继续录制" : "Ⅱ 暂停录制";
        _recordButton.Text = buttonText;
        _stopRecordButton.Enabled = _recordSessionActive;
        _recordButton.Enabled = !_playback.IsRunning;
        _scripts.Enabled = !_recordSessionActive;
        _clickOnlyCheck.Enabled = !_recordSessionActive;
        _playButton.Enabled = !_recordSessionActive && !_playback.IsRunning;
        _status.Text = !_recordSessionActive ? "未运行" : _recordingPaused ? "录制已暂停" : "正在录制…";
        UpdateFloatingState();
        UpdateDebuggerButtons(isPaused: false);
    }

    private async Task StartPlaybackAsync()
    {
        if (_recordSessionActive)
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
        _recordButton.Enabled = false;
        _pauseButton.Enabled = true;
        _stopButton.Enabled = true;
        _pauseButton.Text = "暂停";
        _status.Text = "正在运行";
        UpdateDebuggerButtons(isPaused: false);
        UpdateFloatingState();
        AppendLog($"开始播放：{Path.GetFileNameWithoutExtension(scriptPath)}；次数={(options.IsInfinite ? "无限" : options.RepeatCount)}；每轮间隔={options.IntervalMilliseconds}ms。");

        try
        {
            _runTask = _playback.RunAsync(scriptPath, options);
            UpdateFloatingState();
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
        UpdateFloatingState();
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
        _playButton.Enabled = !_recordSessionActive;
        _pauseButton.Enabled = false;
        _stopButton.Enabled = false;
        _pauseButton.Text = "暂停";
        _status.Text = !_recordSessionActive ? "未运行" : _recordingPaused ? "正在录制…" : "录制已暂停";
        _progress.Text = "";
        _scripts.Enabled = !_recordSessionActive;
        _editorTabs.Enabled = true;
        UpdateRecordingUi();
        UpdateDebuggerButtons(isPaused: false);
    }

    private async Task StartDebugAsync()
    {
        if (_recordSessionActive || _playback.IsRunning || _activeTab is null || _current is null || !SaveCurrent())
            return;

        var tab = _activeTab;
        var scriptPath = tab.Entry.FullPath;
        var breakpoints = tab.Breakpoints
            .Order()
            .Select(line => new LuaDebugBreakpoint(scriptPath, line))
            .ToArray();
        var launchOptions = new LuaDebugLaunchOptions(breakpoints);
        var playbackOptions = new PlaybackOptions { RepeatCount = 1, IntervalMilliseconds = 0 };

        ClearDebuggerView();
        _scripts.Enabled = false;
        _editorTabs.Enabled = false;
        _playButton.Enabled = false;
        _recordButton.Enabled = false;
        _pauseButton.Enabled = false;
        _status.Text = "正在启动调试…";
        _debugSummary.Text = "等待 Lua 首个执行行…";
        _stopButton.Enabled = true;
        UpdateDebuggerButtons(isPaused: false);
        AppendLog($"开始调试：{Path.GetFileName(scriptPath)}；断点 {breakpoints.Length} 个。");

        try
        {
            _runTask = _playback.RunAsync(scriptPath, playbackOptions, launchOptions);
            UpdateFloatingState();
            await _runTask;
        }
        catch (Exception ex)
        {
            AppendLog("调试运行失败：" + ex);
            MessageBox.Show(this, ex.Message, "调试失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _runTask = null;
            ClearDebuggerView();
            SetIdle();
        }
    }

    private void SubmitDebugCommand(LuaDebugCommand command)
    {
        if (!_playback.IsDebugging || _debugSnapshot is null)
            return;

        _debugSummary.Text = "继续执行中…";
        _debugContinueButton.Enabled = false;
        _debugStepIntoButton.Enabled = false;
        _debugStepOverButton.Enabled = false;
        _debugStepOutButton.Enabled = false;
        _debugRunToCursorButton.Enabled = false;
        _playback.SubmitDebugCommand(command);
    }

    private void RunToCursor()
    {
        if (_current is null || _activeTab is null)
            return;

        SubmitDebugCommand(LuaDebugCommand.RunToCursor(_current.FullPath, _activeTab.Editor.CaretLine));
    }

    private void ShowDebugSnapshot(LuaDebugSnapshot snapshot)
    {
        _debugSnapshot = snapshot;
        _debugCallStack.BeginUpdate();
        _debugCallStack.Items.Clear();
        foreach (var frame in snapshot.Frames)
        {
            var location = string.IsNullOrWhiteSpace(frame.Source)
                ? "(无源码位置)"
                : $"{Path.GetFileName(frame.Source)}:{frame.Line}";
            _debugCallStack.Items.Add($"{frame.Index}: {frame.Name} — {location}");
        }
        _debugCallStack.EndUpdate();
        if (_debugCallStack.Items.Count > 0)
            _debugCallStack.SelectedIndex = 0;

        if (_activeTab is not null &&
            string.Equals(NormalizeScriptPath(snapshot.Source), NormalizeScriptPath(_activeTab.Entry.FullPath),
                StringComparison.OrdinalIgnoreCase))
            _activeTab.Editor.SetCurrentExecutionLine(snapshot.Line);
        else if (_activeTab is not null)
            _activeTab.Editor.SetCurrentExecutionLine(null);

        _debugSummary.Text = $"暂停于 {Path.GetFileName(snapshot.Source)}:{snapshot.Line}";
        _status.Text = "调试已暂停";
        AppendLog($"调试暂停：{Path.GetFileName(snapshot.Source)} 第 {snapshot.Line} 行。");
        RefreshDebugVariables();
        UpdateDebuggerButtons(isPaused: true);
    }

    private void RefreshDebugVariables()
    {
        _debugVariables.Rows.Clear();
        if (_debugSnapshot is null)
            return;

        var selectedIndex = Math.Clamp(_debugCallStack.SelectedIndex, 0, Math.Max(0, _debugSnapshot.Frames.Count - 1));
        if (_debugSnapshot.Frames.Count > 0)
        {
            foreach (var variable in _debugSnapshot.Frames[selectedIndex].Locals)
                _debugVariables.Rows.Add("局部", variable.Name, variable.Type, variable.Value, variable.Limitation ?? "");
        }

        foreach (var variable in _debugSnapshot.Globals)
            _debugVariables.Rows.Add("全局", variable.Name, variable.Type, variable.Value, variable.Limitation ?? "");
    }

    private void ClearDebuggerView()
    {
        _debugSnapshot = null;
        _debugCallStack.Items.Clear();
        _debugVariables.Rows.Clear();
        _debugSummary.Text = "";
        foreach (var tab in _openTabs.Values)
            tab.Editor.SetCurrentExecutionLine(null);
        UpdateDebuggerButtons(isPaused: false);
    }

    private void UpdateDebuggerButtons(bool isPaused)
    {
        var running = _playback.IsRunning;
        var debugging = _playback.IsDebugging;
        _debugStartButton.Enabled = !_recordSessionActive && !running && _activeTab is not null;
        _debugContinueButton.Enabled = debugging && isPaused;
        _debugStepIntoButton.Enabled = debugging && isPaused;
        _debugStepOverButton.Enabled = debugging && isPaused;
        _debugStepOutButton.Enabled = debugging && isPaused;
        _debugRunToCursorButton.Enabled = debugging && isPaused && _activeTab is not null;
    }

    private static string NormalizeScriptPath(string path)
    {
        try { return Path.GetFullPath(path.TrimStart('@')); }
        catch { return path; }
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
        if (_activeTab is not null)
            _activeTab.Page.Text = _activeTab.Entry.Name + (_activeTab.Dirty ? " *" : string.Empty);
        UpdateFloatingState();
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
        GlobalHotkeyManager.StartAction => !_recordSessionActive && !_playback.IsRunning,
        GlobalHotkeyManager.PauseResumeAction or GlobalHotkeyManager.StopAction => !_recordSessionActive && _playback.IsRunning,
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
                    if (!_recordSessionActive && !_playback.IsRunning)
                        _ = StartPlaybackAsync();
                    break;
                case GlobalHotkeyManager.PauseResumeAction:
                    if (!_recordSessionActive && _playback.IsRunning)
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
        if (!_allowExit && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            HideMainWindow(showFloatingWindow: false);
            return;
        }

        if (_recordSessionActive) StopRecording();
        SaveSettings();
        SaveCurrent();
        _playback.Stop();
        _playback.Dispose();
        _recorder.Dispose();

        _floating?.CloseForExit();
        _floating = null;
        if (_trayIcon is not null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _trayIcon = null;
        }
        _applicationIcon?.Dispose();
        _applicationIcon = null;
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
