using System.Text.Json;
using Anjian.Core.Permissions;

namespace Anjian.Core.Api;

public sealed partial class WorkerLuaApi
{
    public string GetClipboardText()
    {
        var response = RequestSystemApi("clipboard-read-text", null);
        if (string.IsNullOrWhiteSpace(response))
            throw new IOException("剪贴板读取 IPC 没有返回数据。");
        var text = JsonSerializer.Deserialize<string>(response)
            ?? throw new InvalidDataException("剪贴板读取 IPC 返回了无效文本。");
        if (text.Length > ScriptClipboardDialogPolicy.MaximumClipboardTextLength)
            throw new InvalidDataException("剪贴板文本超过允许的长度上限。");
        return text;
    }

    public bool SetClipboardText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return ParseSystemApiBoolean(RequestSystemApi("clipboard-write-text", text), "SetClipboardText");
    }

    public bool MessageBox(string text, string title)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(title);
        return ParseSystemApiBoolean(
            RequestDialog("dialog-message", new ScriptDialogRequest(text, title)), "MessageBox");
    }

    public string? InputBox(string prompt, string title, string defaultValue)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(defaultValue);
        var response = RequestDialog("dialog-input", new ScriptDialogRequest(prompt, title, defaultValue));
        if (string.IsNullOrWhiteSpace(response))
            throw new IOException("InputBox IPC 没有返回数据。");
        return JsonSerializer.Deserialize<string?>(response);
    }

    public bool Confirm(string text, string title)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(title);
        return ParseSystemApiBoolean(
            RequestDialog("dialog-confirm", new ScriptDialogRequest(text, title)), "Confirm");
    }

    private string? RequestDialog(string command, ScriptDialogRequest request) =>
        RequestSystemApi(command, JsonSerializer.Serialize(request));

    private string? RequestSystemApi(string command, string? payload)
    {
        // Enforce the capability and payload contract before emitting IPC. The host repeats
        // the same validation independently and never trusts this worker-side check.
        _ = ScriptClipboardDialogPolicy.ValidateCommand(Permissions, command, payload);
        return pipe.Request(command, text: payload);
    }

    private static bool ParseSystemApiBoolean(string? response, string apiName)
    {
        if (!bool.TryParse(response, out var result))
            throw new InvalidDataException($"{apiName} IPC 返回了无效布尔值。");
        return result;
    }
}
