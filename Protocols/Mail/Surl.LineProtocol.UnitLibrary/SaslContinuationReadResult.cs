namespace Surl.LineProtocol;

/// <summary>
/// The result of reading one SASL continuation line: the decoded response, or the reason there is none.
/// </summary>
/// <param name="Outcome">How the read ended.</param>
/// <param name="Response">
/// The decoded response, empty for an empty line; <see langword="null"/> unless
/// <paramref name="Outcome"/> is <see cref="SaslContinuationOutcome.ResponseRead"/>.
/// </param>
public sealed record SaslContinuationReadResult(SaslContinuationOutcome Outcome, byte[]? Response)
{
    /// <summary>
    /// A result that carries a decoded response.
    /// </summary>
    /// <param name="response">The decoded response.</param>
    /// <returns>A <see cref="SaslContinuationOutcome.ResponseRead"/> result.</returns>
    public static SaslContinuationReadResult Read(byte[] response) => new(SaslContinuationOutcome.ResponseRead, response);

    /// <summary>
    /// A result that carries no response.
    /// </summary>
    /// <param name="outcome">Why there is no response.</param>
    /// <returns>A result with <see cref="Response"/> <see langword="null"/>.</returns>
    public static SaslContinuationReadResult NoResponse(SaslContinuationOutcome outcome) => new(outcome, null);
}
