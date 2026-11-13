using System.Collections.Concurrent;
using System.Diagnostics;
using Anjian.Core;
using Anjian.Core.Runtime;

namespace Anjian.App.Services;

/// <summary>
/// Validates the event/log contract used by the runtime instance monitor against the packaged host and real Workers.
/// </summary>
internal static class RunInstanceMonitorSmokeTestRunner
{
    public static int Run() => RunAsync().GetAwaiter().GetResult();

    private static async Task<int> RunAsync()
    {
        var logPath = Environment.GetEnvironmentVariable("ANJIAN_RUN_INSTANCE_MONITOR_SMOKE_LOG");
        var scriptPath = Path.Combine(Path.GetTempPath(), "Anjian2026-Monitor-" + Guid.NewGuid().ToString("N") + ".lua");
        var lines = new ConcurrentQueue<string>();
        var seenStates = new ConcurrentQueue<RunInstanceSnapshot>();
        var coordinator = new RunInstanceRuntimeCoordinator();
        try
        {
            File.WriteAllText(scriptPath, """
                Log("monitor-smoke-marker")
                Delay(20000)
                Log("monitor-smoke-end")
                """);
            coordinator.StateChanged += snapshot => seenStates.Enqueue(snapshot);
            coordinator.StructuredLogAdded += entry =>
                lines.Enqueue($"{entry.InstanceId?.Value ?? "GLOBAL"}|{entry.EventCode}|{entry.Message}");

            var options = new PlaybackOptions { RepeatCount = 1, IntervalMilliseconds = 0 };
            var firstTask = coordinator.StartAsync(Guid.NewGuid().ToString("N"), scriptPath, options);
            var firstId = coordinator.GetInstances().Single().InstanceId;
            var secondTask = coordinator.StartAsync(Guid.NewGuid().ToString("N"), scriptPath, options);
            var ids = coordinator.GetInstances().Select(item => item.InstanceId).ToArray();
            if (ids.Length != 2 || ids[0] == ids[1])
                throw new InvalidOperationException("Coordinator did not retain two independent same-script instance IDs.");
            var first = ids.Single(id => id == firstId);
            var second = ids.Single(id => id != firstId);

            await WaitUntilAsync(() =>
            {
                var states = coordinator.GetInstanceStatuses();
                return states.Any(item => item.InstanceId == first && item.Snapshot.State == RunInstanceState.Running) &&
                       states.Any(item => item.InstanceId == second && item.Snapshot.State == RunInstanceState.Running);
            }, TimeSpan.FromSeconds(30));

            await WaitUntilAsync(
                () => coordinator.GetLogs(first, text: "monitor-smoke-marker", includeGlobal: false).Count > 0 &&
                      coordinator.GetLogs(second, text: "monitor-smoke-marker", includeGlobal: false).Count > 0,
                TimeSpan.FromSeconds(10));

            var firstLogs = coordinator.GetLogs(first, includeGlobal: false);
            var secondLogs = coordinator.GetLogs(second, includeGlobal: false);
            if (firstLogs.Count == 0 || firstLogs.Any(entry => entry.InstanceId != first))
                throw new InvalidOperationException("First instance log query returned a missing or cross-instance record.");
            if (secondLogs.Count == 0 || secondLogs.Any(entry => entry.InstanceId != second))
                throw new InvalidOperationException("Second instance log query returned a missing or cross-instance record.");

            await coordinator.StopInstanceAsync(first);
            var afterFirstStop = coordinator.GetInstanceStatuses();
            if (afterFirstStop.Single(item => item.InstanceId == first).Snapshot.State != RunInstanceState.Cleaned ||
                afterFirstStop.Single(item => item.InstanceId == second).Snapshot.State != RunInstanceState.Running)
                throw new InvalidOperationException("Stopping the selected instance affected the wrong instance or did not finish cleanup.");

            await coordinator.StopInstanceAsync(second);
            await Task.WhenAll(firstTask, secondTask).WaitAsync(TimeSpan.FromSeconds(10));
            var finalStates = coordinator.GetInstanceStatuses();
            if (finalStates.Any(item => item.Snapshot.State != RunInstanceState.Cleaned))
                throw new InvalidOperationException("One or more instances did not reach Cleaned.");

            foreach (var id in new[] { first, second })
            {
                if (!seenStates.Any(snapshot => snapshot.InstanceId == id && snapshot.State == RunInstanceState.Running) ||
                    !seenStates.Any(snapshot => snapshot.InstanceId == id && snapshot.State == RunInstanceState.Cleaned))
                    throw new InvalidOperationException($"Coordinator state event stream was incomplete for {id.Value}.");
            }
            if (lines.Count < 2)
                throw new InvalidOperationException("Structured log event stream did not deliver both instances' records.");

            lines.Enqueue($"same-script-instance-count={finalStates.Count}");
            lines.Enqueue($"state-events={seenStates.Count}");
            WriteLog(logPath, "PASS", lines);
            return 0;
        }
        catch (Exception ex)
        {
            lines.Enqueue(ex.ToString());
            WriteLog(logPath, "FAIL", lines);
            return 2;
        }
        finally
        {
            try { await coordinator.StopAllAsync(); } catch { }
            try { coordinator.Dispose(); } catch { }
            try { if (File.Exists(scriptPath)) File.Delete(scriptPath); } catch { }
        }
    }

    private static async Task WaitUntilAsync(Func<bool> predicate, TimeSpan timeout)
    {
        var timer = Stopwatch.StartNew();
        while (timer.Elapsed < timeout)
        {
            if (predicate())
                return;
            await Task.Delay(25).ConfigureAwait(false);
        }
        throw new TimeoutException($"Coordinator monitor smoke condition did not become true within {timeout}.");
    }

    private static void WriteLog(string? path, string status, IEnumerable<string> lines)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;
        try { File.WriteAllLines(path, new[] { status }.Concat(lines)); }
        catch (IOException) { }
    }
}
