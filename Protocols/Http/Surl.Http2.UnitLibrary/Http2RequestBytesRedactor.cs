using System.Buffers;
using System.Buffers.Binary;
using Surl.Hpack;
using Surl.Protocol.Abstractions;

namespace Surl.Http2;

/// <summary>
/// An <see cref="IReceivedBytesRedactor"/> for the bytes a client sends on one HTTP/2 connection:
/// the connection preface, then frames. A header block that carries a secret field - one of
/// <see cref="HttpWithheldHeaderNames.Credentials"/>, or one the redactor was made to withhold as
/// well - is logged with each of its <c>HEADERS</c> and <c>CONTINUATION</c>
/// payloads replaced by <c>****</c>. A <c>PUSH_PROMISE</c> payload is always withheld, and so is the
/// payload of every <c>DATA</c> frame on a stream that carried a <c>CONNECT</c> header block,
/// because it holds another protocol's bytes, credentials included. Once the redactor can no longer
/// follow the header blocks, every later <c>HEADERS</c>, <c>CONTINUATION</c>, <c>PUSH_PROMISE</c>
/// and <c>DATA</c> payload is withheld, on every stream; so is every later <c>DATA</c> payload once
/// more than 4,096 streams have carried a <c>CONNECT</c> block. An empty <c>DATA</c> payload leaves
/// nothing to withhold: its frame header is written alone. After the preface, the 9-byte frame
/// headers are always logged as received, and so is every frame of any other type, and every
/// <c>DATA</c> frame none of these rules withholds (ADR-0182, decisions 1, 6 and 7, and amendments
/// 11 and 13). Bytes that do not open with the preface are withheld, as the remarks say.
/// </summary>
/// <remarks>
/// The preface is compared byte by byte as it arrives, and the bytes that match it are logged as
/// received. From the first byte that differs - an HTTP/1.x request sent in the preface's place, say -
/// the redactor writes one <c>****</c> and nothing more: not that byte, no later byte, and nothing
/// from <see cref="Flush"/> (fail closed; ADR-0182, amendment 17).
/// <para>
/// HPACK values are recoverable from a dump whether Huffman-coded or literal, and an indexed field
/// can refer to a value an earlier block put in the dynamic table, so a copy of the connection's
/// HPACK decoder decodes the blocks, in order, to know which fields each carries. A header block is
/// held back, frame by frame, until its <c>END_HEADERS</c>; <see cref="Flush"/> writes an unfinished
/// one withheld. The frames held for an open block, each counted as its 9-byte frame header plus its
/// payload, never total more than 64 KiB (65,536 bytes), so at most 7,281 frames are held, empty
/// ones included. A frame whose header and declared length would take the total past that is not
/// held. A block the copy cannot decode, a block whose frames outgrow the cap, any frame other than
/// <c>HEADERS</c> or <c>CONTINUATION</c> inside an open block, a <c>HEADERS</c> inside an open
/// block, a <c>CONTINUATION</c> with no open block or on another stream than its block's (RFC 9113
/// section 6.10), and a <c>PUSH_PROMISE</c>, which a client may not send (section 8.4), each leave
/// the copy's table unknown. The open block's held frames are then written withheld, and from then
/// on every <c>HEADERS</c>, <c>CONTINUATION</c> and <c>PUSH_PROMISE</c> payload, the offending
/// frame's included if it is one of these, is withheld (fail closed), as is every <c>DATA</c>
/// payload from then on, an interrupting <c>DATA</c> frame's included. Frames of other types are
/// never held or decoded: each is written as it arrives.
/// </para>
/// <para>
/// A stream carried a <c>CONNECT</c> header block when a block the copy decoded on it has
/// <c>:method</c> <c>CONNECT</c>, whether that block was logged or withheld; it stays one for the
/// rest of the connection. At most 4,096 such streams are told apart one by one. A connection that
/// opens more has every later <c>DATA</c> payload withheld, and no more than 4,097 stream IDs are
/// ever kept. A <c>DATA</c> frame sent on a stream before its header block, which a client may not do, is
/// logged as received.
/// </para>
/// </remarks>
public sealed class Http2RequestBytesRedactor : IReceivedBytesRedactor
{
    private const int FrameHeaderLength = 9;
    private const int HeldFramesLimit = 64 * 1024;
    private const int RememberedConnectStreamsLimit = 4096;
    private const byte DataType = 0x0;
    private const byte HeadersType = 0x1;
    private const byte PushPromiseType = 0x5;
    private const byte ContinuationType = 0x9;
    private const byte EndHeadersFlag = 0x4;
    private const byte PaddedFlag = 0x8;
    private const byte PriorityFlag = 0x20;
    private const int PriorityFieldLength = 5;

