using System.Diagnostics;
using Anjian.Core;
using Anjian.Core.Runtime;

namespace Anjian.App.Services;

/// <summary>
/// Packaged-runtime acceptance smoke: verifies two independent child Worker processes
/// can be alive at once and that cancelling one does not terminate the other.
/// </summary>
internal static class MultiInstanceSmokeTestRunner
{
    public static int Run() => RunAsync().GetAwaiter().GetResult();

    private static async Task<int> RunAsync()
    {
        var logPath = Environment.GetEnvironmentVariable("ANJIAN_MULTI_INSTANCE_SMOKE_LOG");
        var messages = new List<string>();
        var scriptPath = Path.Combine(
            Path.GetTempPath(), "Anjian2026-MultiInstance-" + Guid.NewGuid().ToString("N") + ".lua");
        try
        {
            File.WriteAllText(scriptPath, "Log(\"multi-instance-worker-start\")\nDelay(20000)\nLog(\"multi-instance-worker-end\")\n");
            using var inputArbiter = new InputArbiter();
            using var first = new PlaybackRunner(RunInstanceId.Create(), inputArbiter);
            using var second = new PlaybackRunner(RunInstanceId.Create(), inputArbiter);
            var firstReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var secondReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            first.WorkerReady += () => firstReady.TrySetResult();
            second.WorkerReady += () => secondReady.TrySetResult();
            first.Log += message => messages.Add("first: " + message);
            second.Log += message => messages.Add("second: " + message);

            var options = new PlaybackOptions { RepeatCount = 1, IntervalMilliseconds = 0 };
            var firstTask = first.RunAsync(scriptPath, options);
            await firstReady.Task.WaitAsync(TimeSpan.FromSeconds(30));
            var secondTask = second.RunAsync(scriptPath, options);
            await secondReady.Task.WaitAsync(TimeSpan.FromSeconds(30));

            var firstPid = first.WorkerProcessId
                ?? throw new InvalidOperationException("第一个 Worker 未保持存活。");
            var secondPid = second.WorkerProcessId
                ?? throw new InvalidOperationException("第二个 Worker 未保持存活。");
            if (firstPid == secondPid || !first.IsWorkerAlive || !second.IsWorkerAlive)
                throw new InvalidOperationException("两个实例未建立独立、同时存活的 Worker 进程。");

            messages.Add($"two-workers-alive:{firstPid},{secondPid}");
            first.Stop();
            await firstTask.WaitAsync(TimeSpan.FromSeconds(10));
            if (first.IsWorkerAlive || !second.IsWorkerAlive || !second.IsRunning)
                throw new InvalidOperationException("停止第一个实例时影响了第二个 Worker，或第一个 Worker 未退出。");

            messages.Add($"first-stopped-second-alive:{firstPid},{secondPid}");

            using var third = new PlaybackRunner(RunInstanceId.Create(), inputArbiter);
            var thirdReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            third.WorkerReady += () => thirdReady.TrySetResult();
            third.Log += message => messages.Add("third: " + message);
            var thirdTask = third.RunAsync(scriptPath, options);
            await thirdReady.Task.WaitAsync(TimeSpan.FromSeconds(30));
            var thirdPid = third.WorkerProcessId
                ?? throw new InvalidOperationException("第三个 Worker 未保持存活。");

            using (var crashedWorker = Process.GetProcessById(secondPid))
                crashedWorker.Kill(entireProcessTree: true);
            try
            {
                await secondTask.WaitAsync(TimeSpan.FromSeconds(10));
                throw new InvalidOperationException("被外部终止的 Worker 未将所属运行报告为失败。");
            }
            catch (InvalidOperationException ex) when (
                ex.Message.Contains("脚本执行失败", StringComparison.Ordinal))
            {
                messages.Add($"second-worker-crashed-third-alive:{secondPid},{thirdPid}");
            }

            if (second.IsWorkerAlive || !third.IsWorkerAlive)
                throw new InvalidOperationException("Worker 崩溃未局限于所属实例。");

            third.Stop();
            await thirdTask.WaitAsync(TimeSpan.FromSeconds(10));
            if (third.IsWorkerAlive)
                throw new InvalidOperationException("第三个 Worker 在 Stop 后未退出。");

            WriteLog(logPath, "PASS", messages);
            return 0;
        }
        catch (Exception ex)
        {
            messages.Add(ex.ToString());
            WriteLog(logPath, "FAIL", messages);
            return 2;
        }
        finally
        {
            try { if (File.Exists(scriptPath)) File.Delete(scriptPath); } catch { }
        }
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
            // The executable exit code remains the authoritative smoke-test result.
        }
    }
}
