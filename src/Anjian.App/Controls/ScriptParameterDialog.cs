using System.Text.Json;
using Anjian.App.Services;
using Anjian.Core.Parameters;

namespace Anjian.App.Controls;

internal sealed class ScriptParameterDialog : Form
{
    private readonly ScriptParameterSchema _schema;
    private readonly string _scriptPath;
    private readonly Dictionary<string, Control> _fields = new(StringComparer.Ordinal);
    public IReadOnlyDictionary<string, JsonElement> Values { get; private set; } =
        new Dictionary<string, JsonElement>(StringComparer.Ordinal);

    public ScriptParameterDialog(string scriptPath, ScriptParameterSchema schema,
        IReadOnlyDictionary<string, JsonElement> initialValues)
    {
        _scriptPath = scriptPath;
        _schema = schema;
        Text = "运行参数 - " + Path.GetFileName(scriptPath);
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(520, 360);
        Size = new Size(680, Math.Min(760, 180 + schema.Parameters.Count * 58));
        MinimizeBox = false;
        MaximizeBox = true;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, AutoScroll = true, ColumnCount = 2,
            Padding = new Padding(14), AutoSize = false
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foreach (var p in schema.Parameters.OrderBy(x => x.Order))
        {
            var label = new Label
            {
                Text = p.Label + (p.Required ? " *" : ""),
                AutoSize = true, Anchor = AnchorStyles.Left,
                Margin = new Padding(3, 8, 8, 8)
            };
            if (!string.IsNullOrWhiteSpace(p.Description))
                label.Text += Environment.NewLine + p.Description;
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.Controls.Add(label, 0, layout.RowCount);
            var field = CreateField(p, initialValues);
            field.Dock = DockStyle.Top;
            field.Margin = new Padding(3, 4, 6, 8);
            layout.Controls.Add(field, 1, layout.RowCount);
            _fields[p.Name] = field;
            layout.RowCount++;
        }

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 48, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8) };
        var ok = new Button { Text = "确定运行", DialogResult = DialogResult.None, AutoSize = true };
        ok.Click += (_, _) => ValidateAndSave();
        var cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, AutoSize = true };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(ok);
        Controls.Add(layout);
        Controls.Add(buttons);
        AcceptButton = ok;
        CancelButton = cancel;
    }

    private Control CreateField(ScriptParameterDefinition p, IReadOnlyDictionary<string, JsonElement> initial)
    {
        JsonElement value = default;
        var hasValue = initial.TryGetValue(p.Name, out value);
        if (!hasValue && p.DefaultValue is { } d && d.ValueKind != JsonValueKind.Undefined)
        {
            value = d;
            hasValue = true;
        }

        if (p.Type == "boolean")
            return new CheckBox { Checked = hasValue && value.ValueKind == JsonValueKind.True, AutoSize = true };
        if (p.Type == "select")
        {
            var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            combo.Items.AddRange(p.Options.Cast<object>().ToArray());
            if (hasValue && value.ValueKind == JsonValueKind.String) combo.SelectedItem = value.GetString();
            if (combo.SelectedIndex < 0 && combo.Items.Count > 0) combo.SelectedIndex = 0;
            return combo;
        }
        if (p.Type == "multiSelect")
        {
            var list = new CheckedListBox { Height = Math.Min(100, 26 + p.Options.Count * 22), CheckOnClick = true };
            list.Items.AddRange(p.Options.Cast<object>().ToArray());
            if (hasValue && value.ValueKind == JsonValueKind.Array)
            {
                var selected = value.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()).ToHashSet(StringComparer.Ordinal);
                for (var i = 0; i < list.Items.Count; i++)
                    list.SetItemChecked(i, selected.Contains(list.Items[i]?.ToString()));
            }
            return list;
        }
        if (p.Type is "integer" or "number")
        {
            var number = new NumericUpDown
            {
                DecimalPlaces = p.Type == "integer" ? 0 : 4,
                Minimum = ToDecimal(p.Minimum, -1_000_000_000_000m),
                Maximum = ToDecimal(p.Maximum, 1_000_000_000_000m),
                ThousandsSeparator = true,
                Width = 240
            };
            if (number.Minimum > number.Maximum)
            {
                number.Minimum = -1_000_000_000_000m;
                number.Maximum = 1_000_000_000_000m;
            }
            if (hasValue && value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var n))
                number.Value = Math.Clamp(n, number.Minimum, number.Maximum);
            else number.Value = Math.Clamp(0m, number.Minimum, number.Maximum);
            return number;
        }
        var text = new TextBox { Width = 360 };
        if (hasValue)
        {
            text.Text = value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? ""
                : value.GetRawText();
        }
        if (p.Type is "filePath" or "directoryPath")
        {
            var panel = new Panel { Height = 28, Width = 420 };
            text.Dock = DockStyle.Fill;
            var browse = new Button { Text = "…", Dock = DockStyle.Right, Width = 36 };
            browse.Click += (_, _) =>
            {
                if (p.Type == "filePath")
                {
                    using var picker = new OpenFileDialog { CheckFileExists = p.MustExist, FileName = text.Text };
                    if (picker.ShowDialog(this) == DialogResult.OK) text.Text = picker.FileName;
                }
                else
                {
                    using var picker = new FolderBrowserDialog { SelectedPath = text.Text };
                    if (picker.ShowDialog(this) == DialogResult.OK) text.Text = picker.SelectedPath;
                }
            };
            panel.Controls.Add(text);
            panel.Controls.Add(browse);
            return panel;
        }
        return text;
    }

    private void ValidateAndSave()
    {
        var supplied = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        try
        {
            foreach (var p in _schema.Parameters)
            {
                var control = _fields[p.Name];
                string json = p.Type switch
                {
                    "boolean" => ((CheckBox)control).Checked ? "true" : "false",
                    "integer" => decimal.ToInt64(((NumericUpDown)control).Value).ToString(System.Globalization.CultureInfo.InvariantCulture),
                    "number" => ((NumericUpDown)control).Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    "select" => JsonSerializer.Serialize(((ComboBox)control).SelectedItem?.ToString() ?? ""),
                    "multiSelect" => JsonSerializer.Serialize(((CheckedListBox)control).CheckedItems.Cast<object>().Select(x => x.ToString()).ToArray()),
                    "filePath" or "directoryPath" when control is Panel panel =>
                        JsonSerializer.Serialize(panel.Controls.OfType<TextBox>().First().Text),
                    _ => JsonSerializer.Serialize(((TextBox)control).Text)
                };
                using var document = JsonDocument.Parse(json);
                supplied[p.Name] = document.RootElement.Clone();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "参数值格式错误：" + ex.Message, "参数验证", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var validation = ScriptParameterValidator.ValidateValues(_schema, supplied, Path.GetDirectoryName(_scriptPath));
        if (!validation.IsValid)
        {
            var errors = validation.Issues.Select(x => $"{x.Label}：{x.Message}");
            MessageBox.Show(this, string.Join(Environment.NewLine, errors), "参数验证失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            ParameterConfigurationStore.SaveAtomic(ScriptParameterService.ConfigurationPath(_scriptPath),
                new ParameterConfiguration { Values = validation.Values.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal) });
            Values = validation.Values;
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "保存参数配置失败：" + ex.Message, "参数配置", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static decimal ToDecimal(double? value, decimal fallback)
    {
        if (!value.HasValue || !double.IsFinite(value.Value)) return fallback;
        try { return Math.Clamp((decimal)value.Value, -1_000_000_000_000m, 1_000_000_000_000m); }
        catch (OverflowException) { return value.Value < 0 ? -1_000_000_000_000m : 1_000_000_000_000m; }
    }
}
