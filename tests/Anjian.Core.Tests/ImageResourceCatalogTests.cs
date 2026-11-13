using System;
using Anjian.Core.Imaging;
using Xunit;
namespace Anjian.Core.Tests;
public sealed class ImageResourceCatalogTests
{
    [Fact]
    public void CatalogImportsValidatesAndDetectsTampering()
    {
        var root = Path.Combine(Path.GetTempPath(), "AnjianImageTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var script = Path.Combine(root, "test.lua");
            File.WriteAllText(script, "");
            var png = Path.Combine(root, "source.png");
            File.WriteAllBytes(png, MinimalPng(4, 3));
            var catalog = new ImageResourceCatalog(script);
            var entry = catalog.ImportPng(png, "登录按钮");
            Assert.Single(catalog.ValidateAll());
            Assert.Equal("valid", catalog.ValidateAll()[0].Status);
            var manifest = catalog.Rename(entry.Id, "确认按钮");
            Assert.Equal("确认按钮", Assert.Single(manifest.Resources).Name);
            var asset = catalog.ResolveEntryPath(entry.RelativePath);
            File.WriteAllBytes(asset, MinimalPng(4, 3).Concat(new byte[] { 1 }).ToArray());
            Assert.Equal("size-mismatch", Assert.Single(catalog.ValidateAll()).Status);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void CatalogRejectsPathTraversalAndUnsupportedSchemaWithoutOverwriting()
    {
        var root = Path.Combine(Path.GetTempPath(), "AnjianImageTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var script = Path.Combine(root, "test.lua");
            File.WriteAllText(script, "");
            var catalog = new ImageResourceCatalog(script);
            Assert.Throws<InvalidDataException>(() => catalog.ResolveEntryPath("../outside.png"));
            File.WriteAllText(catalog.ManifestPath, "{\"SchemaVersion\":99}");
            Assert.Throws<NotSupportedException>(() => catalog.Load());
            Assert.Equal("{\"SchemaVersion\":99}", File.ReadAllText(catalog.ManifestPath));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void DependencyScannerIgnoresCommentsAndFindsLiteralAndDynamicCalls()
    {
        const string source = "-- LoadImage('comment.png')\nlocal a = LoadImage(\"ui\\\\ok.png\")\nlocal b = LoadImage(path)";
        var report = ImageResourceDependencyScanner.Scan(source);
        Assert.Equal(new[] { "ui\\ok.png" }, report.LiteralPaths);
        Assert.Equal(1, report.DynamicLoadImageCalls);
    }

    private static byte[] MinimalPng(int width, int height)
    {
        var bytes = new byte[24];
        new byte[] { 137,80,78,71,13,10,26,10 }.CopyTo(bytes, 0);
        new byte[] { 0,0,0,13 }.CopyTo(bytes, 8);
        "IHDR"u8.CopyTo(bytes.AsSpan(12));
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(16), width);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(20), height);
        return bytes;
    }
}
