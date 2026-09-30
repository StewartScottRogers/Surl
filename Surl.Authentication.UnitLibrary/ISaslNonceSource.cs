namespace Surl.Authentication;

/// <summary>
/// Where the random part of a <c>CRAM-MD5</c> challenge and a <c>DIGEST-MD5</c> nonce come from:
/// random in production (<see cref="RandomSaslNonceSource"/>), fixed in tests so an answer
/// measured from upstream curl can be checked (ADR-0049, section 5).
/// </summary>
internal interface ISaslNonceSource
{
    /// <summary>
    /// New random bytes.
    /// </summary>
    /// <param name="length">How many bytes.</param>
    /// <returns><paramref name="length"/> bytes.</returns>
    byte[] CreateNonce(int length);
}
