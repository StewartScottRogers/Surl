namespace Surl.Protocol.Abstractions;

/// <summary>
/// The rest of a login that binds the request's body, judged once the body has been read
/// (ADR-0045): AWS Signature Version 4 signs the body's SHA-256, so its signature can only be
/// finished, or its signed content hash compared, with the body in hand. It rides on a
/// <see cref="HttpAuthenticationOutcome.Proceed"/> verdict as
/// <see cref="HttpAuthenticationVerdict.BodyCheck"/>.
/// </summary>
public interface IHttpRequestBodyCheck
{
    /// <summary>
    /// Judges the request by its body's SHA-256: served, or challenged or refused as any
    /// verdict is. The verdict returned carries no <see cref="HttpAuthenticationVerdict.BodyCheck"/>.
    /// </summary>
    /// <param name="bodySha256">The SHA-256 of the body's bytes as received, after any chunked coding is removed; the empty body's hash when there is none.</param>
    /// <param name="cancellationToken">Cancels the judgement.</param>
    /// <returns>The verdict on the request, with its login note.</returns>
    ValueTask<HttpAuthenticationVerdict> JudgeBodyAsync(
        ReadOnlyMemory<byte> bodySha256, CancellationToken cancellationToken);
}
