using System.Security.Cryptography;
using System.Text;

namespace Anjian.Core.Recorder;

public sealed record RecorderSyncUpdate(
    bool Success,
    bool RequiresConfirmation,
    string? Error,
    string ScriptText,
    RecorderDocument Document,
    string ExistingBlock,
    string ReplacementBlock);

public static class RecorderLuaGenerator
{
    private const string StartPrefix = "-- <anjian:generated id=\"";
    private const string EndMarker = "-- </anjian:generated>";
    private const long MaximumDelayPerCall = 600_000;

    public static string BuildBlock(RecorderDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        document.Validate();

        var output = new StringBuilder();
        AppendLine(output, $"-- <anjian:generated id=\"{document.GeneratedBlockId}\" schema=\"{document.SchemaVersion}\">");
        AppendLine(output, $"-- recorderVersion={document.RecorderVersion}; coordinateSpace={document.CoordinateSpace}");
        AppendLine(output, "-- 由动作列表生成；手工修改此区块后会提示与动作数据脱离同步。");

        string? previousGroup = null;
        foreach (var action in document.Actions)
        {
            AppendLine(output, $"-- <anjian:action id=\"{action.ActionId}\" kind=\"{action.Kind.ToString().ToLowerInvariant()}\">");
            if (!string.IsNullOrWhiteSpace(action.Group) &&
                !string.Equals(previousGroup, action.Group, StringComparison.Ordinal))
                AppendLine(output, "-- 分组：" + SanitizeComment(action.Group));
            previousGroup = action.Group;

            if (!string.IsNullOrWhiteSpace(action.Note))
                AppendLine(output, "-- 注释：" + SanitizeComment(action.Note));

            if (!action.Enabled)
            {
                AppendLine(output, "-- [已禁用] " + SanitizeComment(Describe(action)));
                continue;
            }

            AppendDelay(output, action.DelayMs);
            foreach (var statement in GetStatements(action))
                AppendLine(output, statement);
        }

        AppendLine(output, EndMarker);
        return output.ToString();
    }

    public static string BuildActionSnippet(RecorderAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (!action.Enabled)
            throw new InvalidOperationException("已禁用的动作不能试运行。");

        var output = new StringBuilder();
        AppendDelay(output, action.DelayMs);
        foreach (var statement in GetStatements(action))
            AppendLine(output, statement);
        return output.ToString();
    }

    public static RecorderDocument Inspect(string scriptText, RecorderDocument document)
    {
        ArgumentNullException.ThrowIfNull(scriptText);
        ArgumentNullException.ThrowIfNull(document);

        var found = FindBlock(scriptText, document.GeneratedBlockId);
        if (found.Error is not null)
            return document with { SynchronizationState = RecorderSynchronizationState.Detached };
        if (found.Block is null)
            return document with { SynchronizationState = RecorderSynchronizationState.Missing };

        var hash = Hash(found.Block);
        if (!string.IsNullOrWhiteSpace(document.GeneratedBlockHash) &&
            string.Equals(hash, document.GeneratedBlockHash, StringComparison.Ordinal))
        {
            return document with
            {
                SynchronizationState = document.SynchronizationState == RecorderSynchronizationState.Pending
                    ? RecorderSynchronizationState.Pending
                    : RecorderSynchronizationState.Synchronized
            };
        }

        return document with { SynchronizationState = RecorderSynchronizationState.Detached };
    }

