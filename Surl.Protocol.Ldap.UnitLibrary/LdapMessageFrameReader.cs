using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Ldap;

/// <summary>
/// Reads <c>LDAPMessage</c> encodings from one connection, one after another (RFC 4511,
/// section 5.1), without decoding them, and the SASL security layer's buffers once a bind has
/// installed one (RFC 4422 section 3.7; ADR-0072 decision 4).
/// </summary>
/// <remarks>
/// The tag and length are read one byte at a time, so a message over the message limit is
/// refused from its length alone, before any of its value is read (ADR-0006: <c>--max-message</c>
/// bounds an LDAP message). The limit counts the whole message, tag and length included. Only the
/// definite length form is accepted, with at most four length octets: a longer length could never
/// describe a message this reader can hold. The value is read in reads of at least
/// <see cref="ReadAheadBytes"/>, so bytes the client sent after it - pipelined - may already be
/// held; the next read takes them first, and <see cref="DiscardReadAhead"/> throws them away
/// before a <c>StartTLS</c> upgrade (ADR-0010). The value's buffer grows as its bytes arrive
/// rather than being allocated at the announced length, so a length the peer never sends costs
/// at most 64 KiB and at most twice what did arrive. It is not safe for concurrent calls, and
/// after any outcome but <see cref="LdapFrameReadOutcome.FrameRead"/> the caller stops reading.
/// </remarks>
internal sealed class LdapMessageFrameReader
{
    /// <summary>
    /// The fewest bytes a read of a value asks the connection for; what arrives past the value is
    /// held for the next read.
    /// </summary>
    public const int ReadAheadBytes = 4096;

    private const byte SequenceTag = 0x30;
    private const int IndefiniteLengthOctet = 0x80;
    private const int MaxLengthOctets = 4;
    private const int SecurityLayerLengthOctets = 4;

    /// <summary>
    /// How much of a value is allocated before any of it arrives. The buffer doubles as bytes
    /// come, so a peer that announces a long message and sends nothing costs no more.
    /// </summary>
    private const int InitialValueBytes = 65536;

    private readonly IConnection connection;
    private readonly long maxMessageBytes;
    private readonly byte[] oneByte = new byte[1];
    private readonly byte[] readAhead = new byte[ReadAheadBytes];
    private int readAheadStart;
    private int readAheadEnd;

    /// <summary>
    /// Creates a reader over <paramref name="connection"/>.
    /// </summary>
    /// <param name="connection">The connection to read from.</param>
    /// <param name="maxMessageBytes">The most bytes a message may hold, tag and length included; 0 means no limit.</param>
    public LdapMessageFrameReader(IConnection connection, long maxMessageBytes)
    {
        this.connection = connection;
        this.maxMessageBytes = maxMessageBytes;
    }

    /// <summary>
    /// Reads the next message's whole encoding.
    /// </summary>
    /// <param name="cancellationToken">Cuts the read off.</param>
    /// <returns>The message's encoding, or the named reason there is none.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> cut the read off.</exception>
    /// <exception cref="IOException">The connection was aborted, reset or failed.</exception>
    public async ValueTask<LdapFrameReadResult> ReadFrameAsync(CancellationToken cancellationToken)
    {
        var tag = await ReadByteAsync(cancellationToken);
        if (tag < 0)
        {
            return LdapFrameReadResult.NoFrame(LdapFrameReadOutcome.ConnectionClosed);
        }

        return tag == SequenceTag
            ? await ReadSequenceAsync(cancellationToken)
            : LdapFrameReadResult.NoFrame(LdapFrameReadOutcome.NotASequence);
    }

    /// <summary>
    /// Reads the next security-layer buffer: a 4-byte big-endian length, then that many protected
    /// bytes (ADR-0072 decision 4). A length past <paramref name="maxProtectedBytes"/> or the
    /// message limit is refused before any protected byte is read.
    /// </summary>
    /// <param name="maxProtectedBytes">The most protected bytes the security layer takes from the peer.</param>
    /// <param name="cancellationToken">Cuts the read off.</param>
    /// <returns>
    /// The protected bytes, without their length, as <see cref="LdapFrameReadResult.Message"/>; or
    /// the named reason there are none.
    /// </returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> cut the read off.</exception>
    /// <exception cref="IOException">The connection was aborted, reset or failed.</exception>
    public async ValueTask<LdapFrameReadResult> ReadSecurityLayerBufferAsync(int maxProtectedBytes, CancellationToken cancellationToken)
    {
        var length = 0L;
        for (var index = 0; index < SecurityLayerLengthOctets; index++)
        {
            var octet = await ReadByteAsync(cancellationToken);
            if (octet < 0)
            {
                return LdapFrameReadResult.NoFrame(index == 0 ? LdapFrameReadOutcome.ConnectionClosed : LdapFrameReadOutcome.ConnectionClosedMidMessage);
            }

            length = (length << 8) | (uint)octet;
        }

        return length > maxProtectedBytes || IsPastTheLimit(length)
            ? LdapFrameReadResult.TooLarge(length)
            : await ReadValueAsync([], (int)length, cancellationToken);
    }

