using System.Diagnostics;

namespace Anjian.App.Services;

internal sealed class ExecutionGate
{
    private readonly object _sync = new();
    private TaskCompletionSource<bool> _resume = CompletedSource();
    private bool _paused;

    public bool IsPaused { get { lock (_sync) return _paused; } }

    public void Pause()
    {
        lock (_sync)
        {
            if (_paused) return;
            _paused = true;
            _resume = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    public void Resume()
    {
        TaskCompletionSource<bool>? signal = null;
        lock (_sync)
        {
            if (!_paused) return;
            _paused = false;
            signal = _resume;
        }
        signal.TrySetResult(true);
    }

    public async Task WaitUntilRunnableAsync(CancellationToken cancellationToken)
    {
        Task wait;
        lock (_sync)
            wait = _paused ? _resume.Task : Task.CompletedTask;
        await wait.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DelayAsync(int milliseconds, CancellationToken cancellationToken, bool pauseAware = true)
    {
        if (milliseconds < 0 || milliseconds > 600_000)
            throw new ArgumentOutOfRangeException(nameof(milliseconds));

        var remaining = TimeSpan.FromMilliseconds(milliseconds);
        while (remaining > TimeSpan.Zero)
        {
            if (pauseAware)
                await WaitUntilRunnableAsync(cancellationToken).ConfigureAwait(false);

            var slice = remaining > TimeSpan.FromMilliseconds(40)
                ? TimeSpan.FromMilliseconds(40)
                : remaining;
            var stopwatch = Stopwatch.StartNew();
            await Task.Delay(slice, cancellationToken).ConfigureAwait(false);
            remaining -= stopwatch.Elapsed;
        }

        if (pauseAware)
            await WaitUntilRunnableAsync(cancellationToken).ConfigureAwait(false);
    }

    private static TaskCompletionSource<bool> CompletedSource()
    {
        var source = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        source.SetResult(true);
        return source;
    }
}
