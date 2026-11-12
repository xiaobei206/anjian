using System.Text;
using System.Text.RegularExpressions;
using Anjian.Core.Api;
using NLua;

namespace Anjian.Core.Editor;

public enum LuaEditorTokenKind
{
    Keyword,
    String,
    Comment,
    Number,
    Api,
    Operator
}

public sealed record LuaEditorToken(int Start, int Length, LuaEditorTokenKind Kind)
{
    public int End => Start + Length;
}

public sealed record LuaEditorDiagnostic(int Line, int Column, string Message);

public sealed record LuaApiCompletion(LuaApiDefinition Api, LuaApiCategory Category);

public sealed record LuaApiSignatureHelp(
    LuaApiDefinition Api,
    int ActiveParameter,
    LuaApiParameter? Parameter)
{
    public string DisplayText
    {
        get
        {
            var parameters = Api.Parameters.Select((p, index) =>
                index == ActiveParameter ? $"[{p.Name}]" : p.Name);
            return $"{Api.Name}({string.Join(", ", parameters)}) — {Api.Title}";
        }
    }
}

/// <summary>
/// Editor language services. The API catalogue is always supplied by the runtime registry;
/// this class deliberately contains no second list of public automation functions.
/// </summary>
public static class LuaEditorService
{
    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "and", "break", "do", "else", "elseif", "end", "false", "for", "function",
        "goto", "if", "in", "local", "nil", "not", "or", "repeat", "return",
        "then", "true", "until", "while"
    };

    public static IReadOnlyList<LuaEditorToken> Tokenize(
        string source,
        LuaApiRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(registry);

        var apiNames = registry.Apis
            .Where(api => api.ImplementationStatus == "implemented")
            .Select(api => api.Name)
            .ToHashSet(StringComparer.Ordinal);
        var tokens = new List<LuaEditorToken>();
        var index = 0;

        while (index < source.Length)
        {
            var current = source[index];
            if (char.IsWhiteSpace(current))
            {
                index++;
                continue;
            }

            var start = index;
            if (current == '-' && index + 1 < source.Length && source[index + 1] == '-')
            {
                index += 2;
                if (TryReadLongBracket(source, index, out var contentStart, out var closeLength))
                {
                    var close = FindLongBracketClose(source, contentStart, closeLength);
                    index = close < 0 ? source.Length : close + closeLength;
                }
                else
                {
                    while (index < source.Length && source[index] is not '\r' and not '\n')
                        index++;
                }

                tokens.Add(new LuaEditorToken(start, index - start, LuaEditorTokenKind.Comment));
                continue;
            }

            if (current is '\'' or '"')
            {
                index++;
                while (index < source.Length)
                {
                    if (source[index] == '\\')
                    {
                        index = Math.Min(source.Length, index + 2);
                        continue;
                    }

                    if (source[index++] == current)
                        break;
                }

                tokens.Add(new LuaEditorToken(start, index - start, LuaEditorTokenKind.String));
                continue;
            }

            if (current == '[' && TryReadLongBracket(source, index, out var longContentStart, out var longCloseLength))
            {
                var close = FindLongBracketClose(source, longContentStart, longCloseLength);
                index = close < 0 ? source.Length : close + longCloseLength;
                tokens.Add(new LuaEditorToken(start, index - start, LuaEditorTokenKind.String));
                continue;
            }

            if (IsIdentifierStart(current))
            {
                index++;
                while (index < source.Length && IsIdentifierPart(source[index]))
                    index++;

                var word = source[start..index];
                var kind = Keywords.Contains(word)
                    ? LuaEditorTokenKind.Keyword
                    : apiNames.Contains(word)
                        ? LuaEditorTokenKind.Api
                        : (LuaEditorTokenKind?)null;
                if (kind.HasValue)
                    tokens.Add(new LuaEditorToken(start, index - start, kind.Value));
                continue;
            }

            if (char.IsAsciiDigit(current))
            {
                index++;
                while (index < source.Length &&
                       (char.IsAsciiLetterOrDigit(source[index]) ||
                        source[index] is '.' or '_' or '+' or '-'))
                {
                    if ((source[index] == '+' || source[index] == '-') &&
                        source[index - 1] != 'e' && source[index - 1] != 'E' &&
                        source[index - 1] != 'p' && source[index - 1] != 'P')
                        break;
                    index++;
                }

                tokens.Add(new LuaEditorToken(start, index - start, LuaEditorTokenKind.Number));
                continue;
            }

            if ("+-*/%^#=<>~;:,().{}[]".IndexOf(current) >= 0)
            {
                index++;
                if (index < source.Length &&
                    ((current == '.' && source[index] == '.') ||
                     ((current == '=' || current == '<' || current == '>' || current == '~') &&
                      source[index] == '=')))
                {
                    index++;
                    if (current == '.' && index < source.Length && source[index] == '.')
                        index++;
                }

                tokens.Add(new LuaEditorToken(start, index - start, LuaEditorTokenKind.Operator));
                continue;
            }

            index++;
        }

        return tokens;
    }

    public static bool IsCaretInCommentOrString(string source, int caret)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (caret < 0 || caret > source.Length)
            throw new ArgumentOutOfRangeException(nameof(caret));

        var lineStart = caret == 0 ? 0 : source.LastIndexOfAny(['\r', '\n'], caret - 1) + 1;
        var quote = '\0';
        var escaped = false;

        for (var index = lineStart; index < caret; index++)
        {
            var current = source[index];
            if (quote != '\0')
            {
                if (escaped)
                    escaped = false;
                else if (current == '\\')
                    escaped = true;
                else if (current == quote)
                    quote = '\0';
                continue;
            }

            if (current == '-' && index + 1 < caret && source[index + 1] == '-')
                return true;
            if (current is '\'' or '"')
                quote = current;
        }

        return quote != '\0';
    }

    public static string GetIdentifierPrefix(string source, int caret)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (caret < 0 || caret > source.Length)
            throw new ArgumentOutOfRangeException(nameof(caret));

        var start = caret;
        while (start > 0 && IsIdentifierPart(source[start - 1]))
            start--;

        if (start < caret && !IsIdentifierStart(source[start]))
            return string.Empty;

        return source[start..caret];
    }

    public static IReadOnlyList<LuaApiCompletion> GetCompletions(
        LuaApiRegistry registry,
        string prefix,
        int maxResults = 40)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(prefix);
        if (maxResults < 1)
            throw new ArgumentOutOfRangeException(nameof(maxResults));

        var categories = registry.Categories.ToDictionary(category => category.Id, StringComparer.Ordinal);
        return registry.Apis
            .Where(api => api.ImplementationStatus == "implemented" &&
                          api.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .OrderBy(api => api.Name, StringComparer.OrdinalIgnoreCase)
            .Take(maxResults)
            .Select(api => new LuaApiCompletion(api, categories[api.Category]))
            .ToArray();
    }

    public static LuaApiSignatureHelp? GetSignatureHelp(
        string source,
        int caret,
        LuaApiRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(registry);
        if (caret < 0 || caret > source.Length)
            throw new ArgumentOutOfRangeException(nameof(caret));

        var tokens = Tokenize(source[..caret], registry);
        var openStack = new Stack<int>();
        var unmatchedOpen = -1;

        foreach (var token in tokens)
        {
            var text = source.Substring(token.Start, token.Length);
            if (token.Kind != LuaEditorTokenKind.Operator)
                continue;

            if (text == "(")
                openStack.Push(token.Start);
            else if (text == ")" && openStack.Count > 0)
                openStack.Pop();
        }

        if (openStack.Count == 0)
            return null;

        unmatchedOpen = openStack.Peek();
        var before = tokens.LastOrDefault(token =>
            token.End <= unmatchedOpen && token.Kind is not LuaEditorTokenKind.Comment);
        if (before is null || before.Kind is LuaEditorTokenKind.String or LuaEditorTokenKind.Number)
            return null;

        var name = source.Substring(before.Start, before.Length);
        var api = registry.Apis.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, name, StringComparison.Ordinal));
        if (api is null)
            return null;

        var argumentIndex = 0;
        var nestedDepth = 0;
        foreach (var token in tokens.Where(token => token.Start > unmatchedOpen))
        {
            var text = source.Substring(token.Start, token.Length);
            if (token.Kind != LuaEditorTokenKind.Operator)
                continue;

            if (text is "(" or "[" or "{")
                nestedDepth++;
            else if (text is ")" or "]" or "}")
                nestedDepth = Math.Max(0, nestedDepth - 1);
            else if (text == "," && nestedDepth == 0)
                argumentIndex++;
        }

        return new LuaApiSignatureHelp(
            api,
            argumentIndex,
            argumentIndex < api.Parameters.Count ? api.Parameters[argumentIndex] : null);
    }

    /// <summary>
    /// Compiles the chunk using NLua without invoking it. No script statements are executed.
    /// </summary>
    public static IReadOnlyList<LuaEditorDiagnostic> AnalyzeSyntax(
        string source,
        string chunkName = "editor.lua")
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(chunkName);

        try
        {
            using var lua = new Lua(openLibs: false);
            lua.State.Encoding = Encoding.UTF8;
            using var compiledChunk = lua.LoadString(source, chunkName);
            return Array.Empty<LuaEditorDiagnostic>();
        }
        catch (Exception exception)
        {
            var message = exception.Message;
            var match = Regex.Match(message, @":(?<line>\d+):");
            var line = match.Success && int.TryParse(match.Groups["line"].Value, out var parsedLine)
                ? Math.Max(1, parsedLine)
                : 1;
            return [new LuaEditorDiagnostic(line, 1, message)];
        }
    }

    private static bool IsIdentifierStart(char value) =>
        value == '_' || char.IsAsciiLetter(value);

    private static bool IsIdentifierPart(char value) =>
        value == '_' || char.IsAsciiLetterOrDigit(value);

    private static bool TryReadLongBracket(
        string source,
        int start,
        out int contentStart,
        out int closeLength)
    {
        contentStart = start;
        closeLength = 0;
        if (start >= source.Length || source[start] != '[')
            return false;

        var cursor = start + 1;
        while (cursor < source.Length && source[cursor] == '=')
            cursor++;

        if (cursor >= source.Length || source[cursor] != '[')
            return false;

        var equals = cursor - start - 1;
        contentStart = cursor + 1;
        closeLength = equals + 2;
        return true;
    }

    private static int FindLongBracketClose(string source, int contentStart, int closeLength)
    {
        if (closeLength < 2)
            return -1;

        var close = "]" + new string('=', closeLength - 2) + "]";
        return source.IndexOf(close, contentStart, StringComparison.Ordinal);
    }
}
