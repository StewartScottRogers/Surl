namespace Surl.Kerberos;

/// <summary>
/// The key derivation of RFC 3961's simplified profile (section 5.1), as RFC 3962 instantiates it
/// for AES: <c>DK(key, constant) = random-to-key(DR(key, constant))</c>, where random-to-key is
/// the identity function (RFC 3962 section 6).
/// </summary>
internal static class SimplifiedProfileKeyDerivation
{
    /// <summary>
    /// Computes <c>DK(baseKey, constant)</c>: the constant is n-folded to one AES block, which is
    /// enciphered under the base key again and again, the blocks laid end to end until they are
    /// as long as the base key.
    /// </summary>
    /// <param name="baseKey">A 16- or 32-byte AES key.</param>
    /// <param name="constant">The derivation constant; at least one byte.</param>
    /// <returns>The derived key, as long as <paramref name="baseKey" />.</returns>
    public static byte[] DeriveKey(ReadOnlySpan<byte> baseKey, ReadOnlySpan<byte> constant)
    {
        byte[] derived = new byte[baseKey.Length];
        byte[] block = NFold.Fold(constant, AesCiphertextStealing.BlockLength);
        for (int offset = 0; offset < derived.Length; offset += AesCiphertextStealing.BlockLength)
        {
            block = AesCiphertextStealing.EncryptBlock(baseKey, block);
            block.CopyTo(derived, offset);
        }

        return derived;
    }
}
