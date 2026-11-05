using System.Buffers.Binary;

namespace Surl.Extended.Protocol.Coap;

/// <summary>
/// Reads one datagram as a CoAP message (RFC 7252 section 3): the 4-byte header, the token, the
/// options with their delta and length nibbles and the 13 and 14 extensions, and the payload after
/// the 0xFF marker.
/// </summary>
public static class CoapMessageReader
{
    private const int HeaderLength = 4;
    private const byte PayloadMarker = 0xFF;

    /// <summary>
    /// Reads <paramref name="datagram"/>. The token, option values and payload are copied, so the
    /// datagram's buffer may be reused afterwards.
    /// </summary>
    /// <param name="datagram">One whole datagram.</param>
    /// <returns>
    /// The message; or <see cref="CoapReadOutcomeKind.NotCoap"/> for a datagram shorter than the
    /// header or of a version other than 1; or <see cref="CoapReadOutcomeKind.FormatError"/>,
    /// with the header's type and Message ID, for a token length of 9 to 15, an option delta or
    /// length nibble of 15, a payload marker with no payload, a token or option running past the
    /// datagram, an option number past 65535, or an Empty message with a token or any byte after
    /// its Message ID.
    /// </returns>
    public static CoapReadOutcome Read(ReadOnlySpan<byte> datagram)
    {
        if (datagram.Length < HeaderLength)
        {
            return CoapReadOutcome.NotCoap("A datagram shorter than the 4-byte CoAP header.");
        }

        var version = datagram[0] >> 6;

        if (version != 1)
        {
            return CoapReadOutcome.NotCoap($"A datagram of CoAP version {version}, not 1.");
        }

        var type = (CoapMessageType)((datagram[0] >> 4) & 0x03);
        var messageId = BinaryPrimitives.ReadUInt16BigEndian(datagram[2..]);

        try
        {
            return CoapReadOutcome.Read(ReadMessage(datagram, type, messageId));
        }
        catch (CoapFormatException fault)
        {
            return CoapReadOutcome.FormatError(type, messageId, fault.Message);
        }
    }

    private static CoapMessage ReadMessage(ReadOnlySpan<byte> datagram, CoapMessageType type, ushort messageId)
    {
        var tokenLength = datagram[0] & 0x0F;
        var code = new CoapCode(datagram[1]);

        if (code.IsEmpty && (tokenLength != 0 || datagram.Length != HeaderLength))
        {
            throw new CoapFormatException("An Empty message with a token or bytes after its Message ID.");
        }

        if (tokenLength > CoapMessage.MaxTokenLength)
        {
            throw new CoapFormatException($"Token length {tokenLength} is reserved.");
        }

        var offset = HeaderLength;
        var token = Take(datagram, ref offset, tokenLength, "The token runs past the end of the datagram.");
        var (options, payload) = ReadOptionsAndPayload(datagram, offset);

        return new CoapMessage(type, code, messageId, token, options, payload);
    }

    private static (List<CoapOption> Options, byte[] Payload) ReadOptionsAndPayload(ReadOnlySpan<byte> datagram, int offset)
    {
        var options = new List<CoapOption>();
        var number = 0;

        while (offset < datagram.Length)
        {
            if (datagram[offset] == PayloadMarker)
            {
                return (options, ReadPayload(datagram, offset + 1));
            }

            var option = ReadOption(datagram, ref offset, number);
            number = option.Number;
            options.Add(option);
        }

        return (options, []);
    }

    private static byte[] ReadPayload(ReadOnlySpan<byte> datagram, int start)
    {
        if (start == datagram.Length)
        {
            throw new CoapFormatException("A payload marker with no payload.");
        }

        return datagram[start..].ToArray();
    }

    private static CoapOption ReadOption(ReadOnlySpan<byte> datagram, ref int offset, int previousNumber)
    {
        var first = datagram[offset++];
        var delta = ReadNibbleValue(first >> 4, datagram, ref offset, "delta");
        var length = ReadNibbleValue(first & 0x0F, datagram, ref offset, "length");
        var number = previousNumber + delta;

        if (number > CoapOption.MaxNumber)
        {
            throw new CoapFormatException($"Option number {number} is past 65535.");
        }

        var value = Take(datagram, ref offset, length, $"Option {number} runs past the end of the datagram.");

        return new CoapOption(number, value);
    }

    private static int ReadNibbleValue(int nibble, ReadOnlySpan<byte> datagram, ref int offset, string field) => nibble switch
    {
        < 13 => nibble,
        13 => 13 + Take(datagram, ref offset, 1, $"An option {field} extension runs past the end of the datagram.")[0],
        14 => 269 + BinaryPrimitives.ReadUInt16BigEndian(Take(datagram, ref offset, 2, $"An option {field} extension runs past the end of the datagram.")),
        _ => throw new CoapFormatException($"Option {field} 15 is reserved outside the payload marker."),
    };

    private static byte[] Take(ReadOnlySpan<byte> datagram, ref int offset, int count, string fault)
    {
        if (datagram.Length - offset < count)
        {
            throw new CoapFormatException(fault);
        }

        var taken = datagram.Slice(offset, count).ToArray();
        offset += count;

        return taken;
    }

    private sealed class CoapFormatException(string message) : Exception(message);
}
