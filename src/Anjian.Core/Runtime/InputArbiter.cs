namespace Anjian.Core.Runtime;

/// <summary>
/// Process-wide FIFO ownership for desktop SendInput/SetCursorPos side effects.
/// Leases are re-entrant for the current owner so a held key can still be released
/// while other instances wait. A failed release locks all future owners until manually acknowledged.
/// </summary>
public sealed class InputArbiter : IDisposable
{
    private sealed class Waiter(RunInstanceId owner, CancellationToken token)
    {
        public RunInstanceId Owner { get; } = owner;
        public CancellationToken Token { get; } = token;
        public TaskCompletionSource<InputArbiterLease> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public LinkedListNode<Waiter>? Node { get; set; }
        public CancellationTokenRegistration Registration { get; set; }
    }

    private readonly object _sync = new();
    private readonly LinkedList<Waiter> _queue = [];
    private RunInstanceId? _owner;
    private int _leaseCount;
    private bool _faulted;
    private bool _disposed;
    private string? _faultReason;

    public bool IsFaulted { get { lock (_sync) return _faulted; } }
    public string? FaultReason { get { lock (_sync) return _faultReason; } }
    public string? CurrentOwnerId { get { lock (_sync) return _owner?.Value; } }
    public int WaitingCount { get { lock (_sync) return _queue.Count; } }

    public ValueTask<InputArbiterLease> AcquireAsync(
        RunInstanceId owner,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(owner);
        Waiter? waiter = null;
        lock (_sync)
        {
            ThrowIfUnavailable();
            // Re-entrant acquisitions let the current owner send a matching Up even after
            // its script token was cancelled. The retained Down lease still owns the desktop.
            if (_owner == owner)
            {
                _leaseCount++;
                return ValueTask.FromResult(new InputArbiterLease(this, owner));
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (_owner is null && _queue.Count == 0)
            {
                _owner = owner;
                _leaseCount = 1;
                return ValueTask.FromResult(new InputArbiterLease(this, owner));
            }

            waiter = new Waiter(owner, cancellationToken);
            waiter.Node = _queue.AddLast(waiter);
        }

        if (cancellationToken.CanBeCanceled)
        {
            waiter.Registration = cancellationToken.Register(
                static state =>
                {
                    var tuple = ((InputArbiter Arbiter, Waiter Waiter))state!;
                    tuple.Arbiter.CancelWaiter(tuple.Waiter);
                },
                (this, waiter));
        }

        return new ValueTask<InputArbiterLease>(waiter.Completion.Task);
    }

    public void MarkFaulted(RunInstanceId owner, string reason)
    {
        ArgumentNullException.ThrowIfNull(owner);
        if (string.IsNullOrWhiteSpace(reason))
            reason = "输入释放失败，桌面状态未知。";

        Waiter[] rejected;
        lock (_sync)
        {
            if (_owner is not null && _owner != owner)
                throw new InvalidOperationException("只有当前输入所有者可以锁定输入仲裁器。");

            _faulted = true;
            _faultReason = reason;
            rejected = DrainQueue();
        }

        foreach (var waiter in rejected)
        {
            waiter.Registration.Dispose();
            waiter.Completion.TrySetException(new InputArbiterFaultedException(reason));
        }
    }

    /// <summary>
    /// Must only be called after a human has verified/recovered the physical keyboard/mouse state.
    /// </summary>
    public bool TryRecoverAfterManualAcknowledgement()
    {
        lock (_sync)
        {
            if (_disposed || _owner is not null || _queue.Count != 0)
                return false;

            _faulted = false;
            _faultReason = null;
            return true;
        }
    }

    private void CancelWaiter(Waiter waiter)
    {
        var removed = false;
        lock (_sync)
        {
            if (waiter.Node?.List is not null)
            {
                _queue.Remove(waiter.Node);
                waiter.Node = null;
                removed = true;
            }
        }

        if (removed)
            waiter.Completion.TrySetCanceled(waiter.Token);
    }

    private void Release(RunInstanceId owner)
    {
        Waiter? next = null;
        lock (_sync)
        {
            if (_owner != owner || _leaseCount <= 0)
                return;

            _leaseCount--;
            if (_leaseCount != 0)
                return;

            _owner = null;
            if (_faulted || _disposed)
                return;

            while (_queue.First is { } node)
            {
                _queue.RemoveFirst();
                var candidate = node.Value;
                candidate.Node = null;
                if (candidate.Token.IsCancellationRequested)
                {
                    candidate.Registration.Dispose();
                    candidate.Completion.TrySetCanceled(candidate.Token);
                    continue;
                }

                _owner = candidate.Owner;
                _leaseCount = 1;
                next = candidate;
                break;
            }
        }

        if (next is not null)
        {
            next.Registration.Dispose();
            next.Completion.TrySetResult(new InputArbiterLease(this, next.Owner));
        }
    }

    private Waiter[] DrainQueue()
    {
        var result = _queue.ToArray();
        _queue.Clear();
        foreach (var waiter in result)
            waiter.Node = null;
        return result;
    }

    private void ThrowIfUnavailable()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_faulted)
            throw new InputArbiterFaultedException(_faultReason ?? "全局输入仲裁器已锁定。");
    }

    public void Dispose()
    {
        Waiter[] rejected;
        lock (_sync)
        {
            if (_disposed)
                return;
            _disposed = true;
            rejected = DrainQueue();
        }

        foreach (var waiter in rejected)
        {
            waiter.Registration.Dispose();
            waiter.Completion.TrySetException(new ObjectDisposedException(nameof(InputArbiter)));
        }
    }

    public sealed class InputArbiterLease : IDisposable
    {
        private InputArbiter? _arbiter;
        private readonly RunInstanceId _owner;

        public string OwnerId => _owner.Value;

        internal InputArbiterLease(InputArbiter arbiter, RunInstanceId owner)
        {
            _arbiter = arbiter;
            _owner = owner;
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref _arbiter, null)?.Release(_owner);
        }
    }
}

public sealed class InputArbiterFaultedException(string message) : InvalidOperationException(message);
