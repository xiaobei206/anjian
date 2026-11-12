using System.Drawing;

namespace Anjian.App;

/// <summary>
/// Self-owned WinForms test target for M7 UI Automation Pattern and foreground-input acceptance.
/// Launch the packaged app with --automation-probe; this opens only this test form.
/// </summary>
internal sealed class AutomationProbeForm : Form
{
    private readonly TextBox _textBox = new()
    {
        Name = "AutomationProbeTextBox",
        AccessibleName = "Automation probe text",
        Text = "UI Automation value probe",
        Dock = DockStyle.Fill
    };

    private readonly TextBox _passwordBox = new()
    {
        Name = "AutomationProbePasswordBox",
        AccessibleName = "Automation probe password",
        UseSystemPasswordChar = true,
        Text = "not-for-reading",
        Dock = DockStyle.Fill
    };

    private readonly CheckBox _checkBox = new()
    {
        Name = "AutomationProbeCheckBox",
        AccessibleName = "Automation probe toggle",
        Text = "UIA TogglePattern",
        Checked = true,
        AutoSize = true,
        Anchor = AnchorStyles.Left
    };

    private readonly ComboBox _comboBox = new()
    {
        Name = "AutomationProbeComboBox",
        AccessibleName = "Automation probe selection",
        DropDownStyle = ComboBoxStyle.DropDownList,
        Dock = DockStyle.Fill
    };

    private readonly ListBox _listBox = new()
    {
        Name = "AutomationProbeListBox",
        AccessibleName = "Automation probe list",
        SelectionMode = SelectionMode.One,
        Dock = DockStyle.Fill
    };

    private readonly Button _invokeButton = new()
    {
        Name = "AutomationProbeInvokeButton",
        AccessibleName = "Automation probe invoke",
        Text = "Invoke Pattern：点我",
        AutoSize = true,
        Anchor = AnchorStyles.Left
    };

    private readonly Label _status = new()
    {
        Name = "AutomationProbeStatus",
        AccessibleName = "Automation probe status",
        Text = "等待 UI Automation 调用",
        AutoSize = true,
        Anchor = AnchorStyles.Left
    };

    private int _invokeCount;

    public AutomationProbeForm()
    {
        Text = "Anjian2026 Automation Probe";
        Name = "AnjianAutomationProbe";
        AccessibleName = "Anjian2026 Automation Probe";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(620, 520);
        Size = new Size(760, 640);
        AutoScaleMode = AutoScaleMode.Dpi;

        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            ColumnCount = 2,
            RowCount = 7,
            AutoSize = false
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var i = 0; i < 7; i++)
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / 7));

        AddRow(grid, 0, "ValuePattern / TextPattern", _textBox);
        AddRow(grid, 1, "Password redaction", _passwordBox);
        AddRow(grid, 2, "TogglePattern", _checkBox);
        _comboBox.Items.AddRange(["Apple", "Banana", "Cherry"]);
        _comboBox.SelectedIndex = 0;
        AddRow(grid, 3, "SelectionPattern", _comboBox);
        _listBox.Items.AddRange(["First option", "Second option", "Third option"]);
        _listBox.SelectedIndex = 0;
        AddRow(grid, 4, "SelectionItemPattern", _listBox);
        _invokeButton.Click += (_, _) =>
        {
            _invokeCount++;
            _status.Text = $"Invoke 已调用 {_invokeCount} 次";
        };
        AddRow(grid, 5, "InvokePattern", _invokeButton);
        AddRow(grid, 6, "结果", _status);

        Controls.Add(grid);
    }

    private static void AddRow(TableLayoutPanel grid, int row, string label, Control control)
    {
        grid.Controls.Add(new Label
        {
            Text = label,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, row);
        control.Margin = new Padding(4, 6, 4, 6);
        grid.Controls.Add(control, 1, row);
    }
}
