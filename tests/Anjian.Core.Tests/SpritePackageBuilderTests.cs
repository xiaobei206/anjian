using System.Text.Json;
using Anjian.Core.Packaging;
using Anjian.Core.Parameters;
using Xunit;

namespace Anjian.Core.Tests;

public sealed class SpritePackageBuilderTests
{
    [Fact]
    public void ScannerFindsLiteralDependenciesAndIgnoresCommentsAndStringContents()
    {
        const string source = """
            -- require("commented.out")
            local note = "LoadImage('not-an-asset.png')"
            --[=[ require("long.comment") ]=]
            local image = LoadImage([[images/button.png]])
            local module = require("automation.mouse")
            """;

        var result = LuaStaticDependencyScanner.Scan(source);

        Assert.Equal(["automation.mouse"], result.RequiredModules);
        Assert.Equal(["images/button.png"], result.ImagePaths);
        Assert.False(result.HasDynamicRequire);
        Assert.False(result.HasDynamicImageLoad);
    }

    [Fact]
    public void ScannerFlagsDynamicDependenciesInsteadOfSilentlyOmittingThem()
    {
        var result = LuaStaticDependencyScanner.Scan("""
            local moduleName = GetModuleName()
            require(moduleName)
            LoadImage(resolveImage())
            """);

        Assert.True(result.HasDynamicRequire);
        Assert.True(result.HasDynamicImageLoad);
        Assert.Empty(result.RequiredModules);
        Assert.Empty(result.ImagePaths);
    }

    [Fact]
    public void BuilderCreatesSelfContainedFolderAndLauncherZipWithVerifiedManifest()
    {
        var root = CreateTemporaryDirectory();
        var runtime = Path.Combine(root, "runtime");
        var project = Path.Combine(root, "project");
        var output = Path.Combine(root, "output");
        Directory.CreateDirectory(runtime);
        Directory.CreateDirectory(Path.Combine(project, "lib"));
        Directory.CreateDirectory(Path.Combine(project, "images"));
        WriteSelfContainedRuntime(runtime);

        File.WriteAllText(Path.Combine(project, "main.lua"),
            "local helper = require(\"lib.helper\")\nLoadImage(\"images/button.png\")\nLog(Args.message)\n");
        File.WriteAllText(Path.Combine(project, "lib", "helper.lua"), "return { value = 42 }\n");
        File.WriteAllText(Path.Combine(project, "images", "button.png"), "test image bytes");
        var schema = new ScriptParameterSchema
        {
            ScriptId = "main",
            Parameters =
            [
                new()
                {
                    Id = "message",
                    Name = "message",
                    Label = "消息",
                    Type = "text",
                    Required = true,
                    DefaultValue = Json("\"hello\"")
                }
            ]
        };
        File.WriteAllText(
            Path.Combine(project, "main.params.json"),
            JsonSerializer.Serialize(schema, new JsonSerializerOptions(JsonSerializerDefaults.Web)));

        try
        {
            var result = SpritePackageBuilder.Build(new SpritePackageBuildOptions
            {
                RuntimeDirectory = runtime,
                ProjectRoot = project,
                EntryScriptPaths = [Path.Combine(project, "main.lua")],
                OutputDirectory = output,
                ProductName = "订单助手",
                Version = "1.2.3",
                Description = "自动化订单处理",
                Author = "测试作者",
                MinimumApiVersion = "0.7",
                MinimumRuntimeVersion = "10.0"
            });

            Assert.True(Directory.Exists(result.PackageDirectory));
            Assert.True(File.Exists(result.LauncherZip));
            Assert.True(File.Exists(Path.Combine(result.PackageDirectory, result.Manifest.LauncherExecutable)));
            Assert.Single(result.Manifest.Entries);
            Assert.NotNull(result.Manifest.Entries[0].ParameterSchemaPath);

            var manifest = result.Manifest;
            var entry = manifest.Entries[0];
            Assert.True(File.Exists(Path.Combine(result.PackageDirectory, entry.ScriptPath.Replace('/', Path.DirectorySeparatorChar))));
            Assert.True(File.Exists(Path.Combine(result.PackageDirectory, entry.ParameterSchemaPath!.Replace('/', Path.DirectorySeparatorChar))));
            Assert.True(File.Exists(Path.Combine(result.PackageDirectory, entry.ModuleRootPath.Replace('/', Path.DirectorySeparatorChar), "lib", "helper.lua")));
            Assert.Contains(manifest.Files, f => f.Path.EndsWith("/images/button.png", StringComparison.Ordinal));
            Assert.Empty(SpritePackageBuilder.Verify(result.PackageDirectory));
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch (IOException) { }
        }
    }

