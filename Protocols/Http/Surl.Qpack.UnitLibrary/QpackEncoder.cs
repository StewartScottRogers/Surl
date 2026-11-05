using Surl.Hpack;

namespace Surl.Qpack;

/// <summary>
/// The QPACK encoder of one HTTP/3 connection, as ADR-0082 decision 8 has Surl's server use it: the
/// static table and literals only, never the dynamic table, so every field section has a Required
/// Insert Count of 0, no stream ever waits on another, and the encoder stream carries nothing. It
/// also reads the peer's decoder stream, which can then carry only Stream Cancellations.
/// </summary>
public sealed class QpackEncoder
{
    private readonly HpackStringCoding stringCoding;

    private readonly QpackInstructionStream decoderStream;

    /// <summary>Initializes a new instance of the <see cref="QpackEncoder" /> class.</summary>
    /// <param name="stringCoding">
    /// When to Huffman-code a string literal: <see cref="HpackStringCoding.WhenShorter" /> for the
    /// server (ADR-0082 decision 8), <see cref="HpackStringCoding.Raw" /> to reproduce RFC 9204
    /// Appendix B.1.
    /// </param>
    public QpackEncoder(HpackStringCoding stringCoding)
    {
        this.stringCoding = stringCoding;
        decoderStream = new QpackInstructionStream(QpackErrorCode.DecoderStreamError, ReadDecoderInstruction);
    }

    /// <summary>
    /// Encodes a field section (RFC 9204 section 4.5): a prefix of Required Insert Count 0 and Base 0,
    /// then for each line an Indexed Field Line when the static table has the whole line, a Literal
    /// Field Line with Name Reference when it has the name, and a Literal Field Line with Literal Name
    /// otherwise. A never-indexed line is never sent indexed and keeps its N bit.
    /// </summary>
    /// <param name="fieldLines">The field lines, in order; names already lowercase.</param>
    /// <returns>The encoded field section, a <c>HEADERS</c> frame's payload.</returns>
    public byte[] EncodeFieldSection(IEnumerable<QpackFieldLine> fieldLines)
    {
        ArgumentNullException.ThrowIfNull(fieldLines);
        var output = new List<byte> { 0x00, 0x00 };
        foreach (var line in fieldLines)
        {
            WriteFieldLine(output, line);
        }

        return [.. output];
    }

    /// <summary>
    /// Reads the decoder instructions in bytes received on the peer's decoder stream (RFC 9204 section
    /// 4.4). This encoder never references the dynamic table, so a Section Acknowledgment or an Insert
    /// Count Increment is <see cref="QpackErrorCode.DecoderStreamError" />; a Stream Cancellation is
    /// accepted and needs nothing done.
    /// </summary>
    /// <param name="bytes">The bytes the decoder stream delivered.</param>
    /// <returns>Whether the instructions were accepted, and if not, why.</returns>
    public QpackInstructionResult ReceiveDecoderStream(ReadOnlySpan<byte> bytes) => decoderStream.Receive(bytes);

    private void WriteFieldLine(List<byte> output, QpackFieldLine line)
    {
        if (!line.IsNeverIndexed && QpackStaticTable.TryFindLine(line, out var lineIndex))
        {
            QpackPrimitives.WriteInteger(output, lineIndex, 6, 0xC0);
            return;
        }

        var neverIndexedFlag = line.IsNeverIndexed ? (byte)0x20 : (byte)0x00;
        if (QpackStaticTable.TryFindName(line.Name, out var nameIndex))
        {
            QpackPrimitives.WriteInteger(output, nameIndex, 4, (byte)(0x50 | neverIndexedFlag));
        }
        else
        {
            QpackPrimitives.WriteString(output, line.Name, 4, (byte)(0x20 | (neverIndexedFlag >> 1)), stringCoding);
        }

        QpackPrimitives.WriteString(output, line.Value, 8, 0x00, stringCoding);
    }

    private static int ReadDecoderInstruction(ReadOnlySpan<byte> bytes, int position)
    {
        var first = bytes[position];
        if ((first & 0x80) != 0)
        {
            QpackPrimitives.ReadInteger(bytes, ref position, 7);
            throw new QpackDecodingException(QpackDecodingError.UnexpectedSectionAcknowledgment);
        }

        QpackPrimitives.ReadInteger(bytes, ref position, 6);
        QpackDecodingException.ThrowIf((first & 0x40) == 0, QpackDecodingError.InvalidInsertCountIncrement);
        return position;
    }
}
