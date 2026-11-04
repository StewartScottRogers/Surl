namespace Surl.Protocol.Ssh;

/// <summary>
/// One direction's cipher under an HMAC (RFC 4253, section 6.3): it carries its running state -
/// a CTR counter, a CBC chaining block or an RC4 keystream - from one packet to the next, so an
/// instance encrypts one direction's packets or decrypts them, never both.
/// </summary>
internal interface ISshPacketCipher
{
    /// <summary>
    /// The block size packets are padded to: the block cipher's, or 8 for a stream cipher
    /// (RFC 4253, section 6).
    /// </summary>
    int BlockSize { get; }

    /// <summary>
    /// Encrypts the next bytes of the direction.
    /// </summary>
    /// <param name="plaintext">The bytes, a whole number of blocks long.</param>
    /// <returns>The ciphertext.</returns>
    byte[] Encrypt(ReadOnlySpan<byte> plaintext);

    /// <summary>
    /// Decrypts the next bytes of the direction.
    /// </summary>
    /// <param name="ciphertext">The bytes, a whole number of blocks long.</param>
    /// <returns>The plaintext.</returns>
    byte[] Decrypt(ReadOnlySpan<byte> ciphertext);
}
