using Surl.Quic;

namespace Surl.Protocol.Http;

/// <summary>
/// Writes the HTTP/3 frames the server sends (RFC 9114 section 7): each is its type and payload
/// length as QUIC variable-length integers, then the payload.
/// </summary>
internal static class Http3FrameWriter
{
    /// <summary>The <c>SETTINGS_MAX_FIELD_SECTION_SIZE</c> identifier (RFC 9114 section 7.2.4.1).</summary>
    public const ulong MaxFieldSectionSizeSetting = 0x06;

    /// <summary>One frame.</summary>
    /// <param name="type">The frame type.</param>
    /// <param name="payload">The payload.</param>
    /// <returns>The frame's bytes.</returns>
    public static byte[] Frame(ulong type, ReadOnlySpan<byte> payload) =>
        [.. QuicVariableLengthInteger.Encode(type), .. QuicVariableLengthInteger.Encode((ulong)payload.Length), .. payload];

    /// <summary>
    /// The server's <c>SETTINGS</c> (ADR-0082 decisions 7 and 8): <c>SETTINGS_MAX_FIELD_SECTION_SIZE</c>
    /// <c>--max-message</c>, left out when it is 0 (no limit). The QPACK settings keep their defaults
    /// of 0 and are not sent.
    /// </summary>
    /// <param name="maxMessageBytes">The <c>--max-message</c> limit, 0 for none.</param>
    /// <returns>The frame's bytes.</returns>
    public static byte[] Settings(long maxMessageBytes) => Frame(
        Http3FrameType.Settings,
        maxMessageBytes == 0
            ? []
            : [.. QuicVariableLengthInteger.Encode(MaxFieldSectionSizeSetting), .. QuicVariableLengthInteger.Encode((ulong)maxMessageBytes)]);

    /// <summary><c>GOAWAY</c> naming <paramref name="streamId"/>, the first request stream the server will not process.</summary>
    /// <param name="streamId">A client-initiated bidirectional stream ID.</param>
    /// <returns>The frame's bytes.</returns>
    public static byte[] GoAway(ulong streamId) => Frame(Http3FrameType.GoAway, QuicVariableLengthInteger.Encode(streamId));
}
