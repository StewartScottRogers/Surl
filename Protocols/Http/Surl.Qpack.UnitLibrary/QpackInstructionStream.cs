namespace Surl.Qpack;

/// <summary>
/// Reads one instruction from <paramref name="bytes" /> at <paramref name="position" />, applies it,
/// and returns where it ended. It must change nothing before the whole instruction is read.
/// </summary>
/// <param name="bytes">The bytes received and not yet consumed.</param>
/// <param name="position">Where the instruction starts.</param>
/// <returns>Where the instruction ended.</returns>
internal delegate int QpackInstructionReader(ReadOnlySpan<byte> bytes, int position);

/// <summary>
/// The receiving end of an encoder or decoder stream (RFC 9204 section 4.2): an unframed sequence of
/// instructions that can arrive split anywhere, so the bytes of an unfinished instruction wait for the
/// next ones.
/// </summary>
/// <param name="failureCode">The connection error a refused instruction is.</param>
/// <param name="readInstruction">Reads and applies one instruction.</param>
internal sealed class QpackInstructionStream(QpackErrorCode failureCode, QpackInstructionReader readInstruction)
{
    private readonly List<byte> unconsumed = [];

    /// <summary>Gets the number of bytes of an unfinished instruction waiting for more.</summary>
    public int UnconsumedCount => unconsumed.Count;

    /// <summary>Appends <paramref name="bytes" /> and applies every instruction they complete.</summary>
    /// <param name="bytes">The bytes the stream delivered.</param>
    /// <returns>Whether every complete instruction was accepted, and if not, why.</returns>
    public QpackInstructionResult Receive(ReadOnlySpan<byte> bytes)
    {
        unconsumed.AddRange(bytes);
        var buffered = unconsumed.ToArray();
        var consumed = 0;
        try
        {
            while (consumed < buffered.Length)
            {
                consumed = readInstruction(buffered, consumed);
            }
        }
        catch (QpackDecodingException exception) when (!exception.IsTruncation)
        {
            return new QpackInstructionResult(exception.Error, failureCode);
        }
        catch (QpackDecodingException)
        {
            // The last instruction is unfinished; its bytes wait for the next ones.
        }
        finally
        {
            unconsumed.RemoveRange(0, consumed);
        }

        return QpackInstructionResult.Accepted;
    }
}
