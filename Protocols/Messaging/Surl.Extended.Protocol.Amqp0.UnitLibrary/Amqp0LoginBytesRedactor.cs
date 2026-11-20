using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Amqp0;

/// <summary>
/// The <see cref="IReceivedBytesRedactor"/> of an AMQP 0-9-1 connection (ADR-0182, decision 6):
/// it holds each channel-0 method frame until it is whole, up to <c>connection.open</c>, and
/// writes <c>connection.start-ok</c> and <c>connection.secure-ok</c> with their response
/// withheld. A <c>PLAIN</c> response keeps its authorization id and login and has its password
/// written as <see cref="LineBytesRedactor.SecretMask"/>; an <c>AMQPLAIN</c> response keeps its
/// <c>LOGIN</c> and has its <c>PASSWORD</c> value masked; any other mechanism's response, and any
/// <c>secure-ok</c> response, is masked whole. The frame size and the response's length are
/// rewritten to match. Everything else - the protocol header, other frames, every frame from
/// <c>connection.open</c> on - is logged as it arrived, so a <c>basic.publish</c> body that
/// holds the password's bytes is not touched.
/// </summary>
/// <remarks>
/// A login frame that does not decode is written as its header, class and method, then
/// <c>****</c>. A frame over <see cref="MaxHeldBytes"/> is written as <c>****</c> and the rest of
/// it dropped. A frame that is not a method on channel 0 ends the holding, because the login is
/// over or the stream is not AMQP.
/// </remarks>
internal sealed class Amqp0LoginBytesRedactor : IReceivedBytesRedactor
{
    /// <summary>The most bytes held while a frame is cut: 64 KiB (ADR-0182, decision 6).</summary>
    public const int MaxHeldBytes = LineBytesRedactor.DefaultMaxHeldBytes;

    private const int ClassAndMethodBytes = 4;
    private const int SizeFieldBytes = sizeof(uint);

    private readonly List<byte> held = [];
    private int protocolHeaderBytesLeft = Amqp0ProtocolHeader.Length;
    private int frameLength = -1;
    private int bytesToDrop;
    private bool isPassingThrough;

    /// <inheritdoc />
    public void Redact(ReadOnlySpan<byte> received, ArrayBufferWriter<byte> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        var index = 0;
        while (index < received.Length)
        {
            if (isPassingThrough)
            {
                output.Write(received[index..]);
                return;
            }

            if (protocolHeaderBytesLeft > 0)
            {
                var passed = Math.Min(protocolHeaderBytesLeft, received.Length - index);
                output.Write(received.Slice(index, passed));
                index += passed;
                protocolHeaderBytesLeft -= passed;
            }
            else if (bytesToDrop > 0)
            {
                var dropped = Math.Min(bytesToDrop, received.Length - index);
                index += dropped;
                bytesToDrop -= dropped;
            }
            else
            {
                HoldNext(received[index++], output);
            }
        }
    }

    /// <inheritdoc />
    public void ObserveSent(ReadOnlySpan<byte> sent)
    {
        // A server's bytes carry nothing the client typed, so nothing changes what is masked.
    }

    /// <inheritdoc />
    public void Flush(ArrayBufferWriter<byte> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        var partial = CollectionsMarshal.AsSpan(held);
        var prefix = Amqp0Frame.HeaderBytes + ClassAndMethodBytes;
        if (partial.Length > prefix && IsResponseMethod(partial[Amqp0Frame.HeaderBytes..prefix]))
        {
            // Cut short, so no field can be told apart from its secret: keep only what names the method.
            output.Write(partial[..prefix]);
            output.Write(LineBytesRedactor.SecretMask);
        }
        else
        {
            output.Write(partial);
        }

        held.Clear();
        isPassingThrough = true;
    }

    private static bool IsResponseMethod(ReadOnlySpan<byte> classAndMethod)
    {
        var classId = BinaryPrimitives.ReadUInt16BigEndian(classAndMethod);
        var methodId = BinaryPrimitives.ReadUInt16BigEndian(classAndMethod[sizeof(ushort)..]);
        return classId == Amqp0ConnectionMethodId.ClassId
            && methodId is Amqp0ConnectionMethodId.StartOk or Amqp0ConnectionMethodId.SecureOk;
    }

    private void HoldNext(byte next, ArrayBufferWriter<byte> output)
    {
        held.Add(next);
        if (held.Count == Amqp0Frame.HeaderBytes)
        {
            if (!BeginFrame(output))
            {
                return;
            }
        }

        if (held.Count == frameLength)
        {
            WriteFrame(CollectionsMarshal.AsSpan(held), output);
            held.Clear();
            frameLength = -1;
        }
        else if (held.Count > MaxHeldBytes)
        {
            output.Write(LineBytesRedactor.SecretMask);
            bytesToDrop = frameLength - held.Count;
            held.Clear();
            frameLength = -1;
        }
    }

