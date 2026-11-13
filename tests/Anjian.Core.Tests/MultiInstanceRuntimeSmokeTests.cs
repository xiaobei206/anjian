using System.Diagnostics;
using Xunit;

namespace Anjian.Core.Tests;

public sealed class MultiInstanceRuntimeSmokeTests
{
    [Fact]
    public void PublishedRuntimeRunsTwoWorkersConcurrentlyAndStopsIndependently()
    {
        var runtime = Environment.GetEnvironmentVariable("ANJIAN_PUBLISHED_RUNTIME");
        if (string.IsNullOrWhiteSpace(runtime))
            return; // Windows CI repeats this after publishing the self-contained apphost.

        Assert.True(Directory.Exists(runtime), "Published runtime directory does not exist: " + runtime);
        var executable = Path.Combine(runtime, "Anjian.App.exe");
        Assert.True(File.Exists(executable), "Published apphost is missing: " + executable);
        var logPath = Path.Combine(
            Path.GetTempPath(), "anjian-multi-worker-smoke-" + Guid.NewGuid().ToString("N") + ".log");
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = executable,
                WorkingDirectory = runtime,
                UseShellExecute = false,
                CreateNoWindow = true,
                Environment =
                {
                    ["ANJIAN_MULTI_INSTANCE_SMOKE_LOG"] = logPath
                },
                ArgumentList = { "--multi-instance-smoke-test" }
            });
            Assert.NotNull(process);
            if (!process!.WaitForExit(90_000))
            {
                process.Kill(entireProcessTree: true);
                Assert.Fail("Published multi-instance Worker smoke did not finish within 90 seconds.");
            }

            var log = File.Exists(logPath) ? File.ReadAllText(logPath) : "";
            Assert.True(process.ExitCode == 0, $"Published multi-instance Worker smoke failed ({process.ExitCode}). {log}");
            Assert.Contains("PASS", log, StringComparison.Ordinal);
            Assert.Contains("two-workers-alive:", log, StringComparison.Ordinal);
            Assert.Contains("first-stopped-second-alive:", log, StringComparison.Ordinal);
        }
        finally
        {
            try { if (File.Exists(logPath)) File.Delete(logPath); } catch { }
        }
    }
}
