using System.Text.Json;
using Anjian.Core.Permissions;
using Xunit;

namespace Anjian.Core.Tests;

public sealed class ScriptClipboardDialogPolicyTests
{
    [Fact]
    public void HostPolicyDeniesMissingOrForgedCapabilities()
    {
        Assert.Throws<UnauthorizedAccessException>(() => ScriptClipboardDialogPolicy.ValidateCommand(ScriptPermissionGrant.Empty, "clipboard-read-text", null));
        Assert.Throws<UnauthorizedAccessException>(() => ScriptClipboardDialogPolicy.ValidateCommand(ScriptPermissionGrant.Empty, "clipboard-write-text", "text"));
        Assert.Throws<UnauthorizedAccessException>(() => ScriptClipboardDialogPolicy.ValidateCommand(ScriptPermissionGrant.Empty, "dialog-message", JsonSerializer.Serialize(new ScriptDialogRequest("hello", "title"))));
        Assert.Throws<UnauthorizedAccessException>(() => ScriptClipboardDialogPolicy.ValidateCommand(new ScriptPermissionGrant([], [], true, false, [], [], [], [], false), "dialog-message", JsonSerializer.Serialize(new ScriptDialogRequest("hello", "title"))));
        Assert.Throws<InvalidDataException>(() => ScriptClipboardDialogPolicy.ValidateCommand(ScriptPermissionGrant.Empty, "clipboard-clear", null));
    }

    [Fact]
    public void ApprovedClipboardAndDialogRequestsAreNormalized()
    {
        var grant = new ScriptPermissionGrant([], [], true, true, ["message", "input", "confirm"], [], [], [], false);
        Assert.Equal("clipboard-read-text", ScriptClipboardDialogPolicy.ValidateCommand(grant, "clipboard-read-text", null).Command);
        Assert.Equal("hello", ScriptClipboardDialogPolicy.ValidateCommand(grant, "clipboard-write-text", "hello").Text);
        var input = ScriptClipboardDialogPolicy.ValidateCommand(grant, "dialog-input", JsonSerializer.Serialize(new ScriptDialogRequest("prompt", "title", "default")));
        Assert.Equal("prompt", input.Text);
        Assert.Equal("default", input.DefaultValue);
        Assert.Equal("continue?", ScriptClipboardDialogPolicy.ValidateCommand(grant, "dialog-confirm", JsonSerializer.Serialize(new ScriptDialogRequest("continue?", "title"))).Text);
    }

    [Fact]
    public void HostPolicyRejectsOversizedTextAndMalformedPayloads()
    {
        var grant = new ScriptPermissionGrant([], [], true, true, ["message", "input", "confirm"], [], [], [], false);
        Assert.Throws<InvalidDataException>(() => ScriptClipboardDialogPolicy.ValidateCommand(grant, "clipboard-write-text", new string('x', ScriptClipboardDialogPolicy.MaximumClipboardTextLength + 1)));
        Assert.Throws<InvalidDataException>(() => ScriptClipboardDialogPolicy.ValidateCommand(grant, "dialog-message", "{"));
        Assert.Throws<InvalidDataException>(() => ScriptClipboardDialogPolicy.ValidateCommand(grant, "dialog-message", JsonSerializer.Serialize(new ScriptDialogRequest(new string('x', ScriptClipboardDialogPolicy.MaximumDialogTextLength + 1), "title"))));
        Assert.Throws<InvalidDataException>(() => ScriptClipboardDialogPolicy.ValidateCommand(grant, "dialog-input", JsonSerializer.Serialize(new ScriptDialogRequest("prompt", "title", null))));
    }

    [Fact]
    public void DialogCapabilitiesAreIndependentAndCaseInsensitive()
    {
        var grant = new ScriptPermissionGrant([], [], false, false, ["input"], [], [], [], false);
        Assert.Throws<UnauthorizedAccessException>(() => ScriptClipboardDialogPolicy.ValidateCommand(grant, "dialog-message", JsonSerializer.Serialize(new ScriptDialogRequest("hello", "title"))));
        Assert.Equal("dialog-input", ScriptClipboardDialogPolicy.ValidateCommand(grant, "dialog-input", JsonSerializer.Serialize(new ScriptDialogRequest("prompt", "title", ""))).Command);
        Assert.True(grant.AllowsDialog("INPUT"));
        Assert.False(grant.AllowsClipboardRead);
        Assert.False(grant.AllowsClipboardWrite);
    }


    [Fact]
    public void LuaClipboardAndDialogApisUseTypedRequestsAndRequireGrant()
    {
        var grant = new ScriptPermissionGrant([], [], true, true, ["message", "input", "confirm"], [], [], [], false);
        var pipe = new ApiRecorder([
            JsonSerializer.Serialize("剪贴板内容"),
            "true",
            "true",
            JsonSerializer.Serialize("用户输入"),
            "false"
        ]);
        using var lua = new NLua.Lua(openLibs: true);
        Anjian.Core.Api.LuaApiRuntime.Register(lua,
            new Anjian.Core.Api.WorkerLuaApi(pipe, lua, permissions: grant));
        var result = lua.DoString("""
            local clipboard = GetClipboardText()
            local wrote = SetClipboardText("写入文本")
            local shown = MessageBox("提示")
            local value = InputBox("请输入")
            local confirmed = Confirm("继续？")
            return clipboard, wrote, shown, value, confirmed
            """);
        Assert.Equal("剪贴板内容", result[0]?.ToString());
        Assert.True(Convert.ToBoolean(result[1]));
        Assert.True(Convert.ToBoolean(result[2]));
        Assert.Equal("用户输入", result[3]?.ToString());
        Assert.False(Convert.ToBoolean(result[4]));
        Assert.Equal(
            ["clipboard-read-text", "clipboard-write-text", "dialog-message", "dialog-input", "dialog-confirm"],
            pipe.Messages.Select(m => m.Command).ToArray());
        var inputRequest = JsonDocument.Parse(pipe.Messages.Single(m => m.Command == "dialog-input").Payload!);
        Assert.Equal("按键精灵 2026", inputRequest.RootElement.GetProperty("Title").GetString());
        Assert.Equal("", inputRequest.RootElement.GetProperty("DefaultValue").GetString());
    }

    private sealed class ApiRecorder(IEnumerable<string?> responses) : Anjian.Core.Api.ILuaCommandPipe
    {
        private readonly Queue<string?> _responses = new(responses);
        public List<ApiMessage> Messages { get; } = [];
        public void Send(string type, string? name = null, double x = 0, double y = 0, int milliseconds = 0, string? text = null) =>
            _ = Request(type, name, x, y, milliseconds, text);
        public string? Request(string type, string? name = null, double x = 0, double y = 0, int milliseconds = 0, string? text = null)
        {
            Messages.Add(new ApiMessage(type, text));
            return _responses.Count == 0 ? "true" : _responses.Dequeue();
        }
    }

    private sealed record ApiMessage(string Command, string? Payload);

}
