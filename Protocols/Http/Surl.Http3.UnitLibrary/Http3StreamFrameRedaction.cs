using System.Buffers;
using Surl.Quic;

namespace Surl.Http3;

/// <summary>
/// Follows one stream's HTTP/3 frames across the chunks they arrive in and writes what may be logged
/// (ADR-0182, amendments 4 and 21). Each frame header is written as received, as soon as its bytes arrive.
/// A <c>HEADERS</c> payload whole in the chunk that completes its frame header is written as received when the redactor allows it; one
/// carrying a secret field, one that does not decode, and one running past that chunk are written as
/// <c>****</c> once, and every further byte of that payload is left out. Every other frame's payload is
/// written as received. A stream its opening shows to be a session stream carries no frames: from the chunk
/// that completes its first integer, every chunk is written as received (amendment 35). The bytes of that
/// integer in earlier chunks are written as the start of a frame header, so as received too.
/// </summary>
/// <param name="carriesSecret">Whether a whole <c>HEADERS</c> payload must be withheld.</param>
/// <param name="opening">
/// The stream's opening, given the stream from its first byte; <see langword="null"/> when these bytes are
/// not to be read as a session stream.
/// </param>
internal sealed class Http3StreamFrameRedaction(Func<ReadOnlySpan<byte>, bool> carriesSecret, Http3StreamOpening? opening = null)
{
    // A frame header is two variable-length integers of at most 8 bytes each (RFC 9114 section 7.1).
    private const int LongestFrameHeader = 16;

    private static readonly byte[] Withheld = "****"u8.ToArray();

    private byte[] partialHeader = [];
    private ulong payloadRemaining;
    private bool isWithholdingPayload;

    /// <summary>
    /// Whether the stream's next byte starts a frame, so a redactor without a session marker needs to
    /// remember nothing of the stream. It says nothing about a session stream, which carries no frames.
    /// </summary>
    public bool IsAtFrameBoundary => partialHeader.Length == 0 && payloadRemaining == 0;

    /// <summary>Takes the stream's next bytes and writes what may be logged of them.</summary>
    /// <param name="received">The bytes, in stream order.</param>
    /// <param name="output">Where the loggable bytes are appended.</param>
    public void Redact(ReadOnlySpan<byte> received, ArrayBufferWriter<byte> output)
    {
        if (opening?.TakeBytesAndCheckForSessionStream(received) == true)
        {
            output.Write(received);

            return;
        }

        while (received.Length > 0)
        {
            var taken = payloadRemaining > 0 ? TakePayload(received, output) : TakeFrameHeader(received, output);
            received = received[taken..];
        }
    }

    // Takes what the chunk holds of the current frame's payload, writing it unless the payload is withheld.
    private int TakePayload(ReadOnlySpan<byte> received, ArrayBufferWriter<byte> output)
    {
        var taken = (int)Math.Min(payloadRemaining, (ulong)received.Length);
        payloadRemaining -= (ulong)taken;
        if (!isWithholdingPayload)
        {
            output.Write(received[..taken]);
        }

        return taken;
    }

    // Takes a frame header, or what the chunk holds of one, and decides whether its payload is withheld.
    private int TakeFrameHeader(ReadOnlySpan<byte> received, ArrayBufferWriter<byte> output)
    {
        var before = partialHeader.Length;
        byte[] header = [.. partialHeader, .. received[..Math.Min(received.Length, LongestFrameHeader - before)]];
        if (!QuicVariableLengthInteger.TryRead(header, out var type, out var typeLength)
            || !QuicVariableLengthInteger.TryRead(header.AsSpan(typeLength), out var length, out var lengthLength))
        {
            partialHeader = header;
            output.Write(received);

            return received.Length;
        }

        partialHeader = [];
        var taken = typeLength + lengthLength - before;
        output.Write(received[..taken]);
        payloadRemaining = length;
        isWithholdingPayload = type == Http3FrameType.Headers && !IsLoggableHeadersPayload(received[taken..], length);
        if (isWithholdingPayload)
        {
            output.Write(Withheld);
        }

        return taken;
    }

    // Only a HEADERS payload that is whole in the rest of the chunk can be read, and so be shown to carry no secret.
    private bool IsLoggableHeadersPayload(ReadOnlySpan<byte> rest, ulong length) =>
        length <= (ulong)rest.Length && !carriesSecret(rest[..(int)length]);
}
