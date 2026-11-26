using System.Buffers;
using System.Text;
using Surl.Protocol.Abstractions;
using Surl.Qpack;

namespace Surl.Http3;

/// <summary>
/// An <see cref="IStreamReceivedBytesRedactor"/> for the plaintext notes of an HTTP/3 connection
/// (ADR-0182, decisions 1, 6 and 7, and amendments 4, 14, 21, 22 and 35).
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><see cref="RedactStream"/> takes a QUIC stream's bytes. A client's bidirectional stream, unless it
/// is a session stream (below), is read as HTTP/3 frames from the stream's first byte, however it is split
/// into chunks: each frame header is logged as received, a <c>HEADERS</c> payload is logged as received only
/// when it is whole in the chunk that completes its frame header, decodes and carries no secret field, and is
/// otherwise written as <c>****</c> once with every further byte of it left out; every other frame's payload
/// is logged as received. A unidirectional stream carries no request, so each of its chunks is judged alone,
/// as <see cref="Redact"/> does, unless it is a session stream (below).</item>
/// <item>A redactor made with a session marker for a direction logs a session stream of that direction, one
/// whose first variable-length integer is the marker, as received, however the integer's bytes arrive: from
/// the chunk that completes the integer, every chunk is written as received while the stream is remembered
/// (below). Only the stream's first integer counts; the same value later in a stream, or the other
/// direction's marker, changes nothing. Without a marker no stream is a session stream (amendment 35).</item>
/// <item>For each direction at most <see cref="MaxRememberedStreamsPerDirection"/> streams are remembered:
/// a bidirectional stream while it is part-way through a frame, or always when the redactor has a
/// bidirectional marker; a unidirectional stream always when the redactor has a unidirectional marker, and
/// never otherwise. One more forgets the lowest stream ID of that direction. A stream that is not remembered
/// and whose ID is at or below the highest one forgotten may have been forgotten: each chunk of a
/// bidirectional one is written as <c>****</c>, and each chunk of a unidirectional one is judged alone, even
/// one that starts with the marker. Higher stream IDs are followed as before (amendment 35).</item>
/// <item><see cref="Redact"/> treats its bytes as a whole unit, read as HTTP/3 frames from its first byte by
/// the same rules, whatever the session markers; a <c>HEADERS</c> payload that runs past it is written as
/// <c>****</c>.</item>
/// <item><see cref="RedactDecodedText"/> takes the decoded header text of a request (<c>name: value</c> lines,
/// ADR-0169 decision 1), whatever its first field: the value of a secret field's line is written as
/// <c>****</c>. A line ends only where the text's renderer ended a header, because it writes a CR or LF
/// inside a name or value as <c>\r</c> or <c>\n</c>.</item>
/// </list>
/// A secret field is one of <see cref="HttpWithheldHeaderNames.Credentials"/>, or one the redactor was
/// made to withhold as well, matched without regard to case. A QPACK value is recoverable from the bytes
/// whether Huffman-coded or literal, which is why the whole payload goes, as the HTTP/2 redactor does with a
/// header block. Surl advertises a QPACK dynamic table capacity of 0 (ADR-0082 decision 8), so every field
/// section is self-contained and a fresh decoder reads it.
/// </remarks>
public sealed class Http3RequestBytesRedactor : IStreamReceivedBytesRedactor, IDecodedTextReceivedBytesRedactor
{
    /// <summary>
    /// The most streams of one direction <see cref="RedactStream"/> remembers before it forgets the lowest
    /// stream ID of that direction.
    /// </summary>
    public const int MaxRememberedStreamsPerDirection = 4096;

    // The second bit of a QUIC stream ID marks a unidirectional stream (RFC 9000 section 2.1).
    private const long UnidirectionalStreamBit = 0x2;

    private static readonly byte[] Withheld = "****"u8.ToArray();
    private static readonly byte[] LineEnd = "\r\n"u8.ToArray();

    private readonly HashSet<string> secretFieldNames =
        new(HttpWithheldHeaderNames.Credentials, StringComparer.OrdinalIgnoreCase);

    private readonly Http3RememberedStreams<Http3StreamFrameRedaction> bidirectionalStreams =
        new(MaxRememberedStreamsPerDirection);

    private readonly Http3RememberedStreams<Http3StreamOpening> unidirectionalStreams =
        new(MaxRememberedStreamsPerDirection);

    private readonly ulong? bidirectionalStreamSignal;
    private readonly ulong? unidirectionalStreamType;

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
    /// <see cref="HttpWithheldHeaderNames.Credentials"/> and of any further field, and reads no stream as a
    /// session stream.
    /// </summary>
    /// <param name="additionalSecretFieldNames">
    /// Further fields whose value is a secret, such as <see cref="HttpWithheldHeaderNames.ApiKeysAndSessionTokens"/>,
    /// matched without regard to case.
    /// </param>
    public Http3RequestBytesRedactor(IEnumerable<string> additionalSecretFieldNames)
        : this(additionalSecretFieldNames, null, null)
    {
    }

