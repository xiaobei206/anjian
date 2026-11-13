using System.Diagnostics;
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

        Assert.Equal(new[] { "automation.mouse" }, result.RequiredModules);
        Assert.Equal(new[] { "images/button.png" }, result.ImagePaths);
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
        File.WriteAllText(Path.Combine(project, "lib", "helper.lua"), "LoadImage(\"images/button.png\")\nreturn { value = 42 }\n");
        File.WriteAllText(Path.Combine(project, "secondary.lua"), "Log(\"secondary entry\")\n");
        var ocrModels = Path.Combine(root, "ocr-models");
        Directory.CreateDirectory(ocrModels);
        File.WriteAllText(Path.Combine(ocrModels, "en-det.onnx"), "explicitly selected model");
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
                EntryScriptPaths = [Path.Combine(project, "main.lua"), Path.Combine(project, "secondary.lua")],
                OutputDirectory = output,
                ProductName = "订单助手",
                Version = "1.2.3",
                Description = "自动化订单处理",
                Author = "测试作者",
                MinimumApiVersion = "0.7",
                MinimumRuntimeVersion = "10.0",
                RuntimeOptions = new SpritePackageRuntimeOptions(3, 250, "Ctrl+F10", false, true, 2, true, true),
                OcrModelDirectories = new Dictionary<string, string> { ["en"] = ocrModels }
            });

            Assert.True(Directory.Exists(result.PackageDirectory));
            Assert.True(File.Exists(result.LauncherZip));
            Assert.True(File.Exists(result.LauncherZipChecksumPath));
            Assert.Contains("1.2.3", result.LauncherZipChecksumPath, StringComparison.Ordinal);
            Assert.True(File.Exists(Path.Combine(result.PackageDirectory, result.Manifest.LauncherExecutable)));
            Assert.Equal(2, result.Manifest.Entries.Count);
            Assert.Single(result.Manifest.OcrModels);
            Assert.Equal(3, result.Manifest.RuntimeOptions.RepeatCount);
            Assert.Equal("Ctrl+F10", result.Manifest.RuntimeOptions.StartupHotkey);
            Assert.Equal(2, result.Manifest.RuntimeOptions.MaximumRetries);
            Assert.Contains(result.Manifest.Files, f => f.Path.EndsWith("en-det.onnx", StringComparison.Ordinal));
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
            Assert.Contains(issues, issue => issue.Contains("文件大小不一致", StringComparison.Ordinal)
                || issue.Contains("SHA-256 不一致", StringComparison.Ordinal));
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
    public void BuilderRejectsUnresolvedModulesAndImagePathsOutsideProjectRoot()
    {
        var root = CreateTemporaryDirectory();
        var runtime = Path.Combine(root, "runtime");
        var project = Path.Combine(root, "project");
        var output = Path.Combine(root, "output");
        Directory.CreateDirectory(runtime);
        Directory.CreateDirectory(project);
        WriteSelfContainedRuntime(runtime);
        File.WriteAllText(Path.Combine(project, "main.lua"), "require(\"missing.module\")\n");

        try
        {
            var missingModule = Assert.Throws<FileNotFoundException>(() => SpritePackageBuilder.Build(new SpritePackageBuildOptions
            {
                RuntimeDirectory = runtime,
                ProjectRoot = project,
                EntryScriptPaths = [Path.Combine(project, "main.lua")],
                OutputDirectory = output,
                ProductName = "Invalid",
                Version = "1.0.0"
            }));
            Assert.Contains("找不到 require 模块", missingModule.Message, StringComparison.Ordinal);

            File.WriteAllText(Path.Combine(project, "main.lua"), "LoadImage(\"../outside.png\")\n");
            var traversal = Assert.Throws<InvalidDataException>(() => SpritePackageBuilder.Build(new SpritePackageBuildOptions
            {
                RuntimeDirectory = runtime,
                ProjectRoot = project,
                EntryScriptPaths = [Path.Combine(project, "main.lua")],
                OutputDirectory = Path.Combine(root, "output-traversal"),
                ProductName = "Invalid",
                Version = "1.0.0"
            }));
            Assert.Contains("越过工程根目录", traversal.Message, StringComparison.Ordinal);
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


    [Fact]
    public void PublishedRuntimeBuildsAndRunsAnIndependentSpriteWorker()
    {
        var runtime = Environment.GetEnvironmentVariable("ANJIAN_PUBLISHED_RUNTIME");
        if (string.IsNullOrWhiteSpace(runtime))
            return; // The workflow repeats this test after publishing the actual self-contained runtime.
        Assert.True(Directory.Exists(runtime), "Published runtime directory does not exist: " + runtime);

        var root = CreateTemporaryDirectory();
        var project = Path.Combine(root, "project");
        var output = Path.Combine(root, "output");
        Directory.CreateDirectory(Path.Combine(project, "lib"));
        Directory.CreateDirectory(Path.Combine(project, "images"));
        File.WriteAllText(Path.Combine(project, "main.lua"),
            "local function unusedImageProbe() return LoadImage(\"images/smoke.png\") end\nlocal moduleMessage = require(\"lib.helper\")\nLog(moduleMessage .. \":\" .. Args.message)\n");
        File.WriteAllText(Path.Combine(project, "lib", "helper.lua"), "return \"module-ok\"\n");
        File.WriteAllText(Path.Combine(project, "images", "smoke.png"), "included resource");
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
                    DefaultValue = Json("\"parameter-ok\"")
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
                ProductName = "CI-Sprite",
                Version = "1.0.0",
                MinimumApiVersion = "0.7",
                MinimumRuntimeVersion = "10.0"
            });

            Assert.Empty(SpritePackageBuilder.Verify(result.PackageDirectory));
            Assert.True(File.Exists(result.LauncherZip));
            Assert.True(File.Exists(result.LauncherZipChecksumPath));
            Assert.Contains(result.Manifest.Files, file => file.Path.EndsWith("/images/smoke.png", StringComparison.Ordinal));

            var executable = Path.Combine(result.PackageDirectory, result.Manifest.LauncherExecutable);
            Assert.True(File.Exists(executable));
            var logPath = Path.Combine(root, "sprite-smoke.log");
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = executable,
                WorkingDirectory = result.PackageDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                Environment =
                {
                    ["ANJIAN_SPRITE_SMOKE_LOG"] = logPath
                },
                ArgumentList = { "--sprite-smoke-test" }
            });
            Assert.NotNull(process);
            if (!process!.WaitForExit(90_000))
            {
                process.Kill(entireProcessTree: true);
                Assert.True(false, "Packaged sprite launcher did not finish its Worker smoke test within 90 seconds.");
            }
            var log = File.Exists(logPath) ? File.ReadAllText(logPath) : "";
            Assert.True(process.ExitCode == 0, $"Packaged sprite smoke test failed ({process.ExitCode}). {log}");
            Assert.Contains("module-ok:parameter-ok", log, StringComparison.Ordinal);
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