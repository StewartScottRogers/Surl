namespace Surl.Protocol.Http;

/// <summary>
/// The optional padding of <c>DATA</c>, <c>HEADERS</c> and <c>PUSH_PROMISE</c> frames (RFC 9113
/// sections 6.1, 6.2 and 6.6): a <c>Pad Length</c> byte first, and that many zero bytes last.
/// </summary>
/// <remarks>
/// A padding length of <see langword="null"/> means the frame is not padded at all, which differs
/// on the wire and in flow control from a padded frame whose padding length is 0.
/// </remarks>
internal static class Http2Padding
{
    /// <summary>
    /// The bytes padding adds to a payload: none when unpadded, else the <c>Pad Length</c> byte
    /// and the padding.
    /// </summary>
    /// <param name="paddingLength">The padding length, or <see langword="null"/> when unpadded.</param>
    /// <returns>The bytes added.</returns>
    public static int AddedLength(int? paddingLength) => paddingLength is { } length ? length + 1 : 0;

    /// <summary>
    /// Writes the <c>Pad Length</c> byte, when padded, at the start of <paramref name="payload"/>.
    /// The padding itself is the payload's last bytes, which a new array already holds as zeros.
    /// </summary>
    /// <param name="payload">The frame payload.</param>
    /// <param name="paddingLength">The padding length, or <see langword="null"/> when unpadded.</param>
    /// <returns>Where the fields after the <c>Pad Length</c> byte start.</returns>
    public static int WriteLength(Span<byte> payload, int? paddingLength)
    {
        if (paddingLength is not { } length)
        {
            return 0;
        }

        payload[0] = (byte)length;
        return 1;
    }
}
