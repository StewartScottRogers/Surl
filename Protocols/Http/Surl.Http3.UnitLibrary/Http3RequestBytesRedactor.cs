using System.Buffers;
using System.Text;
using Surl.Protocol.Abstractions;
using Surl.Qpack;
using Surl.Quic;

namespace Surl.Http3;

/// <summary>
/// An <see cref="IReceivedBytesRedactor"/> for the plaintext notes of an HTTP/3 connection
/// (ADR-0182, decisions 1, 6 and 7, and its BL-1424 amendment). Every reported chunk is redacted by a
/// fresh instance, so each <see cref="Redact"/> call treats its bytes as a whole unit:
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>A chunk that starts with <c>:</c> is the decoded header text of a request
/// (<c>name: value</c> lines, ADR-0169 decision 1): the value of an <c>authorization</c> or
/// <c>proxy-authorization</c> line is written as <c>****</c>.</item>
/// <item>Any other chunk is read as HTTP/3 frames from its first byte: each frame header is logged as
/// received, a <c>HEADERS</c> payload is logged as received unless its QPACK field section carries
/// <c>authorization</c> or <c>proxy-authorization</c>, cannot be decoded, or runs past the chunk, in which
/// case the payload (or the rest of it) is written as <c>****</c>; every other frame's payload is logged as
/// received. A QPACK value is recoverable from the bytes whether Huffman-coded or literal, which is why
/// the whole payload goes, as the HTTP/2 redactor does with a header block.</item>
/// </list>
/// Surl advertises a QPACK dynamic table capacity of 0 (ADR-0082 decision 8), so every field section is
/// self-contained and a fresh decoder reads it. A chunk that begins in the middle of a frame (a large body
/// split across QUIC packets) is not told apart from a frame start; its bytes are logged as received unless
/// they happen to read as a <c>HEADERS</c> frame that does not decode, which is withheld, failing closed.
/// </remarks>
public sealed class Http3RequestBytesRedactor : IReceivedBytesRedactor
{
    private static readonly byte[] Withheld = "****"u8.ToArray();
    private static readonly byte[] LineEnd = "\r\n"u8.ToArray();

    /// <inheritdoc />
    public void Redact(ReadOnlySpan<byte> received, ArrayBufferWriter<byte> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (received.Length > 0 && received[0] == (byte)':')
        {
            RedactDecodedText(received, output);

            return;
        }

        RedactFrames(received, output);
    }

    /// <inheritdoc />
    public void ObserveSent(ReadOnlySpan<byte> sent)
    {
    }

    /// <inheritdoc />
    public void Flush(ArrayBufferWriter<byte> output) => ArgumentNullException.ThrowIfNull(output);

    private static void RedactFrames(ReadOnlySpan<byte> received, ArrayBufferWriter<byte> output)
    {
        var position = 0;
        while (position < received.Length)
        {
            var rest = received[position..];
            if (!QuicVariableLengthInteger.TryRead(rest, out var type, out var typeLength)
                || !QuicVariableLengthInteger.TryRead(rest[typeLength..], out var length, out var lengthLength))
            {
                output.Write(rest);

                return;
            }

            var headerLength = typeLength + lengthLength;
            output.Write(rest[..headerLength]);
            var available = (ulong)(rest.Length - headerLength);
            var payload = rest.Slice(headerLength, (int)Math.Min(length, available));
            if (type == Http3FrameType.Headers)
            {
                WriteHeadersPayload(payload, complete: length <= available, output);
            }
            else
            {
                output.Write(payload);
            }

            position += headerLength + payload.Length;
        }
    }

    private static void WriteHeadersPayload(ReadOnlySpan<byte> payload, bool complete, ArrayBufferWriter<byte> output)
    {
        if (complete && !CarriesSecret(payload))
        {
            output.Write(payload);

            return;
        }

        output.Write(Withheld);
    }

    private static bool CarriesSecret(ReadOnlySpan<byte> section)
    {
        var decoded = new QpackDecoder(0, int.MaxValue).DecodeFieldSection(0, section);

        return !decoded.IsDecoded || decoded.FieldLines.Any(line => IsSecretName(line.Name));
    }

    private static void RedactDecodedText(ReadOnlySpan<byte> text, ArrayBufferWriter<byte> output)
    {
        while (text.Length > 0)
        {
            var end = text.IndexOf(LineEnd);
            var line = end < 0 ? text : text[..end];
            WriteDecodedLine(line, output);
            if (end < 0)
            {
                return;
            }

            output.Write(LineEnd);
            text = text[(end + LineEnd.Length)..];
        }
    }

    // A pseudo-header's name starts with ':', so the name ends at the first colon after it; a line with
    // no colon is logged as is.
    private static void WriteDecodedLine(ReadOnlySpan<byte> line, ArrayBufferWriter<byte> output)
    {
        var start = line.Length > 0 && line[0] == (byte)':' ? 1 : 0;
        var colon = line[start..].IndexOf((byte)':');
        if (colon >= 0)
        {
            colon += start;
        }

        if (colon > 0 && IsSecretName(Encoding.UTF8.GetString(line[..colon])))
        {
            output.Write(line[..(colon + 1)]);
            output.Write(" "u8);
            output.Write(Withheld);

            return;
        }

        output.Write(line);
    }

    private static bool IsSecretName(string name) =>
        name.Equals("authorization", StringComparison.OrdinalIgnoreCase)
        || name.Equals("proxy-authorization", StringComparison.OrdinalIgnoreCase);
}
