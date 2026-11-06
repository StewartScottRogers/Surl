namespace Surl.Quic;

/// <summary>
/// The connection IDs the client has issued for the server to send to (RFC 9000 sections 5.1.1 and
/// 5.1.2): its first Source Connection ID as sequence number 0, then each <c>NEW_CONNECTION_ID</c>.
/// It keeps at most the server's <c>active_connection_id_limit</c> active, retires every one below the
/// largest Retire Prior To received, and moves the one in use to the lowest still active when the one
/// in use is retired.
/// </summary>
internal sealed class QuicPeerConnectionIds
{
    private readonly SortedDictionary<ulong, byte[]> active = [];
    private readonly Dictionary<ulong, byte[]> received = [];
    private readonly ulong activeConnectionIdLimit;
    private ulong retirePriorTo;
    private ulong inUseSequenceNumber;

    /// <summary>Starts with the client's first Source Connection ID, sequence number 0, in use.</summary>
    /// <param name="initialConnectionId">The Source Connection ID of the client's first Initial; it may be empty.</param>
    /// <param name="activeConnectionIdLimit">The <c>active_connection_id_limit</c> the server sent, or 2 when it sent none.</param>
    public QuicPeerConnectionIds(byte[] initialConnectionId, ulong activeConnectionIdLimit)
    {
        active[0] = initialConnectionId;
        received[0] = initialConnectionId;
        this.activeConnectionIdLimit = activeConnectionIdLimit;
    }

    /// <summary>The connection ID the server's packets carry as their Destination Connection ID.</summary>
    public ReadOnlyMemory<byte> InUse => active[inUseSequenceNumber];

    /// <summary>
    /// Takes a <c>NEW_CONNECTION_ID</c> and returns the sequence numbers the server now retires, each
    /// owed a <c>RETIRE_CONNECTION_ID</c>. A repeat of one received is ignored. One received while the
    /// client uses a zero-length connection ID, a known sequence number with another connection ID, or
    /// an active connection ID under another sequence number is <c>PROTOCOL_VIOLATION</c>; more active
    /// connection IDs than the limit once the retirements are made is <c>CONNECTION_ID_LIMIT_ERROR</c>.
    /// </summary>
    public IReadOnlyList<ulong> Receive(QuicNewConnectionIdFrame frame)
    {
        RequireNonZeroLengthConnectionIds();
        var connectionId = frame.ConnectionId.ToArray();
        if (IsRepeat(frame.SequenceNumber, connectionId))
        {
            return [];
        }

        received[frame.SequenceNumber] = connectionId;
        List<ulong> retired = [];
        if (frame.SequenceNumber < retirePriorTo)
        {
            retired.Add(frame.SequenceNumber);
        }
        else
        {
            active[frame.SequenceNumber] = connectionId;
        }

        retired.AddRange(RetireBelow(frame.RetirePriorTo));
        RequireWithinLimit();
        if (!active.ContainsKey(inUseSequenceNumber))
        {
            inUseSequenceNumber = active.Keys.First();
        }

        return retired;
    }

    private void RequireNonZeroLengthConnectionIds()
    {
        if (received[0].Length == 0)
        {
            throw new QuicTransportException(
                QuicTransportErrorCode.ProtocolViolation,
                "A client using a zero-length connection ID sent NEW_CONNECTION_ID (RFC 9000 section 19.15).",
                QuicFrameTypes.NewConnectionId);
        }
    }

    /// <summary>Whether the frame repeats one received; throws when it reuses a sequence number or an active connection ID for something else.</summary>
    private bool IsRepeat(ulong sequenceNumber, byte[] connectionId)
    {
        if (received.TryGetValue(sequenceNumber, out var known))
        {
            return known.AsSpan().SequenceEqual(connectionId)
                ? true
                : throw new QuicTransportException(
                    QuicTransportErrorCode.ProtocolViolation,
                    $"NEW_CONNECTION_ID gives sequence number {sequenceNumber} a different connection ID (RFC 9000 section 19.15).",
                    QuicFrameTypes.NewConnectionId);
        }

        if (active.Values.Any(other => other.AsSpan().SequenceEqual(connectionId)))
        {
            throw new QuicTransportException(
                QuicTransportErrorCode.ProtocolViolation,
                $"NEW_CONNECTION_ID gives an active connection ID sequence number {sequenceNumber} as well (RFC 9000 section 19.15).",
                QuicFrameTypes.NewConnectionId);
        }

        return false;
    }

    /// <summary>Raises the Retire Prior To kept and removes, in order, each active connection ID below it.</summary>
    private List<ulong> RetireBelow(ulong frameRetirePriorTo)
    {
        retirePriorTo = Math.Max(retirePriorTo, frameRetirePriorTo);
        var retired = active.Keys.TakeWhile(sequenceNumber => sequenceNumber < retirePriorTo).ToList();
        retired.ForEach(sequenceNumber => active.Remove(sequenceNumber));
        return retired;
    }

    private void RequireWithinLimit()
    {
        if ((ulong)active.Count > activeConnectionIdLimit)
        {
            throw new QuicTransportException(
                QuicTransportErrorCode.ConnectionIdLimitError,
                $"The client has {active.Count} active connection IDs; the server's active_connection_id_limit is {activeConnectionIdLimit} (RFC 9000 section 5.1.1).",
                QuicFrameTypes.NewConnectionId);
        }
    }
}
