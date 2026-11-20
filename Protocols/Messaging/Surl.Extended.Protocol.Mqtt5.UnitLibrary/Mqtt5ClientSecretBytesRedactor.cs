using System.Buffers;
using System.Runtime.InteropServices;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Mqtt5;

/// <summary>
/// The <see cref="IReceivedBytesRedactor"/> of an MQTT 5 connection (ADR-0182, decision 6): it holds
/// each <c>CONNECT</c> and <c>AUTH</c> packet until it is whole and writes it with the Password and
/// every Authentication Data property value written as <see cref="LineBytesRedactor.SecretMask"/>.
/// The user name, client identifier, Will and every other property stay as sent; the Remaining
/// Length, Property Length and the secret's own length are rewritten to match. Every other packet
/// is logged as it arrived, never held, so a <c>PUBLISH</c> payload that holds the password's bytes
/// is not touched.
/// </summary>
/// <remarks>
/// A <c>CONNECT</c> or <c>AUTH</c> body that does not decode is written as its first byte and a
/// four-byte Remaining Length naming <c>****</c>. One over <see cref="MaxHeldBytes"/> is written
/// the same way and the rest of it dropped. A fixed header whose Remaining Length is over four
/// bytes is not MQTT, so the rest of the connection is logged as received.
/// </remarks>
internal sealed class Mqtt5ClientSecretBytesRedactor : IReceivedBytesRedactor
{
    /// <summary>The most bytes held while a packet is cut: 64 KiB (ADR-0182, decision 6).</summary>
    public const int MaxHeldBytes = LineBytesRedactor.DefaultMaxHeldBytes;

    private const int MaxFixedHeaderBytes = 1 + Mqtt5BodyReader.MaxVariableByteIntegerBytes;
    private const int ContinuationBit = 0x80;
    private const int ValueBits = 0x7F;
    private const int PacketTypeShift = 4;

    private readonly List<byte> held = [];
    private int headerLength;
    private int bodyLength = -1;
    private int bytesToPass;
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

            if (bytesToPass > 0)
            {
                var passed = Math.Min(bytesToPass, received.Length - index);
                output.Write(received.Slice(index, passed));
                index += passed;
                bytesToPass -= passed;
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
        if (bodyLength >= 0)
        {
            // Cut short, so no field can be told apart from its secret: keep only the first byte.
            WriteMasked(partial[0], output);
        }
        else
        {
            output.Write(partial);
        }

        held.Clear();
        isPassingThrough = true;
    }

    private static bool CarriesSecrets(byte firstByte) =>
        (firstByte >> PacketTypeShift) is (int)Mqtt5PacketType.Connect or (int)Mqtt5PacketType.Authentication;

    private static void WriteMasked(byte firstByte, ArrayBufferWriter<byte> output)
    {
        output.Write([firstByte, (byte)LineBytesRedactor.SecretMask.Length]);
        output.Write(LineBytesRedactor.SecretMask);
    }

    private static int ReadRemainingLength(ReadOnlySpan<byte> lengthBytes)
    {
        var value = 0;
        for (var index = lengthBytes.Length - 1; index >= 0; index--)
        {
            value = (value << 7) | (lengthBytes[index] & ValueBits);
        }

        return value;
    }

    private static byte[]? RedactBody(Mqtt5PacketType type, ReadOnlyMemory<byte> body)
    {
        try
        {
            return type == Mqtt5PacketType.Connect ? RedactConnect(body) : RedactAuthentication(body);
        }
        catch (Mqtt5PacketFaultException)
        {
            return null;
        }
    }

    private static byte[] RedactConnect(ReadOnlyMemory<byte> body)
    {
        var reader = new Mqtt5BodyReader(body);
        var writer = new Mqtt5BodyWriter();
        reader.ReadBinaryData();
        reader.ReadByte();
        var flags = reader.ReadByte();
        reader.ReadTwoByteInteger();
        writer.WriteBytes(body.Span[..PositionOf(body, reader)]);
        WriteProperties(reader, writer);

        var tailStart = PositionOf(body, reader);
        reader.ReadBinaryData();
        if ((flags & ConnectFlag.Will) != 0)
        {
            reader.ReadBytes(reader.ReadVariableByteInteger());
            reader.ReadBinaryData();
            reader.ReadBinaryData();
        }

        if ((flags & ConnectFlag.UserName) != 0)
        {
            reader.ReadBinaryData();
        }

        writer.WriteBytes(body.Span[tailStart..PositionOf(body, reader)]);
        if ((flags & ConnectFlag.Password) != 0)
        {
            reader.ReadBinaryData();
            writer.WriteBinaryData(LineBytesRedactor.SecretMask);
        }

        reader.RequireEnd("[MQTT-3.1.3-1]");
        return writer.WrittenSpan.ToArray();
    }

