namespace Surl.Dtls;

/// <summary>
/// Splits an outgoing handshake message into fragments that each fit one record (RFC 6347
/// section 4.2.3), so every datagram the server sends stays within ADR-0087 decision 7's
/// 1200 bytes, or within the client's <c>max_fragment_length</c>.
/// </summary>
public static class DtlsHandshakeFragmenter
{
    /// <summary>The largest datagram the server sends: 1200 bytes (ADR-0087 decision 7).</summary>
    public const int MaximumDatagramLength = 1200;

    /// <summary>
    /// Splits <paramref name="message"/> into fragments of at most <paramref name="maximumFragmentLength"/>
    /// bytes each, its 12-byte header included, in order of their offsets. An empty body is one
    /// fragment of 12 bytes.
    /// </summary>
    /// <param name="message">The message to send.</param>
    /// <param name="maximumFragmentLength">
    /// The largest record plaintext the caller can send: the datagram budget less the record header
    /// and the protection's expansion, or the negotiated <c>max_fragment_length</c> when smaller.
    /// </param>
    /// <returns>Each fragment's bytes, header then body slice.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The budget leaves no room for a body byte.</exception>
    public static IReadOnlyList<byte[]> Fragment(DtlsHandshakeMessage message, int maximumFragmentLength)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maximumFragmentLength, DtlsHandshakeHeader.Size);

        var bodyPerFragment = maximumFragmentLength - DtlsHandshakeHeader.Size;
        var fragments = new List<byte[]>();
        var offset = 0;
        do
        {
            var length = Math.Min(bodyPerFragment, message.Body.Length - offset);
            fragments.Add(FragmentAt(message, offset, length));
            offset += length;
        }
        while (offset < message.Body.Length);

        return fragments;
    }

    private static byte[] FragmentAt(DtlsHandshakeMessage message, int offset, int length)
    {
        var fragment = new byte[DtlsHandshakeHeader.Size + length];
        new DtlsHandshakeHeader(message.MessageType, message.Body.Length, message.MessageSeq, offset, length).Write(fragment);
        message.Body.AsSpan(offset, length).CopyTo(fragment.AsSpan(DtlsHandshakeHeader.Size));
        return fragment;
    }
}