    // Judges the 7-byte header; false when the frame is not one the login can use.
    private bool BeginFrame(ArrayBufferWriter<byte> output)
    {
        var header = CollectionsMarshal.AsSpan(held);
        var isMethodOnChannelZero = header[0] == (byte)Amqp0FrameType.Method && BinaryPrimitives.ReadUInt16BigEndian(header[1..]) == 0;
        if (!isMethodOnChannelZero)
        {
            output.Write(header);
            held.Clear();
            isPassingThrough = true;
            return false;
        }

        frameLength = (int)Math.Min(
            (long)BinaryPrimitives.ReadUInt32BigEndian(header[3..]) + Amqp0Frame.OverheadBytes,
            int.MaxValue);
        return true;
    }

    private void WriteFrame(ReadOnlySpan<byte> frame, ArrayBufferWriter<byte> output)
    {
        var payload = frame[Amqp0Frame.HeaderBytes..^1];
        if (payload.Length < ClassAndMethodBytes)
        {
            output.Write(frame);
            isPassingThrough = true;
            return;
        }

        var classId = BinaryPrimitives.ReadUInt16BigEndian(payload);
        var methodId = BinaryPrimitives.ReadUInt16BigEndian(payload[sizeof(ushort)..]);
        if (classId != Amqp0ConnectionMethodId.ClassId || methodId == Amqp0ConnectionMethodId.Open)
        {
            output.Write(frame);
            isPassingThrough = true;
        }
        else if (methodId == Amqp0ConnectionMethodId.StartOk)
        {
            WriteStartOk(frame, output);
        }
        else if (methodId == Amqp0ConnectionMethodId.SecureOk)
        {
            WriteSecureOk(frame, output);
        }
        else
        {
            output.Write(frame);
        }
    }

    // start-ok arguments: client-properties table, mechanism short string, response long string, locale.
    private static void WriteStartOk(ReadOnlySpan<byte> frame, ArrayBufferWriter<byte> output)
    {
        var arguments = frame[(Amqp0Frame.HeaderBytes + ClassAndMethodBytes)..^1];
        if (!TryLocateStartOkResponse(arguments, out var mechanism, out var responseStart, out var responseEnd))
        {
            WriteUndecodable(frame, output);
            return;
        }

        var response = arguments[(responseStart + SizeFieldBytes)..responseEnd];
        var redacted = RedactResponse(mechanism, response);
        WriteFrameWithResponse(frame, arguments, responseStart, responseEnd, redacted, output);
    }

    private static void WriteSecureOk(ReadOnlySpan<byte> frame, ArrayBufferWriter<byte> output)
    {
        var arguments = frame[(Amqp0Frame.HeaderBytes + ClassAndMethodBytes)..^1];
        if (arguments.Length < SizeFieldBytes
            || BinaryPrimitives.ReadUInt32BigEndian(arguments) != arguments.Length - SizeFieldBytes)
        {
            WriteUndecodable(frame, output);
            return;
        }

        var response = arguments[SizeFieldBytes..];
        WriteFrameWithResponse(frame, arguments, 0, arguments.Length, response.IsEmpty ? [] : LineBytesRedactor.SecretMask.ToArray(), output);
    }

    private static void WriteUndecodable(ReadOnlySpan<byte> frame, ArrayBufferWriter<byte> output)
    {
        output.Write(frame[..(Amqp0Frame.HeaderBytes + ClassAndMethodBytes)]);
        output.Write(LineBytesRedactor.SecretMask);
    }

    // Writes the frame with arguments[responseStart..responseEnd] (the long string, size included) replaced by the redacted response.
    private static void WriteFrameWithResponse(
        ReadOnlySpan<byte> frame,
        ReadOnlySpan<byte> arguments,
        int responseStart,
        int responseEnd,
        byte[] redacted,
        ArrayBufferWriter<byte> output)
    {
        var originalResponseLength = responseEnd - responseStart - SizeFieldBytes;
        var payloadLength = frame.Length - Amqp0Frame.OverheadBytes - originalResponseLength + redacted.Length;
        Span<byte> number = stackalloc byte[SizeFieldBytes];

        output.Write(frame[..(Amqp0Frame.HeaderBytes - SizeFieldBytes)]);
        BinaryPrimitives.WriteUInt32BigEndian(number, (uint)payloadLength);
        output.Write(number);
        output.Write(frame[Amqp0Frame.HeaderBytes..(Amqp0Frame.HeaderBytes + ClassAndMethodBytes)]);
        output.Write(arguments[..responseStart]);
        BinaryPrimitives.WriteUInt32BigEndian(number, (uint)redacted.Length);
        output.Write(number);
        output.Write(redacted);
        output.Write(arguments[responseEnd..]);
        output.Write(frame[^1..]);
    }

