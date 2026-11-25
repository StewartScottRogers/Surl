using System.Buffers;
using System.Buffers.Binary;
using Surl.Hpack;
using Surl.Protocol.Abstractions;

namespace Surl.Http2;

/// <summary>
/// An <see cref="IReceivedBytesRedactor"/> for the bytes a client sends on one HTTP/2 connection:
/// the connection preface, then frames. A header block that carries an <c>authorization</c> or
/// <c>proxy-authorization</c> field is logged with each of its <c>HEADERS</c> and <c>CONTINUATION</c>
/// payloads replaced by <c>****</c>. The 9-byte frame headers are always logged as received, and so
/// is every frame of a type other than <c>HEADERS</c>, <c>CONTINUATION</c> and <c>PUSH_PROMISE</c>.
/// A <c>PUSH_PROMISE</c> payload is always withheld, and once the redactor can no longer follow the
/// header blocks, so is every later payload of those three types (ADR-0182, decisions 1, 6 and 7).
/// </summary>
/// <remarks>
/// HPACK values are recoverable from a dump whether Huffman-coded or literal, and an indexed field
/// can refer to a value an earlier block put in the dynamic table, so a copy of the connection's
/// HPACK decoder decodes the blocks, in order, to know which fields each carries. A header block is
/// held back, frame by frame, until its <c>END_HEADERS</c>; <see cref="Flush"/> writes an unfinished
/// one withheld. No more than 64 KiB of payload is ever held; the cap does not count the frame
/// headers held with it, so it does not limit the number of empty frames held. A block the copy
/// cannot decode, a block whose payloads outgrow the cap, any frame other than <c>HEADERS</c> or
/// <c>CONTINUATION</c> inside an open block, a <c>HEADERS</c> inside an open block, a
/// <c>CONTINUATION</c> with no open block or on another stream than its block's (RFC 9113
/// section 6.10), and a <c>PUSH_PROMISE</c>, which a client may not send (section 8.4), each leave
/// the copy's table unknown. The open block's held frames are then written withheld, and from then
/// on every <c>HEADERS</c>, <c>CONTINUATION</c> and <c>PUSH_PROMISE</c> payload, the offending
/// frame's included if it is one of these, is withheld (fail closed). Frames of other types, one
/// that interrupts an open block included, stream through as they arrive and are never decoded.
/// </remarks>
public sealed class Http2RequestBytesRedactor : IReceivedBytesRedactor
{
    private const int FrameHeaderLength = 9;
    private const int HeldBlockLimit = 64 * 1024;
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

    private readonly HpackDecoder decoder = new(HpackDecoder.DefaultMaximumTableSize, int.MaxValue);
    private readonly byte[] frameHeader = new byte[FrameHeaderLength];
    private readonly List<(byte[] Header, byte[] Payload)> heldFrames = [];
    private int prefaceRemaining = Http2ConnectionPreface.Preface.Length;
    private int frameHeaderRead;
    private int payloadRemaining;
    private PayloadMode payloadMode = PayloadMode.PassThrough;
    private byte[] heldPayload = [];
    private int heldPayloadRead;
    private int heldBlockLength;
    private bool decoderLost;

    /// <inheritdoc />
    public void Redact(ReadOnlySpan<byte> received, ArrayBufferWriter<byte> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        var index = 0;
        while (index < received.Length)
        {
            var rest = received[index..];
            index += prefaceRemaining > 0 ? PassThroughPreface(rest, output)
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

    private int PassThroughPreface(ReadOnlySpan<byte> bytes, ArrayBufferWriter<byte> output)
    {
        var take = Math.Min(prefaceRemaining, bytes.Length);
        output.Write(bytes[..take]);
        prefaceRemaining -= take;

        return take;
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
            LoseDecoderIfBlockOpenAndStartPassThroughFrame(output);
        }
        else if (decoderLost || !FitsHeaderBlockSequence(type) || heldBlockLength + length > HeldBlockLimit)
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

    private void LoseDecoderIfBlockOpenAndStartPassThroughFrame(ArrayBufferWriter<byte> output)
    {
        // A frame of any other type ends an open block early; the block's table effects are unknown.
        if (heldFrames.Count > 0)
        {
            LoseDecoderAndWriteHeldFramesWithheld(output);
        }

        payloadMode = PayloadMode.PassThrough;
        output.Write(frameHeader);
    }

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
        heldBlockLength += heldPayload.Length;
        payloadMode = PayloadMode.PassThrough;
        payloadRemaining = 0;
        if ((frameHeader[4] & EndHeadersFlag) != 0)
        {
            CompleteBlock(output);
        }
    }

    private void CompleteBlock(ArrayBufferWriter<byte> output)
    {
        var mustWithhold = !TryReadBlockCarriesSecret(out var carriesSecret) || carriesSecret;
        if (mustWithhold)
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
        heldBlockLength = 0;
    }

    private bool TryReadBlockCarriesSecret(out bool carriesSecret)
    {
        carriesSecret = false;
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

        carriesSecret = decoded.Fields.Any(field => IsSecretName(field.Name));

        return true;
    }

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

    private static bool IsSecretName(string name) =>
        name.Equals("authorization", StringComparison.OrdinalIgnoreCase)
        || name.Equals("proxy-authorization", StringComparison.OrdinalIgnoreCase);

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
        heldBlockLength = 0;
    }
}