    public static RecorderSyncUpdate PrepareUpdate(
        string scriptText,
        RecorderDocument document,
        bool confirmDetachedReplacement = false)
    {
        ArgumentNullException.ThrowIfNull(scriptText);
        ArgumentNullException.ThrowIfNull(document);
        document.Validate();

        var replacement = BuildBlock(document);
        var found = FindBlock(scriptText, document.GeneratedBlockId);
        if (found.Error is not null)
            return new RecorderSyncUpdate(false, false, found.Error, scriptText, document, found.Block ?? string.Empty, replacement);

        if (found.Block is not null)
        {
            var currentHash = Hash(found.Block);
            var isUnmodified = !string.IsNullOrWhiteSpace(document.GeneratedBlockHash) &&
                               string.Equals(currentHash, document.GeneratedBlockHash, StringComparison.Ordinal);
            if (!isUnmodified && !confirmDetachedReplacement)
            {
                var detached = document with { SynchronizationState = RecorderSynchronizationState.Detached };
                return new RecorderSyncUpdate(
                    false, true, null, scriptText, detached, found.Block, replacement);
            }

            var updatedText = scriptText[..found.Start] + replacement + scriptText[found.End..];
            var updatedDocument = document with
            {
                GeneratedBlockHash = Hash(replacement),
                SynchronizationState = RecorderSynchronizationState.Synchronized
            };
            return new RecorderSyncUpdate(true, false, null, updatedText, updatedDocument, found.Block, replacement);
        }

        if (!string.IsNullOrWhiteSpace(document.GeneratedBlockHash) && !confirmDetachedReplacement)
        {
            var missing = document with { SynchronizationState = RecorderSynchronizationState.Missing };
            return new RecorderSyncUpdate(
                false, true, null, scriptText, missing, string.Empty, replacement);
        }

        var separator = scriptText.Length == 0 || scriptText.EndsWith('\n') ? string.Empty : "\n";
        var appendedText = scriptText + separator + replacement;
        var appendedDocument = document with
        {
            GeneratedBlockHash = Hash(replacement),
            SynchronizationState = RecorderSynchronizationState.Synchronized
        };
        return new RecorderSyncUpdate(true, false, null, appendedText, appendedDocument, string.Empty, replacement);
    }

    public static string GetHash(string text) => Hash(text);

    public static string Describe(RecorderAction action) => action.Kind switch
    {
        RecorderActionKind.MouseMove => $"鼠标移动 ({action.X}, {action.Y})",
        RecorderActionKind.MouseClick => $"鼠标点击 {action.Name} ({action.X}, {action.Y})",
        RecorderActionKind.MouseDown => $"鼠标按下 {action.Name}",
        RecorderActionKind.MouseUp => $"鼠标释放 {action.Name}",
        RecorderActionKind.MouseWheel => $"滚轮 {action.Delta}",
        RecorderActionKind.KeyPress => $"按键 {action.Name}",
        RecorderActionKind.KeyDown => $"按键按下 {action.Name}",
        RecorderActionKind.KeyUp => $"按键释放 {action.Name}",
        RecorderActionKind.TypeText => "输入文本",
        _ => action.Kind.ToString()
    };

