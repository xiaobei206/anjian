using Anjian.Core.Permissions;

namespace Anjian.App.Controls;

internal sealed class ScriptPermissionReviewForm : Form
{
    private enum ChoiceKind
    {
        FileReadRoot,
        FileWriteRoot,
        ClipboardRead,
        ClipboardWrite,
        Dialog,
        ProcessExecutable,
        HttpHost,
        HttpMethod,
        InsecureHttp
    }

    private sealed record Choice(ChoiceKind Kind, string Value, CheckBox Control);
    private readonly List<Choice> _choices = [];

    public ScriptPermissionApproval Approval => new()
    {
        FileReadRoots = Selected(ChoiceKind.FileReadRoot),
        FileWriteRoots = Selected(ChoiceKind.FileWriteRoot),
        ClipboardRead = IsSelected(ChoiceKind.ClipboardRead),
        ClipboardWrite = IsSelected(ChoiceKind.ClipboardWrite),
        Dialogs = Selected(ChoiceKind.Dialog),
        ProcessExecutables = Selected(ChoiceKind.ProcessExecutable),
        HttpHosts = Selected(ChoiceKind.HttpHost),
        HttpMethods = Selected(ChoiceKind.HttpMethod),
        AllowInsecureHttp = IsSelected(ChoiceKind.InsecureHttp)
    };

    public ScriptPermissionReviewForm(ScriptPermissionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        Text = "脚本权限审阅（仅本次运行）";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(560, 420);
        Size = new Size(720, 680);
        MinimizeBox = false;
        MaximizeBox = true;

        var intro = new Label
        {
            Dock = DockStyle.Top,
            AutoSize = false,
            Height = 58,
            Padding = new Padding(12, 10, 12, 6),
            Text = "脚本声明了以下能力请求。声明本身不会获得权限；请逐项选择本次运行允许的资源。所有项目默认拒绝，关闭窗口也会取消运行。"
        };

        var content = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(10, 4, 10, 8) };
        var stack = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false
        };
        content.Controls.Add(stack);

        AddGroup(stack, "文件读取根目录", request.FileReadRoots, ChoiceKind.FileReadRoot, value => value);
        AddGroup(stack, "文件写入根目录", request.FileWriteRoots, ChoiceKind.FileWriteRoot, value => value);

        var clipboard = new List<(ChoiceKind Kind, string Label)>();
        if (request.ClipboardRead) clipboard.Add((ChoiceKind.ClipboardRead, "读取系统剪贴板文本"));
        if (request.ClipboardWrite) clipboard.Add((ChoiceKind.ClipboardWrite, "写入系统剪贴板文本"));
        AddGroup(stack, "剪贴板", clipboard, item => item.Label, item => item.Kind, _ => "");

        AddGroup(stack, "对话框", request.Dialogs, ChoiceKind.Dialog, value => value switch
        {
            "message" => "显示消息",
            "input" => "请求用户输入",
            "confirm" => "请求用户确认",
            _ => value
        });
        AddGroup(stack, "允许启动的可执行文件", request.ProcessExecutables,
            ChoiceKind.ProcessExecutable, value => value);
        AddGroup(stack, "允许访问的精确 HTTP 主机", request.HttpHosts, ChoiceKind.HttpHost, value => value);
        AddGroup(stack, "允许的 HTTP 方法", request.HttpMethods, ChoiceKind.HttpMethod, value => value);
        if (request.AllowInsecureHttp)
            AddGroup(stack, "网络附加选项",
                new[] { (ChoiceKind.InsecureHttp, "允许明文 HTTP（不加密）") },
                item => item.Item2, item => item.Item1, _ => "");

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 50,
            Padding = new Padding(8),
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };
        var approve = new Button { Text = "批准所选并运行", AutoSize = true, DialogResult = DialogResult.OK };
        var deny = new Button { Text = "拒绝并取消运行", AutoSize = true, DialogResult = DialogResult.Cancel };
        buttons.Controls.Add(deny);
        buttons.Controls.Add(approve);

        Controls.Add(content);
        Controls.Add(buttons);
        Controls.Add(intro);
        AcceptButton = approve;
        CancelButton = deny;
    }

    private void AddGroup(
        FlowLayoutPanel parent,
        string title,
        IEnumerable<string> values,
        ChoiceKind kind,
        Func<string, string> label)
    {
        AddGroup(parent, title, values.Select(value => (kind, value)), item => label(item.value),
            item => item.kind, item => item.value);
    }

    private void AddGroup<T>(
        FlowLayoutPanel parent,
        string title,
        IEnumerable<T> values,
        Func<T, string> label,
        Func<T, ChoiceKind> kind,
        Func<T, string> value)
    {
        var items = values.ToArray();
        if (items.Length == 0)
            return;

        var group = new GroupBox
        {
            Text = title,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Width = 650,
            Padding = new Padding(10, 8, 10, 8),
            Margin = new Padding(3, 4, 3, 4)
        };
        var layout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false
        };

        foreach (var item in items)
        {
            var check = new CheckBox
            {
                Text = label(item),
                AutoSize = true,
                MaximumSize = new Size(610, 0),
                Margin = new Padding(4, 3, 4, 3),
                Checked = false
            };
            _choices.Add(new Choice(kind(item), value(item), check));
            layout.Controls.Add(check);
        }

        group.Controls.Add(layout);
        parent.Controls.Add(group);
    }

    private List<string> Selected(ChoiceKind kind) =>
        _choices.Where(x => x.Kind == kind && x.Control.Checked)
            .Select(x => x.Value).ToList();

    private bool IsSelected(ChoiceKind kind) =>
        _choices.Any(x => x.Kind == kind && x.Control.Checked);
}
