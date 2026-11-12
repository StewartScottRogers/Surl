namespace Surl.Extended.Protocol.SignalR;

/// <summary>
/// The numbering a stateful-reconnect connection keeps across its WebSockets: the records the server
/// sent and no Ack covers yet, and how many numbered records it has received (HubProtocol.md "Ack
/// Message Encoding" and "Sequence Message Encoding"; ADR-0119, decision 6).
/// </summary>
public sealed class SignalRReconnectState
{
    /// <summary>
    /// The most bytes of unacknowledged records kept a connection: ASP.NET Core's
    /// <c>StatefulReconnectBufferSize</c> default.
    /// </summary>
    public const int MaxUnacknowledgedBytes = 100000;

    private readonly Lock gate = new();
    private readonly Queue<(int SequenceId, byte[] Record)> unacknowledged = new();
    private int lastSentId;
    private int unacknowledgedBytes;
    private int receivedCount;
    private int duplicatesToDrop;
    private bool hasBeenSuspended;

    /// <summary>
    /// Whether a WebSocket of this connection has been lost already, so the next one resumes it.
    /// </summary>
    internal bool IsResumption
    {
        get
        {
            lock (gate)
            {
                return hasBeenSuspended;
            }
        }
    }

    /// <summary>
    /// Notes that the connection's WebSocket was lost and the connection is waiting for a new one.
    /// </summary>
    internal void MarkSuspended()
    {
        lock (gate)
        {
            hasBeenSuspended = true;
        }
    }

    /// <summary>
    /// Numbers a record about to be sent and keeps it until an Ack covers it.
    /// </summary>
    /// <param name="record">The record, with its terminator.</param>
    /// <returns>Whether it is kept; <see langword="false"/> when the unacknowledged records would pass <see cref="MaxUnacknowledgedBytes"/>.</returns>
    internal bool TryKeep(byte[] record)
    {
        lock (gate)
        {
            if (unacknowledgedBytes + record.Length > MaxUnacknowledgedBytes)
            {
                return false;
            }

            lastSentId++;
            unacknowledged.Enqueue((lastSentId, record));
            unacknowledgedBytes += record.Length;

            return true;
        }
    }

    /// <summary>
    /// Forgets the records a client Ack covers. An Ack below one already received frees nothing.
    /// </summary>
    /// <param name="sequenceId">The Ack's sequence ID.</param>
    /// <returns>Whether the Ack is valid; <see langword="false"/> when it is above the last record sent.</returns>
    internal bool TryAcknowledge(int sequenceId)
    {
        lock (gate)
        {
            if (sequenceId > lastSentId)
            {
                return false;
            }

            while (unacknowledged.TryPeek(out var oldest) && oldest.SequenceId <= sequenceId)
            {
                unacknowledgedBytes -= unacknowledged.Dequeue().Record.Length;
            }

            return true;
        }
    }

    /// <summary>
    /// The sequence ID the resumed connection's Sequence names: that of the oldest kept record.
    /// </summary>
    /// <returns>The sequence ID and the kept records from it, in order.</returns>
    internal (int FirstSequenceId, IReadOnlyList<byte[]> Records) Resend()
    {
        lock (gate)
        {
            var records = unacknowledged.Select(kept => kept.Record).ToArray();

            return (lastSentId - records.Length + 1, records);
        }
    }

    /// <summary>
    /// Takes a client Sequence: its next numbered message is <paramref name="nextSequenceId"/>, so
    /// those it sends again that were received already are dropped.
    /// </summary>
    /// <param name="nextSequenceId">The Sequence's sequence ID.</param>
    internal void AcceptClientSequence(int nextSequenceId)
    {
        lock (gate)
        {
            duplicatesToDrop = Math.Max(0, receivedCount - nextSequenceId + 1);
        }
    }

    /// <summary>
    /// Counts one numbered client message received.
    /// </summary>
    /// <returns>The Ack's sequence ID, or <see langword="null"/> when the message is a duplicate to drop.</returns>
    internal int? CountReceived()
    {
        lock (gate)
        {
            if (duplicatesToDrop > 0)
            {
                duplicatesToDrop--;

                return null;
            }

            return ++receivedCount;
        }
    }
}
