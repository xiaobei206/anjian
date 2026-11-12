using Anjian.Core.Recorder;
using Xunit;

namespace Anjian.Core.Tests;

public sealed class RecorderDocumentTests
{
    [Fact]
    public void BuilderPairsAdjacentInputsAndKeepsHoldSeparateFromActionGap()
    {
        var document = RecorderDocumentBuilder.Create(
        [
            new(1_000, RecorderRawEventKind.Move, -1920, 40),
            new(1_100, RecorderRawEventKind.MouseDown, -1910, 45, "left", Source: "LowLevelHook", Flags: 1),
            new(1_150, RecorderRawEventKind.MouseUp, -1910, 45, "left"),
            new(1_300, RecorderRawEventKind.KeyDown, Name: "CTRL"),
            new(1_350, RecorderRawEventKind.KeyUp, Name: "CTRL"),
            new(1_500, RecorderRawEventKind.Wheel, -1900, 50, Delta: -120),
        ], clickOnly: false, timestampFrequency: 1_000, scriptId: "script-a", recordingId: "recording-a",
            createdAtUtc: DateTimeOffset.UnixEpoch);

        Assert.Equal(6, document.Events.Count);
        Assert.Equal("LowLevelHook", document.Actions[1].Source);
        Assert.Equal((uint)1, document.Events[1].Flags);
        Assert.Equal(0, document.DroppedEventCount);
        Assert.Equal(4, document.Actions.Count);
        Assert.Equal(RecorderActionKind.MouseMove, document.Actions[0].Kind);
        Assert.Equal(-1920, document.Actions[0].X);
        Assert.Equal(RecorderActionKind.MouseClick, document.Actions[1].Kind);
        Assert.Equal(50, document.Actions[1].HoldMs);
        Assert.Equal(100, document.Actions[1].DelayMs);
        Assert.Equal(new[] { "event-000002", "event-000003" }, document.Actions[1].RawEventIds);
        Assert.Equal(RecorderActionKind.KeyPress, document.Actions[2].Kind);
        Assert.Equal(50, document.Actions[2].HoldMs);
        Assert.Equal(150, document.Actions[2].DelayMs);
        Assert.Equal(RecorderActionKind.MouseWheel, document.Actions[3].Kind);
        Assert.Equal(-120, document.Actions[3].Delta);
        Assert.Equal(150, document.Actions[3].DelayMs);
    }

    [Fact]
    public void BuilderDoesNotCollapsePairsAcrossInterveningEvents()
    {
        var document = RecorderDocumentBuilder.Create(
        [
            new(100, RecorderRawEventKind.MouseDown, 10, 20, "left"),
            new(120, RecorderRawEventKind.Move, 30, 40),
            new(150, RecorderRawEventKind.MouseUp, 30, 40, "left")
        ], clickOnly: false, timestampFrequency: 1_000);

        Assert.Collection(document.Actions,
            action => Assert.Equal(RecorderActionKind.MouseDown, action.Kind),
            action => Assert.Equal(RecorderActionKind.MouseMove, action.Kind),
            action => Assert.Equal(RecorderActionKind.MouseUp, action.Kind));
        Assert.Equal(3, document.Actions.SelectMany(action => action.RawEventIds ?? []).Distinct().Count());
        Assert.Equal(20, document.Actions[1].DelayMs);
        Assert.Equal(30, document.Actions[2].DelayMs);
    }

    [Fact]
    public void BuilderKeepsOverlappingKeysAsDownAndUpEvents()
    {
        var document = RecorderDocumentBuilder.Create(
        [
            new(100, RecorderRawEventKind.KeyDown, Name: "A"),
            new(110, RecorderRawEventKind.KeyDown, Name: "B"),
            new(130, RecorderRawEventKind.KeyUp, Name: "A"),
            new(140, RecorderRawEventKind.KeyUp, Name: "B")
        ], clickOnly: false, timestampFrequency: 1_000);

        Assert.Equal(
            new[] { RecorderActionKind.KeyDown, RecorderActionKind.KeyDown, RecorderActionKind.KeyUp, RecorderActionKind.KeyUp },
            document.Actions.Select(action => action.Kind));
        Assert.Equal(4, document.Actions.SelectMany(action => action.RawEventIds ?? []).Distinct().Count());
        Assert.Equal(20, document.Actions[2].DelayMs);
        Assert.Equal(10, document.Actions[3].DelayMs);
    }