    /// <summary>
    /// Creates a redactor that withholds the values of
    /// <see cref="HttpWithheldHeaderNames.Credentials"/> and of any further field, and logs session streams,
    /// such as WebTransport's, as received.
    /// </summary>
    /// <param name="additionalSecretFieldNames">
    /// Further fields whose value is a secret, such as <see cref="HttpWithheldHeaderNames.ApiKeysAndSessionTokens"/>,
    /// matched without regard to case.
    /// </param>
    /// <param name="bidirectionalStreamSignal">
    /// The first variable-length integer of a client's bidirectional session stream, WebTransport's signal
    /// <c>0x41</c>; <see langword="null"/> when every such stream is read as HTTP/3 frames.
    /// </param>
    /// <param name="unidirectionalStreamType">
    /// The stream type of a unidirectional session stream, WebTransport's <c>0x54</c>;
    /// <see langword="null"/> when every unidirectional stream chunk is judged alone.
    /// </param>
    public Http3RequestBytesRedactor(
        IEnumerable<string> additionalSecretFieldNames, ulong? bidirectionalStreamSignal, ulong? unidirectionalStreamType)
    {
        ArgumentNullException.ThrowIfNull(additionalSecretFieldNames);
        secretFieldNames.UnionWith(additionalSecretFieldNames);
        this.bidirectionalStreamSignal = bidirectionalStreamSignal;
        this.unidirectionalStreamType = unidirectionalStreamType;
    }

    /// <inheritdoc />
    public void Redact(ReadOnlySpan<byte> received, ArrayBufferWriter<byte> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        NewFrameRedaction().Redact(received, output);
    }

    /// <inheritdoc />
    public void RedactDecodedText(ReadOnlySpan<byte> decodedText, ArrayBufferWriter<byte> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        while (decodedText.Length > 0)
        {
            var end = decodedText.IndexOf(LineEnd);
            var line = end < 0 ? decodedText : decodedText[..end];
            WriteDecodedLine(line, output);
            if (end < 0)
            {
                return;
            }

            output.Write(LineEnd);
            decodedText = decodedText[(end + LineEnd.Length)..];
        }
    }

    /// <inheritdoc />
    public void RedactStream(long streamId, ReadOnlySpan<byte> received, ArrayBufferWriter<byte> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        if ((streamId & UnidirectionalStreamBit) != 0)
        {
            RedactUnidirectionalStream(streamId, received, output);
        }
        else
        {
            RedactBidirectionalStream(streamId, received, output);
        }
    }

    /// <inheritdoc />
    public void ObserveSent(ReadOnlySpan<byte> sent)
    {
    }

    /// <inheritdoc />
    public void Flush(ArrayBufferWriter<byte> output) => ArgumentNullException.ThrowIfNull(output);

    // Without a session marker, a stream at a frame boundary needs no memory: a new frame redaction reads its
    // next byte the same way. With one, only a remembered stream's later chunk is known not to be its opening.
    private void RedactBidirectionalStream(long streamId, ReadOnlySpan<byte> received, ArrayBufferWriter<byte> output)
    {
        if (!bidirectionalStreams.TryTake(streamId, out var frames))
        {
            if (bidirectionalStreams.MayHaveForgotten(streamId))
            {
                output.Write(Withheld);

                return;
            }

            frames = NewFrameRedaction(NewOpening(bidirectionalStreamSignal));
        }

        frames.Redact(received, output);
        if (bidirectionalStreamSignal is not null || !frames.IsAtFrameBoundary)
        {
            bidirectionalStreams.Remember(streamId, frames);
        }
    }

    private void RedactUnidirectionalStream(long streamId, ReadOnlySpan<byte> received, ArrayBufferWriter<byte> output)
    {
        var opening = TakeUnidirectionalOpening(streamId);
        if (opening is not null)
        {
            unidirectionalStreams.Remember(streamId, opening);
        }

        if (opening?.TakeBytesAndCheckForSessionStream(received) == true)
        {
            output.Write(received);
        }
        else
        {
            Redact(received, output);
        }
    }

    // A stream that may have been forgotten gets no opening: its next chunk is not known to be its first.
    private Http3StreamOpening? TakeUnidirectionalOpening(long streamId)
    {
        if (unidirectionalStreams.TryTake(streamId, out var opening))
        {
            return opening;
        }

        return unidirectionalStreams.MayHaveForgotten(streamId) ? null : NewOpening(unidirectionalStreamType);
    }

    private static Http3StreamOpening? NewOpening(ulong? sessionMarker) =>
        sessionMarker is { } marker ? new Http3StreamOpening(marker) : null;

    private Http3StreamFrameRedaction NewFrameRedaction(Http3StreamOpening? opening = null) => new(CarriesSecret, opening);

    private bool CarriesSecret(ReadOnlySpan<byte> section)
    {
        var decoded = new QpackDecoder(0, int.MaxValue).DecodeFieldSection(0, section);

        return !decoded.IsDecoded || decoded.FieldLines.Any(line => secretFieldNames.Contains(line.Name));
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