    private static readonly byte[] Withheld = "****"u8.ToArray();

    private enum PayloadMode
    {
        PassThrough,
        Discard,
        Hold,
    }

    private readonly HashSet<string> secretFieldNames =
        new(HttpWithheldHeaderNames.Credentials, StringComparer.OrdinalIgnoreCase);

    private readonly HpackDecoder decoder = new(HpackDecoder.DefaultMaximumTableSize, int.MaxValue);
    private readonly byte[] frameHeader = new byte[FrameHeaderLength];
    private readonly List<(byte[] Header, byte[] Payload)> heldFrames = [];

    // One more than the limit means more CONNECT streams were opened than can be told apart.
    private readonly HashSet<uint> connectStreams = [];
    private int prefaceRemaining = Http2ConnectionPreface.Preface.Length;
    private bool departedFromPreface;
    private int frameHeaderRead;
    private int payloadRemaining;
    private PayloadMode payloadMode = PayloadMode.PassThrough;
    private byte[] heldPayload = [];
    private int heldPayloadRead;
    private int heldFramesLength;
    private bool decoderLost;

    /// <summary>
    /// Creates a redactor that withholds the header blocks carrying one of
    /// <see cref="HttpWithheldHeaderNames.Credentials"/>.
    /// </summary>
    public Http2RequestBytesRedactor()
        : this([])
    {
    }

    /// <summary>
    /// Creates a redactor that withholds the header blocks carrying one of
    /// <see cref="HttpWithheldHeaderNames.Credentials"/> or any further field.
    /// </summary>
    /// <param name="additionalSecretFieldNames">
    /// Further fields whose value is a secret, such as <see cref="HttpWithheldHeaderNames.ApiKeysAndSessionTokens"/>,
    /// matched without regard to case.
    /// </param>
    public Http2RequestBytesRedactor(IEnumerable<string> additionalSecretFieldNames)
    {
        ArgumentNullException.ThrowIfNull(additionalSecretFieldNames);
        secretFieldNames.UnionWith(additionalSecretFieldNames);
    }

    /// <inheritdoc />
    public void Redact(ReadOnlySpan<byte> received, ArrayBufferWriter<byte> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        var index = 0;
        while (index < received.Length && !departedFromPreface)
        {
            var rest = received[index..];
            index += prefaceRemaining > 0 ? ReadPreface(rest, output)
                : payloadRemaining > 0 ? ReadPayload(rest, output)
                : ReadFrameHeaderByte(rest[0], output);
        }
    }

    /// <inheritdoc />
    public void ObserveSent(ReadOnlySpan<byte> sent)
    {
    }

    /// <inheritdoc />
    public void Flush(ArrayBufferWriter<byte> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (payloadMode == PayloadMode.Hold && payloadRemaining > 0)
        {
            // The frame in progress never completed: its header goes out, its payload is withheld.
            heldFrames.Add((frameHeader.ToArray(), []));
        }

        WriteHeldFramesWithheld(output);
        if (frameHeaderRead is > 0 and < FrameHeaderLength)
        {
            output.Write(frameHeader.AsSpan(0, frameHeaderRead));
        }

        frameHeaderRead = 0;
        payloadRemaining = 0;
    }

