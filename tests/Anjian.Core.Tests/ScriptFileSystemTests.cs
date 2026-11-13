using System.ComponentModel;
using System.Runtime.InteropServices;
using Anjian.Core.Permissions;
using Xunit;

namespace Anjian.Core.Tests;

public sealed class ScriptFileSystemTests
{
    [Fact]
    public void EmptyGrantRejectsFileReadAndWriteAndForgedIpc()
    {
        using var sandbox = new FileSandbox();
        var file = Path.Combine(sandbox.Root, "input.txt");
        File.WriteAllText(file, "secret");
        var service = new ScriptFileSystem(ScriptPermissionGrant.Empty, sandbox.Root);

        Assert.Throws<UnauthorizedAccessException>(() => service.ReadTextFile(file));
        Assert.Throws<UnauthorizedAccessException>(() => service.WriteTextFile(file, "changed"));
        Assert.Throws<UnauthorizedAccessException>(() =>
            ScriptFileSystemHostDispatcher.Dispatch(ScriptPermissionGrant.Empty, sandbox.Root,
                "file-read-text", file, null, CancellationToken.None));
        Assert.Throws<InvalidDataException>(() =>
            ScriptFileSystemHostDispatcher.Dispatch(ScriptPermissionGrant.Empty, sandbox.Root,
                "file-delete", file, null, CancellationToken.None));
    }

    [Fact]
    public void ApprovedRootsSupportTextRoundTripAppendAndExistence()
    {
        using var sandbox = new FileSandbox();
        var grant = sandbox.Grant(read: true, write: true);
        var service = new ScriptFileSystem(grant, sandbox.Root);
        var file = Path.Combine(sandbox.Root, "data.txt");

        Assert.True(service.WriteTextFile("data.txt", "第一行"));
        Assert.True(service.AppendTextFile("data.txt", "\n第二行"));
        Assert.Equal("第一行\n第二行", service.ReadTextFile("data.txt"));
        Assert.True(service.FileExists(file));
        Assert.True(service.DirectoryExists("."));
        Assert.False(service.FileExists("missing.txt"));
        Assert.False(service.DirectoryExists("missing-dir"));
    }

    [Fact]
    public void WritesAreUtf8BoundedAndRejectOversizedExistingFiles()
    {
        using var sandbox = new FileSandbox();
        var service = new ScriptFileSystem(sandbox.Grant(read: true, write: true), sandbox.Root);
        var tooLarge = new string('x', ScriptFileSystem.MaximumTextBytes + 1);

        Assert.Throws<InvalidDataException>(() => service.WriteTextFile("large.txt", tooLarge));
        File.WriteAllBytes(Path.Combine(sandbox.Root, "large.txt"),
            new byte[ScriptFileSystem.MaximumTextBytes + 1]);
        Assert.Throws<InvalidDataException>(() => service.ReadTextFile("large.txt"));
        Assert.Throws<InvalidDataException>(() => service.AppendTextFile("large.txt", "x"));
    }

    [Fact]
    public void PathTraversalAndSiblingPrefixAreRejected()
    {
        using var sandbox = new FileSandbox();
        var sibling = sandbox.Root + "-sibling";
        Directory.CreateDirectory(sibling);
        File.WriteAllText(Path.Combine(sibling, "secret.txt"), "secret");
        var service = new ScriptFileSystem(sandbox.Grant(read: true), sandbox.Root);

        Assert.Throws<UnauthorizedAccessException>(() => service.ReadTextFile("../" + Path.GetFileName(sibling) + "/secret.txt"));
        Assert.Throws<UnauthorizedAccessException>(() => service.ReadTextFile(Path.Combine(sibling, "secret.txt")));
        try { Directory.Delete(sibling, recursive: true); } catch { }
    }

    [Fact]
    public void DirectoryListingIsTopLevelBoundedAndCreationRequiresExistingParent()
    {
        using var sandbox = new FileSandbox();
        var service = new ScriptFileSystem(sandbox.Grant(read: true, write: true), sandbox.Root);
        File.WriteAllText(Path.Combine(sandbox.Root, "a.txt"), "a");
        Directory.CreateDirectory(Path.Combine(sandbox.Root, "nested"));
        File.WriteAllText(Path.Combine(sandbox.Root, "nested", "hidden.txt"), "x");

        Assert.True(service.CreateDirectory("created"));
        Assert.True(service.DirectoryExists("created"));
        Assert.Throws<DirectoryNotFoundException>(() => service.CreateDirectory("missing-parent/child"));
        Assert.Equal(["a.txt"], service.ListFiles("."));
        Assert.Equal(["created", "nested"], service.ListDirectories("."));
    }

