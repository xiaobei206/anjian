using Anjian.Core.Api;
using Anjian.Core.Editor;

namespace Anjian.App.Controls;

/// <summary>
/// WinForms Lua editor surface with line numbers, registry-backed completion,
/// signature help and lightweight syntax colouring.
/// </summary>
internal sealed class LuaEditorControl : UserControl
{
    private readonly LuaApiRegistry _registry;
    private readonly RichTextBox _editor = new();
    private readonly Panel _gutter = new();
    private readonly ListBox _completionList = new();
    private readonly System.Windows.Forms.Timer _highlightTimer = new() { Interval = 180 };
    private readonly ToolTip _signatureTip = new() { ShowAlways = true, InitialDelay = 350, ReshowDelay = 150 };
    private bool _formatting;
    private int _textVersion;
    private IReadOnlyList<LuaApiCompletion> _completions = [];

    public RichTextBox TextBox => _editor;

    public event EventHandler? ContentChanged;

    public LuaEditorControl(LuaApiRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        BackColor = Color.White;

        _gutter.Dock = DockStyle.Left;
        _gutter.Width = 48;
        _gutter.BackColor = Color.FromArgb(245, 247, 250);
        _gutter.Paint += PaintLineNumbers;

        _editor.Dock = DockStyle.Fill;
        _editor.Font = new Font("Consolas", 10.5F);
        _editor.WordWrap = false;
        _editor.AcceptsTab = true;
        _editor.DetectUrls = false;
        _editor.HideSelection = false;
        _editor.BorderStyle = BorderStyle.None;
        _editor.BackColor = Color.FromArgb(250, 250, 250);
        _editor.ForeColor = Color.FromArgb(35, 39, 45);
        _editor.ScrollBars = RichTextBoxScrollBars.Both;
        _editor.TextChanged += EditorTextChanged;
        _editor.SelectionChanged += (_, _) => UpdateSignatureHelp();
        _editor.VScroll += (_, _) => { _gutter.Invalidate(); PositionCompletionList(); };
        _editor.HScroll += (_, _) => PositionCompletionList();
        _editor.Resize += (_, _) => PositionCompletionList();
        _editor.KeyDown += EditorKeyDown;
        _editor.KeyPress += (_, _) => _highlightTimer.Stop();

        _completionList.Visible = false;
        _completionList.TabStop = false;
        _completionList.IntegralHeight = false;
        _completionList.BorderStyle = BorderStyle.FixedSingle;
        _completionList.BackColor = Color.White;
        _completionList.Font = new Font("Microsoft YaHei UI", 9F);
        _completionList.Width = 360;
        _completionList.Height = 176;
        _completionList.MouseClick += (_, e) =>
        {
            var index = _completionList.IndexFromPoint(e.Location);
            if (index >= 0)
            {
                _completionList.SelectedIndex = index;
                AcceptCompletion();
            }
        };
        _completionList.SelectedIndexChanged += (_, _) => UpdateCompletionDetails();

        Controls.Add(_editor);
        Controls.Add(_gutter);
        Controls.Add(_completionList);
        _highlightTimer.Tick += async (_, _) => await HighlightAsync();
    }

    private void EditorTextChanged(object? sender, EventArgs e)
    {
        if (_formatting)
            return;

        _textVersion++;
        _highlightTimer.Stop();
        _highlightTimer.Start();
        _gutter.Invalidate();
        ContentChanged?.Invoke(this, EventArgs.Empty);
        UpdateCompletionFromCaret();
        UpdateSignatureHelp();
    }

