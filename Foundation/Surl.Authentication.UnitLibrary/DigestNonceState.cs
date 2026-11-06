namespace Surl.Authentication;

/// <summary>
/// What an <see cref="IDigestNonceBook"/> knows of a nonce an answer echoes.
/// </summary>
internal enum DigestNonceState
{
    /// <summary>
    /// Not a nonce this server issued: the answer is refused.
    /// </summary>
    Unknown,

    /// <summary>
    /// Issued here and still within <see cref="DigestNonceBook.Lifetime"/>.
    /// </summary>
    Fresh,

    /// <summary>
    /// Issued here but older than <see cref="DigestNonceBook.Lifetime"/>: a right answer gets
    /// the <c>stale=true</c> challenge (RFC 7616 section 3.3).
    /// </summary>
    Expired,
}
