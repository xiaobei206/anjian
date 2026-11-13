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
}
