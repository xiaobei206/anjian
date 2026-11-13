using System.Globalization;
using System.Text;
using System.Text.Json;
using Anjian.App.Services;
using Anjian.Core.Runtime;

namespace Anjian.App.Controls;

internal sealed class RunInstanceManagerForm : Form
{
    private readonly RunInstanceRuntimeCoordinator _coordinator;
    private readonly TextBox _scriptFilter = new() { Width = 240, PlaceholderText = "按脚本名或完整路径筛选" };
    private readonly ComboBox _stateFilter = new() { Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly DataGridView _instances = new();
    private readonly TextBox _logFilter = new() { Width = 220, PlaceholderText = "筛选事件码或日志文本" };
    private readonly ComboBox _levelFilter = new() { Width = 120, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly CheckBox _onlySelected = new() { Text = "仅所选实例日志", Checked = true, AutoSize = true };
    private readonly RichTextBox _logs = new();
    private readonly Label _inputFaultLabel = new() { AutoSize = true, Text = "全局键鼠仲裁：正常" };
    private readonly Button _recoverButton = new() { Text = "人工核验后恢复", AutoSize = true, Visible = false };
    private readonly Label _instanceSummary = new() { AutoSize = true };
    private readonly System.Windows.Forms.Timer _refreshTimer = new() { Interval = 750 };
    private IReadOnlyList<RunInstanceRuntimeStatus> _lastStatuses = [];
    private bool _refreshing;

    public RunInstanceManagerForm(RunInstanceRuntimeCoordinator coordinator)
    {
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        Text = "运行实例与日志";
        MinimumSize = new Size(1100, 620);
        Size = new Size(1500, 820);
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Microsoft YaHei UI", 9F);
        BuildUi();

        _scriptFilter.TextChanged += (_, _) => RefreshFromCoordinator();
        _stateFilter.SelectedIndexChanged += (_, _) => RefreshFromCoordinator();
        _logFilter.TextChanged += (_, _) => RefreshLogs();
        _levelFilter.SelectedIndexChanged += (_, _) => RefreshLogs();
        _onlySelected.CheckedChanged += (_, _) => RefreshLogs();
        _instances.SelectionChanged += (_, _) =>
        {
            if (!_refreshing)
            {
                UpdateActionButtons();
                RefreshLogs();
            }
        };
        _recoverButton.Click += (_, _) => RecoverInputAfterManualCheck();
        _refreshTimer.Tick += (_, _) =>
        {
            if (Visible && !IsDisposed)
                RefreshFromCoordinator();
        };
        Shown += (_, _) =>
        {
            _refreshTimer.Start();
            RefreshFromCoordinator();
        };
        FormClosed += (_, _) =>
        {
            _refreshTimer.Stop();
            _refreshTimer.Dispose();
        };
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(8)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var filters = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight,
            AutoScroll = true,
            Padding = new Padding(0, 3, 0, 0)
        };
        filters.Controls.Add(new Label { Text = "脚本", AutoSize = true, Margin = new Padding(4, 7, 2, 0) });
        filters.Controls.Add(_scriptFilter);
        filters.Controls.Add(new Label { Text = "状态", AutoSize = true, Margin = new Padding(8, 7, 2, 0) });
        _stateFilter.Items.AddRange(new object[]
        {
            "全部状态", "排队", "启动中", "运行中", "已暂停", "停止中", "清理中",
            "成功", "失败", "已取消", "已超时", "清理失败"
        });
        _stateFilter.SelectedIndex = 0;
        filters.Controls.Add(_stateFilter);
        filters.Controls.Add(MakeButton("刷新", (_, _) => RefreshFromCoordinator()));
        filters.Controls.Add(_instanceSummary);
        root.Controls.Add(filters, 0, 0);

        var faultPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(0, 4, 0, 0)
        };
        faultPanel.Controls.Add(_inputFaultLabel);
        faultPanel.Controls.Add(_recoverButton);
        root.Controls.Add(faultPanel, 0, 1);

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterDistance = 330,
            Panel1MinSize = 180,
            Panel2MinSize = 150
        };
        ConfigureGrid();
        split.Panel1.Controls.Add(_instances);
        split.Panel2.Controls.Add(BuildLogPanel());
        root.Controls.Add(split, 0, 2);
        Controls.Add(root);
    }

    private void ConfigureGrid()
    {
        _instances.Dock = DockStyle.Fill;
        _instances.ReadOnly = true;
        _instances.AllowUserToAddRows = false;
        _instances.AllowUserToDeleteRows = false;
        _instances.AllowUserToOrderColumns = true;
        _instances.AllowUserToResizeRows = false;
        _instances.MultiSelect = false;
        _instances.RowHeadersVisible = false;
        _instances.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _instances.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
        _instances.ScrollBars = ScrollBars.Both;
        _instances.Columns.Add("InstanceId", "实例 ID");
        _instances.Columns.Add("ScriptName", "脚本名");
        _instances.Columns.Add("ScriptPath", "脚本路径");
        _instances.Columns.Add("CreatedUtc", "创建时间 UTC");
        _instances.Columns.Add("State", "状态");
        _instances.Columns.Add("Outcome", "结果");
        _instances.Columns.Add("Iteration", "循环");
        _instances.Columns.Add("Attempt", "Worker attempt");
        _instances.Columns.Add("Pid", "PID");
        _instances.Columns.Add("ExitCode", "退出码");
        _instances.Columns.Add("Termination", "终止方式");
        _instances.Columns.Add("Cleanup", "清理结果");
        _instances.Columns.Add("Timeout", "运行超时");
        _instances.Columns.Add("Diagnostic", "异常/清理诊断");
        _instances.Columns["InstanceId"]!.Width = 255;
        _instances.Columns["ScriptName"]!.Width = 150;
        _instances.Columns["ScriptPath"]!.Width = 260;
        _instances.Columns["CreatedUtc"]!.Width = 160;
        _instances.Columns["State"]!.Width = 90;
        _instances.Columns["Outcome"]!.Width = 90;
        _instances.Columns["Iteration"]!.Width = 72;
        _instances.Columns["Attempt"]!.Width = 255;
        _instances.Columns["Pid"]!.Width = 72;
        _instances.Columns["ExitCode"]!.Width = 72;
        _instances.Columns["Termination"]!.Width = 85;
        _instances.Columns["Cleanup"]!.Width = 90;
        _instances.Columns["Timeout"]!.Width = 90;
        _instances.Columns["Diagnostic"]!.Width = 360;
    }

    private Control BuildLogPanel()
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            WrapContents = false,
            AutoScroll = true,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(0, 4, 0, 0)
        };
        toolbar.Controls.Add(new Label { Text = "日志", AutoSize = true, Margin = new Padding(3, 7, 2, 0) });
        toolbar.Controls.Add(_logFilter);
        _levelFilter.Items.AddRange(new object[] { "全部级别", "信息", "警告", "错误" });
        _levelFilter.SelectedIndex = 0;
        toolbar.Controls.Add(_levelFilter);
        toolbar.Controls.Add(_onlySelected);
        toolbar.Controls.Add(MakeButton("复制当前日志", (_, _) => CopyVisibleLogs()));
        toolbar.Controls.Add(MakeButton("导出安全日志", (_, _) => ExportLogs()));
        BuildActionsIntoToolbar(toolbar);
        panel.Controls.Add(toolbar, 0, 0);
        _logs.Dock = DockStyle.Fill;
        _logs.ReadOnly = true;
        _logs.WordWrap = false;
        _logs.Font = new Font("Consolas", 9F);
        _logs.BackColor = Color.FromArgb(247, 248, 250);
        panel.Controls.Add(_logs, 0, 1);
        return panel;
    }

    public void RefreshFromCoordinator()
    {
        if (IsDisposed)
            return;
        if (InvokeRequired)
        {
            try { BeginInvoke(RefreshFromCoordinator); } catch (InvalidOperationException) { }
            return;
        }

        var preserve = GetSelectedInstanceId();
        _lastStatuses = _coordinator.GetInstanceStatuses();
        var scriptText = _scriptFilter.Text.Trim();
        var stateText = _stateFilter.SelectedItem as string ?? "全部状态";
        var filteredDescriptors = RunInstanceListQuery.Filter(
            _coordinator.GetInstances(),
            scriptText,
            snapshot => MatchesSnapshotFilter(snapshot, stateText));
        var visibleIds = filteredDescriptors.Select(item => item.InstanceId).ToHashSet();
        var statusesById = _lastStatuses.ToDictionary(status => status.InstanceId);
        var filtered = filteredDescriptors
            .Where(descriptor => statusesById.ContainsKey(descriptor.InstanceId))
            .Select(descriptor => statusesById[descriptor.InstanceId])
            .Where(status => visibleIds.Contains(status.InstanceId))
            .OrderByDescending(status => status.CreatedAtUtc)
            .ToArray();

        _refreshing = true;
        try
        {
            _instances.Rows.Clear();
            foreach (var status in filtered)
            {
                var snapshot = status.Snapshot;
                var index = _instances.Rows.Add(
                    status.InstanceId.Value,
                    Path.GetFileName(status.ScriptPath),
                    status.ScriptPath,
                    FormatUtc(status.CreatedAtUtc),
                    StateLabel(snapshot),
                    OutcomeLabel(snapshot.Outcome),
                    status.TotalIterations == 0
                        ? $"{status.CurrentIteration}/∞"
                        : status.TotalIterations > 0
                            ? $"{status.CurrentIteration}/{status.TotalIterations}"
                            : "—",
                    status.WorkerAttemptId ?? "—",
                    status.WorkerProcessId?.ToString(CultureInfo.InvariantCulture) ?? "—",
                    status.WorkerExitCode?.ToString(CultureInfo.InvariantCulture) ?? "—",
                    TerminationLabel(snapshot.TerminationMode),
                    CleanupLabel(snapshot.CleanupStatus),
                    status.RuntimeTimeout is null ? "关闭" : status.RuntimeTimeout.Value.ToString(),
                    status.LastDiagnostic ?? "");
                _instances.Rows[index].Tag = status.InstanceId;
                _instances.Rows[index].ToolTipText = status.LastDiagnostic ?? "";
                if (preserve == status.InstanceId)
                    _instances.Rows[index].Selected = true;
            }

            if (_instances.SelectedRows.Count == 0 && _instances.Rows.Count > 0)
                _instances.Rows[0].Selected = true;
            _instanceSummary.Text =
                $"显示 {filtered.Length}/{_lastStatuses.Count} 个实例；活动 {_coordinator.ActiveCount}，排队 {_coordinator.QueuedCount}";
        }
        finally
        {
            _refreshing = false;
        }

        _inputFaultLabel.Text = _coordinator.InputFaulted
            ? "全局键鼠仲裁：故障锁定 — " + (_coordinator.InputFaultReason ?? "状态未知")
            : "全局键鼠仲裁：正常";
        _inputFaultLabel.ForeColor = _coordinator.InputFaulted ? Color.DarkRed : SystemColors.ControlText;
        _recoverButton.Visible = _coordinator.InputFaulted;
        _recoverButton.Enabled = _coordinator.InputFaulted;
        UpdateActionButtons();
        RefreshLogs();
    }

    private void RefreshLogs()
    {
        if (IsDisposed || _logs.IsDisposed)
            return;
        if (InvokeRequired)
        {
            try { BeginInvoke(RefreshLogs); } catch (InvalidOperationException) { }
            return;
        }

        var selectedId = GetSelectedInstanceId();
        var onlySelected = _onlySelected.Checked;
        if (onlySelected && selectedId is null)
        {
            _logs.Clear();
            return;
        }

        RunInstanceLogLevel? level = _levelFilter.SelectedIndex switch
        {
            1 => RunInstanceLogLevel.Information,
            2 => RunInstanceLogLevel.Warning,
            3 => RunInstanceLogLevel.Error,
            _ => null
        };
        var entries = _coordinator.GetLogs(
            onlySelected ? selectedId : null,
            level,
            _logFilter.Text,
            limit: 2_000,
            includeGlobal: !onlySelected);

        var output = new StringBuilder(Math.Min(entries.Count * 160, 256_000));
        foreach (var entry in entries)
        {
            var id = entry.InstanceId?.Value ?? "GLOBAL";
            var attempt = entry.WorkerAttemptId ?? "—";
            var iteration = entry.Iteration?.ToString(CultureInfo.InvariantCulture) ?? "—";
            output.Append(FormatUtc(entry.TimestampUtc)).Append(" | ")
                .Append(LevelLabel(entry.Level)).Append(" | ")
                .Append(entry.EventCode).Append(" | ")
                .Append(id).Append(" | attempt=").Append(attempt)
                .Append(" | iteration=").Append(iteration).Append(" | ")
                .AppendLine(entry.Message);
        }

        _logs.Text = output.ToString();
        _logs.SelectionStart = _logs.TextLength;
        _logs.ScrollToCaret();
        UpdateActionButtons();
    }

    private void UpdateActionButtons()
    {
        var selectedId = GetSelectedInstanceId();
        var status = selectedId is null
            ? null
            : _lastStatuses.FirstOrDefault(candidate => candidate.InstanceId == selectedId);
        var snapshot = status?.Snapshot;
        var canOperate = snapshot is not null && snapshot.State != RunInstanceState.Cleaned;
        _pauseButton.Enabled = canOperate && (snapshot!.State is RunInstanceState.Running or RunInstanceState.Paused);
        _pauseButton.Text = snapshot?.State == RunInstanceState.Paused ? "继续" : "暂停";
        _stopButton.Enabled = canOperate;
    }

    private readonly Button _pauseButton = new() { Text = "暂停", AutoSize = true };
    private readonly Button _stopButton = new() { Text = "停止所选实例", AutoSize = true };

    private void BuildActionsIntoToolbar(FlowLayoutPanel toolbar)
    {
        toolbar.Controls.Add(_pauseButton);
        toolbar.Controls.Add(_stopButton);
        _pauseButton.Click += (_, _) =>
        {
            var id = GetSelectedInstanceId();
            if (id is null)
                return;
            var status = _lastStatuses.FirstOrDefault(candidate => candidate.InstanceId == id);
            if (status?.Snapshot.State == RunInstanceState.Paused)
                _coordinator.ResumeInstance(id);
            else
                _coordinator.PauseInstance(id);
            RefreshFromCoordinator();
        };
        _stopButton.Click += async (_, _) =>
        {
            var id = GetSelectedInstanceId();
            if (id is null)
                return;
            _stopButton.Enabled = false;
            try { await _coordinator.StopInstanceAsync(id); }
            catch (Exception ex)
            {
                MessageBox.Show(this, "停止所选实例失败：" + ex.Message, "运行实例",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            RefreshFromCoordinator();
        };
    }

    private static bool MatchesSnapshotFilter(RunInstanceSnapshot snapshot, string filter)
    {
        if (filter == "全部状态")
            return true;
        return filter switch
        {
            "排队" => snapshot.State == RunInstanceState.Queued,
            "启动中" => snapshot.State == RunInstanceState.Starting,
            "运行中" => snapshot.State == RunInstanceState.Running,
            "已暂停" => snapshot.State == RunInstanceState.Paused,
            "停止中" => snapshot.State is RunInstanceState.StopRequested or RunInstanceState.TimeoutRequested or RunInstanceState.Cancelling,
            "清理中" => snapshot.State is RunInstanceState.CleaningUp or RunInstanceState.ForceTerminating,
            "成功" => snapshot.State == RunInstanceState.Cleaned && snapshot.Outcome == RunInstanceOutcome.Succeeded,
            "失败" => snapshot.State == RunInstanceState.Cleaned && snapshot.Outcome == RunInstanceOutcome.Failed,
            "已取消" => snapshot.State == RunInstanceState.Cleaned && snapshot.Outcome == RunInstanceOutcome.Cancelled,
            "已超时" => snapshot.State == RunInstanceState.Cleaned && snapshot.Outcome == RunInstanceOutcome.TimedOut,
            "清理失败" => snapshot.CleanupStatus == RunInstanceCleanupStatus.Failed,
            _ => true
        };
    }

    private void CopyVisibleLogs()
    {
        try
        {
            Clipboard.SetText(_logs.Text);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "复制日志失败：" + ex.Message, "运行日志",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void ExportLogs()
    {
        var selectedId = GetSelectedInstanceId();
        if (_onlySelected.Checked && selectedId is null)
        {
            MessageBox.Show(this, "请先选择运行实例，或取消“仅所选实例日志”。",
                "导出安全日志", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Title = "导出已脱敏的运行日志",
            Filter = "JSON Lines (*.jsonl)|*.jsonl|JSON (*.json)|*.json",
            DefaultExt = "jsonl",
            AddExtension = true,
            FileName = "Anjian2026-run-logs.jsonl"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        RunInstanceLogLevel? level = _levelFilter.SelectedIndex switch
        {
            1 => RunInstanceLogLevel.Information,
            2 => RunInstanceLogLevel.Warning,
            3 => RunInstanceLogLevel.Error,
            _ => null
        };
        var entries = _coordinator.GetLogs(
            _onlySelected.Checked ? selectedId : null,
            level,
            _logFilter.Text,
            limit: RunInstanceLogStore.DefaultCapacity,
            includeGlobal: !_onlySelected.Checked);

        try
        {
            if (dialog.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
            else
                File.WriteAllLines(dialog.FileName, entries.Select(entry => JsonSerializer.Serialize(entry)), new UTF8Encoding(false));
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "导出安全日志失败：" + ex.Message, "运行日志",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void RecoverInputAfterManualCheck()
    {
        if (!_coordinator.InputFaulted)
            return;
        var reason = _coordinator.InputFaultReason ?? "输入状态未知。";
        var answer = MessageBox.Show(
            this,
            "全局键鼠输入仲裁已锁定。请先人工检查桌面，确认所有键盘按键和鼠标按钮均已释放，必要时手动释放。\n\n" +
            "故障原因：" + reason + "\n\n" +
            "确认解除的只会是全局输入锁定。任何实例已有的 CleanupStatus=Failed 记录不会被改写为成功。",
            "人工核验键鼠状态",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);
        if (answer != DialogResult.Yes)
            return;
        if (!_coordinator.TryRecoverInputAfterManualAcknowledgement())
        {
            MessageBox.Show(this, "仍有输入租约或等待者；目前无法安全解除全局锁定。",
                "无法恢复", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        RefreshFromCoordinator();
    }

    private RunInstanceId? GetSelectedInstanceId() =>
        _instances.SelectedRows.Count > 0
            ? _instances.SelectedRows[0].Tag as RunInstanceId
            : null;

    private static string FormatUtc(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

    private static string LevelLabel(RunInstanceLogLevel value) => value switch
    {
        RunInstanceLogLevel.Information => "信息",
        RunInstanceLogLevel.Warning => "警告",
        RunInstanceLogLevel.Error => "错误",
        _ => value.ToString()
    };

    private static string StateLabel(RunInstanceSnapshot snapshot)
    {
        if (snapshot.CleanupStatus == RunInstanceCleanupStatus.Failed)
            return "清理失败";
        return snapshot.State switch
        {
            RunInstanceState.Created => "已创建",
            RunInstanceState.Validating => "验证中",
            RunInstanceState.Queued => "排队",
            RunInstanceState.Starting => "启动中",
            RunInstanceState.Running => "运行中",
            RunInstanceState.Paused => "已暂停",
            RunInstanceState.StopRequested or RunInstanceState.TimeoutRequested or RunInstanceState.Cancelling => "停止中",
            RunInstanceState.ForceTerminating => "强制终止",
            RunInstanceState.CleaningUp => "清理中",
            RunInstanceState.Cleaned => "已清理",
            _ => snapshot.State.ToString()
        };
    }

    private static string OutcomeLabel(RunInstanceOutcome value) => value switch
    {
        RunInstanceOutcome.None => "—",
        RunInstanceOutcome.Succeeded => "成功",
        RunInstanceOutcome.Failed => "失败",
        RunInstanceOutcome.Cancelled => "已取消",
        RunInstanceOutcome.TimedOut => "已超时",
        _ => value.ToString()
    };

    private static string TerminationLabel(RunInstanceTerminationMode value) => value switch
    {
        RunInstanceTerminationMode.None => "无",
        RunInstanceTerminationMode.Graceful => "正常取消",
        RunInstanceTerminationMode.Forced => "强制",
        _ => value.ToString()
    };

    private static string CleanupLabel(RunInstanceCleanupStatus value) => value switch
    {
        RunInstanceCleanupStatus.NotStarted => "未开始",
        RunInstanceCleanupStatus.InProgress => "进行中",
        RunInstanceCleanupStatus.Succeeded => "成功",
        RunInstanceCleanupStatus.Failed => "失败",
        _ => value.ToString()
    };

    private static Button MakeButton(string text, EventHandler handler)
    {
        var button = new Button { Text = text, AutoSize = true, Margin = new Padding(3, 2, 3, 2) };
        button.Click += handler;
        return button;
    }
}
