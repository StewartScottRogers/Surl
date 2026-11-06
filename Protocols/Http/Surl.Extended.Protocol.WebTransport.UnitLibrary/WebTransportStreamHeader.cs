using Surl.Quic;

namespace Surl.Extended.Protocol.WebTransport;

/// <summary>
/// The header of a WebTransport data stream (draft-ietf-webtrans-http3-16 sections 4.2 and 4.3):
/// <c>0x41</c> or <c>0x54</c>, then the session ID, both QUIC variable-length integers.
/// </summary>
/// <param name="Kind">Which stream the header starts.</param>
/// <param name="SessionId">The session's ID: the stream ID of its CONNECT stream.</param>
public sealed record WebTransportStreamHeader(WebTransportStreamKind Kind, ulong SessionId)
{
    /// <summary>Returns the header's bytes, each integer in its shortest encoding.</summary>
    /// <returns>The header's bytes.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="SessionId"/> is above <see cref="QuicVariableLengthInteger.MaxValue"/>.</exception>
    public byte[] Encode()
    {
        var kindLength = QuicVariableLengthInteger.EncodedLength((ulong)Kind);
        var bytes = new byte[kindLength + QuicVariableLengthInteger.EncodedLength(SessionId)];
        QuicVariableLengthInteger.Write(bytes, (ulong)Kind);
        QuicVariableLengthInteger.Write(bytes.AsSpan(kindLength), SessionId);
        return bytes;
    }

    /// <summary>Reads a header from the start of a stream's bytes received so far.</summary>
    /// <param name="source">The stream's first bytes.</param>
    /// <returns>The header and its length; incomplete while it has not all arrived; malformed when the stream starts with neither <c>0x41</c> nor <c>0x54</c>.</returns>
    public static WebTransportStreamHeaderReadResult Read(ReadOnlySpan<byte> source)
    {
        if (!QuicVariableLengthInteger.TryRead(source, out var kind, out var kindLength))
        {
            return new WebTransportStreamHeaderReadResult(WebTransportReadStatus.Incomplete, null, 0);
        }

        if (kind is not ((ulong)WebTransportStreamKind.Bidirectional or (ulong)WebTransportStreamKind.Unidirectional))
        {
            return new WebTransportStreamHeaderReadResult(WebTransportReadStatus.Malformed, null, kindLength);
        }

        if (!QuicVariableLengthInteger.TryRead(source[kindLength..], out var sessionId, out var sessionIdLength))
        {
            return new WebTransportStreamHeaderReadResult(WebTransportReadStatus.Incomplete, null, 0);
        }

        var header = new WebTransportStreamHeader((WebTransportStreamKind)kind, sessionId);
        return new WebTransportStreamHeaderReadResult(WebTransportReadStatus.Complete, header, kindLength + sessionIdLength);
    }
}