    /// <summary>
    /// Throws away the bytes already read past the last message or buffer, as a server does
    /// before it upgrades the connection to TLS (ADR-0010).
    /// </summary>
    /// <returns>How many bytes were thrown away.</returns>
    public int DiscardReadAhead()
    {
        var discarded = readAheadEnd - readAheadStart;
        readAheadStart = 0;
        readAheadEnd = 0;

        return discarded;
    }

    // Reads the length and value of a message whose SEQUENCE tag is already read.
    private async ValueTask<LdapFrameReadResult> ReadSequenceAsync(CancellationToken cancellationToken)
    {
        var header = new List<byte> { SequenceTag };
        var (outcome, valueLength) = await ReadLengthAsync(header, cancellationToken);
        if (outcome is not LdapFrameReadOutcome.FrameRead and not LdapFrameReadOutcome.MessageTooLarge)
        {
            return LdapFrameReadResult.NoFrame(outcome);
        }

        return outcome == LdapFrameReadOutcome.MessageTooLarge || IsPastTheLimit(header.Count + valueLength)
            ? LdapFrameReadResult.TooLarge(header.Count + valueLength)
            : await ReadValueAsync(header, (int)valueLength, cancellationToken);
    }

    private bool IsPastTheLimit(long messageBytes) => maxMessageBytes > 0 && messageBytes > maxMessageBytes;

    // Reads the length octets into header, after the tag already there.
    private async ValueTask<(LdapFrameReadOutcome Outcome, long ValueLength)> ReadLengthAsync(
        List<byte> header, CancellationToken cancellationToken)
    {
        var first = await ReadByteAsync(cancellationToken);
        if (first < 0)
        {
            return (LdapFrameReadOutcome.ConnectionClosedMidMessage, 0);
        }

        header.Add((byte)first);
        return first switch
        {
            < IndefiniteLengthOctet => (LdapFrameReadOutcome.FrameRead, first),
            IndefiniteLengthOctet => (LdapFrameReadOutcome.IndefiniteLength, 0),
            > IndefiniteLengthOctet + MaxLengthOctets => (LdapFrameReadOutcome.MalformedLength, 0),
            _ => await ReadLongLengthAsync(header, first - IndefiniteLengthOctet, cancellationToken),
        };
    }

    private async ValueTask<(LdapFrameReadOutcome Outcome, long ValueLength)> ReadLongLengthAsync(
        List<byte> header, int lengthOctets, CancellationToken cancellationToken)
    {
        var length = 0L;
        for (var index = 0; index < lengthOctets; index++)
        {
            var octet = await ReadByteAsync(cancellationToken);
            if (octet < 0)
            {
                return (LdapFrameReadOutcome.ConnectionClosedMidMessage, 0);
            }

            header.Add((byte)octet);
            length = (length << 8) | (uint)octet;
        }

        // A value this long could never be held in one array, whatever the limit.
        return length > Array.MaxLength - header.Count
            ? (LdapFrameReadOutcome.MessageTooLarge, length)
            : (LdapFrameReadOutcome.FrameRead, length);
    }

    // One byte, from what is held first, else straight off the connection so nothing past it is read.
    private async ValueTask<int> ReadByteAsync(CancellationToken cancellationToken)
    {
        if (readAheadStart < readAheadEnd)
        {
            return readAhead[readAheadStart++];
        }

        return await connection.ReadAsync(oneByte, cancellationToken) == 0 ? -1 : oneByte[0];
    }

    // Some bytes into destination: what is held first; else a read of at least ReadAheadBytes,
    // whose surplus is held for the next read. 0 when the peer has closed.
    private async ValueTask<int> ReadSomeAsync(Memory<byte> destination, CancellationToken cancellationToken)
    {
        if (readAheadStart == readAheadEnd)
        {
            if (destination.Length >= ReadAheadBytes)
            {
                return await connection.ReadAsync(destination, cancellationToken);
            }

            readAheadStart = 0;
            readAheadEnd = await connection.ReadAsync(readAhead, cancellationToken);
        }

        var count = Math.Min(destination.Length, readAheadEnd - readAheadStart);
        readAhead.AsMemory(readAheadStart, count).CopyTo(destination);
        readAheadStart += count;

        return count;
    }

    // Reads exactly valueLength bytes after the header, growing the buffer as they arrive.
    private async ValueTask<LdapFrameReadResult> ReadValueAsync(List<byte> header, int valueLength, CancellationToken cancellationToken)
    {
        var target = header.Count + valueLength;
        var message = new byte[Math.Min(target, header.Count + InitialValueBytes)];
        header.CopyTo(message);
        var filled = header.Count;
        while (filled < target)
        {
            if (filled == message.Length)
            {
                Array.Resize(ref message, (int)Math.Min(target, 2L * message.Length));
            }

            var read = await ReadSomeAsync(message.AsMemory(filled, message.Length - filled), cancellationToken);
            if (read == 0)
            {
                return LdapFrameReadResult.NoFrame(LdapFrameReadOutcome.ConnectionClosedMidMessage);
            }

            filled += read;
        }

        return LdapFrameReadResult.Read(message);
    }
}