    // Writes the bytes that match the preface. The first one that differs shows these are not HTTP/2
    // frames, so no rule here can find a secret in them: one mask, and nothing after it (fail closed).
    private int ReadPreface(ReadOnlySpan<byte> bytes, ArrayBufferWriter<byte> output)
    {
        var preface = Http2ConnectionPreface.Preface;
        var expected = preface[(preface.Length - prefaceRemaining)..];
        var compared = Math.Min(expected.Length, bytes.Length);
        var matched = bytes[..compared].CommonPrefixLength(expected[..compared]);
        output.Write(bytes[..matched]);
        prefaceRemaining -= matched;
        if (matched < compared)
        {
            departedFromPreface = true;
            output.Write(Withheld);
        }

        return compared;
    }

    private int ReadFrameHeaderByte(byte next, ArrayBufferWriter<byte> output)
    {
        frameHeader[frameHeaderRead++] = next;
        if (frameHeaderRead < FrameHeaderLength)
        {
            return 1;
        }

        frameHeaderRead = 0;
        var length = (frameHeader[0] << 16) | (frameHeader[1] << 8) | frameHeader[2];
        StartFrame(frameHeader[3], length, output);

        return 1;
    }

    private void StartFrame(byte type, int length, ArrayBufferWriter<byte> output)
    {
        payloadRemaining = length;
        if (type is not (HeadersType or PushPromiseType or ContinuationType))
        {
            LoseDecoderIfBlockOpen(output);
            StartFrameOutsideHeaderBlocks(type, length, output);
        }
        else if (decoderLost || !FitsHeaderBlockSequence(type)
            || heldFramesLength + FrameHeaderLength + length > HeldFramesLimit)
        {
            LoseDecoderAndStartWithheldFrame(output);
        }
        else
        {
            StartHeldFrame(length, output);
        }
    }

    private bool FitsHeaderBlockSequence(byte type) => type switch
    {
        HeadersType => heldFrames.Count == 0,
        ContinuationType => heldFrames.Count > 0 && StreamIdOf(heldFrames[0].Header) == StreamIdOf(frameHeader),
        _ => false, // PUSH_PROMISE: a client may not send one.
    };

