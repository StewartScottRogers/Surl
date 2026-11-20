using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Amqp1;

/// <summary>
/// The <see cref="IReceivedBytesRedactor"/> of one AMQP 1.0 connection (ADR-0182): it follows the
/// protocol header and the SASL frames that come after a SASL header (Part 2 section 2.3, Part 5),
/// across chunk boundaries, and writes the password in the initial response of a <c>sasl-init</c>
/// choosing <c>PLAIN</c> (authzid NUL user NUL password, RFC 4616) as <see cref="LineBytesRedactor.SecretMask"/>.
/// The user name stays visible and every header, size and length octet is logged as received; only
/// the password's bytes change. Every other frame is logged as received, and so is every byte after
/// one that is not an AMQP protocol header or a SASL frame of at most
/// <see cref="Amqp1FrameLimits.MinMaxFrameSize"/> bytes (the AMQP frames after SASL, or TLS records).
/// </summary>
/// <remarks>
/// A SASL frame is held until it is whole, never more than <see cref="Amqp1FrameLimits.MinMaxFrameSize"/>
/// bytes. A <c>sasl-init</c> cut off by the end of the exchange is logged up to its descriptor and
/// then as one mask.
/// </remarks>
internal sealed class Amqp1SaslInitBytesRedactor : IReceivedBytesRedactor
{
    private const int ProtocolHeaderBytes = 8;
    private const int ProtocolIdOffset = 4;
    private const int SaslProtocolId = 3;
    private const int TlsProtocolId = 2;
    private const int FrameSizeBytes = 4;
    private const int FrameHeaderBytes = 8;
    private const int DataOffsetIndex = 4;
    private const int FrameTypeIndex = 5;
    private const byte SaslFrameType = 1;
    private const byte NarrowListCode = 0xC0;
    private const byte NarrowSymbolCode = 0xA3;
    private const byte NarrowBinaryCode = 0xA0;
    private const byte WideOffset = 0x10;

    private static ReadOnlySpan<byte> ProtocolHeaderPrefix => "AMQP"u8;

    private static ReadOnlySpan<byte> SaslInitDescriptor => [0x00, 0x53, 0x41];

    private static ReadOnlySpan<byte> PlainMechanism => "PLAIN"u8;

    private readonly List<byte> held = [];
    private Stage stage = Stage.Header;

    private enum Stage
    {
        Header,
        Frames,
        PassThrough,
    }

    /// <inheritdoc/>
    public void Redact(ReadOnlySpan<byte> received, ArrayBufferWriter<byte> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        held.AddRange(received);
        while (held.Count > 0 && Advance(output))
        {
        }
    }

    /// <inheritdoc/>
    public void ObserveSent(ReadOnlySpan<byte> sent)
    {
        // The initial response is never preceded by something the server sent.
    }

    /// <inheritdoc/>
    public void Flush(ArrayBufferWriter<byte> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        var bytes = CollectionsMarshal.AsSpan(held);
        var descriptorEnd = FrameHeaderBytes + SaslInitDescriptor.Length;
        if (stage == Stage.Frames && bytes.Length > descriptorEnd && bytes[FrameTypeIndex] == SaslFrameType
            && bytes[FrameHeaderBytes..descriptorEnd].SequenceEqual(SaslInitDescriptor))
        {
            output.Write(bytes[..descriptorEnd]);
            output.Write(LineBytesRedactor.SecretMask);
        }
        else
        {
            output.Write(bytes);
        }

        held.Clear();
    }

    // Does the next step on the held bytes; false when more bytes are needed.
    private bool Advance(ArrayBufferWriter<byte> output)
    {
        switch (stage)
        {
            case Stage.Header:
                return AdvanceHeader(output);
            case Stage.Frames:
                return AdvanceFrame(output);
            default:
                Consume(held.Count, output, CollectionsMarshal.AsSpan(held));
                return false;
        }
    }

    private bool AdvanceHeader(ArrayBufferWriter<byte> output)
    {
        var bytes = CollectionsMarshal.AsSpan(held);
        if (!bytes[..Math.Min(bytes.Length, ProtocolHeaderPrefix.Length)].SequenceEqual(ProtocolHeaderPrefix[..Math.Min(bytes.Length, ProtocolHeaderPrefix.Length)]))
        {
            stage = Stage.PassThrough;
            return true;
        }

        if (bytes.Length < ProtocolHeaderBytes)
        {
            return false;
        }

        stage = bytes[ProtocolIdOffset] switch
        {
            SaslProtocolId => Stage.Frames,
            TlsProtocolId => Stage.Header,
            _ => Stage.PassThrough,
        };
        Consume(ProtocolHeaderBytes, output, bytes);
        return true;
    }