    [Fact]
    public void VerifierDetectsTamperedAndUnlistedPackageFiles()
    {
        var root = CreateTemporaryDirectory();
        var runtime = Path.Combine(root, "runtime");
        var project = Path.Combine(root, "project");
        var output = Path.Combine(root, "output");
        Directory.CreateDirectory(runtime);
        Directory.CreateDirectory(project);
        WriteSelfContainedRuntime(runtime);
        File.WriteAllText(Path.Combine(project, "main.lua"), "Log(\"ok\")\n");

        try
        {
            var result = SpritePackageBuilder.Build(new SpritePackageBuildOptions
            {
                RuntimeDirectory = runtime,
                ProjectRoot = project,
                EntryScriptPaths = [Path.Combine(project, "main.lua")],
                OutputDirectory = output,
                ProductName = "Verifier",
                Version = "1.0.0"
            });

            var scriptPath = Path.Combine(result.PackageDirectory,
                result.Manifest.Entries[0].ScriptPath.Replace('/', Path.DirectorySeparatorChar));
            File.AppendAllText(scriptPath, "-- tampered\n");
            File.WriteAllText(Path.Combine(result.PackageDirectory, "unexpected.dll"), "not declared");

            var issues = SpritePackageBuilder.Verify(result.PackageDirectory);
            Assert.Contains(issues, issue => issue.Contains("SHA-256 不一致", StringComparison.Ordinal));
            Assert.Contains(issues, issue => issue.Contains("checksum", StringComparison.OrdinalIgnoreCase)
                || issue.Contains("SHA256SUMS", StringComparison.Ordinal));
            Assert.Contains(issues, issue => issue.Contains("未登记", StringComparison.Ordinal));
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch (IOException) { }
        }
    }

    [Fact]
    public void BuilderRejectsDynamicRequireAndModulesThatCannotBeResolved()
    {
        var root = CreateTemporaryDirectory();
        var runtime = Path.Combine(root, "runtime");
        var project = Path.Combine(root, "project");
        var output = Path.Combine(root, "output");
        Directory.CreateDirectory(runtime);
        Directory.CreateDirectory(project);
        WriteSelfContainedRuntime(runtime);
        File.WriteAllText(Path.Combine(project, "main.lua"), "require(getName())\n");

        try
        {
            var exception = Assert.Throws<InvalidDataException>(() => SpritePackageBuilder.Build(new SpritePackageBuildOptions
            {
                RuntimeDirectory = runtime,
                ProjectRoot = project,
                EntryScriptPaths = [Path.Combine(project, "main.lua")],
                OutputDirectory = output,
                ProductName = "Invalid",
                Version = "1.0.0"
            }));
            Assert.Contains("动态 require", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch (IOException) { }
        }
    }

    [Fact]
    public void BuilderRejectsFrameworkDependentRuntimeAndProjectPathTraversal()
    {
        var root = CreateTemporaryDirectory();
        var runtime = Path.Combine(root, "runtime");
        var incompleteRuntime = Path.Combine(root, "incomplete-runtime");
        var project = Path.Combine(root, "project");
        var outside = Path.Combine(root, "outside.lua");
        Directory.CreateDirectory(runtime);
        Directory.CreateDirectory(incompleteRuntime);
        Directory.CreateDirectory(project);
        WriteSelfContainedRuntime(runtime);
        File.WriteAllText(Path.Combine(project, "main.lua"), "Log(\"ok\")\n");
        File.WriteAllText(outside, "Log(\"outside\")\n");

        try
        {
            var runtimeError = Assert.Throws<InvalidDataException>(() => SpritePackageBuilder.Build(new SpritePackageBuildOptions
            {
                RuntimeDirectory = incompleteRuntime,
                ProjectRoot = project,
                EntryScriptPaths = [Path.Combine(project, "main.lua")],
                OutputDirectory = Path.Combine(root, "out1"),
                ProductName = "Invalid",
                Version = "1.0.0"
            }));
            Assert.Contains("self-contained", runtimeError.Message, StringComparison.Ordinal);

            var pathError = Assert.Throws<InvalidDataException>(() => SpritePackageBuilder.Build(new SpritePackageBuildOptions
            {
                RuntimeDirectory = runtime,
                ProjectRoot = project,
                EntryScriptPaths = [outside],
                OutputDirectory = Path.Combine(root, "out2"),
                ProductName = "Invalid",
                Version = "1.0.0"
            }));
            Assert.Contains("工程目录", pathError.Message, StringComparison.Ordinal);
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch (IOException) { }
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "anjian-sprite-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void WriteSelfContainedRuntime(string directory)
    {
        foreach (var file in new[]
        {
            "Anjian.App.exe",
            "Anjian.App.dll",
            "Anjian.App.deps.json",
            "Anjian.App.runtimeconfig.json",
            "hostfxr.dll",
            "coreclr.dll"
        })
        {
            File.WriteAllText(Path.Combine(directory, file), "runtime:" + file);
        }
        Directory.CreateDirectory(Path.Combine(directory, "runtimes", "win-x64"));
        File.WriteAllText(Path.Combine(directory, "runtimes", "win-x64", "native.dll"), "native");
    }

    private static JsonElement Json(string value)
    {
        using var document = JsonDocument.Parse(value);
        return document.RootElement.Clone();
    }
}