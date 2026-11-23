using Surl.Protocol.Abstractions;

namespace Surl.Core;

/// <summary>
/// The byte count one exchange's <see cref="FaultKind.Drop"/> rule with
/// <see cref="FaultRule.AfterBytes"/> is shared by (ADR-0124 amendment, BL-1518): the control
/// connection and every data connection the exchange opens add the bytes they send to one count,
/// and when it reaches the rule's number every connection joined to it is aborted, once - or, for a
/// graceful cut (ADR-0188), disposed, so the peer sees an ordinary end of stream.
/// </summary>
/// <remarks>
/// A server may write its control and data connections from different tasks, so the count and the
/// drop are guarded by a lock.
/// </remarks>
/// <param name="afterBytes">How many bytes the exchange may send before the drop; 1 or more.</param>
/// <param name="onDropped">Called once, when the drop happens.</param>
/// <param name="graceful">
/// <see langword="true"/> to close each joined connection through its lingering dispose, data
/// connections before the control connection (ADR-0188, decision 2); <see langword="false"/> to abort them.
/// </param>
internal sealed class ExchangeDropBudget(long afterBytes, Action onDropped, bool graceful = false)
{
    private readonly Lock gate = new();
    private readonly List<IConnection> joined = [];
    private long sentByteCount;
    private bool dropped;

    /// <summary>
    /// Gets how many bytes the exchange may send in all before the drop.
    /// </summary>
    public long AfterBytes => afterBytes;

    /// <summary>
    /// Gets a value indicating whether the drop has happened.
    /// </summary>
    public bool Dropped
    {
        get
        {
            lock (gate)
            {
                return dropped;
            }
        }
    }

    /// <summary>
    /// Joins <paramref name="connection"/>, so the drop aborts it.
    /// </summary>
    /// <param name="connection">A connection of the exchange.</param>
    public void Join(IConnection connection)
    {
        lock (gate)
        {
            joined.Add(connection);
        }
    }

    /// <summary>
    /// Takes up to <paramref name="length"/> bytes from the count.
    /// </summary>
    /// <param name="length">How many bytes a write wants to send.</param>
    /// <returns>
    /// How many of them the count allows, and whether they reach it, so the caller drops once they
    /// are written; <see langword="null"/> when the drop has already happened.
    /// </returns>
    public (int Allowed, bool Reached)? Take(int length)
    {
        lock (gate)
        {
            if (dropped)
            {
                return null;
            }

            var allowed = (int)Math.Min(length, afterBytes - sentByteCount);
            sentByteCount += allowed;
            dropped = sentByteCount == afterBytes;

            return (allowed, dropped);
        }
    }

    /// <summary>
    /// Aborts every joined connection, or closes each gracefully (the last joined first, so a data
    /// connection goes before the control connection), and reports the drop.
    /// </summary>
    /// <returns>A task that completes once every joined connection is closed and the drop reported.</returns>
    public async ValueTask DropAsync()
    {
        IConnection[] toClose;

        lock (gate)
        {
            toClose = [.. joined];
        }

        if (graceful)
        {
            Array.Reverse(toClose);
        }

        foreach (var connection in toClose)
        {
            if (graceful)
            {
                await connection.DisposeAsync();
            }
            else
            {
                connection.Abort();
            }
        }

        onDropped();
    }
}