    private static IEnumerable<string> GetStatements(RecorderAction action)
    {
        var name = action.Name ?? string.Empty;
        switch (action.Kind)
        {
            case RecorderActionKind.MouseMove:
                yield return $"MoveTo({action.X}, {action.Y})";
                break;
            case RecorderActionKind.MouseClick:
                yield return $"MoveTo({action.X}, {action.Y})";
                yield return $"{ClickFunction(name)}({ValidateMilliseconds(action.HoldMs, nameof(action.HoldMs))})";
                break;
            case RecorderActionKind.MouseDown:
                yield return $"MoveTo({action.X}, {action.Y})";
                yield return $"{ButtonFunction(name, down: true)}()";
                break;
            case RecorderActionKind.MouseUp:
                yield return $"MoveTo({action.X}, {action.Y})";
                yield return $"{ButtonFunction(name, down: false)}()";
                break;
            case RecorderActionKind.MouseWheel:
                yield return $"MoveTo({action.X}, {action.Y})";
                yield return $"MouseWheel({action.Delta})";
                break;
            case RecorderActionKind.KeyPress:
                yield return $"KeyPress(\"{EscapeLuaString(name)}\", {ValidateMilliseconds(action.HoldMs, nameof(action.HoldMs))})";
                break;
            case RecorderActionKind.KeyDown:
                yield return $"KeyDown(\"{EscapeLuaString(name)}\")";
                break;
            case RecorderActionKind.KeyUp:
                yield return $"KeyUp(\"{EscapeLuaString(name)}\")";
                break;
            case RecorderActionKind.TypeText:
                yield return $"TypeText(\"{EscapeLuaString(action.Text ?? string.Empty)}\")";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(action), action.Kind, "不支持的动作类型。");
        }
    }

    private static string ClickFunction(string button) => button.ToLowerInvariant() switch
    {
        "left" => "LeftClick",
        "right" => "RightClick",
        "middle" => "MiddleClick",
        _ => throw new InvalidDataException($"未知鼠标按键：{button}")
    };

    private static string ButtonFunction(string button, bool down) => button.ToLowerInvariant() switch
    {
        "left" => down ? "LeftDown" : "LeftUp",
        "right" => down ? "RightDown" : "RightUp",
        "middle" => down ? "MiddleDown" : "MiddleUp",
        _ => throw new InvalidDataException($"未知鼠标按键：{button}")
    };

    private static long ValidateMilliseconds(long value, string name)
    {
        if (value < 0 || value > MaximumDelayPerCall)
            throw new ArgumentOutOfRangeException(name, $"毫秒值必须在 0–{MaximumDelayPerCall} 之间。");
        return value;
    }

    private static void AppendDelay(StringBuilder output, long delayMs)
    {
        if (delayMs < 0)
            throw new InvalidDataException("动作延时不能为负数.");

        if (delayMs <= MaximumDelayPerCall * 10)
        {
            while (delayMs > 0)
            {
                var part = Math.Min(delayMs, MaximumDelayPerCall);
                AppendLine(output, $"Delay({part})");
                delayMs -= part;
            }
            return;
        }

        // Keep very long idle intervals compact: the scoped local cannot leak into
        // or shadow user Lua code following the generated block.
        AppendLine(output, "do");
        AppendLine(output, $"    local __anjian_delay_remaining = {delayMs}");
        AppendLine(output, $"    while __anjian_delay_remaining > {MaximumDelayPerCall} do");
        AppendLine(output, $"        Delay({MaximumDelayPerCall})");
        AppendLine(output, $"        __anjian_delay_remaining = __anjian_delay_remaining - {MaximumDelayPerCall}");
        AppendLine(output, "    end");
        AppendLine(output, "    if __anjian_delay_remaining > 0 then Delay(__anjian_delay_remaining) end");
        AppendLine(output, "end");
    }

    private static void AppendLine(StringBuilder output, string value) => output.Append(value).Append('\n');

    private static string EscapeLuaString(string value)
    {
        var output = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            switch (character)
            {
                case '\\': output.Append("\\\\"); break;
                case '"': output.Append("\\\""); break;
                case '\n': output.Append("\\n"); break;
                case '\r': output.Append("\\r"); break;
                case '\t': output.Append("\\t"); break;
                default:
                    if (char.IsControl(character))
                        output.Append('\\').Append(((int)character).ToString("D3", System.Globalization.CultureInfo.InvariantCulture));
                    else
                        output.Append(character);
                    break;
            }
        }
        return output.ToString();
    }

    private static string SanitizeComment(string value) =>
        value.Replace('\r', ' ').Replace('\n', ' ').Replace("--", "—", StringComparison.Ordinal);

    private static string Hash(string value)
    {
        var normalized = value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized))).ToLowerInvariant();
    }

    private static (int Start, int End, string? Block, string? Error) FindBlock(string scriptText, string blockId)
    {
        var marker = StartPrefix + blockId + "\" schema=\"";
        var index = scriptText.IndexOf(marker, StringComparison.Ordinal);
        if (index < 0)
            return (-1, -1, null, null);

        var next = scriptText.IndexOf(marker, index + marker.Length, StringComparison.Ordinal);
        if (next >= 0)
            return (-1, -1, null, "脚本中存在多个相同 generated block ID，拒绝自动覆盖。");

        var lineEnd = scriptText.IndexOf('\n', index);
        if (lineEnd < 0)
            return (-1, -1, null, "generated block 起始标记不完整，拒绝自动覆盖。");

        var closeStart = scriptText.IndexOf(EndMarker, lineEnd + 1, StringComparison.Ordinal);
        if (closeStart < 0)
            return (-1, -1, null, "generated block 缺少结束标记，拒绝自动覆盖。");

        var end = closeStart + EndMarker.Length;
        if (end < scriptText.Length && scriptText[end] == '\r') end++;
        if (end < scriptText.Length && scriptText[end] == '\n') end++;
        return (index, end, scriptText[index..end], null);
    }
}