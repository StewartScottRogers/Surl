namespace Surl.Authentication;

/// <summary>
/// Issues the nonces Digest challenges carry and recognises them in answers. A seam so the
/// answers recorded from upstream curl, which echo the fixed nonce their recording offered,
/// can be replayed.
/// </summary>
internal interface IDigestNonceBook
{
    /// <summary>
    /// A new nonce for one <c>401</c>'s Digest challenges.
    /// </summary>
    /// <returns>The nonce, as it is written between the quotes.</returns>
    string Issue();

    /// <summary>
    /// Whether <paramref name="nonce"/> was issued here, and whether it has expired.
    /// </summary>
    /// <param name="nonce">The <c>nonce</c> parameter of an answer.</param>
    /// <returns>Unknown, fresh or expired.</returns>
    DigestNonceState Check(string nonce);

    /// <summary>
    /// Records a verified answer's <c>nc</c> for a fresh nonce, refusing a count no higher than
    /// one already used with it, so a captured answer cannot be replayed.
    /// </summary>
    /// <param name="nonce">A fresh nonce.</param>
    /// <param name="nonceCount">The answer's <c>nc</c>.</param>
    /// <returns><see langword="false"/> when the count was already used or passed.</returns>
    bool TryRecordUse(string nonce, uint nonceCount);
}
