using System.Runtime.InteropServices;

namespace Surl.Quic;

/// <summary>
/// One stream of a <see cref="QuicServerConnection"/> (RFC 9000 sections 2 and 3): a client's
/// bidirectional or unidirectional stream, or a unidirectional stream the server opened. Its
/// receiving part puts the client's <c>STREAM</c> data back in order for <see cref="Read"/>, which
/// grants the client more credit as it consumes; its sending part holds what <see cref="Write"/>
/// was given until the client's credit lets the connection send it. Nothing is sent until the
/// connection's <see cref="QuicServerConnection.Transmit"/> or next <see cref="QuicServerConnection.ReceiveDatagram"/>.
/// </summary>
public sealed class QuicStream
{
    private readonly QuicStreams owner;
    private readonly ulong receiveWindow;
    private readonly QuicOrderedBytes ordered = new();
    private readonly List<byte> readable = [];
    private readonly List<byte> unsent = [];
    private ulong receiveLimit;
    private ulong highestReceived;
    private ulong? finalSize;
    private ulong readOffset;
    private ulong sendLimit;
    private ulong sendOffset;
    private ulong? blockedReportedAt;
    private bool finWritten;
    private bool finSent;
    private bool resetSent;
    private (ulong ErrorCode, ulong ReliableSize)? resetToSendAt;
    private (ulong ErrorCode, ulong ReliableSize)? resetReceived;

    internal QuicStream(QuicStreams owner, ulong id, ulong receiveWindow, ulong sendLimit)
    {
        this.owner = owner;
        Id = id;
        this.receiveWindow = receiveWindow;
        receiveLimit = receiveWindow;
        this.sendLimit = sendLimit;
    }

    /// <summary>The stream ID: bit 0 is set on a stream the server opened, bit 1 on a unidirectional stream.</summary>
    public ulong Id { get; }

    /// <summary>Whether the client opened the stream.</summary>
    public bool IsClientInitiated => (Id & 1) == 0;

    /// <summary>Whether the stream carries data both ways.</summary>
    public bool IsBidirectional => (Id & 2) == 0;

    /// <summary>Whether the stream has a receiving part: the client opened it.</summary>
    public bool CanRead => IsClientInitiated;

    /// <summary>Whether the stream has a sending part: it is bidirectional, or the server opened it.</summary>
    public bool CanWrite => IsBidirectional || !IsClientInitiated;

    /// <summary>How many bytes <see cref="Read"/> would return now.</summary>
    public int ReadableLength => readable.Count;

    /// <summary>Whether the client ended the stream with <c>FIN</c> and every byte has been read.</summary>
    public bool IsReadComplete => finalSize == readOffset && ResetErrorCode is null;

    /// <summary>
    /// The application error code of the client's <c>RESET_STREAM</c> or <c>RESET_STREAM_AT</c>, or
    /// null while it has not reset the stream or the reset has not taken effect: a
    /// <c>RESET_STREAM_AT</c> takes effect once <see cref="Read"/> has returned every byte below its
    /// Reliable Size. Unread data past that is discarded.
    /// </summary>
    public ulong? ResetErrorCode { get; private set; }

    /// <summary>The application error code of the client's <c>STOP_SENDING</c>, or null when it has not sent one; the server answers it with <c>RESET_STREAM</c> carrying the same code.</summary>
    public ulong? StopSendingErrorCode { get; private set; }

    /// <summary>How many bytes the server has given <see cref="Write"/> that are not sent yet, held back by the client's credit.</summary>
    public int UnsentLength => unsent.Count;

    /// <summary>The highest offset the client may send to, as last granted.</summary>
    internal ulong ReceiveLimit => receiveLimit;

    internal bool IsReceiveDone => !CanRead || ResetErrorCode is not null || IsReadComplete;

    internal bool IsSendDone => !CanWrite || finSent || resetSent;

    internal bool HasSomethingToSend => unsent.Count > 0 || (finWritten && !finSent && !resetSent);

