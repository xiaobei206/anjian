using System.Text.Json;

namespace Anjian.Core.Permissions;

/// <summary>Size and command policy shared by the Lua bridge and the independent host IPC dispatcher.</summary>
public static class ScriptClipboardDialogPolicy
{
    public const int MaximumClipboardTextLength = 1_048_576;
    public const int MaximumDialogTextLength = 32_768;
    public const int MaximumDialogTitleLength = 256;
    public const int MaximumInputTextLength = 32_768;
    public const string DefaultTitle = "按键精灵 2026";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static ScriptClipboardDialogCommand ValidateCommand(
        ScriptPermissionGrant? grant,
        string command,
        string? payload)
    {
        grant ??= ScriptPermissionGrant.Empty;
        ArgumentException.ThrowIfNullOrWhiteSpace(command);

        switch (command)
        {
            case "clipboard-read-text":
                Demand(grant.AllowsClipboardRead, "脚本没有获得本次运行的剪贴板读取权限。");
                if (payload is not null)
                    throw new InvalidDataException("剪贴板读取命令不接受额外载荷。");
                return new ScriptClipboardDialogCommand(command);

            case "clipboard-write-text":
                Demand(grant.AllowsClipboardWrite, "脚本没有获得本次运行的剪贴板写入权限。");
                if (payload is null)
                    throw new InvalidDataException("SetClipboardText 的文本载荷不能为空值；请传入空字符串以清空剪贴板文本。");
                ValidateLength(payload, MaximumClipboardTextLength, "剪贴板文本");
                return new ScriptClipboardDialogCommand(command, Text: payload);

            case "dialog-message":
                return ValidateDialog(grant, command, "message", payload, needsDefaultValue: false);

            case "dialog-input":
                return ValidateDialog(grant, command, "input", payload, needsDefaultValue: true);

            case "dialog-confirm":
                return ValidateDialog(grant, command, "confirm", payload, needsDefaultValue: false);

            default:
                throw new InvalidDataException($"未知剪贴板/对话框 IPC 命令：{command}");
        }
    }

    private static ScriptClipboardDialogCommand ValidateDialog(
        ScriptPermissionGrant grant,
        string command,
        string kind,
        string? payload,
        bool needsDefaultValue)
    {
        Demand(grant.AllowsDialog(kind), $"脚本没有获得本次“{kind}”对话框权限。");
        if (string.IsNullOrWhiteSpace(payload))
            throw new InvalidDataException("对话框 IPC 缺少请求载荷。");

        ScriptDialogRequest request;
        try
        {
            request = JsonSerializer.Deserialize<ScriptDialogRequest>(payload, JsonOptions)
                ?? throw new InvalidDataException("对话框请求为空。");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("对话框请求不是有效 JSON。", ex);
        }

        if (request.Text is null)
            throw new InvalidDataException("对话框文本不能为空值。");
        if (request.Title is null)
            throw new InvalidDataException("对话框标题不能为空值。");
        ValidateLength(request.Text, MaximumDialogTextLength, "对话框文本");
        ValidateLength(request.Title, MaximumDialogTitleLength, "对话框标题");

        if (needsDefaultValue)
        {
            if (request.DefaultValue is null)
                throw new InvalidDataException("InputBox 缺少默认文本值。");
            ValidateLength(request.DefaultValue, MaximumInputTextLength, "InputBox 默认值");
        }
        else if (request.DefaultValue is not null)
        {
            throw new InvalidDataException("此对话框类型不接受默认输入值。");
        }

        return new ScriptClipboardDialogCommand(command, request.Text, request.Title, request.DefaultValue);
    }

    private static void ValidateLength(string value, int maximum, string label)
    {
        if (value.Length > maximum)
            throw new InvalidDataException($"{label}超过 {maximum} 个 UTF-16 字符的上限。");
    }

    private static void Demand(bool allowed, string message)
    {
        if (!allowed)
            throw new UnauthorizedAccessException(message);
    }
}

public sealed record ScriptDialogRequest(string Text, string Title, string? DefaultValue = null);

public sealed record ScriptClipboardDialogCommand(
    string Command,
    string? Text = null,
    string? Title = null,
    string? DefaultValue = null);
