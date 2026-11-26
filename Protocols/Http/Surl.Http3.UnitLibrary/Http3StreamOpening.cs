using Surl.Quic;

namespace Surl.Http3;

/// <summary>
/// Reads a stream's opening across the chunks it arrives in, to tell whether its first variable-length
/// integer (RFC 9000 section 16) is the marker that starts a session stream, such as WebTransport's
/// (ADR-0182, amendment 35). It decides once, as soon as that integer is whole.
/// </summary>
/// <param name="sessionMarker">The first integer of a session stream.</param>
internal sealed class Http3StreamOpening(ulong sessionMarker)
{
    private readonly byte[] firstBytes = new byte[QuicVariableLengthInteger.MaxLength];
    private int firstBytesLength;
    private bool? isSessionStream;

    /// <summary>Takes the stream's next bytes and tells whether the stream is known to be a session stream.</summary>
    /// <param name="received">The bytes, in stream order.</param>
    /// <returns>
    /// Whether the stream's first integer is the marker; <see langword="false"/> while that integer is not
    /// yet whole.
    /// </returns>
    public bool TakeBytesAndCheckForSessionStream(ReadOnlySpan<byte> received)
    {
        if (isSessionStream is null)
        {
            ReadFirstInteger(received);
        }

        return isSessionStream == true;
    }

    private void ReadFirstInteger(ReadOnlySpan<byte> received)
    {
        var taken = Math.Min(received.Length, firstBytes.Length - firstBytesLength);
        received[..taken].CopyTo(firstBytes.AsSpan(firstBytesLength));
        firstBytesLength += taken;
        if (QuicVariableLengthInteger.TryRead(firstBytes.AsSpan(0, firstBytesLength), out var first, out _))
        {
            isSessionStream = first == sessionMarker;
        }
    }
}