    private static uint StreamIdOf(byte[] header) =>
        BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(5)) & 0x7FFF_FFFF;

    private void LoseDecoderIfBlockOpen(ArrayBufferWriter<byte> output)
    {
        // A frame of any other type ends an open block early; the block's table effects are unknown.
        if (heldFrames.Count > 0)
        {
            LoseDecoderAndWriteHeldFramesWithheld(output);
        }
    }

    private void StartFrameOutsideHeaderBlocks(byte type, int length, ArrayBufferWriter<byte> output)
    {
        output.Write(frameHeader);
        if (type == DataType && MustWithholdData(StreamIdOf(frameHeader)))
        {
            payloadMode = PayloadMode.Discard;
            output.Write(length > 0 ? Withheld : []);
        }
        else
        {
            payloadMode = PayloadMode.PassThrough;
        }
    }

    // With the decoder lost, or too many CONNECT streams to remember, any stream may be a tunnel.
    private bool MustWithholdData(uint streamId) =>
        decoderLost || connectStreams.Count > RememberedConnectStreamsLimit || connectStreams.Contains(streamId);

    private void LoseDecoderAndStartWithheldFrame(ArrayBufferWriter<byte> output)
    {
        LoseDecoderAndWriteHeldFramesWithheld(output);
        payloadMode = PayloadMode.Discard;
        output.Write(frameHeader);
        output.Write(Withheld);
    }

    private void StartHeldFrame(int length, ArrayBufferWriter<byte> output)
    {
        payloadMode = PayloadMode.Hold;
        heldPayload = new byte[length];
        heldPayloadRead = 0;
        if (length == 0)
        {
            CompleteHeldFrame(output);
        }
    }

    private int ReadPayload(ReadOnlySpan<byte> bytes, ArrayBufferWriter<byte> output)
    {
        var take = Math.Min(payloadRemaining, bytes.Length);
        switch (payloadMode)
        {
            case PayloadMode.PassThrough:
                output.Write(bytes[..take]);
                break;
            case PayloadMode.Hold:
                bytes[..take].CopyTo(heldPayload.AsSpan(heldPayloadRead));
                heldPayloadRead += take;
                break;
        }

        payloadRemaining -= take;
        if (payloadRemaining == 0 && payloadMode == PayloadMode.Hold)
        {
            CompleteHeldFrame(output);
        }

        return take;
    }

    private void CompleteHeldFrame(ArrayBufferWriter<byte> output)
    {
        heldFrames.Add((frameHeader.ToArray(), heldPayload));
        heldFramesLength += FrameHeaderLength + heldPayload.Length;
        payloadMode = PayloadMode.PassThrough;
        payloadRemaining = 0;
        if ((frameHeader[4] & EndHeadersFlag) != 0)
        {
            CompleteBlock(output);
        }
    }

    private void CompleteBlock(ArrayBufferWriter<byte> output)
    {
        var decoded = TryDecodeBlock(out var fields);
        if (fields.Any(IsConnectMethod))
        {
            RememberConnectStream(StreamIdOf(heldFrames[0].Header));
        }

        if (!decoded || fields.Any(field => secretFieldNames.Contains(field.Name)))
        {
            WriteHeldFramesWithheld(output);
        }
        else
        {
            foreach (var (header, payload) in heldFrames)
            {
                output.Write(header);
                output.Write(payload);
            }
        }

        heldFrames.Clear();
        heldFramesLength = 0;
    }

    private void RememberConnectStream(uint streamId)
    {
        if (connectStreams.Count <= RememberedConnectStreamsLimit)
        {
            connectStreams.Add(streamId);
        }
    }

    private bool TryDecodeBlock(out IReadOnlyList<HpackHeaderField> fields)
    {
        fields = [];
        var fragment = new ArrayBufferWriter<byte>();
        foreach (var (header, payload) in heldFrames)
        {
            if (!TryAppendFragment(header[3], header[4], payload, fragment))
            {
                decoderLost = true;

                return false;
            }
        }

        var decoded = decoder.Decode(fragment.WrittenSpan);
        if (decoded.Error is not null)
        {
            decoderLost = true;

            return false;
        }

        fields = decoded.Fields;

        return true;
    }

    private static bool IsConnectMethod(HpackHeaderField field) =>
        field is { Name: ":method", Value: "CONNECT" };

    private static bool TryAppendFragment(byte type, byte flags, byte[] payload, ArrayBufferWriter<byte> fragment)
    {
        var start = 0;
        var padding = 0;
        if (type == HeadersType)
        {
            if ((flags & PaddedFlag) != 0)
            {
                if (payload.Length < 1)
                {
                    return false;
                }

                padding = payload[0];
                start = 1;
            }

            start += (flags & PriorityFlag) != 0 ? PriorityFieldLength : 0;
        }

        if (start + padding > payload.Length)
        {
            return false;
        }

        fragment.Write(payload.AsSpan(start, payload.Length - start - padding));

        return true;
    }

    private void LoseDecoderAndWriteHeldFramesWithheld(ArrayBufferWriter<byte> output)
    {
        decoderLost = true;
        WriteHeldFramesWithheld(output);
    }

    private void WriteHeldFramesWithheld(ArrayBufferWriter<byte> output)
    {
        foreach (var (header, _) in heldFrames)
        {
            output.Write(header);
            output.Write(Withheld);
        }

        heldFrames.Clear();
        heldFramesLength = 0;
    }
}
