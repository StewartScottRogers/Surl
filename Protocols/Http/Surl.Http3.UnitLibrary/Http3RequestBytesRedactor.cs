using System.Buffers;
using System.Text;
using Surl.Protocol.Abstractions;
using Surl.Qpack;

namespace Surl.Http3;

/// <summary>
/// An <see cref="IStreamReceivedBytesRedactor"/> for the plaintext notes of an HTTP/3 connection
/// (ADR-0182, decisions 1, 6 and 7, and amendments 4, 14 and 21).
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><see cref="RedactStream"/> takes a QUIC stream's bytes. A client's bidirectional stream is read
/// as HTTP/3 frames from the stream's first byte, however it is split into chunks: each frame header is
/// logged as received, a <c>HEADERS</c> payload is logged as received only when it is whole in the chunk that completes its frame header,
/// decodes and carries no secret field, and is otherwise written as <c>****</c> once with every further
/// byte of it left out; every other frame's payload is logged as received. A unidirectional stream carries
/// no request, so each of its chunks is judged alone, as <see cref="Redact"/> does. At most
/// <see cref="MaxStreamsPartWayThroughAFrame"/> streams are remembered part-way through a frame; past that
/// the redactor has lost a stream's place, and every later bidirectional stream chunk is written as
/// <c>****</c>.</item>
/// <item><see cref="Redact"/> treats its bytes as a whole unit. A chunk that starts with <c>:</c> is the
/// decoded header text of a request (<c>name: value</c> lines, ADR-0169 decision 1): the value of a secret
/// field's line is written as <c>****</c>. Any other chunk is read as HTTP/3 frames from its first byte,
/// by the same rules, and a <c>HEADERS</c> payload that runs past it is written as <c>****</c>.</item>
/// </list>
/// A secret field is one of <see cref="HttpWithheldHeaderNames.Credentials"/>, or one the redactor was
/// made to withhold as well, matched without regard to case. A QPACK value is recoverable from the bytes
/// whether Huffman-coded or literal, which is why the whole payload goes, as the HTTP/2 redactor does with a
/// header block. Surl advertises a QPACK dynamic table capacity of 0 (ADR-0082 decision 8), so every field
/// section is self-contained and a fresh decoder reads it.
/// </remarks>
public sealed class Http3RequestBytesRedactor : IStreamReceivedBytesRedactor
{
    /// <summary>
    /// The most streams <see cref="RedactStream"/> remembers part-way through a frame before it withholds
    /// every later bidirectional stream chunk.
    /// </summary>
    public const int MaxStreamsPartWayThroughAFrame = 4096;

    // The second bit of a QUIC stream ID marks a unidirectional stream (RFC 9000 section 2.1).
    private const long UnidirectionalStreamBit = 0x2;

    private static readonly byte[] Withheld = "****"u8.ToArray();
    private static readonly byte[] LineEnd = "\r\n"u8.ToArray();

    private readonly HashSet<string> secretFieldNames =
        new(HttpWithheldHeaderNames.Credentials, StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<long, Http3StreamFrameRedaction> streamsPartWayThroughAFrame = [];
    private bool hasLostAStreamsPlace;

    /// <summary>
    /// Creates a redactor that withholds the values of
    /// <see cref="HttpWithheldHeaderNames.Credentials"/>.
    /// </summary>
    public Http3RequestBytesRedactor()
        : this([])
    {
    }

    /// <summary>
    /// Creates a redactor that withholds the values of
    /// <see cref="HttpWithheldHeaderNames.Credentials"/> and of any further field.
    /// </summary>
    /// <param name="additionalSecretFieldNames">
    /// Further fields whose value is a secret, such as <see cref="HttpWithheldHeaderNames.ApiKeysAndSessionTokens"/>,
    /// matched without regard to case.
    /// </param>
    public Http3RequestBytesRedactor(IEnumerable<string> additionalSecretFieldNames)
    {
        ArgumentNullException.ThrowIfNull(additionalSecretFieldNames);
        secretFieldNames.UnionWith(additionalSecretFieldNames);
    }

    /// <inheritdoc />
    public void Redact(ReadOnlySpan<byte> received, ArrayBufferWriter<byte> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (received.Length > 0 && received[0] == (byte)':')
        {
            RedactDecodedText(received, output);

            return;
        }

        NewFrameRedaction().Redact(received, output);
    }

    /// <inheritdoc />
    public void RedactStream(long streamId, ReadOnlySpan<byte> received, ArrayBufferWriter<byte> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        if ((streamId & UnidirectionalStreamBit) != 0)
        {
            Redact(received, output);
        }
        else if (hasLostAStreamsPlace)
        {
            output.Write(Withheld);
        }
        else
        {
            FollowStream(streamId, received, output);
        }
    }

    /// <inheritdoc />
    public void ObserveSent(ReadOnlySpan<byte> sent)
    {
    }

    /// <inheritdoc />
    public void Flush(ArrayBufferWriter<byte> output) => ArgumentNullException.ThrowIfNull(output);

    // A stream at a frame boundary needs no memory: a new frame redaction reads its next byte the same way.
    private void FollowStream(long streamId, ReadOnlySpan<byte> received, ArrayBufferWriter<byte> output)
    {
        if (!streamsPartWayThroughAFrame.Remove(streamId, out var frames))
        {
            frames = NewFrameRedaction();
        }

        frames.Redact(received, output);
        if (frames.IsAtFrameBoundary)
        {
            return;
        }

        if (streamsPartWayThroughAFrame.Count < MaxStreamsPartWayThroughAFrame)
        {
            streamsPartWayThroughAFrame.Add(streamId, frames);
        }
        else
        {
            hasLostAStreamsPlace = true;
            streamsPartWayThroughAFrame.Clear();
        }
    }

    private Http3StreamFrameRedaction NewFrameRedaction() => new(CarriesSecret);

    private bool CarriesSecret(ReadOnlySpan<byte> section)
    {
        var decoded = new QpackDecoder(0, int.MaxValue).DecodeFieldSection(0, section);

        return !decoded.IsDecoded || decoded.FieldLines.Any(line => secretFieldNames.Contains(line.Name));
    }

    private void RedactDecodedText(ReadOnlySpan<byte> text, ArrayBufferWriter<byte> output)
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
    private void WriteDecodedLine(ReadOnlySpan<byte> line, ArrayBufferWriter<byte> output)
    {
        var start = line.Length > 0 && line[0] == (byte)':' ? 1 : 0;
        var colon = line[start..].IndexOf((byte)':');
        if (colon >= 0)
        {
            colon += start;
        }

        if (colon > 0 && secretFieldNames.Contains(Encoding.UTF8.GetString(line[..colon])))
        {
            output.Write(line[..(colon + 1)]);
            output.Write(" "u8);
            output.Write(Withheld);

            return;
        }

        output.Write(line);
    }
}
