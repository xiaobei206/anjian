using System.Text;

namespace Anjian.App.Controls;

internal sealed class ScriptSearchReplaceForm : Form
{
    private readonly RichTextBox _target;
    private readonly TextBox _find = new();
    private readonly TextBox _replace = new();
    private readonly CheckBox _matchCase = new() { Text = "区分大小写", AutoSize = true };
    private readonly Label _result = new() { AutoSize = true, ForeColor = Color.FromArgb(75, 90, 110) };

    public ScriptSearchReplaceForm(RichTextBox target)
    {
        _target = target ?? throw new ArgumentNullException(nameof(target));
        Text = "查找与替换";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(510, 170);
        Font = new Font("Microsoft YaHei UI", 9F);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 2,
            RowCount = 4
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 82));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        layout.Controls.Add(new Label { Text = "查找内容", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        _find.Dock = DockStyle.Fill;
        layout.Controls.Add(_find, 1, 0);
        layout.Controls.Add(new Label { Text = "替换为", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        _replace.Dock = DockStyle.Fill;
        layout.Controls.Add(_replace, 1, 1);
        layout.Controls.Add(_matchCase, 1, 2);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };
        actions.Controls.Add(MakeButton("查找下一个", FindNext));
        actions.Controls.Add(MakeButton("替换", ReplaceCurrent));
        actions.Controls.Add(MakeButton("全部替换", ReplaceAll));
        var close = new Button { Text = "关闭", AutoSize = true };
        close.Click += (_, _) => Close();
        actions.Controls.Add(close);
        actions.Controls.Add(_result);
        layout.Controls.Add(actions, 0, 3);
        layout.SetColumnSpan(actions, 2);
        Controls.Add(layout);

        AcceptButton = actions.Controls.OfType<Button>().First();
        _find.Text = _target.SelectedText;
        _find.SelectAll();
    }

    private static Button MakeButton(string text, Action action)
    {
        var button = new Button { Text = text, AutoSize = true };
        button.Click += (_, _) => action();
        return button;
    }

    private RichTextBoxFinds FindOptions =>
        _matchCase.Checked ? RichTextBoxFinds.MatchCase : RichTextBoxFinds.None;

    private void FindNext()
    {
        if (string.IsNullOrEmpty(_find.Text))
        {
            _result.Text = "请输入查找内容。";
            return;
        }

        var start = Math.Min(_target.TextLength, _target.SelectionStart + _target.SelectionLength);
        var found = _target.Find(_find.Text, start, FindOptions);
        if (found < 0 && start > 0)
            found = _target.Find(_find.Text, 0, start, FindOptions);

        if (found < 0)
        {
            _result.Text = "未找到匹配内容。";
            return;
        }

        _target.Focus();
        _result.Text = $"位置：第 {_target.GetLineFromCharIndex(found) + 1} 行";
    }

    private void ReplaceCurrent()
    {
        if (string.IsNullOrEmpty(_find.Text))
        {
            _result.Text = "请输入查找内容。";
            return;
        }

        var comparison = _matchCase.Checked ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        if (_target.SelectionLength > 0 &&
            string.Equals(_target.SelectedText, _find.Text, comparison))
        {
            _target.SelectedText = _replace.Text;
            _result.Text = "已替换当前匹配项。";
        }

        FindNext();
    }

    private void ReplaceAll()
    {
        if (string.IsNullOrEmpty(_find.Text))
        {
            _result.Text = "请输入查找内容。";
            return;
        }

        var comparison = _matchCase.Checked ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var source = _target.Text;
        var output = new StringBuilder(source.Length);
        var cursor = 0;
        var count = 0;
        var firstReplacement = -1;

        while (cursor < source.Length)
        {
            var match = source.IndexOf(_find.Text, cursor, comparison);
            if (match < 0)
                break;

            output.Append(source, cursor, match - cursor);
            if (firstReplacement < 0)
                firstReplacement = output.Length;
            output.Append(_replace.Text);
            cursor = match + _find.Text.Length;
            count++;
        }

        if (count == 0)
        {
            _result.Text = "未找到匹配内容。";
            return;
        }

        output.Append(source, cursor, source.Length - cursor);
        _target.Text = output.ToString();
        _target.Select(Math.Clamp(firstReplacement, 0, _target.TextLength), _replace.Text.Length);
        _target.Focus();
        _result.Text = $"已替换 {count} 处。";
    }
}
