namespace Surl.Extended.Protocol.WebTransport;

/// <summary>
/// One of the client's flow-control limits on what the server sends a session (ADR-0117 decision 4;
/// WTH3 5.6): bytes against its <c>WT_MAX_DATA</c>, or server streams against its
/// <c>WT_MAX_STREAMS</c>, starting from its <c>SETTINGS_WT_INITIAL_*</c>. A taker that finds no
/// credit waits for the client to raise the limit, and the first to wait at each limit value says so,
/// so the session sends <c>WT_DATA_BLOCKED</c> or <c>WT_STREAMS_BLOCKED</c> once for it.
/// </summary>
/// <param name="initialLimit">The client's initial limit.</param>
internal sealed class WebTransportSendCredit(ulong initialLimit)
{
    private readonly Lock gate = new();
    private ulong limit = initialLimit;
    private ulong used;
    private ulong? reportedBlockedAt;
    private TaskCompletionSource raised = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Takes up to <paramref name="wanted"/> units of credit, waiting until at least one is free.</summary>
    /// <param name="wanted">The units wanted; at least one.</param>
    /// <param name="reportBlockedAsync">Called with the limit, once for each limit value a taker is blocked at.</param>
    /// <param name="cancellationToken">Cancelled when the session ends.</param>
    /// <returns>The units taken: between one and <paramref name="wanted"/>.</returns>
    public async Task<ulong> TakeAsync(ulong wanted, Func<ulong, Task> reportBlockedAsync, CancellationToken cancellationToken)
    {
        while (true)
        {
            Task waiting;
            ulong? blockedAt = null;
            lock (gate)
            {
                if (limit > used)
                {
                    var taken = Math.Min(wanted, limit - used);
                    used += taken;
                    return taken;
                }

                if (reportedBlockedAt != limit)
                {
                    reportedBlockedAt = limit;
                    blockedAt = limit;
                }

                waiting = raised.Task;
            }

            if (blockedAt is { } value)
            {
                await reportBlockedAsync(value);
            }

            await waiting.WaitAsync(cancellationToken);
        }
    }

    /// <summary>Takes the client's new limit, which must be above the last (WTH3 5.6.2, 5.6.3).</summary>
    /// <param name="newLimit">The limit the client sent.</param>
    /// <returns><see langword="false"/> when it does not increase the last: a flow-control error.</returns>
    public bool TryRaise(ulong newLimit)
    {
        lock (gate)
        {
            if (newLimit <= limit)
            {
                return false;
            }

            limit = newLimit;
            raised.TrySetResult();
            raised = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            return true;
        }
    }
}
