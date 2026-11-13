using System.Diagnostics;
using Xunit;

namespace Anjian.Core.Tests;

public sealed class RunInstanceMonitorSmokeTests
{
    [Fact]
    public void PublishedRuntimeExposesCoordinatorStateEventsAndIsolatedInstanceLogs()
    {
        var runtime = Environment.GetEnvironmentVariable("ANJIAN_PUBLISHED_RUNTIME");
        if (string.IsNullOrWhiteSpace(runtime))
            return; // Windows CI repeats this against the self-contained published runtime.

        Assert.True(Directory.Exists(runtime), "Published runtime directory does not exist: " + runtime);
        var executable = Path.Combine(runtime, "Anjian.App.exe");
        Assert.True(File.Exists(executable), "Published apphost is missing: " + executable);
        var logPath = Path.Combine(Path.GetTempPath(), "anjian-monitor-smoke-" + Guid.NewGuid().ToString("N") + ".log");
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
                    ["ANJIAN_RUN_INSTANCE_MONITOR_SMOKE_LOG"] = logPath
                },
                ArgumentList = { "--run-instance-monitor-smoke-test" }
            });
            Assert.NotNull(process);
            if (!process!.WaitForExit(90_000))
            {
                process.Kill(entireProcessTree: true);
                Assert.Fail("Published coordinator monitor smoke did not finish within 90 seconds.");
            }

            var log = File.Exists(logPath) ? File.ReadAllText(logPath) : "";
            Assert.True(process.ExitCode == 0, $"Published coordinator monitor smoke failed ({process.ExitCode}). {log}");
            Assert.Contains("PASS", log, StringComparison.Ordinal);
            Assert.Contains("same-script-instance-count=2", log, StringComparison.Ordinal);
            Assert.Contains("state-events=", log, StringComparison.Ordinal);
        }
        finally
        {
            try { if (File.Exists(logPath)) File.Delete(logPath); } catch { }
        }
    }
}