    private static bool TryLocateStartOkResponse(ReadOnlySpan<byte> arguments, out string mechanism, out int responseStart, out int responseEnd)
    {
        mechanism = string.Empty;
        responseStart = 0;
        responseEnd = 0;
        if (arguments.Length < SizeFieldBytes)
        {
            return false;
        }

        var cursor = (long)SizeFieldBytes + BinaryPrimitives.ReadUInt32BigEndian(arguments);
        if (arguments.Length < cursor + 1 + SizeFieldBytes)
        {
            return false;
        }

        var mechanismLength = arguments[(int)cursor];
        var mechanismEnd = cursor + 1 + mechanismLength;
        if (arguments.Length < mechanismEnd + SizeFieldBytes)
        {
            return false;
        }

        var responseLength = BinaryPrimitives.ReadUInt32BigEndian(arguments[(int)mechanismEnd..]);
        var end = mechanismEnd + SizeFieldBytes + responseLength;
        if (arguments.Length < end)
        {
            return false;
        }

        mechanism = Encoding.UTF8.GetString(arguments.Slice((int)cursor + 1, mechanismLength));
        responseStart = (int)mechanismEnd;
        responseEnd = (int)end;
        return true;
    }

    private static byte[] RedactResponse(string mechanism, ReadOnlySpan<byte> response)
    {
        if (response.IsEmpty)
        {
            return [];
        }

        return mechanism switch
        {
            "PLAIN" => RedactPlain(response),
            "AMQPLAIN" => RedactAmqplain(response),
            _ => LineBytesRedactor.SecretMask.ToArray(),
        };
    }

    // RFC 4616: authzid NUL login NUL password; everything after the second NUL is the password.
    private static byte[] RedactPlain(ReadOnlySpan<byte> response)
    {
        var first = response.IndexOf((byte)0);
        var second = first < 0 ? -1 : response[(first + 1)..].IndexOf((byte)0);
        if (second < 0)
        {
            return LineBytesRedactor.SecretMask.ToArray();
        }

        return [.. response[..(first + 1 + second + 1)], .. LineBytesRedactor.SecretMask];
    }

    // A field table's contents: entries of short-string name, type octet and a value. Only long-string
    // values can be walked without a table reader, so any other type masks the whole response.
    private static byte[] RedactAmqplain(ReadOnlySpan<byte> response)
    {
        var output = new List<byte>();
        var cursor = 0;
        while (cursor < response.Length)
        {
            if (!TryReadLongStringEntry(response, ref cursor, out var entryStart, out var name, out var valueStart, out var valueLength))
            {
                return LineBytesRedactor.SecretMask.ToArray();
            }

            if (string.Equals(name, "PASSWORD", StringComparison.OrdinalIgnoreCase))
            {
                output.AddRange(response[entryStart..valueStart]);
                output.AddRange(new byte[] { 0, 0, 0, 4 });
                output.AddRange(LineBytesRedactor.SecretMask.ToArray());
            }
            else
            {
                output.AddRange(response[entryStart..(valueStart + SizeFieldBytes + valueLength)]);
            }
        }

        return [.. output];
    }

    private static bool TryReadLongStringEntry(
        ReadOnlySpan<byte> table,
        ref int cursor,
        out int entryStart,
        out string name,
        out int valueStart,
        out int valueLength)
    {
        entryStart = cursor;
        name = string.Empty;
        valueStart = 0;
        valueLength = 0;
        var nameLength = table[cursor];
        var typeAt = cursor + 1 + nameLength;
        if (table.Length < typeAt + 1 + SizeFieldBytes || table[typeAt] != (byte)'S')
        {
            return false;
        }

        valueStart = typeAt + 1;
        var length = BinaryPrimitives.ReadUInt32BigEndian(table[valueStart..]);
        if (table.Length - valueStart - SizeFieldBytes < length)
        {
            return false;
        }

        valueLength = (int)length;
        name = Encoding.UTF8.GetString(table.Slice(cursor + 1, nameLength));
        cursor = valueStart + SizeFieldBytes + valueLength;
        return true;
    }
}