    /// <summary>Whether the sending part has been reset, its <c>RESET_STREAM_AT</c> perhaps still waiting for the bytes below its Reliable Size.</summary>
    private bool IsResetting => resetSent || resetToSendAt is not null;

    /// <summary>Copies the bytes received in order and not yet read into <paramref name="buffer"/>, and grants the client more credit when half the window is used.</summary>
    /// <param name="buffer">Where the bytes go.</param>
    /// <returns>How many bytes were copied: 0 when nothing is waiting.</returns>
    public int Read(Span<byte> buffer)
    {
        var count = Math.Min(buffer.Length, readable.Count);
        if (count == 0)
        {
            return 0;
        }

        CollectionsMarshal.AsSpan(readable)[..count].CopyTo(buffer);
        readable.RemoveRange(0, count);
        readOffset += (ulong)count;
        owner.Consume((ulong)count);
        if (finalSize is null && receiveLimit - readOffset < receiveWindow / 2)
        {
            receiveLimit = readOffset + receiveWindow;
            owner.QueueControlFrame(new QuicMaxStreamDataFrame(Id, receiveLimit));
        }

        TakeResetOnceReliableBytesAreRead();
        return count;
    }

    /// <summary>Queues <paramref name="data"/> to send, and with <paramref name="fin"/> ends the stream after it.</summary>
    /// <param name="data">The bytes to send.</param>
    /// <param name="fin">Whether the stream ends after these bytes.</param>
    /// <exception cref="InvalidOperationException">The stream has no sending part, or it has ended or been reset.</exception>
    public void Write(ReadOnlySpan<byte> data, bool fin = false)
    {
        if (!CanWrite || finWritten || IsResetting)
        {
            throw new InvalidOperationException($"Stream {Id} cannot be written: it has no sending part, or it has ended or been reset.");
        }

        unsent.AddRange(data);
        finWritten = fin;
    }

    /// <summary>
    /// Abandons the sending part with <c>RESET_STREAM</c> (RFC 9000 section 3.1): data not yet sent
    /// is discarded. A sending part already reset, or whose <c>FIN</c> is sent, is left as it is.
    /// </summary>
    /// <param name="applicationErrorCode">Why, in the application protocol's error codes.</param>
    /// <exception cref="InvalidOperationException">The stream has no sending part.</exception>
    public void Reset(ulong applicationErrorCode)
    {
        if (!CanWrite)
        {
            throw new InvalidOperationException($"Stream {Id} has no sending part to reset.");
        }

        ResetSending(applicationErrorCode);
    }

    /// <summary>
    /// Abandons the sending part with <c>RESET_STREAM_AT</c> (draft-ietf-quic-reliable-stream-reset-09):
    /// the first <paramref name="reliableSize"/> bytes written are still delivered, sent and sent
    /// again until acknowledged, and the rest is discarded; the frame goes once every byte below
    /// <paramref name="reliableSize"/> has been sent, with that as its Final Size when more was
    /// sent before. Unless the connection's <see cref="QuicServerConnectionOptions.ResetStreamAt"/>
    /// is set and the client sent <c>reset_stream_at</c>, it is <see cref="Reset"/>: a plain
    /// <c>RESET_STREAM</c>. A sending part already reset, or whose <c>FIN</c> is sent, is left as it is.
    /// </summary>
    /// <param name="applicationErrorCode">Why, in the application protocol's error codes.</param>
    /// <param name="reliableSize">How many bytes from the start of the stream are still delivered.</param>
    /// <exception cref="InvalidOperationException">The stream has no sending part.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="reliableSize"/> is more than was written.</exception>
    public void ResetAt(ulong applicationErrorCode, ulong reliableSize)
    {
        if (!CanWrite)
        {
            throw new InvalidOperationException($"Stream {Id} has no sending part to reset.");
        }

        ArgumentOutOfRangeException.ThrowIfGreaterThan(reliableSize, sendOffset + (ulong)unsent.Count);
        if (!owner.SendsResetStreamAt)
        {
            ResetSending(applicationErrorCode);
        }
        else if (!IsResetting && !finSent)
        {
            var kept = (int)(reliableSize - Math.Min(reliableSize, sendOffset));
            unsent.RemoveRange(kept, unsent.Count - kept);
            finWritten = false;
            resetToSendAt = (applicationErrorCode, reliableSize);
            owner.RecordReset(Id, reliableSize);
        }
    }

