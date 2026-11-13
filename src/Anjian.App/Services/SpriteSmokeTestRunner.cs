using System.Text.Json;
using Anjian.Core;
using Anjian.Core.Packaging;
using Anjian.Core.Parameters;

namespace Anjian.App.Services;

/// <summary>
/// Headless smoke entry used by Windows CI to prove that a renamed, self-contained sprite apphost
/// can start its own packaged Worker and execute a package-local require module with M10 Args.
/// </summary>
internal static class SpriteSmokeTestRunner
{
    public static int Run()
    {
        var logPath = Environment.GetEnvironmentVariable("ANJIAN_SPRITE_SMOKE_LOG");
        var messages = new List<string>();
        try
        {
            var packageRoot = AppContext.BaseDirectory;
            var issues = SpritePackageBuilder.Verify(packageRoot);
            if (issues.Count > 0)
                throw new InvalidDataException("包校验失败：" + string.Join("；", issues));

            var manifestPath = Path.Combine(packageRoot, SpritePackageBuilder.ManifestFileName);
            var manifest = JsonSerializer.Deserialize<SpritePackageManifest>(
                File.ReadAllText(manifestPath),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidDataException("manifest 为空。");
            var entry = manifest.Entries.FirstOrDefault()
                ?? throw new InvalidDataException("manifest 没有入口脚本。");
            var scriptPath = Resolve(packageRoot, entry.ScriptPath);
            var moduleRoot = Resolve(packageRoot, entry.ModuleRootPath);
            var ocrRoot = manifest.OcrModels.Count > 0 ? Resolve(packageRoot, manifest.OcrModels[0].RelativePath) : null;

            IReadOnlyDictionary<string, JsonElement> parameters = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            if (entry.ParameterSchemaPath is not null)
            {
                var schema = JsonSerializer.Deserialize<ScriptParameterSchema>(
                    File.ReadAllText(Resolve(packageRoot, entry.ParameterSchemaPath)),
                    new JsonSerializerOptions(JsonSerializerDefaults.Web))
                    ?? throw new InvalidDataException("参数 schema 为空。");
                var validation = ScriptParameterValidator.ValidateValues(
                    schema,
                    new Dictionary<string, JsonElement>(StringComparer.Ordinal),
                    Path.GetDirectoryName(scriptPath));
                if (!validation.IsValid)
                    throw new InvalidDataException("参数默认值验证失败：" +
                        string.Join("；", validation.Issues.Select(x => x.Label + "：" + x.Message)));
                parameters = validation.Values;
            }

            using var runner = new PlaybackRunner();
            runner.Log += message => messages.Add(message);
            runner.RunAsync(
                scriptPath,
                new PlaybackOptions(),
                parameters: parameters,
                spriteRuntimeOptions: new SpriteWorkerRuntimeOptions(moduleRoot, ocrRoot))
                .GetAwaiter().GetResult();

            if (!messages.Any(message => message.Contains("module-ok:parameter-ok", StringComparison.Ordinal)))
                throw new InvalidOperationException("Worker 未返回预期的模块/参数组合结果。");

            WriteLog(logPath, "PASS", messages);
            return 0;
        }
        catch (Exception ex)
        {
            messages.Add(ex.ToString());
            WriteLog(logPath, "FAIL", messages);
            return 2;
        }
    }

    private static string Resolve(string root, string relative)
    {
        if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative))
            throw new InvalidDataException("包内路径无效：" + relative);
        var fullRoot = Path.GetFullPath(root);
        var fullPath = Path.GetFullPath(Path.Combine(fullRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
        var prefix = fullRoot.EndsWith(Path.DirectorySeparatorChar) ? fullRoot : fullRoot + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("包内路径越界：" + relative);
        return fullPath;
    }

    private static void WriteLog(string? path, string status, IEnumerable<string> messages)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;
        try
        {
            File.WriteAllLines(path, new[] { status }.Concat(messages));
        }
        catch (IOException)
        {
            // The process exit code remains the authoritative smoke-test result.
        }
    }
}