    private static byte[] RedactAuthentication(ReadOnlyMemory<byte> body)
    {
        var reader = new Mqtt5BodyReader(body);
        var writer = new Mqtt5BodyWriter();
        writer.WriteByte(reader.ReadByte());
        if (reader.Remaining > 0)
        {
            WriteProperties(reader, writer);
        }

        reader.RequireEnd("3.15.2");
        return writer.WrittenSpan.ToArray();
    }

    // Reads a Property Length and its list, and writes them with each Authentication Data value masked.
    private static void WriteProperties(Mqtt5BodyReader reader, Mqtt5BodyWriter writer)
    {
        var list = reader.ReadBytes(reader.ReadVariableByteInteger());
        var properties = new Mqtt5BodyReader(list);
        var rewritten = new Mqtt5BodyWriter();
        while (properties.Remaining > 0)
        {
            var start = PositionOf(list, properties);
            var identifier = properties.ReadVariableByteInteger();
            if (!Mqtt5PropertyDefinition.TryFind(identifier, out var definition))
            {
                throw Mqtt5BodyReader.Fault(Mqtt5PacketFault.Malformed("2.2.2.2"));
            }

            properties.ReadPropertyValue(definition);
            if (identifier == (int)Mqtt5PropertyIdentifier.AuthenticationData)
            {
                rewritten.WriteVariableByteInteger(identifier);
                rewritten.WriteBinaryData(LineBytesRedactor.SecretMask);
            }
            else
            {
                rewritten.WriteBytes(list.Span[start..PositionOf(list, properties)]);
            }
        }

        writer.WriteVariableByteInteger(rewritten.Length);
        writer.WriteBytes(rewritten.WrittenSpan);
    }

    private static int PositionOf(ReadOnlyMemory<byte> bytes, Mqtt5BodyReader reader) => bytes.Length - reader.Remaining;

    private void HoldNext(byte next, ArrayBufferWriter<byte> output)
    {
        held.Add(next);
        if (bodyLength >= 0)
        {
            if (held.Count == headerLength + bodyLength)
            {
                WriteWholePacket(output);
            }
        }
        else if (held.Count > 1 && (next & ContinuationBit) == 0)
        {
            BeginPacket(output);
        }
        else if (held.Count == MaxFixedHeaderBytes)
        {
            output.Write(CollectionsMarshal.AsSpan(held));
            held.Clear();
            isPassingThrough = true;
        }
    }

    private void BeginPacket(ArrayBufferWriter<byte> output)
    {
        var firstByte = held[0];
        var remainingLength = ReadRemainingLength(CollectionsMarshal.AsSpan(held)[1..]);
        headerLength = held.Count;
        if (!CarriesSecrets(firstByte))
        {
            output.Write(CollectionsMarshal.AsSpan(held));
            held.Clear();
            bytesToPass = remainingLength;
        }
        else if (remainingLength > MaxHeldBytes)
        {
            WriteMasked(firstByte, output);
            held.Clear();
            bytesToDrop = remainingLength;
        }
        else if (remainingLength == 0)
        {
            output.Write(CollectionsMarshal.AsSpan(held));
            held.Clear();
        }
        else
        {
            bodyLength = remainingLength;
        }
    }

    private void WriteWholePacket(ArrayBufferWriter<byte> output)
    {
        var firstByte = held[0];
        var body = held.GetRange(headerLength, bodyLength).ToArray();
        var redacted = RedactBody((Mqtt5PacketType)(firstByte >> PacketTypeShift), body);
        if (redacted is null)
        {
            WriteMasked(firstByte, output);
        }
        else
        {
            var header = new Mqtt5BodyWriter();
            header.WriteByte(firstByte);
            header.WriteVariableByteInteger(redacted.Length);
            output.Write(header.WrittenSpan);
            output.Write(redacted);
        }

        held.Clear();
        bodyLength = -1;
    }

    private static class ConnectFlag
    {
        public const int Will = 0x04;
        public const int Password = 0x40;
        public const int UserName = 0x80;
    }
}
