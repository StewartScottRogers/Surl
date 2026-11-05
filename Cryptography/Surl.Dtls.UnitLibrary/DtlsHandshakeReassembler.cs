using System.Diagnostics.CodeAnalysis;

namespace Surl.Dtls;

/// <summary>
/// Reassembles incoming handshake fragments, in any order and with any overlap, into whole
/// messages, and hands them over in <c>message_seq</c> order (RFC 6347 sections 4.2.2 and 4.2.3).
/// The bodies being reassembled together never exceed the largest-message limit (ADR-0087
/// decision 9, <c>--max-message</c>).
/// </summary>
public sealed class DtlsHandshakeReassembler
{
    private readonly Dictionary<ushort, PartialMessage> partialMessages = [];
    private readonly int maximumBufferedLength;
    private int bufferedLength;

    /// <summary>Creates a reassembler that expects <paramref name="nextMessageSeq"/> first.</summary>
    /// <param name="maximumBufferedLength">
    /// The largest-message limit: the most body bytes that may be held at once, across every
    /// message not yet handed over.
    /// </param>
    /// <param name="nextMessageSeq">The <c>message_seq</c> of the first message to hand over.</param>
    public DtlsHandshakeReassembler(int maximumBufferedLength, ushort nextMessageSeq = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximumBufferedLength);
        this.maximumBufferedLength = maximumBufferedLength;
        NextMessageSeq = nextMessageSeq;
    }

    /// <summary>The <c>message_seq</c> of the next message <see cref="TryTakeNext"/> hands over.</summary>
    public ushort NextMessageSeq { get; private set; }

    /// <summary>
    /// Takes the handshake fragment at the start of <paramref name="source"/>, one of possibly
    /// several in a handshake record.
    /// </summary>
    /// <param name="source">The rest of a handshake record's plaintext.</param>
    /// <param name="consumed">
    /// How many bytes the fragment took; all of <paramref name="source"/> when it is
    /// <see cref="DtlsHandshakeFragmentOutcome.Malformed"/>.
    /// </param>
    /// <returns>What was done with the fragment.</returns>
    public DtlsHandshakeFragmentOutcome Accept(ReadOnlySpan<byte> source, out int consumed)
    {
        if (!DtlsHandshakeHeader.TryRead(source, out var header) || !FitsItsMessage(header, source.Length))
        {
            consumed = source.Length;
            return DtlsHandshakeFragmentOutcome.Malformed;
        }

        consumed = DtlsHandshakeHeader.Size + header.FragmentLength;
        var body = source.Slice(DtlsHandshakeHeader.Size, header.FragmentLength);
        if (header.MessageSeq < NextMessageSeq)
        {
            return DtlsHandshakeFragmentOutcome.Stale;
        }

        return partialMessages.TryGetValue(header.MessageSeq, out var partial)
            ? AddToPartial(partial, header, body)
            : StartPartial(header, body);
    }

    /// <summary>Hands over the next message in <c>message_seq</c> order once it is whole.</summary>
    /// <param name="message">The message, or <see langword="null"/> when it is not whole yet.</param>
    /// <returns><see langword="true"/> when a message was handed over.</returns>
    public bool TryTakeNext([NotNullWhen(true)] out DtlsHandshakeMessage? message)
    {
        if (!partialMessages.TryGetValue(NextMessageSeq, out var partial) || !partial.IsComplete)
        {
            message = null;
            return false;
        }

        partialMessages.Remove(NextMessageSeq);
        bufferedLength -= partial.Body.Length;
        message = new DtlsHandshakeMessage(partial.MessageType, NextMessageSeq, partial.Body);
        NextMessageSeq++;
        return true;
    }

    private static bool FitsItsMessage(DtlsHandshakeHeader header, int sourceLength) =>
        sourceLength - DtlsHandshakeHeader.Size >= header.FragmentLength
        && header.FragmentOffset + header.FragmentLength <= header.Length;

    private DtlsHandshakeFragmentOutcome StartPartial(DtlsHandshakeHeader header, ReadOnlySpan<byte> body)
    {
        if (header.Length > maximumBufferedLength - bufferedLength)
        {
            return DtlsHandshakeFragmentOutcome.OverLimit;
        }

        var partial = new PartialMessage(header.MessageType, header.Length);
        partialMessages.Add(header.MessageSeq, partial);
        bufferedLength += header.Length;
        partial.Add(header.FragmentOffset, body);
        return partial.IsComplete ? DtlsHandshakeFragmentOutcome.Completed : DtlsHandshakeFragmentOutcome.Buffered;
    }

    private static DtlsHandshakeFragmentOutcome AddToPartial(
        PartialMessage partial, DtlsHandshakeHeader header, ReadOnlySpan<byte> body)
    {
        if (partial.MessageType != header.MessageType || partial.Body.Length != header.Length)
        {
            return DtlsHandshakeFragmentOutcome.Contradictory;
        }

        if (!partial.Add(header.FragmentOffset, body))
        {
            return DtlsHandshakeFragmentOutcome.Duplicate;
        }

        return partial.IsComplete ? DtlsHandshakeFragmentOutcome.Completed : DtlsHandshakeFragmentOutcome.Buffered;
    }

    /// <summary>One message being reassembled: its body and which of its bytes have arrived.</summary>
    private sealed class PartialMessage(DtlsHandshakeType messageType, int length)
    {
        private readonly bool[] received = new bool[length];
        private int receivedCount;

        public DtlsHandshakeType MessageType { get; } = messageType;

        public byte[] Body { get; } = new byte[length];

        public bool IsComplete => receivedCount == Body.Length;

        /// <summary>Copies in the bytes not received yet; <see langword="false"/> when there were none.</summary>
        public bool Add(int offset, ReadOnlySpan<byte> fragment)
        {
            var added = 0;
            for (var index = 0; index < fragment.Length; index++)
            {
                if (!received[offset + index])
                {
                    received[offset + index] = true;
                    Body[offset + index] = fragment[index];
                    added++;
                }
            }

            receivedCount += added;
            return added > 0;
        }
    }
}