    [Fact]
    public void GeneratorPreservesUserCodeAndSkipsDisabledActions()
    {
        var document = DocumentWithActions(
        [
            new RecorderAction("a1", RecorderActionKind.MouseClick, X: -8, Y: 22, Name: "left", HoldMs: 45, DelayMs: 600_001),
            new RecorderAction("a2", RecorderActionKind.TypeText, Text: "should not run", Enabled: false)
        ]);

        var result = RecorderLuaGenerator.PrepareUpdate("local before = true\n\nlocal after = true\n", document);

        Assert.True(result.Success);
        Assert.Contains("local before = true", result.ScriptText, StringComparison.Ordinal);
        Assert.Contains("local after = true", result.ScriptText, StringComparison.Ordinal);
        Assert.Contains("Delay(600000)", result.ScriptText, StringComparison.Ordinal);
        Assert.Contains("Delay(1)", result.ScriptText, StringComparison.Ordinal);
        Assert.Contains("MoveTo(-8, 22)", result.ScriptText, StringComparison.Ordinal);
        Assert.Contains("LeftClick(45)", result.ScriptText, StringComparison.Ordinal);
        Assert.Contains("-- <anjian:action id=\"a1\"", result.ScriptText, StringComparison.Ordinal);
        Assert.Contains("-- [已禁用]", result.ScriptText, StringComparison.Ordinal);
        Assert.DoesNotContain("TypeText(\"should not run\")", result.ScriptText, StringComparison.Ordinal);
        Assert.Equal(RecorderSynchronizationState.Synchronized, result.Document.SynchronizationState);
    }

    [Fact]
    public void GeneratorDetectsManualChangesAndRequiresPreviewConfirmation()
    {
        var document = DocumentWithActions(
        [
            new RecorderAction("a1", RecorderActionKind.MouseMove, X: 10, Y: 20)
        ]);
        var initial = RecorderLuaGenerator.PrepareUpdate("-- user before\nlocal value = 1\n", document);
        Assert.True(initial.Success);

        var editedByUser = initial.ScriptText.Replace("MoveTo(10, 20)", "MoveTo(99, 20)", StringComparison.Ordinal);
        var inspected = RecorderLuaGenerator.Inspect(editedByUser, initial.Document);
        Assert.Equal(RecorderSynchronizationState.Detached, inspected.SynchronizationState);

        var blocked = RecorderLuaGenerator.PrepareUpdate(editedByUser, initial.Document);
        Assert.False(blocked.Success);
        Assert.True(blocked.RequiresConfirmation);
        Assert.Equal(editedByUser, blocked.ScriptText);
        Assert.Contains("MoveTo(99, 20)", blocked.ExistingBlock, StringComparison.Ordinal);
        Assert.Contains("MoveTo(10, 20)", blocked.ReplacementBlock, StringComparison.Ordinal);

        var confirmed = RecorderLuaGenerator.PrepareUpdate(editedByUser, initial.Document, confirmDetachedReplacement: true);
        Assert.True(confirmed.Success);
        Assert.Contains("-- user before", confirmed.ScriptText, StringComparison.Ordinal);
        Assert.Contains("local value = 1", confirmed.ScriptText, StringComparison.Ordinal);
        Assert.Contains("MoveTo(10, 20)", confirmed.ScriptText, StringComparison.Ordinal);
        Assert.DoesNotContain("MoveTo(99, 20)", confirmed.ScriptText, StringComparison.Ordinal);
        Assert.Equal(RecorderSynchronizationState.Synchronized, confirmed.Document.SynchronizationState);
    }

    [Fact]
    public void GeneratorRejectsMalformedGeneratedBlockInsteadOfAppendingOverIt()
    {
        var document = DocumentWithActions([]);
        var malformed = $"-- <anjian:generated id=\"{document.GeneratedBlockId}\" schema=\"1\">\nMoveTo(1, 2)\n";

        var result = RecorderLuaGenerator.PrepareUpdate(malformed, document);

        Assert.False(result.Success);
        Assert.False(result.RequiresConfirmation);
        Assert.Contains("缺少结束标记", result.Error!, StringComparison.Ordinal);
        Assert.Equal(malformed, result.ScriptText);
    }

    [Fact]
    public void StoreRoundTripsAndRejectsUnknownSchemaWithoutChangingOriginalFile()
    {
        using var directory = Directory.CreateTempSubdirectory();
        var scriptPath = Path.Combine(directory.FullName, "test.lua");
        var document = DocumentWithActions(
        [
            new RecorderAction("a1", RecorderActionKind.KeyPress, Name: "ENTER", HoldMs: 30, DelayMs: 12)
        ]);

        RecorderDocumentStore.Save(scriptPath, document);
        var loaded = RecorderDocumentStore.Load(RecorderDocumentStore.GetSidecarPath(scriptPath));
        Assert.Equal(document.SchemaVersion, loaded.SchemaVersion);
        Assert.Equal(document.ScriptId, loaded.ScriptId);
        Assert.Equal(document.RecordingId, loaded.RecordingId);
        Assert.Equal(document.Events.ToArray(), loaded.Events.ToArray());
        Assert.Equal(document.Actions.ToArray(), loaded.Actions.ToArray());
        Assert.Equal(document.Actions[0].Source, loaded.Actions[0].Source);
        Assert.Equal(document.GeneratedBlockId, loaded.GeneratedBlockId);

        var sidecar = RecorderDocumentStore.GetSidecarPath(scriptPath);
        var unsupported = File.ReadAllText(sidecar).Replace("\"schemaVersion\": 1", "\"schemaVersion\": 999", StringComparison.Ordinal);
        File.WriteAllText(sidecar, unsupported);
        var original = File.ReadAllText(sidecar);

        Assert.Throws<NotSupportedException>(() => RecorderDocumentStore.Load(sidecar));
        Assert.Equal(original, File.ReadAllText(sidecar));
    }

