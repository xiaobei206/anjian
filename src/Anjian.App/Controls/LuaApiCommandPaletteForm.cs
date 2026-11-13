using Anjian.Core.Api;

namespace Anjian.App.Controls;

internal sealed class LuaApiCommandPaletteForm : Form
{
    private sealed class ApiChoice(LuaApiDefinition api, string category)
    {
        public LuaApiDefinition Api { get; } = api;
        public override string ToString() => $"{Api.Name}  —  {category}  |  {Api.Signature}";
    }

    private readonly LuaApiRegistry _registry;
    private readonly TextBox _filter = new() { Dock = DockStyle.Fill, PlaceholderText = "输入函数名、签名、分类或说明..." };
    private readonly ListBox _results = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    private readonly Label _details = new() { Dock = DockStyle.Fill, AutoSize = false, TextAlign = ContentAlignment.TopLeft };
    private readonly Button _insert = new() { Text = "插入函数调用", AutoSize = true };
    public LuaApiDefinition? SelectedApi { get; private set; }

    public LuaApiCommandPaletteForm(LuaApiRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        Text = "Lua API 命令面板";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(640, 440);
        Size = new Size(820, 560);
        Font = new Font("Microsoft YaHei UI", 9F);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(10)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };
        var cancel = new Button { Text = "取消", AutoSize = true, DialogResult = DialogResult.Cancel };
        _insert.Click += (_, _) => AcceptSelection();
        cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        buttons.Controls.Add(_insert);
        buttons.Controls.Add(cancel);

        _filter.TextChanged += (_, _) => RefreshResults();
        _results.SelectedIndexChanged += (_, _) => UpdateDetails();
        _results.DoubleClick += (_, _) => AcceptSelection();
        _results.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                AcceptSelection();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        };

        layout.Controls.Add(_filter, 0, 0);
        layout.Controls.Add(_results, 0, 1);
        layout.Controls.Add(_details, 0, 2);
        layout.Controls.Add(buttons, 0, 3);
        Controls.Add(layout);
        AcceptButton = _insert;
        CancelButton = cancel;
        RefreshResults();
        _filter.Focus();
    }

    private void RefreshResults()
    {
        var query = _filter.Text.Trim();
        var categories = _registry.Categories.ToDictionary(category => category.Id, category => category.Title, StringComparer.Ordinal);
        var choices = _registry.Apis
            .Where(api => string.IsNullOrEmpty(query) ||
                api.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                api.Signature.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                api.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                api.Description.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                categories.GetValueOrDefault(api.Category, api.Category).Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderBy(api => api.Name, StringComparer.OrdinalIgnoreCase)
            .Select(api => new ApiChoice(api, categories.GetValueOrDefault(api.Category, api.Category)))
            .ToArray();
        _results.BeginUpdate();
        try
        {
            _results.Items.Clear();
            _results.Items.AddRange(choices);
            if (_results.Items.Count > 0)
                _results.SelectedIndex = 0;
            else
                _details.Text = "没有匹配的函数。";
        }
        finally
        {
            _results.EndUpdate();
        }
    }

    private void UpdateDetails()
    {
        if (_results.SelectedItem is not ApiChoice choice)
        {
            _details.Text = "请选择一个函数。";
            _insert.Enabled = false;
            return;
        }
        var api = choice.Api;
        _details.Text = $"{api.Title}  ·  {api.Signature}{Environment.NewLine}{api.Description}";
        _insert.Enabled = true;
    }

    private void AcceptSelection()
    {
        if (_results.SelectedItem is not ApiChoice choice)
            return;
        SelectedApi = choice.Api;
        DialogResult = DialogResult.OK;
        Close();
    }
}
