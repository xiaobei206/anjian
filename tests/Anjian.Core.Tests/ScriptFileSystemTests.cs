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