    private bool AdvanceFrame(ArrayBufferWriter<byte> output)
    {
        var bytes = CollectionsMarshal.AsSpan(held);
        if (bytes.Length < FrameSizeBytes)
        {
            return false;
        }

        var size = BinaryPrimitives.ReadUInt32BigEndian(bytes);
        if (size < FrameHeaderBytes || size > Amqp1FrameLimits.MinMaxFrameSize)
        {
            stage = Stage.PassThrough;
            return true;
        }

        if (bytes.Length < size)
        {
            return false;
        }

        var frame = bytes[..(int)size];
        if (FindPassword(frame) is var (start, end))
        {
            output.Write(frame[..start]);
            output.Write(LineBytesRedactor.SecretMask);
            output.Write(frame[end..]);
            held.RemoveRange(0, frame.Length);
        }
        else
        {
            Consume(frame.Length, output, bytes);
        }

        return true;
    }

    private void Consume(int count, ArrayBufferWriter<byte> output, ReadOnlySpan<byte> bytes)
    {
        output.Write(bytes[..count]);
        held.RemoveRange(0, count);
    }

    // The byte range of the password in a whole frame, or null when it holds none.
    private static (int Start, int End)? FindPassword(ReadOnlySpan<byte> frame)
    {
        var bodyStart = frame[DataOffsetIndex] * 4;
        if (frame[FrameTypeIndex] != SaslFrameType || bodyStart < FrameHeaderBytes || bodyStart > frame.Length)
        {
            return null;
        }

        var body = frame[bodyStart..];
        if (!body.StartsWith(SaslInitDescriptor) || ListFieldsStart(body) is not { } fields
            || !TryReadVariable(body, fields, NarrowSymbolCode, out var mechanismStart, out var mechanismEnd)
            || !body[mechanismStart..mechanismEnd].SequenceEqual(PlainMechanism)
            || !TryReadVariable(body, mechanismEnd, NarrowBinaryCode, out var responseStart, out var responseEnd))
        {
            return null;
        }

        var passwordStart = PasswordStart(body[responseStart..responseEnd]) + responseStart;
        return passwordStart < responseEnd ? (bodyStart + passwordStart, bodyStart + responseEnd) : null;
    }

    // Where the first field of the performative's list begins, or null for a list that has none.
    private static int? ListFieldsStart(ReadOnlySpan<byte> body)
    {
        var listCode = SaslInitDescriptor.Length < body.Length ? body[SaslInitDescriptor.Length] : (byte)0;
        return listCode switch
        {
            NarrowListCode => SaslInitDescriptor.Length + 3,
            NarrowListCode + WideOffset => SaslInitDescriptor.Length + 9,
            _ => null,
        };
    }

    // Reads a symbol or binary field in its one-octet or four-octet width.
    private static bool TryReadVariable(ReadOnlySpan<byte> body, int position, byte narrowCode, out int valueStart, out int valueEnd)
    {
        valueStart = 0;
        valueEnd = 0;
        if (position >= body.Length)
        {
            return false;
        }

        long length;
        if (body[position] == narrowCode)
        {
            valueStart = position + 2;
            length = valueStart <= body.Length ? body[position + 1] : 0;
        }
        else if (body[position] == narrowCode + WideOffset)
        {
            valueStart = position + 5;
            length = valueStart <= body.Length ? BinaryPrimitives.ReadUInt32BigEndian(body[(position + 1)..]) : 0;
        }
        else
        {
            return false;
        }

        valueEnd = (int)Math.Min(valueStart + length, int.MaxValue);
        return valueStart <= body.Length && valueEnd <= body.Length;
    }

    // authzid NUL user NUL password: the password follows the second NUL; with fewer NULs, whatever follows the first.
    private static int PasswordStart(ReadOnlySpan<byte> response)
    {
        var first = response.IndexOf((byte)0);
        if (first < 0)
        {
            return 0;
        }

        var second = response[(first + 1)..].IndexOf((byte)0);
        return second < 0 ? first + 1 : first + 1 + second + 1;
    }
}
