using System.Data;
using Anjian.Core.Recorder;

namespace Anjian.App.Controls;

internal sealed class RecorderActionEditorForm : Form
{
    private readonly DataGridView _grid = new();
    private readonly ComboBox _insertKind = new();
    private readonly NumericUpDown _batchDelay = new();
    private readonly Label _syncState = new();
    private readonly Func<RecorderAction, Task> _runSingleAction;
    private readonly List<RecorderAction> _actions;
    private RecorderDocument _document;
    private string _scriptText;

    public RecorderDocument UpdatedDocument { get; private set; }
    public string UpdatedScriptText { get; private set; }
    public string? RequestedNavigationActionId { get; private set; }

    public RecorderActionEditorForm(
        RecorderDocument document,
        string scriptText,
        Func<RecorderAction, Task> runSingleAction)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(scriptText);
        ArgumentNullException.ThrowIfNull(runSingleAction);
        document.Validate();

        _document = document;
        _actions = document.Actions.ToList();
        _scriptText = scriptText;
        _runSingleAction = runSingleAction;
        UpdatedDocument = document;
        UpdatedScriptText = scriptText;

        Text = "可视化录制动作列表";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(1040, 620);
        Size = new Size(1320, 780);
        Font = new Font("Microsoft YaHei UI", 9F);
        BuildUi();
        RefreshRows();
    }

    private void BuildUi()
    {
        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 44,
            Padding = new Padding(8, 6, 8, 4),
            WrapContents = false,
            AutoScroll = true
        };

        foreach (var kind in Enum.GetValues<RecorderActionKind>())
            _insertKind.Items.Add(new ActionKindItem(kind, KindLabel(kind)));
        _insertKind.DisplayMember = nameof(ActionKindItem.Label);
        _insertKind.ValueMember = nameof(ActionKindItem.Value);
        _insertKind.SelectedIndex = 0;
        _insertKind.Width = 130;
        toolbar.Controls.Add(_insertKind);
        toolbar.Controls.Add(MakeButton("插入", (_, _) => InsertAction()));
        toolbar.Controls.Add(MakeButton("删除", (_, _) => DeleteSelected()));
        toolbar.Controls.Add(MakeButton("复制", (_, _) => DuplicateSelected()));
        toolbar.Controls.Add(MakeButton("上移", (_, _) => MoveSelected(-1)));
        toolbar.Controls.Add(MakeButton("下移", (_, _) => MoveSelected(1)));
        toolbar.Controls.Add(new Label { Text = "批量延时(ms)", AutoSize = true, Margin = new Padding(8, 8, 2, 0) });
        _batchDelay.Minimum = 0;
        _batchDelay.Maximum = 3_600_000;
        _batchDelay.Increment = 100;
        _batchDelay.Width = 92;
        toolbar.Controls.Add(_batchDelay);
        toolbar.Controls.Add(MakeButton("应用到所选", (_, _) => ApplyBatchDelay()));
        toolbar.Controls.Add(MakeButton("步骤试运行", async (_, _) => await RunSelectedAsync()));
        toolbar.Controls.Add(MakeButton("生成/同步 Lua", (_, _) => GenerateLuaBlock()));
        toolbar.Controls.Add(MakeButton("定位代码", (_, _) => NavigateSelectedCode()));
        _syncState.AutoSize = true;
        _syncState.Margin = new Padding(10, 8, 4, 0);
        toolbar.Controls.Add(_syncState);

        ConfigureGrid();
        var footer = new Panel { Dock = DockStyle.Bottom, Height = 52, Padding = new Padding(8) };
        var help = new Label
        {
            Dock = DockStyle.Fill,
            Text = "双击单元格编辑参数、延时、分组与注释。步骤试运行会真实控制键鼠；执行前需要确认。生成区块外的 Lua 代码不会被改动。",
            ForeColor = Color.FromArgb(80, 90, 105),
            TextAlign = ContentAlignment.MiddleLeft
        };
        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            Width = 190,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };
        var ok = MakeButton("保存动作", (_, _) => SaveAndClose());
        ok.Width = 88;
        var cancel = MakeButton("取消", (_, _) => { DialogResult = DialogResult.Cancel; Close(); });
        cancel.Width = 72;
        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);
        footer.Controls.Add(help);
        footer.Controls.Add(buttons);

        Controls.Add(_grid);
        Controls.Add(footer);
        Controls.Add(toolbar);
    }

    private void ConfigureGrid()
    {
        _grid.Dock = DockStyle.Fill;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AllowUserToOrderColumns = true;
        _grid.AllowUserToResizeRows = false;
        _grid.AutoGenerateColumns = false;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
        _grid.BackgroundColor = Color.White;
        _grid.BorderStyle = BorderStyle.FixedSingle;
        _grid.RowHeadersVisible = true;
        _grid.MultiSelect = true;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2;
        _grid.VirtualMode = true;
        AddReadOnlyTextColumn("ActionId", "动作 ID", 150);
        _grid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            Name = "Enabled", HeaderText = "启用", Width = 48, ValueType = typeof(bool)
        });

        var kindTable = new DataTable();
        kindTable.Columns.Add("Value", typeof(RecorderActionKind));
        kindTable.Columns.Add("Label", typeof(string));
        foreach (var kind in Enum.GetValues<RecorderActionKind>())
            kindTable.Rows.Add(kind, KindLabel(kind));
        _grid.Columns.Add(new DataGridViewComboBoxColumn
        {
            Name = "Kind",
            HeaderText = "动作类型",
            DataSource = kindTable,
            ValueMember = "Value",
            DisplayMember = "Label",
            ValueType = typeof(RecorderActionKind),
            FlatStyle = FlatStyle.Flat,
            Width = 104
        });

        AddTextColumn("X", "X", 64);
        AddTextColumn("Y", "Y", 64);
        AddTextColumn("Name", "按键/鼠标键", 100);
        AddTextColumn("Delta", "滚轮 Delta", 80);
        AddTextColumn("HoldMs", "按住时长", 80);
        AddTextColumn("DelayMs", "动作间隔", 80);
        AddTextColumn("Text", "文本", 160);
        AddTextColumn("Group", "分组", 110);
        AddTextColumn("Note", "注释", 180);
        AddReadOnlyTextColumn("Source", "来源", 110);
        AddReadOnlyTextColumn("RawEventIds", "原始事件 ID", 180);

        _grid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (_grid.IsCurrentCellDirty)
                _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        _grid.CellValueNeeded += (_, e) =>
        {
            if (e.RowIndex < 0 || e.RowIndex >= _actions.Count)
                return;
            var action = _actions[e.RowIndex];
            e.Value = _grid.Columns[e.ColumnIndex].Name switch
            {
                "ActionId" => action.ActionId,
                "Enabled" => action.Enabled,
                "Kind" => action.Kind,
                "X" => action.X,
                "Y" => action.Y,
                "Name" => action.Name,
                "Delta" => action.Delta,
                "HoldMs" => action.HoldMs,
                "DelayMs" => action.DelayMs,
                "Text" => action.Text,
                "Group" => action.Group,
                "Note" => action.Note,
                "Source" => action.Source,
                "RawEventIds" => action.RawEventIds is { Length: > 0 } ids ? string.Join(", ", ids) : null,
                _ => null
            };
        };
        _grid.CellValuePushed += (_, e) =>
        {
            if (e.RowIndex < 0 || e.RowIndex >= _actions.Count)
                return;
            try
            {
                var action = _actions[e.RowIndex];
                var updated = _grid.Columns[e.ColumnIndex].Name switch
                {
                    "Enabled" => action with { Enabled = Convert.ToBoolean(e.Value, System.Globalization.CultureInfo.InvariantCulture) },
                    "Kind" => action with
                    {
                        Kind = e.Value is RecorderActionKind kind
                            ? kind
                            : Enum.Parse<RecorderActionKind>(
                                Convert.ToString(e.Value, System.Globalization.CultureInfo.InvariantCulture) ?? action.Kind.ToString(),
                                ignoreCase: true)
                    },
                    "X" => action with { X = ParseInt(e.Value, "X") },
                    "Y" => action with { Y = ParseInt(e.Value, "Y") },
                    "Name" => action with { Name = Convert.ToString(e.Value, System.Globalization.CultureInfo.InvariantCulture) },
                    "Delta" => action with { Delta = ParseInt(e.Value, "Delta") },
                    "HoldMs" => action with { HoldMs = ParseNonNegativeLong(e.Value, "按住时长") },
                    "DelayMs" => action with { DelayMs = ParseNonNegativeLong(e.Value, "动作间隔") },
                    "Text" => action with { Text = Convert.ToString(e.Value, System.Globalization.CultureInfo.InvariantCulture) },
                    "Group" => action with { Group = Convert.ToString(e.Value, System.Globalization.CultureInfo.InvariantCulture) },
                    "Note" => action with { Note = Convert.ToString(e.Value, System.Globalization.CultureInfo.InvariantCulture) },
                    _ => action
                };
                _actions[e.RowIndex] = updated;
                MarkPending();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "参数无效", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _grid.CancelEdit();
            }
        };
        _grid.DataError += (_, e) =>
        {
            e.ThrowException = false;
            MessageBox.Show(this, "当前单元格的值不符合动作参数要求。", "参数无效",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        };
        _grid.CellEndEdit += (_, _) => _grid.Invalidate();
    }

    private void AddTextColumn(string name, string header, int width) =>
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = name,
            HeaderText = header,
            Width = width,
            SortMode = DataGridViewColumnSortMode.NotSortable
        });

    private void AddReadOnlyTextColumn(string name, string header, int width) =>
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = name,
            HeaderText = header,
            Width = width,
            ReadOnly = true,
            SortMode = DataGridViewColumnSortMode.NotSortable
        });

    private static Button MakeButton(string text, EventHandler handler)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            Height = 28,
            Margin = new Padding(3, 2, 3, 2)
        };
        button.Click += handler;
        return button;
    }

    private void RefreshRows(int? selected = null)
    {
        _grid.RowCount = _actions.Count;
        _grid.Invalidate();
        if (selected is >= 0 && selected < _actions.Count)
        {
            _grid.ClearSelection();
            _grid.Rows[selected.Value].Selected = true;
            _grid.CurrentCell = _grid.Rows[selected.Value].Cells[0];
        }
        UpdateSyncLabel();
    }

    private int SelectedIndex => _grid.CurrentCell?.RowIndex ?? -1;

    private void InsertAction()
    {
        var selected = SelectedIndex;
        var kind = (_insertKind.SelectedItem as ActionKindItem)?.Value ?? RecorderActionKind.MouseMove;
        var action = new RecorderAction(Guid.NewGuid().ToString("N"), kind, Source: "ActionEditor");
        var insertionIndex = selected >= 0 ? selected + 1 : _actions.Count;
        _actions.Insert(insertionIndex, action);
        RefreshRows(insertionIndex);
        MarkPending();
    }

    private void DeleteSelected()
    {
        var indices = _grid.SelectedRows.Cast<DataGridViewRow>()
            .Select(row => row.Index)
            .Where(index => index >= 0 && index < _actions.Count)
            .Distinct().OrderByDescending(index => index).ToArray();
        if (indices.Length == 0 && SelectedIndex >= 0)
            indices = [SelectedIndex];
        foreach (var index in indices)
            _actions.RemoveAt(index);
        RefreshRows(indices.Length == 0 ? null : Math.Min(indices.Min(), _actions.Count - 1));
        MarkPending();
    }

    private void DuplicateSelected()
    {
        var selected = SelectedIndex;
        if (selected < 0 || selected >= _actions.Count)
            return;
        var copy = _actions[selected] with { ActionId = Guid.NewGuid().ToString("N") };
        _actions.Insert(selected + 1, copy);
        RefreshRows(selected + 1);
        MarkPending();
    }

    private void MoveSelected(int offset)
    {
        var selected = SelectedIndex;
        var target = selected + offset;
        if (selected < 0 || selected >= _actions.Count || target < 0 || target >= _actions.Count)
            return;
        (_actions[selected], _actions[target]) = (_actions[target], _actions[selected]);
        RefreshRows(target);
        MarkPending();
    }

    private void ApplyBatchDelay()
    {
        var indices = _grid.SelectedRows.Cast<DataGridViewRow>()
            .Select(row => row.Index)
            .Where(index => index >= 0 && index < _actions.Count)
            .Distinct().ToArray();
        if (indices.Length == 0 && SelectedIndex >= 0)
            indices = [SelectedIndex];
        foreach (var index in indices)
            _actions[index] = _actions[index] with { DelayMs = (long)_batchDelay.Value };
        RefreshRows(indices.FirstOrDefault(-1));
        if (indices.Length > 0)
            MarkPending();
    }

    private async Task RunSelectedAsync()
    {
        var selected = SelectedIndex;
        if (selected < 0 || selected >= _actions.Count)
            return;
        var action = _actions[selected];
        if (!action.Enabled)
        {
            MessageBox.Show(this, "已禁用的动作不能试运行。", "步骤试运行", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        try
        {
            await _runSingleAction(action);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "步骤试运行失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }


    private void NavigateSelectedCode()
    {
        var selected = SelectedIndex;
        if (selected < 0 || selected >= _actions.Count)
            return;
        if (_grid.IsCurrentCellInEditMode && !_grid.EndEdit())
            return;

        var action = _actions[selected];
        var marker = $"-- <anjian:action id=\"{action.ActionId}\"";
        if (!_scriptText.Contains(marker, StringComparison.Ordinal))
        {
            MessageBox.Show(this, "当前动作尚未生成对应 Lua 标记。请先点击“生成/同步 Lua”，再定位代码。",
                "尚无代码映射", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        UpdatedDocument = GetWorkingDocument();
        UpdatedScriptText = _scriptText;
        RequestedNavigationActionId = action.ActionId;
        DialogResult = DialogResult.OK;
        Close();
    }

    private void GenerateLuaBlock()
    {
        try
        {
            var changedDocument = GetWorkingDocument() with
            {
                Actions = _actions.ToList(),
                SynchronizationState = _document.SynchronizationState is RecorderSynchronizationState.Detached or RecorderSynchronizationState.Missing
                    ? _document.SynchronizationState
                    : RecorderSynchronizationState.Pending
            };
            var prepared = RecorderLuaGenerator.PrepareUpdate(_scriptText, changedDocument);
            if (prepared.Error is not null)
                throw new InvalidOperationException(prepared.Error);

            if (prepared.RequiresConfirmation)
            {
                using var preview = new RecorderBlockPreviewForm(prepared.ExistingBlock, prepared.ReplacementBlock);
                if (preview.ShowDialog(this) != DialogResult.OK)
                {
                    _document = prepared.Document;
                    UpdateSyncLabel();
                    return;
                }

                prepared = RecorderLuaGenerator.PrepareUpdate(
                    _scriptText, changedDocument, confirmDetachedReplacement: true);
            }

            if (!prepared.Success)
                throw new InvalidOperationException(prepared.Error ?? "Lua 生成区块更新失败。");

            _scriptText = prepared.ScriptText;
            _document = prepared.Document;
            UpdatedScriptText = _scriptText;
            UpdatedDocument = _document;
            UpdateSyncLabel();
            MessageBox.Show(this, "Lua 生成区块已更新。点击“保存动作”后，脚本和 .actions.json 将一起保存。",
                "生成完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "生成 Lua 失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private RecorderDocument GetWorkingDocument() =>
        _document with { Actions = _actions.ToList() };

    private void MarkPending()
    {
        var state = _document.SynchronizationState is RecorderSynchronizationState.Detached or RecorderSynchronizationState.Missing
            ? _document.SynchronizationState
            : RecorderSynchronizationState.Pending;
        _document = _document with
        {
            Actions = _actions.ToList(),
            SynchronizationState = state
        };
        UpdatedDocument = _document;
        UpdateSyncLabel();
    }

    private void UpdateSyncLabel()
    {
        var stateText = _document.SynchronizationState switch
        {
            RecorderSynchronizationState.Synchronized => "代码同步",
            RecorderSynchronizationState.Detached => "代码已手改/脱离同步",
            RecorderSynchronizationState.Missing => "生成区块缺失",
            _ => "动作有修改，尚未同步代码"
        };
        _syncState.Text = _document.DroppedEventCount > 0
            ? $"⚠ 原始事件丢失 {_document.DroppedEventCount} 条；{stateText}"
            : stateText;
        _syncState.ForeColor = _document.SynchronizationState == RecorderSynchronizationState.Synchronized
            ? Color.SeaGreen : Color.DarkOrange;
    }

    private void SaveAndClose()
    {
        try
        {
            if (_grid.IsCurrentCellInEditMode && !_grid.EndEdit())
                return;
            var document = GetWorkingDocument();
            document.Validate();
            UpdatedDocument = document;
            UpdatedScriptText = _scriptText;
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "保存动作失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static int ParseInt(object? value, string name)
    {
        if (!int.TryParse(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture),
                System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
            throw new FormatException($"{name} 必须是整数。");
        return parsed;
    }

    private static long ParseNonNegativeLong(object? value, string name)
    {
        if (!long.TryParse(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture),
                System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var parsed) || parsed < 0)
            throw new FormatException($"{name} 必须是非负整数。");
        return parsed;
    }

    private static string KindLabel(RecorderActionKind kind) => kind switch
    {
        RecorderActionKind.MouseMove => "鼠标移动",
        RecorderActionKind.MouseClick => "鼠标点击",
        RecorderActionKind.MouseDown => "鼠标按下",
        RecorderActionKind.MouseUp => "鼠标释放",
        RecorderActionKind.MouseWheel => "鼠标滚轮",
        RecorderActionKind.KeyPress => "按键",
        RecorderActionKind.KeyDown => "按键按下",
        RecorderActionKind.KeyUp => "按键释放",
        RecorderActionKind.TypeText => "文本输入",
        _ => kind.ToString()
    };

    private sealed record ActionKindItem(RecorderActionKind Value, string Label)
    {
        public override string ToString() => Label;
    }
}

internal sealed class RecorderBlockPreviewForm : Form
{
    public RecorderBlockPreviewForm(string existingBlock, string replacementBlock)
    {
        Text = "确认替换已手工修改的 Lua 生成区块";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(900, 500);
        Size = new Size(1120, 620);
        Font = new Font("Microsoft YaHei UI", 9F);

        var comparison = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(4)
        };
        comparison.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        comparison.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        comparison.Controls.Add(CreatePreview("当前脚本中的生成区块", existingBlock), 0, 0);
        comparison.Controls.Add(CreatePreview("即将替换为", replacementBlock), 1, 0);

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 48,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(8)
        };
        var confirm = new Button { Text = "确认替换", AutoSize = true };
        confirm.Click += (_, _) => { DialogResult = DialogResult.OK; Close(); };
        var cancel = new Button { Text = "取消", AutoSize = true };
        cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        footer.Controls.Add(confirm);
        footer.Controls.Add(cancel);
        Controls.Add(comparison);
        Controls.Add(footer);
    }

    private static Control CreatePreview(string title, string content)
    {
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
        var label = new Label { Text = title, Dock = DockStyle.Top, Height = 28, Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold) };
        var editor = new RichTextBox
        {
            Text = content,
            Dock = DockStyle.Fill,
            ReadOnly = true,
            WordWrap = false,
            Font = new Font("Consolas", 9.5F),
            BackColor = Color.White
        };
        panel.Controls.Add(editor);
        panel.Controls.Add(label);
        return panel;
    }
}
