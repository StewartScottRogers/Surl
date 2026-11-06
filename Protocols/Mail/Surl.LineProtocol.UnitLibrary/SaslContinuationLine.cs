namespace Surl.LineProtocol;

/// <summary>
/// Classifies one SASL continuation line, read without its CRLF (ADR-0050, decision 8).
/// </summary>
public static class SaslContinuationLine
{
    /// <summary>
    /// Classifies <paramref name="line"/>: <c>*</c> alone cancels, an empty line is an empty
    /// response, and anything else is decoded as base64 or is <see cref="SaslContinuationOutcome.NotBase64"/>.
    /// </summary>
    /// <param name="line">The line's bytes without its CRLF.</param>
    /// <returns>The decoded response, <see cref="SaslContinuationOutcome.Cancelled"/> or <see cref="SaslContinuationOutcome.NotBase64"/>.</returns>
    public static SaslContinuationReadResult Classify(ReadOnlySpan<byte> line)
    {
        if (line.SequenceEqual("*"u8))
        {
            return SaslContinuationReadResult.NoResponse(SaslContinuationOutcome.Cancelled);
        }

        // Convert skips these four as base64 whitespace; a continuation holding one is refused.
        if (line.IndexOfAny(" \t\r\n"u8) >= 0)
        {
            return SaslContinuationReadResult.NoResponse(SaslContinuationOutcome.NotBase64);
        }

        var decoded = new byte[line.Length];

        return Convert.TryFromBase64String(System.Text.Encoding.Latin1.GetString(line), decoded, out var decodedCount)
            ? SaslContinuationReadResult.Read(decoded[..decodedCount])
            : SaslContinuationReadResult.NoResponse(SaslContinuationOutcome.NotBase64);
    }
}
