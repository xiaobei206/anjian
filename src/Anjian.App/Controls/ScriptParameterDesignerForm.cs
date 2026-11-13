using System.Text.Json;
using Anjian.Core.Parameters;

namespace Anjian.App.Controls;

internal sealed class ScriptParameterDesignerForm : Form
{
    private readonly string _schemaPath;
    private readonly DataGridView _grid = new();
    private readonly ScriptParameterSchema _schema;

    public ScriptParameterDesignerForm(string scriptPath)
    {
        _schemaPath = ScriptParameterService.SchemaPath(scriptPath);
        Text = "参数设计器 - " + Path.GetFileName(scriptPath);
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(1120, 600);
        MinimumSize = new Size(850, 420);
        _schema = File.Exists(_schemaPath)
            ? ScriptParameterService.LoadSchema(_schemaPath)
            : new ScriptParameterSchema { ScriptId = Path.GetFileNameWithoutExtension(scriptPath) };

        _grid.Dock = DockStyle.Fill;
        _grid.AllowUserToAddRows = true;
        _grid.AllowUserToDeleteRows = true;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
        _grid.RowHeadersWidth = 26;
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", HeaderText = "稳定 ID", Width = 160 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name", HeaderText = "Lua 参数名", Width = 120 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Label", HeaderText = "中文标签", Width = 120 });
        var type = new DataGridViewComboBoxColumn { Name = "Type", HeaderText = "类型", Width = 100 };
        type.Items.AddRange("text", "integer", "number", "boolean", "select", "multiSelect", "filePath", "directoryPath", "hotkey", "color");
        _grid.Columns.Add(type);
        _grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Required", HeaderText = "必填" });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "DefaultValue", HeaderText = "默认值(JSON)" });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Minimum", HeaderText = "最小值" });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Maximum", HeaderText = "最大值" });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Options", HeaderText = "选项(|分隔)" });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Description", HeaderText = "说明" });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Pattern", HeaderText = "正则" });
        _grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "MustExist", HeaderText = "路径须存在" });
        _grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Sensitive", HeaderText = "敏感" });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Order", HeaderText = "排序" });
        foreach (var p in _schema.Parameters.OrderBy(x => x.Order))
            _grid.Rows.Add(p.Id, p.Name, p.Label, p.Type, p.Required,
                p.DefaultValue is { } value && value.ValueKind != JsonValueKind.Undefined ? value.GetRawText() : "",
                p.Minimum?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "",
                p.Maximum?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "",
                string.Join("|", p.Options), p.Description, p.Pattern ?? "", p.MustExist, p.Sensitive, p.Order);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 44, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(6) };
        var save = new Button { Text = "保存参数定义", AutoSize = true };
        save.Click += (_, _) => SaveSchema();
        var add = new Button { Text = "新增参数", AutoSize = true };
        add.Click += (_, _) => _grid.Rows.Add(Guid.NewGuid().ToString("N"), "newParam", "新参数", "text", false, "", "", "", "", "", "", false, false, _grid.Rows.Count);
        var cancel = new Button { Text = "关闭", AutoSize = true };
        cancel.Click += (_, _) => Close();
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(save);
        buttons.Controls.Add(add);
        Controls.Add(_grid);
        Controls.Add(buttons);
    }

    private void SaveSchema()
    {
        try
        {
            _grid.EndEdit();
            var schema = new ScriptParameterSchema { SchemaVersion = 1, ScriptId = _schema.ScriptId };
            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.IsNewRow) continue;
                var name = Text(row, "Name");
                if (string.IsNullOrWhiteSpace(name)) continue;
                var defaultText = Text(row, "DefaultValue");
                JsonElement? defaultValue = null;
                if (!string.IsNullOrWhiteSpace(defaultText))
                {
                    using var doc = JsonDocument.Parse(defaultText);
                    defaultValue = doc.RootElement.Clone();
                }
                schema.Parameters.Add(new ScriptParameterDefinition
                {
                    Id = Text(row, "Id"), Name = name, Label = Text(row, "Label"),
                    Type = Text(row, "Type"), Required = Bool(row, "Required"), DefaultValue = defaultValue,
                    Minimum = Number(row, "Minimum"), Maximum = Number(row, "Maximum"),
                    Options = Text(row, "Options").Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList(),
                    Description = Text(row, "Description"), Pattern = NullIfEmpty(Text(row, "Pattern")),
                    MustExist = Bool(row, "MustExist"), Sensitive = Bool(row, "Sensitive"),
                    Order = (int)(Number(row, "Order") ?? schema.Parameters.Count)
                });
            }
            ScriptParameterService.SaveSchemaAtomic(_schemaPath, schema);
            MessageBox.Show(this, "参数定义已保存。运行时会在启动前读取并验证，不会执行 Lua 来读取参数。", "参数设计器",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            DialogResult = DialogResult.OK;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "保存失败：" + ex.Message, "参数设计器", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private static string Text(DataGridViewRow row, string name) => Convert.ToString(row.Cells[name].Value) ?? "";
    private static bool Bool(DataGridViewRow row, string name) => row.Cells[name].Value is bool b && b;
    private static double? Number(DataGridViewRow row, string name) =>
        double.TryParse(Text(row, name), System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var value) ? value : null;
    private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
