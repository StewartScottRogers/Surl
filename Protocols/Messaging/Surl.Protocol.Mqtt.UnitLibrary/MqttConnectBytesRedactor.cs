using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Mqtt;

/// <summary>
/// The <see cref="IReceivedBytesRedactor"/> of an MQTT connection: it holds the first packet, the
/// <c>CONNECT</c>, until it is whole, and writes it with the password field (its two length bytes
/// and its bytes, MQTT 3.1.1 section 3.1.3.5) replaced by a length of 4 and <c>****</c>. Every
/// byte of the fixed header and of every other field, and every later packet, is logged as it
/// arrived, so a <c>PUBLISH</c> whose payload holds the password's bytes is not touched (ADR-0182,
/// decision 6). A <c>CONNECT</c> longer than <see cref="MaxHeldBytes"/> is written as
/// <c>****</c> and the rest of that packet is dropped.
/// </summary>
internal sealed class MqttConnectBytesRedactor : IReceivedBytesRedactor
{
    /// <summary>The most bytes held while the <c>CONNECT</c> is cut: 64 KiB (ADR-0182, decision 6).</summary>
    public const int MaxHeldBytes = LineBytesRedactor.DefaultMaxHeldBytes;

    private const byte ConnectFirstByte = 0x10;
    private const byte UserNameFlag = 0x80;
    private const byte PasswordFlag = 0x40;
    private const byte WillFlag = 0x04;
    private const int MaxRemainingLengthBytes = 4;
    private const int FirstPropertiesProtocolLevel = 5;

    private readonly List<byte> held = [];
    private int packetLength = -1;
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

            if (bytesToDrop > 0)
            {
                var dropped = Math.Min(bytesToDrop, received.Length - index);
                index += dropped;
                bytesToDrop -= dropped;
                isPassingThrough = bytesToDrop == 0;
                continue;
            }

            HoldNext(received[index++], output);
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
        // Until the remaining length is whole no field has begun, so nothing held is a password.
        if (packetLength >= 0 && held.Count > 0)
        {
            WriteConnect(CollectionsMarshal.AsSpan(held), output);
        }
        else
        {
            output.Write(CollectionsMarshal.AsSpan(held));
        }

        held.Clear();
        isPassingThrough = true;
    }

    private void HoldNext(byte next, ArrayBufferWriter<byte> output)
    {
        held.Add(next);
        if (held.Count == 1 && next != ConnectFirstByte)
        {
            PassHeldThrough(output);
            return;
        }

        if (packetLength < 0 && !TryMeasurePacket(out var malformed))
        {
            if (malformed)
            {
                PassHeldThrough(output);
            }

            return;
        }

        if (held.Count == packetLength)
        {
            WriteConnect(CollectionsMarshal.AsSpan(held), output);
            held.Clear();
            isPassingThrough = true;
        }
        else if (held.Count > MaxHeldBytes)
        {
            output.Write(LineBytesRedactor.SecretMask);
            bytesToDrop = packetLength - held.Count;
            held.Clear();
        }
    }

    private void PassHeldThrough(ArrayBufferWriter<byte> output)
    {
        output.Write(CollectionsMarshal.AsSpan(held));
        held.Clear();
        isPassingThrough = true;
    }

    // Sets packetLength once the remaining length is whole; a fourth byte with its continuation
    // bit set can never be measured, so the direction is passed through (section 2.2.3).
    private bool TryMeasurePacket(out bool malformed)
    {
        malformed = false;
        var remaining = 0;
        for (var index = 1; index < held.Count; index++)
        {
            remaining |= (held[index] & 0x7F) << (7 * (index - 1));
            if ((held[index] & 0x80) == 0)
            {
                packetLength = index + 1 + remaining;
                return true;
            }

            malformed = index == MaxRemainingLengthBytes;
        }

        return false;
    }

    private static void WriteConnect(ReadOnlySpan<byte> packet, ArrayBufferWriter<byte> output)
    {
        var passwordStart = FindPasswordStart(packet);
        if (passwordStart < 0)
        {
            output.Write(packet);
            return;
        }

        output.Write(packet[..passwordStart]);
        output.Write("\0\u0004"u8);
        output.Write(LineBytesRedactor.SecretMask);
        var rest = packet[passwordStart..];
        var passwordEnd = sizeof(ushort) + BinaryPrimitives.ReadUInt16BigEndian(rest);
        if (rest.Length > passwordEnd)
        {
            output.Write(rest[passwordEnd..]);
        }
    }

    // Walks the CONNECT's fields to where the password's length bytes start; -1 when the packet
    // has no password or is cut short before reaching it (so no byte held is one).
    private static int FindPasswordStart(ReadOnlySpan<byte> packet)
    {
        var position = 1;
        while ((packet[position++] & 0x80) != 0)
        {
        }

        var body = packet[position..];
        var cursor = 0;
        if (!TrySkipField(body, ref cursor) || body.Length < cursor + 4)
        {
            return -1;
        }

        var level = body[cursor];
        var flags = body[cursor + 1];
        cursor += 4;
        if ((flags & PasswordFlag) == 0 || !TrySkipFieldsBeforePassword(body, ref cursor, level, flags))
        {
            return -1;
        }

        return position + cursor;
    }

    private static bool TrySkipFieldsBeforePassword(ReadOnlySpan<byte> body, ref int cursor, byte level, byte flags)
    {
        var hasProperties = level >= FirstPropertiesProtocolLevel;
        var hasWill = (flags & WillFlag) != 0;
        return (!hasProperties || TrySkipProperties(body, ref cursor))
            && TrySkipField(body, ref cursor)
            && (!hasWill || TrySkipWill(body, ref cursor, hasProperties))
            && ((flags & UserNameFlag) == 0 || TrySkipField(body, ref cursor))
            && body.Length >= cursor + sizeof(ushort);
    }

    private static bool TrySkipWill(ReadOnlySpan<byte> body, ref int cursor, bool hasProperties) =>
        (!hasProperties || TrySkipProperties(body, ref cursor))
        && TrySkipField(body, ref cursor)
        && TrySkipField(body, ref cursor);

    private static bool TrySkipField(ReadOnlySpan<byte> body, ref int cursor)
    {
        if (body.Length < cursor + sizeof(ushort))
        {
            return false;
        }

        var end = cursor + sizeof(ushort) + BinaryPrimitives.ReadUInt16BigEndian(body[cursor..]);
        if (body.Length < end)
        {
            return false;
        }

        cursor = end;
        return true;
    }

    private static bool TrySkipProperties(ReadOnlySpan<byte> body, ref int cursor)
    {
        var length = 0;
        for (var shift = 0; shift < 7 * MaxRemainingLengthBytes; shift += 7)
        {
            if (cursor >= body.Length)
            {
                return false;
            }

            var next = body[cursor++];
            length |= (next & 0x7F) << shift;
            if ((next & 0x80) == 0)
            {
                cursor += length;
                return body.Length >= cursor;
            }
        }

        return false;
    }
}