    /// <summary>Asks the client to stop sending with <c>STOP_SENDING</c> (RFC 9000 section 3.5).</summary>
    /// <param name="applicationErrorCode">Why, in the application protocol's error codes.</param>
    /// <exception cref="InvalidOperationException">The stream has no receiving part.</exception>
    public void StopSending(ulong applicationErrorCode)
    {
        if (!CanRead)
        {
            throw new InvalidOperationException($"Stream {Id} has no receiving part to stop.");
        }

        owner.QueueControlFrame(new QuicStopSendingFrame(Id, applicationErrorCode));
    }

    /// <summary>Takes a <c>STREAM</c> frame's data and returns by how much the highest offset received grew, for connection flow control.</summary>
    internal ulong ReceiveData(QuicStreamFrame frame)
    {
        var end = frame.Offset + (ulong)frame.Data.Length;
        var frameType = frame.FrameType();
        RecordFinalSize(end, frame.Fin, frameType);
        var growth = Receive(end, frameType);
        if (ResetErrorCode is null)
        {
            readable.AddRange(ordered.Add(frame.Offset, frame.Data.Span));
            DiscardPastReliableSize();
        }

        return growth;
    }

    /// <summary>
    /// Takes a <c>RESET_STREAM</c> (a Reliable Size of 0) or <c>RESET_STREAM_AT</c> and returns by how
    /// much the highest offset received grew, for connection flow control. The reset takes effect
    /// once every byte below the smallest Reliable Size received has been read; a larger one later
    /// is ignored, and another error code is <c>STREAM_STATE_ERROR</c>
    /// (draft-ietf-quic-reliable-stream-reset-09 section 5.1).
    /// </summary>
    internal ulong ReceiveReset(ulong applicationErrorCode, ulong resetFinalSize, ulong reliableSize, byte frameType)
    {
        RecordFinalSize(resetFinalSize, true, frameType);
        var growth = Receive(resetFinalSize, frameType);
        if (ResetErrorCode is null)
        {
            RecordReliableSize(applicationErrorCode, reliableSize, frameType);
            DiscardPastReliableSize();
            TakeResetOnceReliableBytesAreRead();
        }

        return growth;
    }

    /// <summary>Takes a <c>STOP_SENDING</c>: the sending part is reset with the client's code.</summary>
    internal void ReceiveStopSending(ulong applicationErrorCode)
    {
        StopSendingErrorCode ??= applicationErrorCode;
        ResetSending(applicationErrorCode);
    }

    /// <summary>Takes a <c>MAX_STREAM_DATA</c>: the client's credit only grows (RFC 9000 section 4.1).</summary>
    internal void ReceiveMaxStreamData(ulong maximumStreamData) => sendLimit = Math.Max(sendLimit, maximumStreamData);

    /// <summary>
    /// Cuts what the stream's and the connection's credit allow of the unsent data into
    /// <c>STREAM</c> frames, the last carrying <c>FIN</c> once everything written is in them, and
    /// reports <c>STREAM_DATA_BLOCKED</c> once per limit that holds data back.
    /// </summary>
    internal void QueueStreamFrames(List<QuicFrame> frames)
    {
        while (HasSomethingToSend && NextFrame() is { } frame)
        {
            frames.Add(frame);
        }

        if (unsent.Count > 0 && sendLimit == sendOffset && blockedReportedAt != sendLimit)
        {
            blockedReportedAt = sendLimit;
            frames.Add(new QuicStreamDataBlockedFrame(Id, sendLimit));
        }

        if (resetToSendAt is { } reset && unsent.Count == 0)
        {
            frames.Add(new QuicResetStreamAtFrame(Id, reset.ErrorCode, sendOffset, reset.ReliableSize));
            resetToSendAt = null;
            resetSent = true;
        }
    }

