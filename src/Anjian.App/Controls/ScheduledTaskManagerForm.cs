using Anjian.Core.Runtime;
using Anjian.App.Services;

namespace Anjian.App.Controls;

internal sealed class ScheduledTaskManagerForm : Form
{
    private readonly ScheduledTaskService _service;
    private readonly DataGridView _grid = new();
    private readonly ToolStripStatusLabel _status = new();
    private readonly ToolStrip _toolStrip = new();

    public ScheduledTaskManagerForm(ScheduledTaskService service)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        Text = "定时任务";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(860, 420);
        Size = new Size(1080, 560);
        Font = new Font("Microsoft YaHei UI", 9F);

        _toolStrip.GripStyle = ToolStripGripStyle.Hidden;
        AddButton("新建任务", AddTask);
        AddButton("编辑", EditTask);
        AddButton("启用/禁用", ToggleTask);
        AddButton("删除", DeleteTask);

        _grid.Dock = DockStyle.Fill;
        _grid.ReadOnly = true;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.MultiSelect = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.RowHeadersVisible = false;
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name", HeaderText = "任务", FillWeight = 18 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Trigger", HeaderText = "触发规则", FillWeight = 24 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Next", HeaderText = "下次触发（任务时区）", FillWeight = 25 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Options", HeaderText = "重叠 / 超时 / 失败策略", FillWeight = 28 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "State", HeaderText = "状态", FillWeight = 9 });

        var status = new StatusStrip();
        status.Items.Add(_status);
        Controls.Add(_grid);
        Controls.Add(status);
        Controls.Add(_toolStrip);
        _toolStrip.Dock = DockStyle.Top;
        status.Dock = DockStyle.Bottom;

        _service.Changed += ServiceChanged;
        FormClosed += (_, _) => _service.Changed -= ServiceChanged;
        RefreshTasks();
    }

    private void AddButton(string text, Action action)
    {
        var button = new ToolStripButton(text);
        button.Click += (_, _) => action();
        _toolStrip.Items.Add(button);
    }

    private ScheduledTaskSnapshot? SelectedTask =>
        _grid.CurrentRow?.Tag as ScheduledTaskSnapshot;

    private void AddTask()
    {
        using var editor = new ScheduledTaskEditorForm();
        if (editor.ShowDialog(this) != DialogResult.OK)
            return;
        try
        {
            _service.AddOrUpdate(editor.BuildDefinition());
            _status.Text = "任务已保存。";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "保存任务失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void EditTask()
    {
        var selected = SelectedTask;
        if (selected is null)
            return;
        using var editor = new ScheduledTaskEditorForm(selected.Definition);
        if (editor.ShowDialog(this) != DialogResult.OK)
            return;
        try
        {
            _service.AddOrUpdate(editor.BuildDefinition());
            _status.Text = "任务已更新；已被协调器接纳的实例继续使用原启动快照。";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "更新任务失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ToggleTask()
    {
        var selected = SelectedTask;
        if (selected is null)
            return;
        var enabled = !selected.Definition.Enabled;
        if (_service.SetEnabled(selected.Definition.TaskId, enabled))
            _status.Text = enabled ? "任务已启用。" : "任务已禁用；已接纳的实例不会被撤销。";
    }

    private void DeleteTask()
    {
        var selected = SelectedTask;
        if (selected is null)
            return;
        if (MessageBox.Show(this,
            $"删除任务“{selected.Definition.Name}”？已提交给运行协调器的实例会继续执行。",
            "删除定时任务", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;
        _service.Remove(selected.Definition.TaskId);
        _status.Text = "任务已删除。";
    }

    private void ServiceChanged()
    {
        if (IsDisposed)
            return;
        if (InvokeRequired)
        {
            try { BeginInvoke(RefreshTasks); }
            catch (InvalidOperationException) { }
            return;
        }
        RefreshTasks();
    }

    private void RefreshTasks()
    {
        if (IsDisposed)
            return;
        var selectedId = SelectedTask?.Definition.TaskId;
        _grid.Rows.Clear();
        foreach (var item in _service.GetSnapshots())
        {
            var definition = item.Definition;
            var trigger = DescribeTrigger(definition);
            var next = item.Cursor.NextOccurrenceUtc is { } utc
                ? FormatLocal(utc, definition.TimeZoneId)
                : item.Cursor.Exhausted ? "已完成" : "未计划";
            var options = $"{definition.OverlapPolicy} / " +
                $"{(definition.RuntimeTimeout is { } timeout ? timeout.ToString() : "无超时")} / " +
                $"{definition.FailurePolicy}";
            var index = _grid.Rows.Add(definition.Name, trigger, next, options,
                definition.Enabled ? "启用" : "禁用");
            var row = _grid.Rows[index];
            row.Tag = item;
            if (definition.TaskId == selectedId)
                row.Selected = true;
        }

        _status.Text = $"共 {_grid.Rows.Count} 个任务；调度在后台运行，触发时将逐次请求参数和权限审阅。";
    }

    private static string DescribeTrigger(ScheduledTaskDefinition definition)
    {
        return definition.TriggerKind switch
        {
            ScheduledTriggerKind.OnceAfterDelay => $"延迟 {definition.DelayFromCreation}",
            ScheduledTriggerKind.OnceAtUtc => $"一次性：{FormatLocal(definition.OnceAtUtc!.Value, definition.TimeZoneId)} ({definition.TimeZoneId})",
            ScheduledTriggerKind.DailyLocalTime => $"每天 {definition.LocalTime.ToString("HH:mm")} ({definition.TimeZoneId})",
            ScheduledTriggerKind.WeeklyLocalTime =>
                $"每周 {string.Join("、", definition.DaysOfWeek.Order())} {definition.LocalTime.ToString("HH:mm")} ({definition.TimeZoneId})",
            _ => definition.TriggerKind.ToString()
        };
    }

    private static string FormatLocal(DateTimeOffset utc, string timeZoneId)
    {
        try
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return TimeZoneInfo.ConvertTime(utc, zone).ToString("yyyy-MM-dd HH:mm:ss");
        }
        catch
        {
            return utc.ToString("yyyy-MM-dd HH:mm:ss 'UTC'");
        }
    }
}

internal sealed class ScheduledTaskEditorForm : Form
{
    private sealed record Choice<T>(T Value, string Text)
    {
        public override string ToString() => Text;
    }

    private readonly ScheduledTaskDefinition? _original;
    private readonly TextBox _name = new();
    private readonly TextBox _scriptPath = new();
    private readonly ComboBox _trigger = new();
    private readonly DateTimePicker _dateTime = new();
    private readonly NumericUpDown _delaySeconds = new();
    private readonly TextBox _timeZone = new();
    private readonly CheckedListBox _weekdays = new();
    private readonly ComboBox _misfire = new();
    private readonly NumericUpDown _misfireGrace = new();
    private readonly ComboBox _overlap = new();
    private readonly NumericUpDown _timeoutMinutes = new();
    private readonly ComboBox _failure = new();
    private readonly NumericUpDown _maxRetries = new();
    private readonly NumericUpDown _retryDelaySeconds = new();
    private readonly NumericUpDown _backoff = new();
    private readonly NumericUpDown _maxConsecutiveFailures = new();

    public ScheduledTaskEditorForm(ScheduledTaskDefinition? original = null)
    {
        _original = original;
        Text = original is null ? "新建定时任务" : "编辑定时任务";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(700, 620);
        Size = new Size(820, 760);
        Font = new Font("Microsoft YaHei UI", 9F);

        _name.Text = original?.Name ?? "自动化任务";
        _scriptPath.Text = original?.ScriptPath ?? "";
        _timeZone.Text = original?.TimeZoneId ?? TimeZoneInfo.Local.Id;

        AddChoices<ScheduledTriggerKind>(_trigger,
            new(ScheduledTriggerKind.OnceAtUtc, "指定日期和时间"),
            new(ScheduledTriggerKind.OnceAfterDelay, "延迟启动"),
            new(ScheduledTriggerKind.DailyLocalTime, "每天"),
            new(ScheduledTriggerKind.WeeklyLocalTime, "每周"));
        AddChoices<ScheduledMisfirePolicy>(_misfire,
            new(ScheduledMisfirePolicy.Skip, "错过则跳过"),
            new(ScheduledMisfirePolicy.RunOnce, "宽限期内补跑一次"));
        AddChoices<ScheduledOverlapPolicy>(_overlap,
            new(ScheduledOverlapPolicy.Skip, "上次未结束则跳过"),
            new(ScheduledOverlapPolicy.QueueOne, "最多排队一个触发"),
            new(ScheduledOverlapPolicy.Parallel, "允许并行（受全局 4+16 限额约束）"));
        AddChoices<ScheduledFailurePolicy>(_failure,
            new(ScheduledFailurePolicy.Stop, "失败后禁用任务"),
            new(ScheduledFailurePolicy.Continue, "记录失败并继续后续计划"),
            new(ScheduledFailurePolicy.Retry, "有限重试，之后继续计划"));

        _dateTime.Format = DateTimePickerFormat.Custom;
        _dateTime.CustomFormat = "yyyy-MM-dd HH:mm:ss";
        _dateTime.Width = 220;
        _dateTime.Value = original?.OnceAtUtc is { } due
            ? TimeZoneInfo.ConvertTime(due, TimeZoneInfo.FindSystemTimeZoneById(_timeZone.Text)).DateTime
            : original is null
                ? DateTime.Now.AddMinutes(5)
                : DateTime.Today.Add(original.LocalTime.ToTimeSpan());

        ConfigureNumber(_delaySeconds, 1, 2_592_000, (decimal)(original?.DelayFromCreation.TotalSeconds ?? 60));
        ConfigureNumber(_misfireGrace, 0, 43_200, (decimal)(original?.MisfireGracePeriod.TotalMinutes ?? 1440));
        ConfigureNumber(_timeoutMinutes, 0, 10_080, (decimal)(original?.RuntimeTimeout?.TotalMinutes ?? 0));
        ConfigureNumber(_maxRetries, 0, 10, original?.MaximumRetries ?? 0);
        ConfigureNumber(_retryDelaySeconds, 1, 3600, (decimal)(original?.RetryDelay.TotalSeconds ?? 10));
        ConfigureNumber(_backoff, 1, 10, (decimal)(original?.RetryBackoffMultiplier ?? 2), 0.25m, 2);
        ConfigureNumber(_maxConsecutiveFailures, 1, 1000, original?.MaximumConsecutiveFailures ?? 5);

        _weekdays.CheckOnClick = true;
        _weekdays.Height = 74;
        foreach (var day in Enum.GetValues<DayOfWeek>())
            _weekdays.Items.Add(day, original?.DaysOfWeek.Contains(day) ??
                (day is not DayOfWeek.Saturday and not DayOfWeek.Sunday));

        _trigger.SelectedValueChanged += (_, _) => UpdateTriggerControls();
        _failure.SelectedValueChanged += (_, _) => UpdateFailureControls();
        _trigger.SelectedItem = original?.TriggerKind switch
        {
            ScheduledTriggerKind.OnceAfterDelay => _trigger.Items.Cast<Choice<ScheduledTriggerKind>>().First(x => x.Value == ScheduledTriggerKind.OnceAfterDelay),
            ScheduledTriggerKind.DailyLocalTime => _trigger.Items.Cast<Choice<ScheduledTriggerKind>>().First(x => x.Value == ScheduledTriggerKind.DailyLocalTime),
            ScheduledTriggerKind.WeeklyLocalTime => _trigger.Items.Cast<Choice<ScheduledTriggerKind>>().First(x => x.Value == ScheduledTriggerKind.WeeklyLocalTime),
            _ => _trigger.Items.Cast<Choice<ScheduledTriggerKind>>().First(x => x.Value == ScheduledTriggerKind.OnceAtUtc)
        };
        _misfire.SelectedItem = _misfire.Items.Cast<Choice<ScheduledMisfirePolicy>>().First(x => x.Value == (original?.MisfirePolicy ?? ScheduledMisfirePolicy.Skip));
        _overlap.SelectedItem = _overlap.Items.Cast<Choice<ScheduledOverlapPolicy>>().First(x => x.Value == (original?.OverlapPolicy ?? ScheduledOverlapPolicy.Skip));
        _failure.SelectedItem = _failure.Items.Cast<Choice<ScheduledFailurePolicy>>().First(x => x.Value == (original?.FailurePolicy ?? ScheduledFailurePolicy.Stop));

        var browse = new Button { Text = "浏览…", AutoSize = true };
        browse.Click += (_, _) => BrowseScript();
        var pathRow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false };
        _scriptPath.Width = 500;
        pathRow.Controls.Add(_scriptPath);
        pathRow.Controls.Add(browse);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            ColumnCount = 2,
            Padding = new Padding(12),
            AutoSize = false
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 205));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddRow(layout, "任务名称", _name);
        AddRow(layout, "Lua 脚本路径", pathRow);
        AddRow(layout, "触发方式", _trigger);
        AddRow(layout, "本地触发日期/时间", _dateTime);
        AddRow(layout, "延迟秒数", _delaySeconds);
        AddRow(layout, "时区 ID（Windows）", _timeZone);
        AddRow(layout, "每周日期", _weekdays);
        AddRow(layout, "错过触发", _misfire);
        AddRow(layout, "错过宽限期（分钟）", _misfireGrace);
        AddRow(layout, "重叠策略", _overlap);
        AddRow(layout, "运行超时（分钟，0=关闭）", _timeoutMinutes);
        AddRow(layout, "失败策略", _failure);
        AddRow(layout, "最多重试次数", _maxRetries);
        AddRow(layout, "重试基础延迟（秒）", _retryDelaySeconds);
        AddRow(layout, "重试退避倍数", _backoff);
        AddRow(layout, "连续失败上限", _maxConsecutiveFailures);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 48,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(8)
        };
        var save = new Button { Text = "保存任务", DialogResult = DialogResult.OK, AutoSize = true };
        var cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, AutoSize = true };
        save.DialogResult = DialogResult.None;
        save.Click += (_, _) =>
        {
            try
            {
                _ = BuildDefinition();
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "任务设置无效", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        };
        buttons.Controls.Add(save);
        buttons.Controls.Add(cancel);
        Controls.Add(layout);
        Controls.Add(buttons);
        AcceptButton = save;
        CancelButton = cancel;
        UpdateTriggerControls();
        UpdateFailureControls();
    }

    public ScheduledTaskDefinition BuildDefinition()
    {
        if (_trigger.SelectedItem is not Choice<ScheduledTriggerKind> triggerChoice ||
            _misfire.SelectedItem is not Choice<ScheduledMisfirePolicy> misfireChoice ||
            _overlap.SelectedItem is not Choice<ScheduledOverlapPolicy> overlapChoice ||
            _failure.SelectedItem is not Choice<ScheduledFailurePolicy> failureChoice)
            throw new InvalidOperationException("请选择完整的任务策略。");

        var zoneId = _timeZone.Text.Trim();
        var zone = TimeZoneInfo.FindSystemTimeZoneById(zoneId);
        var trigger = triggerChoice.Value;
        var localDateTime = DateTime.SpecifyKind(_dateTime.Value, DateTimeKind.Unspecified);
        var days = _weekdays.CheckedItems.Cast<DayOfWeek>().ToArray();
        var now = DateTimeOffset.UtcNow;
        var delayFromCreation = TimeSpan.FromSeconds((double)_delaySeconds.Value);

        var scriptPath = _scriptPath.Text.Trim();
        if (string.IsNullOrWhiteSpace(scriptPath) || !Path.IsPathFullyQualified(scriptPath))
            throw new ArgumentException("请选择一个有效的 Lua 脚本文件。");
        if (!File.Exists(scriptPath))
            throw new FileNotFoundException("指定的 Lua 脚本文件不存在。", scriptPath);

        var createdAtUtc = trigger == ScheduledTriggerKind.OnceAfterDelay &&
            _original?.TriggerKind == ScheduledTriggerKind.OnceAfterDelay &&
            _original.DelayFromCreation == delayFromCreation
                ? _original.CreatedAtUtc
                : now;

        var definition = new ScheduledTaskDefinition
        {
            TaskId = _original?.TaskId ?? Guid.NewGuid().ToString("N"),
            Name = _name.Text.Trim(),
            ScriptPath = Path.GetFullPath(scriptPath),
            Enabled = _original?.Enabled ?? true,
            TriggerKind = trigger,
            CreatedAtUtc = createdAtUtc,
            DelayFromCreation = delayFromCreation,
            OnceAtUtc = trigger == ScheduledTriggerKind.OnceAtUtc
                ? ScheduledTaskCalculator.ResolveLocalTime(localDateTime, zone)
                : null,
            LocalTime = TimeOnly.FromDateTime(localDateTime),
            TimeZoneId = zoneId,
            DaysOfWeek = trigger == ScheduledTriggerKind.WeeklyLocalTime ? days : [],
            MisfirePolicy = misfireChoice.Value,
            MisfireGracePeriod = TimeSpan.FromMinutes((double)_misfireGrace.Value),
            OverlapPolicy = overlapChoice.Value,
            RuntimeTimeout = _timeoutMinutes.Value > 0
                ? TimeSpan.FromMinutes((double)_timeoutMinutes.Value)
                : null,
            FailurePolicy = failureChoice.Value,
            MaximumRetries = (int)_maxRetries.Value,
            RetryDelay = TimeSpan.FromSeconds((double)_retryDelaySeconds.Value),
            RetryBackoffMultiplier = (double)_backoff.Value,
            MaximumConsecutiveFailures = (int)_maxConsecutiveFailures.Value
        };
        definition.Validate();
        return definition;
    }

    private void BrowseScript()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Lua 脚本 (*.lua)|*.lua|所有文件 (*.*)|*.*",
            CheckFileExists = true
        };
        if (File.Exists(_scriptPath.Text))
            dialog.FileName = _scriptPath.Text;
        if (dialog.ShowDialog(this) == DialogResult.OK)
            _scriptPath.Text = dialog.FileName;
    }

    private void UpdateTriggerControls()
    {
        var trigger = (_trigger.SelectedItem as Choice<ScheduledTriggerKind>)?.Value;
        _dateTime.Enabled = trigger is ScheduledTriggerKind.OnceAtUtc or
            ScheduledTriggerKind.DailyLocalTime or ScheduledTriggerKind.WeeklyLocalTime;
        _delaySeconds.Enabled = trigger == ScheduledTriggerKind.OnceAfterDelay;
        _weekdays.Enabled = trigger == ScheduledTriggerKind.WeeklyLocalTime;
    }

    private void UpdateFailureControls()
    {
        var failure = (_failure.SelectedItem as Choice<ScheduledFailurePolicy>)?.Value;
        _maxRetries.Enabled = failure == ScheduledFailurePolicy.Retry;
        _retryDelaySeconds.Enabled = failure == ScheduledFailurePolicy.Retry;
        _backoff.Enabled = failure == ScheduledFailurePolicy.Retry;
    }

    private static void ConfigureNumber(
        NumericUpDown number,
        decimal minimum,
        decimal maximum,
        decimal value,
        decimal increment = 1,
        int decimals = 0)
    {
        number.Minimum = minimum;
        number.Maximum = maximum;
        number.Increment = increment;
        number.DecimalPlaces = decimals;
        number.Value = Math.Clamp(value, minimum, maximum);
        number.Width = 180;
    }

    private static void AddChoices<T>(ComboBox combo, params Choice<T>[] choices)
    {
        combo.DropDownStyle = ComboBoxStyle.DropDownList;
        combo.Width = 480;
        combo.Items.AddRange(choices);
    }

    private static void AddRow(TableLayoutPanel layout, string label, Control control)
    {
        var row = layout.RowCount++;
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var title = new Label
        {
            Text = label,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(4, 7, 8, 7)
        };
        control.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        control.Margin = new Padding(4, 4, 4, 4);
        layout.Controls.Add(title, 0, row);
        layout.Controls.Add(control, 1, row);
    }
}