    [Fact]
    public void CancellationIsHonoredBeforeFileOperation()
    {
        using var sandbox = new FileSandbox();
        var file = Path.Combine(sandbox.Root, "cancel.txt");
        File.WriteAllText(file, "x");
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var service = new ScriptFileSystem(sandbox.Grant(read: true), sandbox.Root);

        Assert.Throws<OperationCanceledException>(() => service.ReadTextFile(file, cts.Token));
        Assert.Throws<OperationCanceledException>(() =>
            ScriptFileSystemHostDispatcher.Dispatch(sandbox.Grant(read: true), sandbox.Root,
                "file-read-text", file, null, cts.Token));
    }

    [Fact]
    public void ReparsePointDirectoriesAndFilesAreRejectedAndNeverFollowed()
    {
        using var sandbox = new FileSandbox();
        var outside = Path.Combine(Path.GetTempPath(), "Anjian2026-Outside-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outside);
        var externalFile = Path.Combine(outside, "outside.txt");
        File.WriteAllText(externalFile, "outside");
        var linkDirectory = Path.Combine(sandbox.Root, "linked-dir");
        var linkFile = Path.Combine(sandbox.Root, "linked-file.txt");
        try
        {
            Directory.CreateSymbolicLink(linkDirectory, outside);
            File.CreateSymbolicLink(linkFile, externalFile);
            var service = new ScriptFileSystem(sandbox.Grant(read: true, write: true), sandbox.Root);

            Assert.ThrowsAny<UnauthorizedAccessException>(() => service.ReadTextFile(Path.Combine(linkDirectory, "outside.txt")));
            Assert.ThrowsAny<UnauthorizedAccessException>(() => service.ReadTextFile(linkFile));
            Assert.ThrowsAny<UnauthorizedAccessException>(() => service.WriteTextFile(Path.Combine(linkDirectory, "new.txt"), "no"));
            Assert.ThrowsAny<UnauthorizedAccessException>(() => service.CopyFile(linkFile, Path.Combine(sandbox.Root, "copied-link.txt")));
            Assert.ThrowsAny<UnauthorizedAccessException>(() => service.MoveFile(linkFile, Path.Combine(sandbox.Root, "moved-link.txt")));
            var normalSource = Path.Combine(sandbox.Root, "normal-source.txt");
            File.WriteAllText(normalSource, "safe");
            Assert.ThrowsAny<UnauthorizedAccessException>(() => service.CopyFile(
                normalSource, linkFile, overwrite: true));
            Assert.ThrowsAny<UnauthorizedAccessException>(() => service.MoveFile(
                normalSource, linkFile, overwrite: true));
            Assert.ThrowsAny<UnauthorizedAccessException>(() => service.RenameFile(
                normalSource, "linked-file.txt", overwrite: true));
            Assert.DoesNotContain("linked-file.txt", service.ListFiles("."));
            Assert.DoesNotContain("linked-dir", service.ListDirectories("."));
        }
        finally
        {
            try { Directory.Delete(linkDirectory); } catch { }
            try { File.Delete(linkFile); } catch { }
            try { Directory.Delete(outside, recursive: true); } catch { }
        }
    }

    [Fact]
    public void HostDispatcherPerformsItsOwnGrantCheckForWriteAndList()
    {
        using var sandbox = new FileSandbox();
        var input = Path.Combine(sandbox.Root, "message.txt");
        var payload = System.Text.Json.JsonSerializer.Serialize(new ScriptFileTextRequest(input, "hello"));

        Assert.Throws<UnauthorizedAccessException>(() =>
            ScriptFileSystemHostDispatcher.Dispatch(ScriptPermissionGrant.Empty, sandbox.Root,
                "file-write-text", null, payload, CancellationToken.None));

        var readOnlyGrant = sandbox.Grant(read: true);
        var created = Path.Combine(sandbox.Root, "outside-write.txt");
        Assert.Throws<UnauthorizedAccessException>(() =>
            ScriptFileSystemHostDispatcher.Dispatch(readOnlyGrant, sandbox.Root,
                "file-write-text", null,
                System.Text.Json.JsonSerializer.Serialize(new ScriptFileTextRequest(created, "no")),
                CancellationToken.None));
        Assert.Throws<UnauthorizedAccessException>(() =>
            ScriptFileSystemHostDispatcher.Dispatch(ScriptPermissionGrant.Empty, sandbox.Root,
                "directory-list-files", null,
                System.Text.Json.JsonSerializer.Serialize(new ScriptFilePathRequest(sandbox.Root)),
                CancellationToken.None));
    }

    [Fact]
    public void HardLinksCannotExposeOrModifyFilesOutsideAnApprovedRoot()
    {
        using var sandbox = new FileSandbox();
        var outside = Path.Combine(Path.GetTempPath(), "Anjian2026-HardLink-" + Guid.NewGuid().ToString("N") + ".txt");
        var linked = Path.Combine(sandbox.Root, "linked.txt");
        File.WriteAllText(outside, "outside");
        try
        {
            if (!CreateHardLinkW(linked, outside, IntPtr.Zero))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "无法为硬链接安全测试创建硬链接。");

            var service = new ScriptFileSystem(sandbox.Grant(read: true, write: true), sandbox.Root);
            Assert.ThrowsAny<UnauthorizedAccessException>(() => service.ReadTextFile(linked));
            Assert.ThrowsAny<UnauthorizedAccessException>(() => service.WriteTextFile(linked, "changed"));
            Assert.ThrowsAny<UnauthorizedAccessException>(() => service.CopyFile(linked, Path.Combine(sandbox.Root, "linked-copy.txt")));
            Assert.ThrowsAny<UnauthorizedAccessException>(() => service.MoveFile(linked, Path.Combine(sandbox.Root, "linked-move.txt")));
            var ordinarySource = Path.Combine(sandbox.Root, "ordinary-source.txt");
            var linkedTarget = Path.Combine(sandbox.Root, "linked-target.txt");
            File.WriteAllText(ordinarySource, "safe");
            if (!CreateHardLinkW(linkedTarget, outside, IntPtr.Zero))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "无法创建硬链接目标安全测试。");
            Assert.ThrowsAny<UnauthorizedAccessException>(() =>
                service.CopyFile(ordinarySource, linkedTarget, overwrite: true));
            Assert.ThrowsAny<UnauthorizedAccessException>(() =>
                service.MoveFile(ordinarySource, linkedTarget, overwrite: true));
            Assert.ThrowsAny<UnauthorizedAccessException>(() =>
                service.RenameFile(ordinarySource, "linked-target.txt", overwrite: true));
            Assert.DoesNotContain("linked.txt", service.ListFiles("."));
            Assert.Equal("outside", File.ReadAllText(outside));
        }
        finally
        {
            try { File.Delete(linked); } catch { }
            try { File.Delete(outside); } catch { }
        }
    }

    [Fact]
    public void CopyMoveAndRenameOperateOnApprovedOrdinaryFiles()
    {
        using var sandbox = new FileSandbox();
        var service = new ScriptFileSystem(sandbox.Grant(read: true, write: true), sandbox.Root);
        var source = Path.Combine(sandbox.Root, "source.txt");
        var copy = Path.Combine(sandbox.Root, "copy.txt");
        var moved = Path.Combine(sandbox.Root, "moved.txt");
        var renamed = Path.Combine(sandbox.Root, "renamed.txt");
        File.WriteAllText(source, "内容");

        Assert.True(service.CopyFile(source, copy));
        Assert.Equal("内容", File.ReadAllText(copy));
        Assert.Throws<IOException>(() => service.CopyFile(source, copy));
        Assert.True(service.CopyFile(source, copy, overwrite: true));
        Assert.Equal("内容", File.ReadAllText(copy));

        Assert.True(service.MoveFile(source, moved));
        Assert.False(File.Exists(source));
        Assert.Equal("内容", File.ReadAllText(moved));

        Assert.True(service.RenameFile(moved, "renamed.txt"));
        Assert.False(File.Exists(moved));
        Assert.Equal("内容", File.ReadAllText(renamed));
        Assert.Throws<InvalidDataException>(() => service.RenameFile(renamed, "../escape.txt"));
        Assert.True(File.Exists(renamed));
    }

    [Fact]
    public void CopyMoveRenameAndTemporaryOperationsRequireTheCorrectGrantRoots()
    {
        using var sandbox = new FileSandbox();
        var source = Path.Combine(sandbox.Root, "source.txt");
        var destination = Path.Combine(sandbox.Root, "destination.txt");
        File.WriteAllText(source, "source");
        var readOnly = new ScriptFileSystem(sandbox.Grant(read: true), sandbox.Root);
        var writeOnly = new ScriptFileSystem(sandbox.Grant(write: true), sandbox.Root);

        Assert.Throws<UnauthorizedAccessException>(() => readOnly.CopyFile(source, destination));
        Assert.Throws<UnauthorizedAccessException>(() => writeOnly.CopyFile(source, destination));
        Assert.Throws<UnauthorizedAccessException>(() => readOnly.MoveFile(source, destination));
        Assert.Throws<UnauthorizedAccessException>(() => readOnly.RenameFile(source, "renamed.txt"));
        Assert.Throws<UnauthorizedAccessException>(() => writeOnly.RenameFile(source, "renamed.txt"));
        Assert.Throws<UnauthorizedAccessException>(() => readOnly.CreateTempFile(sandbox.Root));
        Assert.Throws<UnauthorizedAccessException>(() => readOnly.CreateTempDirectory(sandbox.Root));
    }

    [Fact]
    public void TemporaryObjectsAreCreatedOnlyInsideExplicitApprovedWriteDirectory()
    {
        using var sandbox = new FileSandbox();
        var collisionFile = Path.Combine(sandbox.Root, ".anjian-" + Guid.Empty.ToString("N") + ".tmp");
        var collisionDirectory = Path.Combine(sandbox.Root, ".anjian-" + Guid.Empty.ToString("N") + ".tmpdir");
        File.WriteAllText(collisionFile, "keep-existing-file");
        Directory.CreateDirectory(collisionDirectory);
        var ids = new Queue<Guid>([Guid.Empty, Guid.NewGuid(), Guid.Empty, Guid.NewGuid()]);
        var service = new ScriptFileSystem(sandbox.Grant(write: true), sandbox.Root, () => ids.Dequeue());
        var temporaryFile = service.CreateTempFile(".");
        var temporaryDirectory = service.CreateTempDirectory(sandbox.Root);

        Assert.StartsWith(sandbox.Root, temporaryFile, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(sandbox.Root, temporaryDirectory, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(temporaryFile));
        Assert.Equal(0, new FileInfo(temporaryFile).Length);
        Assert.True(Directory.Exists(temporaryDirectory));
        Assert.NotEqual(collisionFile, temporaryFile);
        Assert.NotEqual(collisionDirectory, temporaryDirectory);
        Assert.Equal("keep-existing-file", File.ReadAllText(collisionFile));
        Assert.True(Directory.Exists(collisionDirectory));
        Assert.Empty(ids);

        var sibling = sandbox.Root + "-sibling";
        Directory.CreateDirectory(sibling);
        try
        {
            Assert.Throws<UnauthorizedAccessException>(() => service.CreateTempFile(sibling));
            Assert.Throws<UnauthorizedAccessException>(() => service.CreateTempDirectory(sibling));
            Assert.Throws<UnauthorizedAccessException>(() => service.CreateTempFile(
                Path.Combine(sandbox.Root, "..", Path.GetFileName(sibling))));
        }
        finally
        {
            try { Directory.Delete(sibling, recursive: true); } catch { }
        }
    }

    [Fact]
    public void CopyRejectsOversizedFilesAndCancellationBeforeCommit()
    {
        using var sandbox = new FileSandbox();
        var service = new ScriptFileSystem(sandbox.Grant(read: true, write: true), sandbox.Root);
        var tooLarge = Path.Combine(sandbox.Root, "too-large.bin");
        using (var stream = new FileStream(tooLarge, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            stream.SetLength(ScriptFileSystem.MaximumTransferBytes + 1L);
        Assert.Throws<InvalidDataException>(() => service.CopyFile(tooLarge, "too-large-copy.bin"));

        var cancellableSource = Path.Combine(sandbox.Root, "cancel-source.bin");
        using (var stream = new FileStream(cancellableSource, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            stream.SetLength(ScriptFileSystem.MaximumTransferBytes);
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(20));
        var cancelledDestination = Path.Combine(sandbox.Root, "cancel-destination.bin");
        File.WriteAllText(cancelledDestination, "preserve-existing-target");
        Assert.Throws<OperationCanceledException>(() =>
            service.CopyFile(cancellableSource, cancelledDestination, overwrite: true, cancellationToken: cts.Token));
        Assert.True(File.Exists(cancellableSource));
        Assert.Equal("preserve-existing-target", File.ReadAllText(cancelledDestination));
        Assert.Empty(Directory.EnumerateFiles(sandbox.Root, ".anjian-*.tmp"));
    }

    [Fact]
    public void DispatcherRechecksTransferAndTemporaryPermissionsAndRejectsUnknownCommands()
    {
        using var sandbox = new FileSandbox();
        var source = Path.Combine(sandbox.Root, "source.txt");
        var destination = Path.Combine(sandbox.Root, "destination.txt");
        File.WriteAllText(source, "source");
        var request = System.Text.Json.JsonSerializer.Serialize(
            new ScriptFileTransferRequest(source, destination));

        Assert.Throws<UnauthorizedAccessException>(() =>
            ScriptFileSystemHostDispatcher.Dispatch(ScriptPermissionGrant.Empty, sandbox.Root,
                "file-copy", null, request, CancellationToken.None));
        Assert.Throws<UnauthorizedAccessException>(() =>
            ScriptFileSystemHostDispatcher.Dispatch(sandbox.Grant(read: true), sandbox.Root,
                "file-copy", null, request, CancellationToken.None));
        Assert.Throws<UnauthorizedAccessException>(() =>
            ScriptFileSystemHostDispatcher.Dispatch(sandbox.Grant(read: true), sandbox.Root,
                "file-move", null, request, CancellationToken.None));
        Assert.Throws<UnauthorizedAccessException>(() =>
            ScriptFileSystemHostDispatcher.Dispatch(ScriptPermissionGrant.Empty, sandbox.Root,
                "file-create-temp", sandbox.Root, null, CancellationToken.None));
        Assert.Throws<UnauthorizedAccessException>(() =>
            ScriptFileSystemHostDispatcher.Dispatch(ScriptPermissionGrant.Empty, sandbox.Root,
                "directory-create-temp", sandbox.Root, null, CancellationToken.None));
        Assert.Throws<InvalidDataException>(() =>
            ScriptFileSystemHostDispatcher.Dispatch(sandbox.Grant(read: true, write: true), sandbox.Root,
                "file-delete", source, null, CancellationToken.None));
    }

    [Fact]
    public void CrossVolumeMoveUsesCopyVerificationBeforeDeletingSourceWhenAnotherDriveIsAvailable()
    {
        using var sandbox = new FileSandbox();
        var sourceDrive = Path.GetPathRoot(sandbox.Root);
        var alternateDrive = DriveInfo.GetDrives().FirstOrDefault(drive =>
            drive.IsReady &&
            !string.Equals(drive.RootDirectory.FullName, sourceDrive, StringComparison.OrdinalIgnoreCase));
        if (alternateDrive is null)
            return;

        var destinationRoot = Path.Combine(alternateDrive.RootDirectory.FullName,
            "Anjian2026-CrossVolume-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(destinationRoot);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            return;
        }

        try
        {
            var source = Path.Combine(sandbox.Root, "cross-volume-source.txt");
            var destination = Path.Combine(destinationRoot, "cross-volume-destination.txt");
            File.WriteAllText(source, new string('x', 8192));
            var grant = new ScriptPermissionGrant(
                [sandbox.Root], [sandbox.Root, destinationRoot],
                false, false, [], [], [], [], false);
            var service = new ScriptFileSystem(grant, sandbox.Root);

            Assert.True(service.MoveFile(source, destination));
            Assert.False(File.Exists(source));
            Assert.Equal(new string('x', 8192), File.ReadAllText(destination));

            var oversizedSource = Path.Combine(sandbox.Root, "cross-volume-large.bin");
            var oversizedDestination = Path.Combine(destinationRoot, "cross-volume-large.bin");
            using (var stream = new FileStream(oversizedSource, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                stream.SetLength(ScriptFileSystem.MaximumTransferBytes + 1L);
            Assert.Throws<InvalidDataException>(() => service.MoveFile(oversizedSource, oversizedDestination));
            Assert.True(File.Exists(oversizedSource));
            Assert.False(File.Exists(oversizedDestination));
        }
        finally
        {
            try { Directory.Delete(destinationRoot, recursive: true); } catch { }
        }
    }

    [Fact]
    public void DispatcherExecutesAllNewOperationsThroughTheHostFileSystem()
    {
        using var sandbox = new FileSandbox();
        var grant = sandbox.Grant(read: true, write: true);
        var source = Path.Combine(sandbox.Root, "source.txt");
        File.WriteAllText(source, "dispatcher");

        var copyPath = Path.Combine(sandbox.Root, "copy.txt");
        var copyPayload = System.Text.Json.JsonSerializer.Serialize(
            new ScriptFileTransferRequest(source, copyPath));
        var copied = ScriptFileSystemHostDispatcher.Dispatch(grant, sandbox.Root,
            "file-copy", null, copyPayload, CancellationToken.None);
        Assert.True(System.Text.Json.JsonSerializer.Deserialize<bool>(copied!));
        Assert.Equal("dispatcher", File.ReadAllText(copyPath));

        var movePath = Path.Combine(sandbox.Root, "moved.txt");
        var movePayload = System.Text.Json.JsonSerializer.Serialize(
            new ScriptFileTransferRequest(copyPath, movePath));
        var moved = ScriptFileSystemHostDispatcher.Dispatch(grant, sandbox.Root,
            "file-move", null, movePayload, CancellationToken.None);
        Assert.True(System.Text.Json.JsonSerializer.Deserialize<bool>(moved!));
        Assert.False(File.Exists(copyPath));

        var renamePayload = System.Text.Json.JsonSerializer.Serialize(
            new ScriptFileRenameRequest(movePath, "renamed.txt"));
        var renamed = ScriptFileSystemHostDispatcher.Dispatch(grant, sandbox.Root,
            "file-rename", null, renamePayload, CancellationToken.None);
        Assert.True(System.Text.Json.JsonSerializer.Deserialize<bool>(renamed!));
        Assert.True(File.Exists(Path.Combine(sandbox.Root, "renamed.txt")));

        var temporaryFile = ScriptFileSystemHostDispatcher.Dispatch(grant, sandbox.Root,
            "file-create-temp", sandbox.Root, null, CancellationToken.None);
        var tempFilePath = System.Text.Json.JsonSerializer.Deserialize<string>(temporaryFile!);
        Assert.NotNull(tempFilePath);
        Assert.True(File.Exists(tempFilePath));

        var temporaryDirectory = ScriptFileSystemHostDispatcher.Dispatch(grant, sandbox.Root,
            "directory-create-temp", sandbox.Root, null, CancellationToken.None);
        var tempDirectoryPath = System.Text.Json.JsonSerializer.Deserialize<string>(temporaryDirectory!);
        Assert.NotNull(tempDirectoryPath);
        Assert.True(Directory.Exists(tempDirectoryPath));
    }

    [Fact]
    public void HostDispatcherChecksRenameSourceAndDestinationGrantIndependently()
    {
        using var sandbox = new FileSandbox();
        var source = Path.Combine(sandbox.Root, "source.txt");
        File.WriteAllText(source, "content");
        var renameRequest = System.Text.Json.JsonSerializer.Serialize(
            new ScriptFileRenameRequest(source, "renamed.txt"));
        var readOnly = sandbox.Grant(read: true);

        Assert.Throws<UnauthorizedAccessException>(() =>
            ScriptFileSystemHostDispatcher.Dispatch(readOnly, sandbox.Root,
                "file-rename", null, renameRequest, CancellationToken.None));
        Assert.Throws<UnauthorizedAccessException>(() =>
            ScriptFileSystemHostDispatcher.Dispatch(readOnly, sandbox.Root,
                "file-move", null,
                System.Text.Json.JsonSerializer.Serialize(
                    new ScriptFileTransferRequest(source, Path.Combine(sandbox.Root, "moved.txt"))),
                CancellationToken.None));
    }

    [Fact]
    public void ResolverRejectsDeviceDriveRelativeAndAlternateDataStreamPaths()
    {
        Assert.Throws<InvalidDataException>(() => ScriptFileSystem.ResolvePath(@"C:relative.txt", @"C:\scripts"));
        Assert.Throws<InvalidDataException>(() => ScriptFileSystem.ResolvePath(@"\\?\C:\secret.txt", @"C:\scripts"));
        Assert.Throws<InvalidDataException>(() => ScriptFileSystem.ResolvePath(@"file.txt:stream", @"C:\scripts"));
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateHardLinkW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLinkW(string fileName, string existingFileName, IntPtr securityAttributes);

    private sealed class FileSandbox : IDisposable
    {
        public FileSandbox()
        {
            Root = Path.Combine(Path.GetTempPath(), "Anjian2026-FileTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public ScriptPermissionGrant Grant(bool read = false, bool write = false) => new(
            read ? [Root] : [], write ? [Root] : [],
            false, false, [], [], [], [], false);

        public void Dispose()
        {
            try { Directory.Delete(Root, recursive: true); } catch { }
        }
    }
}
