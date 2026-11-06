using Surl.Hpack;
using Surl.Qpack;

namespace Surl.Http3;

/// <summary>
/// One HTTP/3 connection's QPACK (ADR-0082 decision 8): the decoder for the client's field
/// sections, with no dynamic table and <c>--max-message</c> as the largest section, and the encoder
/// for the server's, with the static table and Huffman literals where shorter. Request streams and
/// the client's QPACK streams use it at once, so each call takes one lock.
/// </summary>
/// <param name="maxMessageBytes">The <c>--max-message</c> limit, 0 for none.</param>
internal sealed class Http3FieldSections(long maxMessageBytes)
{
    private readonly Lock gate = new();
    private readonly QpackDecoder decoder = new(0, (int)Math.Min(maxMessageBytes == 0 ? int.MaxValue : maxMessageBytes, int.MaxValue));
    private readonly QpackEncoder encoder = new(HpackStringCoding.WhenShorter);

    /// <summary>Decodes a <c>HEADERS</c> frame's payload.</summary>
    /// <param name="streamId">The request stream it arrived on.</param>
    /// <param name="section">The payload.</param>
    /// <returns>The field lines, or the rule the section broke.</returns>
    public QpackFieldSectionResult Decode(ulong streamId, byte[] section)
    {
        lock (gate)
        {
            return decoder.DecodeFieldSection((long)streamId, section);
        }
    }

    /// <summary>Encodes the server's field lines as a <c>HEADERS</c> frame's payload.</summary>
    /// <param name="fieldLines">The lines, names lowercase.</param>
    /// <returns>The payload.</returns>
    public byte[] Encode(IEnumerable<QpackFieldLine> fieldLines)
    {
        lock (gate)
        {
            return encoder.EncodeFieldSection(fieldLines);
        }
    }

    /// <summary>Applies bytes from the client's QPACK encoder stream.</summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>Whether they were accepted.</returns>
    public QpackInstructionResult ReceiveEncoderStream(ReadOnlySpan<byte> bytes)
    {
        lock (gate)
        {
            return decoder.ReceiveEncoderStream(bytes);
        }
    }

    /// <summary>Reads bytes from the client's QPACK decoder stream.</summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>Whether they were accepted.</returns>
    public QpackInstructionResult ReceiveDecoderStream(ReadOnlySpan<byte> bytes)
    {
        lock (gate)
        {
            return encoder.ReceiveDecoderStream(bytes);
        }
    }
}
