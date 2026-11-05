using System.Buffers;
using System.Buffers.Binary;

namespace Surl.Extended.Protocol.Coap;

/// <summary>
/// Writes a CoAP message as one datagram (RFC 7252 section 3): version 1, the options
/// delta-encoded in order of their numbers with the 13 and 14 extensions, and the 0xFF payload
/// marker only when a payload follows.
/// </summary>
public static class CoapMessageWriter
{
    private const byte PayloadMarker = 0xFF;

    /// <summary>
    /// Writes <paramref name="message"/>.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <returns>The datagram's bytes.</returns>
    public static byte[] Write(CoapMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        var buffer = new ArrayBufferWriter<byte>();
        var header = buffer.GetSpan(4);
        header[0] = (byte)(0x40 | ((int)message.Type << 4) | message.Token.Length);
        header[1] = message.Code.Value;
        BinaryPrimitives.WriteUInt16BigEndian(header[2..], message.MessageId);
        buffer.Advance(4);
        buffer.Write(message.Token.Span);

        var previousNumber = 0;

        foreach (var option in message.Options)
        {
            WriteOption(buffer, option.Number - previousNumber, option.Value.Span);
            previousNumber = option.Number;
        }

        if (!message.Payload.IsEmpty)
        {
            buffer.Write([PayloadMarker]);
            buffer.Write(message.Payload.Span);
        }

        return buffer.WrittenSpan.ToArray();
    }

    private static void WriteOption(ArrayBufferWriter<byte> buffer, int delta, ReadOnlySpan<byte> value)
    {
        var (deltaNibble, deltaExtension) = Encode(delta);
        var (lengthNibble, lengthExtension) = Encode(value.Length);

        buffer.Write([(byte)((deltaNibble << 4) | lengthNibble)]);
        buffer.Write(deltaExtension);
        buffer.Write(lengthExtension);
        buffer.Write(value);
    }

    private static (int Nibble, byte[] Extension) Encode(int value) => value switch
    {
        < 13 => (value, []),
        < 269 => (13, [(byte)(value - 13)]),
        _ => (14, [(byte)((value - 269) >> 8), (byte)(value - 269)]),
    };
}
