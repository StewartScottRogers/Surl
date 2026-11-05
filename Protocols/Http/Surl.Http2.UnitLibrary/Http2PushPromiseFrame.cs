using System.Buffers.Binary;

namespace Surl.Http2;

/// <summary>
/// A <c>PUSH_PROMISE</c> frame (RFC 9113 section 6.6). Surl never pushes (ADR-0081 decision 6);
/// the type exists so a client's frame is read as what it is and refused by the connection.
/// </summary>
/// <param name="StreamId">The stream the promise is made on, never 0.</param>
/// <param name="PromisedStreamId">The stream the push would use.</param>
/// <param name="FieldBlockFragment">The field block fragment, padding removed.</param>
/// <param name="EndHeaders">The <c>END_HEADERS</c> flag.</param>
/// <param name="PaddingLength">The padding length, or <see langword="null"/> when the frame is not padded.</param>
internal sealed record Http2PushPromiseFrame(
    int StreamId,
    int PromisedStreamId,
    byte[] FieldBlockFragment,
    bool EndHeaders,
    int? PaddingLength = null)
    : Http2Frame(StreamId)
{
    /// <summary>The bytes the promised stream identifier takes on the wire.</summary>
    public const int PromisedStreamIdLength = 4;

    /// <inheritdoc />
    public override Http2FrameType Type => Http2FrameType.PushPromise;

    /// <inheritdoc />
    public override byte Flags => (byte)(Http2FrameFlags.When(EndHeaders, Http2FrameFlags.EndHeaders)
        | Http2FrameFlags.When(PaddingLength is not null, Http2FrameFlags.Padded));

    /// <inheritdoc />
    public override int PayloadLength =>
        PromisedStreamIdLength + FieldBlockFragment.Length + Http2Padding.AddedLength(PaddingLength);

    /// <inheritdoc />
    public override void WritePayload(Span<byte> payload)
    {
        var offset = Http2Padding.WriteLength(payload, PaddingLength);
        BinaryPrimitives.WriteUInt32BigEndian(payload[offset..], (uint)PromisedStreamId);
        FieldBlockFragment.CopyTo(payload[(offset + PromisedStreamIdLength)..]);
    }
}