    private QuicStreamFrame? NextFrame()
    {
        var credit = Math.Min(sendLimit - sendOffset, owner.ConnectionSendCredit);
        var count = (int)Math.Min(Math.Min(credit, (ulong)QuicStreams.MaxStreamFrameData), (ulong)unsent.Count);
        var fin = finWritten && count == unsent.Count;
        if (count == 0 && !fin)
        {
            return null;
        }

        var data = unsent.GetRange(0, count).ToArray();
        unsent.RemoveRange(0, count);
        var offset = sendOffset;
        sendOffset += (ulong)count;
        owner.RecordSent((ulong)count);
        finSent = fin;
        return new QuicStreamFrame(Id, offset, data, fin);
    }

    private void ResetSending(ulong applicationErrorCode)
    {
        if (IsResetting || finSent)
        {
            return;
        }

        resetSent = true;
        owner.RecordReset(Id, 0);
        unsent.Clear();
        owner.QueueControlFrame(new QuicResetStreamFrame(Id, applicationErrorCode, sendOffset));
    }

    /// <summary>Keeps the smallest Reliable Size received; a reset carrying another error code than the first is <c>STREAM_STATE_ERROR</c>.</summary>
    private void RecordReliableSize(ulong applicationErrorCode, ulong reliableSize, byte frameType)
    {
        if (resetReceived is { } earlier && earlier.ErrorCode != applicationErrorCode)
        {
            throw new QuicTransportException(
                QuicTransportErrorCode.StreamStateError,
                $"Stream {Id} was reset with error code {earlier.ErrorCode} and again with {applicationErrorCode} (draft-ietf-quic-reliable-stream-reset-09 section 5.1).",
                frameType);
        }

        resetReceived = (applicationErrorCode, Math.Min(reliableSize, resetReceived?.ReliableSize ?? reliableSize));
    }

    /// <summary>Drops readable bytes at or past the Reliable Size of a reset received: they are never delivered.</summary>
    private void DiscardPastReliableSize()
    {
        if (resetReceived is { } reset)
        {
            var kept = (int)Math.Min((ulong)readable.Count, reset.ReliableSize - Math.Min(reset.ReliableSize, readOffset));
            readable.RemoveRange(kept, readable.Count - kept);
        }
    }

    /// <summary>Lets a reset received take effect once every byte below its Reliable Size has been read, counting what it discards as consumed.</summary>
    private void TakeResetOnceReliableBytesAreRead()
    {
        if (resetReceived is { } reset && readOffset >= reset.ReliableSize)
        {
            ResetErrorCode = reset.ErrorCode;
            readable.Clear();
            owner.Consume(finalSize!.Value - readOffset);
        }
    }

    /// <summary>Checks the final size against what is known (RFC 9000 section 4.5) and records it when the frame carries it.</summary>
    private void RecordFinalSize(ulong end, bool carriesFinalSize, byte frameType)
    {
        if (BreaksFinalSize(end, carriesFinalSize))
        {
            throw new QuicTransportException(
                QuicTransportErrorCode.FinalSizeError, $"Stream {Id}'s data ends at {end}, which breaks its final size (RFC 9000 section 4.5).", frameType);
        }

        finalSize = carriesFinalSize ? end : finalSize;
    }

    /// <summary>Data past a known final size, another final size, or a final size below the data already received.</summary>
    private bool BreaksFinalSize(ulong end, bool carriesFinalSize) =>
        finalSize is { } size
            ? end > size || (carriesFinalSize && end != size)
            : carriesFinalSize && end < highestReceived;

    /// <summary>Checks <paramref name="end"/> against the stream's credit and returns by how much it raises the highest offset received.</summary>
    private ulong Receive(ulong end, byte frameType)
    {
        if (end > receiveLimit)
        {
            throw new QuicTransportException(
                QuicTransportErrorCode.FlowControlError, $"Stream {Id}'s data ends at {end}, past the {receiveLimit} bytes of credit granted (RFC 9000 section 4.1).", frameType);
        }

        var growth = end > highestReceived ? end - highestReceived : 0;
        highestReceived += growth;
        return growth;
    }
}