    private void EditorKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Control && e.KeyCode == Keys.Space)
        {
            ShowCompletions(force: true);
            e.SuppressKeyPress = true;
            return;
        }

        if (_completionList.Visible)
        {
            switch (e.KeyCode)
            {
                case Keys.Down:
                    _completionList.SelectedIndex = Math.Min(_completionList.Items.Count - 1,
                        Math.Max(0, _completionList.SelectedIndex + 1));
                    e.SuppressKeyPress = true;
                    return;
                case Keys.Up:
                    _completionList.SelectedIndex = Math.Max(0, _completionList.SelectedIndex - 1);
                    e.SuppressKeyPress = true;
                    return;
                case Keys.Enter:
                case Keys.Tab:
                    AcceptCompletion();
                    e.SuppressKeyPress = true;
                    return;
                case Keys.Escape:
                    HideCompletions();
                    e.SuppressKeyPress = true;
                    return;
            }
        }

        if (e.KeyCode == Keys.Escape)
            _signatureTip.Hide(_editor);
    }

    private void UpdateCompletionFromCaret()
    {
        if (_completionList.Visible)
            ShowCompletions(force: false);
        else
        {
            var prefix = LuaEditorService.GetIdentifierPrefix(_editor.Text, _editor.SelectionStart);
            if (prefix.Length >= 2)
                ShowCompletions(force: false);
        }
    }

    private void ShowCompletions(bool force)
    {
        var caret = _editor.SelectionStart;
        var source = _editor.Text;
        var prefix = LuaEditorService.GetIdentifierPrefix(source, caret);
        if (!force && prefix.Length < 2)
        {
            HideCompletions();
            return;
        }

        if (!force && LuaEditorService.IsCaretInCommentOrString(source, caret))
        {
            HideCompletions();
            return;
        }

        _completions = LuaEditorService.GetCompletions(_registry, prefix, 24);
        _completionList.BeginUpdate();
        _completionList.Items.Clear();
        foreach (var item in _completions)
            _completionList.Items.Add(item.Api);
        _completionList.DisplayMember = nameof(LuaApiDefinition.Name);
        _completionList.EndUpdate();

        if (_completionList.Items.Count == 0)
        {
            HideCompletions();
            return;
        }

        _completionList.SelectedIndex = 0;
        PositionCompletionList();
        _completionList.Visible = true;
        _completionList.BringToFront();
    }

    private void PositionCompletionList()
    {
        if (!_completionList.Visible)
            return;

        var caret = _editor.GetPositionFromCharIndex(_editor.SelectionStart);
        var x = Math.Clamp(caret.X + _gutter.Width + 6, _gutter.Width,
            Math.Max(_gutter.Width, ClientSize.Width - _completionList.Width));
        var y = caret.Y + _editor.Font.Height + 4;
        if (y + _completionList.Height > ClientSize.Height)
            y = Math.Max(0, caret.Y - _completionList.Height - 4);
        _completionList.Location = new Point(x, y);
    }

    private void AcceptCompletion()
    {
        if (_completionList.SelectedItem is not LuaApiDefinition api)
        {
            HideCompletions();
            return;
        }

        var start = _editor.SelectionStart;
        var prefix = LuaEditorService.GetIdentifierPrefix(_editor.Text, start);
        var replaceStart = start - prefix.Length;
        var suffixIsCall = start < _editor.TextLength && _editor.Text[start] == '(';
        var insertion = api.Name + (suffixIsCall ? string.Empty : "()");
        _editor.Select(replaceStart, prefix.Length);
        _editor.SelectedText = insertion;
        if (!suffixIsCall)
            _editor.SelectionStart = replaceStart + api.Name.Length + 1;

        HideCompletions();
        _editor.Focus();
    }

    private void HideCompletions()
    {
        _completionList.Visible = false;
        _completionList.Items.Clear();
        _completions = [];
    }

    private void UpdateCompletionDetails()
    {
        if (_completionList.SelectedItem is not LuaApiDefinition api)
            return;

        _completionList.AccessibleDescription = $"{api.Signature}\n{api.Description}";
        _signatureTip.SetToolTip(_completionList, $"{api.Signature}\n{api.Description}");
    }

    private void UpdateSignatureHelp()
    {
        var caret = _editor.SelectionStart;
        const int contextLimit = 8192;
        var contextStart = Math.Max(0, caret - contextLimit);
        var help = LuaEditorService.GetSignatureHelp(
            _editor.Text.Substring(contextStart, caret - contextStart),
            caret - contextStart,
            _registry);
        if (help?.Parameter is null)
        {
            _signatureTip.Hide(_editor);
            return;
        }

        var message = help.DisplayText + Environment.NewLine + help.Parameter.Description;
        var location = _editor.GetPositionFromCharIndex(_editor.SelectionStart);
        location.Offset(10, _editor.Font.Height + 2);
        _signatureTip.Show(message, _editor, location, 1800);
    }

    private async Task HighlightAsync()
    {
        _highlightTimer.Stop();
        if (_editor.IsDisposed || _editor.TextLength == 0)
        {
            _gutter.Invalidate();
            return;
        }

        var source = _editor.Text;
        var version = _textVersion;
        var tokens = await Task.Run(() => LuaEditorService.Tokenize(source, _registry));
        if (IsDisposed || _editor.IsDisposed || version != _textVersion)
            return;

        ApplySyntaxColors(tokens);
    }

    private void ApplySyntaxColors(IReadOnlyList<LuaEditorToken> tokens)
    {
        if (_editor.IsDisposed || _editor.TextLength == 0)
        {
            _gutter.Invalidate();
            return;
        }

        var selectionStart = _editor.SelectionStart;
        var selectionLength = _editor.SelectionLength;
        _formatting = true;
        try
        {
            _editor.SelectAll();
            _editor.SelectionColor = Color.FromArgb(35, 39, 45);
            foreach (var token in tokens)
            {
                _editor.Select(token.Start, token.Length);
                _editor.SelectionColor = token.Kind switch
                {
                    LuaEditorTokenKind.Keyword => Color.FromArgb(0, 92, 170),
                    LuaEditorTokenKind.String => Color.FromArgb(163, 70, 35),
                    LuaEditorTokenKind.Comment => Color.FromArgb(105, 135, 110),
                    LuaEditorTokenKind.Number => Color.FromArgb(130, 75, 160),
                    LuaEditorTokenKind.Api => Color.FromArgb(20, 120, 105),
                    LuaEditorTokenKind.Operator => Color.FromArgb(85, 90, 100),
                    _ => Color.FromArgb(35, 39, 45)
                };
            }

            _editor.Select(Math.Min(selectionStart, _editor.TextLength),
                Math.Min(selectionLength, Math.Max(0, _editor.TextLength - selectionStart)));
            _editor.SelectionColor = Color.FromArgb(35, 39, 45);
        }
        finally
        {
            _formatting = false;
        }

        _gutter.Invalidate();
    }

    private void PaintLineNumbers(object? sender, PaintEventArgs e)
    {
        var firstChar = _editor.GetCharIndexFromPosition(new Point(0, 0));
        var lastChar = _editor.GetCharIndexFromPosition(new Point(0, Math.Max(0, _editor.ClientSize.Height - 1)));
        var firstLine = Math.Max(0, _editor.GetLineFromCharIndex(firstChar));
        var lastLine = Math.Max(firstLine, _editor.GetLineFromCharIndex(lastChar));
        using var brush = new SolidBrush(Color.FromArgb(115, 125, 138));

        for (var line = firstLine; line <= lastLine + 1; line++)
        {
            var charIndex = _editor.GetFirstCharIndexFromLine(line);
            if (charIndex < 0)
                break;

            var point = _editor.GetPositionFromCharIndex(charIndex);
            var text = (line + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
            TextRenderer.DrawText(e.Graphics, text, _editor.Font,
                new Rectangle(0, point.Y, _gutter.Width - 8, _editor.Font.Height),
                brush.Color, TextFormatFlags.Right | TextFormatFlags.NoPadding);
        }
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _highlightTimer.Stop();
            _highlightTimer.Dispose();
            _signatureTip.Dispose();
        }

        base.Dispose(disposing);
    }

}
