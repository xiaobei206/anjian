using Anjian.Core.Api;
using Anjian.Core.Editor;
using Xunit;

namespace Anjian.Core.Tests;

public sealed class LuaEditorServiceTests
{
    private readonly LuaApiRegistry _registry = LuaApiRegistry.Load();

    [Fact]
    public void TokenizerRecognizesLuaSyntaxAndOnlyRegisteredApis()
    {
        const string source = """
            -- 中文注释
            local label = "日文テスト"
            MoveTo(10, 20)
            FindWindow("not implemented")
            """;

        var tokens = LuaEditorService.Tokenize(source, _registry);

        Assert.Contains(tokens, token => token.Kind == LuaEditorTokenKind.Comment);
        Assert.Contains(tokens, token => token.Kind == LuaEditorTokenKind.String);
        Assert.Contains(tokens, token => token.Kind == LuaEditorTokenKind.Keyword &&
                                          source.Substring(token.Start, token.Length) == "local");
        Assert.Contains(tokens, token => token.Kind == LuaEditorTokenKind.Api &&
                                          source.Substring(token.Start, token.Length) == "MoveTo");
        Assert.DoesNotContain(tokens, token => source.Substring(token.Start, token.Length) == "FindWindow");
    }

    [Fact]
    public void CompletionCandidatesComeOnlyFromTheRuntimeRegistry()
    {
        var candidates = LuaEditorService.GetCompletions(_registry, "Left");

        Assert.Equal(
            _registry.Apis.Where(api => api.Name.StartsWith("Left", StringComparison.OrdinalIgnoreCase))
                .Select(api => api.Name).Order(StringComparer.Ordinal),
            candidates.Select(candidate => candidate.Api.Name).Order(StringComparer.Ordinal));
        Assert.NotEmpty(candidates);
        Assert.All(candidates, candidate => Assert.Equal("implemented", candidate.Api.ImplementationStatus));
        Assert.Empty(LuaEditorService.GetCompletions(_registry, "FindWindow"));
    }

    [Fact]
    public void SignatureHelpFindsTheActiveParameterAndIgnoresNestedCommas()
    {
        const string source = "LeftDoubleClick(50, Delay(10, 20), \"a,b\"";

        var help = LuaEditorService.GetSignatureHelp(source, source.Length, _registry);

        Assert.NotNull(help);
        Assert.Equal("LeftDoubleClick", help.Api.Name);
        Assert.Equal(1, help.ActiveParameter);
        Assert.Equal("gapMs", help.Parameter?.Name);
    }

    [Fact]
    public void SyntaxAnalysisCompilesButDoesNotExecuteLua()
    {
        const string source = "Log('this must not run')";

        var diagnostics = LuaEditorService.AnalyzeSyntax(source);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void SyntaxAnalysisReturnsLineForInvalidLua()
    {
        const string source = """
            local value = 1
            if value > 0 then
                Log("missing end")
            """;

        var diagnostics = LuaEditorService.AnalyzeSyntax(source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.InRange(diagnostic.Line, 1, 3);
        Assert.False(string.IsNullOrWhiteSpace(diagnostic.Message));
    }

    [Fact]
    public void PrefixExtractionStopsAtIdentifierBoundary()
    {
        Assert.Equal("MoveT", LuaEditorService.GetIdentifierPrefix("MoveT(1, 2)", 5));
        Assert.Equal(string.Empty, LuaEditorService.GetIdentifierPrefix("MoveT", 0));
        Assert.Equal("Left", LuaEditorService.GetIdentifierPrefix("Left", 4));
    }

    [Fact]
    public void TokenizerHandlesLargeMixedLanguageInput()
    {
        var source = string.Concat(Enumerable.Repeat(
            "local 标题 = \"文本\" -- 注释\nMoveTo(1, 2)\n", 30_000));

        var tokens = LuaEditorService.Tokenize(source, _registry);

        Assert.Contains(tokens, token => token.Kind == LuaEditorTokenKind.Api);
        Assert.Contains(tokens, token => token.Kind == LuaEditorTokenKind.Comment);
    }
}