    [Fact]
    public void GeneratedBlockCompilesAsLuaAndHashIgnoresLineEndingChanges()
    {
        var document = DocumentWithActions(
        [
            new RecorderAction("a1", RecorderActionKind.MouseClick, X: -10, Y: 20, Name: "right", HoldMs: 33, DelayMs: 14),
            new RecorderAction("a2", RecorderActionKind.KeyPress, Name: "ENTER", HoldMs: 20, DelayMs: 8)
        ]);
        var block = RecorderLuaGenerator.BuildBlock(document);
        var windowsText = block.Replace("\n", "\r\n", StringComparison.Ordinal);

        Assert.Equal(RecorderLuaGenerator.GetHash(block), RecorderLuaGenerator.GetHash(windowsText));
        using var lua = new NLua.Lua();
        lua.DoString("function Delay(...) end; function MoveTo(...) end; function RightClick(...) end; function KeyPress(...) end");
        lua.DoString(block);

        var longDelay = RecorderLuaGenerator.BuildBlock(DocumentWithActions(
        [
            new RecorderAction("a1", RecorderActionKind.MouseMove, DelayMs: RecorderDocument.MaximumActionDelayMilliseconds)
        ]));
        Assert.Contains("while __anjian_delay_remaining > 600000 do", longDelay, StringComparison.Ordinal);
        using var longDelayLua = new NLua.Lua();
        longDelayLua.DoString("function Delay(...) end; function MoveTo(...) end");
        longDelayLua.DoString(longDelay);
    }

    [Fact]
    public void BuilderPersistsOverflowCountWithoutDroppingStoredEvents()
    {
        var document = RecorderDocumentBuilder.Create(
        [
            new(100, RecorderRawEventKind.KeyDown, Name: "A"),
            new(120, RecorderRawEventKind.KeyUp, Name: "A")
        ], clickOnly: true, timestampFrequency: 1_000, droppedEventCount: 7);

        Assert.Equal(2, document.Events.Count);
        Assert.Equal(7, document.DroppedEventCount);
        Assert.True(document.ClickOnly);
    }

    [Fact]
    public void BuilderAssignsStableEventAndActionIdsForSameOrderedInput()
    {
        RecorderEventInput[] inputs =
        [
            new(10, RecorderRawEventKind.MouseDown, 1, 2, "right"),
            new(20, RecorderRawEventKind.MouseUp, 1, 2, "right")
        ];

        var first = RecorderDocumentBuilder.Create(inputs, false, 1_000, scriptId: "s", recordingId: "r");
        var second = RecorderDocumentBuilder.Create(inputs, false, 1_000, scriptId: "s", recordingId: "r");

        Assert.Equal(first.Events.Select(item => item.EventId), second.Events.Select(item => item.EventId));
        Assert.Equal(first.Actions.Select(item => item.ActionId), second.Actions.Select(item => item.ActionId));
    }


    [Fact]
    public void GeneratorRequiresConfirmationWhenPreviouslyGeneratedBlockWasDeleted()
    {
        var document = DocumentWithActions(
        [
            new RecorderAction("a1", RecorderActionKind.MouseMove, X: 1, Y: 2)
        ]);
        var initial = RecorderLuaGenerator.PrepareUpdate("local before = true\n", document);
        Assert.True(initial.Success);

        var withoutBlock = "local before = true\n";
        var blocked = RecorderLuaGenerator.PrepareUpdate(withoutBlock, initial.Document);
        Assert.False(blocked.Success);
        Assert.True(blocked.RequiresConfirmation);
        Assert.Equal(withoutBlock, blocked.ScriptText);
        Assert.Empty(blocked.ExistingBlock);

        var confirmed = RecorderLuaGenerator.PrepareUpdate(
            withoutBlock, initial.Document, confirmDetachedReplacement: true);
        Assert.True(confirmed.Success);
        Assert.Contains("-- <anjian:generated", confirmed.ScriptText, StringComparison.Ordinal);
    }

    [Fact]
    public void StoreRejectsUnboundedActionDelay()
    {
        var document = DocumentWithActions(
        [
            new RecorderAction("a1", RecorderActionKind.MouseMove, DelayMs: long.MaxValue)
        ]);

        Assert.Throws<InvalidDataException>(() => document.Validate());
    }

    private static RecorderDocument DocumentWithActions(IReadOnlyList<RecorderAction> actions) =>
        new()
        {
            ScriptId = "script-test",
            RecordingId = "recording-test",
            GeneratedBlockId = "recording-test-block",
            CreatedAtUtc = DateTimeOffset.UnixEpoch,
            TimestampFrequency = 1_000,
            Actions = actions.ToList(),
            Events = []
        };
}
