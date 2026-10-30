using System.Collections.Concurrent;
using ApexPost.Models.ValueObjects;

namespace ApexPost.Services.Common;

/// <summary>
/// One async lock per branch. Paying for a basket is read-check-write on stock; two counters at the
/// same branch selling the last box must not both succeed. SemaphoreSlim (not <c>lock</c>) because the
/// critical section awaits file I/O. Different branches never contend.
/// </summary>
public sealed class BranchLockProvider : IDisposable
{
    private readonly ConcurrentDictionary<BranchCode, SemaphoreSlim> _locks = new();

    public async Task<IDisposable> AcquireAsync(BranchCode branch, CancellationToken cancellationToken)
    {
        var gate = _locks.GetOrAdd(branch, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new Releaser(gate);
    }

    public void Dispose()
    {
        foreach (var gate in _locks.Values)
        {
            gate.Dispose();
        }
    }

    private sealed class Releaser(SemaphoreSlim gate) : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                gate.Release();
            }
        }
    }
}
