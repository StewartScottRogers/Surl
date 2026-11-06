namespace Surl.Http2;

/// <summary>
/// Writes a typed HTTP/2 frame to the bytes RFC 9113 sections 4.1 and 6 define: the 9-byte
/// header, then the payload.
/// </summary>
/// <remarks>
/// It writes the frame it is given and judges nothing about the connection: keeping a frame
/// within the peer's <c>SETTINGS_MAX_FRAME_SIZE</c> and its flow-control windows is the
/// caller's job. It refuses only what the wire cannot say.
/// </remarks>
internal static class Http2FrameEncoder
{
    /// <summary>
    /// Encodes <paramref name="frame"/>.
    /// </summary>
    /// <param name="frame">The frame.</param>
    /// <returns>The frame's bytes.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The stream identifier is negative, or the payload is longer than 2^24-1 bytes.
    /// </exception>
    public static byte[] Encode(Http2Frame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentOutOfRangeException.ThrowIfNegative(frame.StreamId);
        var payloadLength = frame.PayloadLength;
        ArgumentOutOfRangeException.ThrowIfGreaterThan(payloadLength, Http2FrameHeader.MaxLength);

        var bytes = new byte[Http2FrameHeader.EncodedLength + payloadLength];
        new Http2FrameHeader(payloadLength, (byte)frame.Type, frame.Flags, frame.StreamId).Write(bytes);
        frame.WritePayload(bytes.AsSpan(Http2FrameHeader.EncodedLength));

        return bytes;
    }
}
