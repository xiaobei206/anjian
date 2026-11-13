using System.Text.Json;
using Anjian.Core.Permissions;

namespace Anjian.App.Services;

/// <summary>
/// Runs clipboard and user-dialog operations on the owning WinForms UI thread (STA).
/// Custom modal forms allow Stop to close a pending dialog instead of blocking forever.
/// </summary>
internal sealed class ClipboardDialogHost(Control owner)
{
    private readonly Control _owner = owner ?? throw new ArgumentNullException(nameof(owner));

    public async Task<string?> DispatchAsync(
        ScriptClipboardDialogCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();

        switch (command.Command)
        {
            case "clipboard-read-text":
            {
                var text = await InvokeOnUiAsync(() =>
                {
                    if (!Clipboard.ContainsText(TextDataFormat.UnicodeText) &&
                        !Clipboard.ContainsText(TextDataFormat.Text))
                        return string.Empty;

                    var value = Clipboard.GetText(TextDataFormat.UnicodeText);
                    if (value.Length > ScriptClipboardDialogPolicy.MaximumClipboardTextLength)
                        throw new InvalidDataException(
                            $"剪贴板文本超过 {ScriptClipboardDialogPolicy.MaximumClipboardTextLength} 个字符的上限。");
                    return value;
                }, cancellationToken).ConfigureAwait(false);
                return JsonSerializer.Serialize(text);
            }

            case "clipboard-write-text":
            {
                var text = command.Text ?? throw new InvalidDataException("剪贴板写入请求缺少文本。");
                await InvokeOnUiAsync(() =>
                {
                    if (text.Length == 0)
                        Clipboard.Clear();
                    else
                        Clipboard.SetText(text, TextDataFormat.UnicodeText);
                    return true;
                }, cancellationToken).ConfigureAwait(false);
                return JsonSerializer.Serialize(true);
            }

            case "dialog-message":
            {
                var result = await ShowDialogOnUiAsync(dialog =>
                {
                    BuildMessageDialog(dialog, command.Text!, command.Title!, confirm: false);
                    return dialog.ShowDialog(_owner) == DialogResult.OK;
                }, cancellationToken).ConfigureAwait(false);
                return JsonSerializer.Serialize(result);
            }

            case "dialog-confirm":
            {
                var result = await ShowDialogOnUiAsync(dialog =>
                {
                    BuildMessageDialog(dialog, command.Text!, command.Title!, confirm: true);
                    return dialog.ShowDialog(_owner) == DialogResult.Yes;
                }, cancellationToken).ConfigureAwait(false);
                return JsonSerializer.Serialize(result);
            }

            case "dialog-input":
            {
                var result = await ShowDialogOnUiAsync(dialog =>
                {
                    BuildInputDialog(dialog, command.Text!, command.Title!, command.DefaultValue!);
                    return dialog.ShowDialog(_owner) == DialogResult.OK
                        ? FindInputTextBox(dialog).Text
                        : null;
                }, cancellationToken).ConfigureAwait(false);
                return JsonSerializer.Serialize(result);
            }

            default:
                throw new InvalidDataException($"未知剪贴板/对话框命令：{command.Command}");
        }
    }

    private Task<T> InvokeOnUiAsync<T>(Func<T> action, CancellationToken cancellationToken) =>
        InvokeOnUiAsync(action, cancellationToken, cancelActiveDialog: null);

    private Task<T> ShowDialogOnUiAsync<T>(Func<Form, T> action, CancellationToken cancellationToken)
    {
        Form? activeDialog = null;
        return InvokeOnUiAsync(() =>
        {
            using var dialog = new Form
            {
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                ShowInTaskbar = false,
                MinimizeBox = false,
                MaximizeBox = false,
                AutoScaleMode = AutoScaleMode.Dpi
            };
            activeDialog = dialog;
            try { return action(dialog); }
            finally { activeDialog = null; }
        }, cancellationToken, () =>
        {
            if (activeDialog is { IsDisposed: false })
                activeDialog.Close();
        });
    }

    private Task<T> InvokeOnUiAsync<T>(
        Func<T> action,
        CancellationToken cancellationToken,
        Action? cancelActiveDialog)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_owner.IsDisposed || !_owner.IsHandleCreated)
            throw new InvalidOperationException("主窗口尚未就绪，无法执行剪贴板或对话框操作。");

        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationTokenRegistration registration = default;
        registration = cancellationToken.Register(() =>
        {
            completion.TrySetCanceled(cancellationToken);
            try
            {
                if (!_owner.IsDisposed && _owner.IsHandleCreated)
                    _owner.BeginInvoke((Action)(() => cancelActiveDialog?.Invoke()));
            }
            catch (InvalidOperationException) { }
        });

        try
        {
            _owner.BeginInvoke((Action)(() =>
            {
                try
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        completion.TrySetCanceled(cancellationToken);
                        return;
                    }

                    var result = action();
                    if (cancellationToken.IsCancellationRequested)
                        completion.TrySetCanceled(cancellationToken);
                    else
                        completion.TrySetResult(result);
                }
                catch (Exception ex)
                {
                    completion.TrySetException(ex);
                }
                finally
                {
                    registration.Dispose();
                }
            }));
        }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException)
        {
            registration.Dispose();
            completion.TrySetException(new InvalidOperationException("无法向主 UI 线程调度剪贴板/对话框操作。", ex));
        }

        return completion.Task;
    }

    private static void BuildMessageDialog(Form dialog, string text, string title, bool confirm)
    {
        dialog.Text = title;
        dialog.ClientSize = new Size(560, 260);

        var message = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Dock = DockStyle.Fill,
            Text = text,
            TabStop = true
        };
        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 48,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(8, 6, 8, 4),
            WrapContents = false
        };

        if (confirm)
        {
            var yes = new Button { Text = "是", AutoSize = true, DialogResult = DialogResult.Yes };
            var no = new Button { Text = "否", AutoSize = true, DialogResult = DialogResult.No };
            buttons.Controls.Add(no);
            buttons.Controls.Add(yes);
            dialog.AcceptButton = yes;
            dialog.CancelButton = no;
        }
        else
        {
            var ok = new Button { Text = "确定", AutoSize = true, DialogResult = DialogResult.OK };
            buttons.Controls.Add(ok);
            dialog.AcceptButton = ok;
            dialog.CancelButton = ok;
        }

        dialog.Controls.Add(message);
        dialog.Controls.Add(buttons);
    }

    private static void BuildInputDialog(Form dialog, string prompt, string title, string defaultValue)
    {
        dialog.Text = title;
        dialog.ClientSize = new Size(560, 300);
        var promptBox = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Dock = DockStyle.Fill,
            Text = prompt,
            TabStop = false
        };
        var input = new TextBox
        {
            Name = "AnjianInputBoxValue",
            Dock = DockStyle.Top,
            MaxLength = ScriptClipboardDialogPolicy.MaximumInputTextLength,
            Text = defaultValue
        };
        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 48,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(8, 6, 8, 4),
            WrapContents = false
        };
        var ok = new Button { Text = "确定", AutoSize = true, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "取消", AutoSize = true, DialogResult = DialogResult.Cancel };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(ok);
        dialog.Controls.Add(promptBox);
        dialog.Controls.Add(input);
        dialog.Controls.Add(buttons);
        dialog.AcceptButton = ok;
        dialog.CancelButton = cancel;
        dialog.Shown += (_, _) => { input.Focus(); input.SelectAll(); };
    }

    private static TextBox FindInputTextBox(Control root) =>
        root.Controls.Find("AnjianInputBoxValue", searchAllChildren: true).OfType<TextBox>().Single();
}
