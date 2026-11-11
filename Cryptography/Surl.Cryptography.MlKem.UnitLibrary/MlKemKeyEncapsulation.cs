namespace Surl.Cryptography.MlKem;

/// <summary>
/// ML-KEM.KeyGen_internal, ML-KEM.Encaps_internal and ML-KEM.Decaps_internal (FIPS 203 section 6)
/// with the input checks of section 7, over one parameter set's <see cref="KPke"/>. The public
/// <see cref="MlKem768"/> and <see cref="MlKem1024"/> each delegate to one instance.
/// </summary>
internal sealed class MlKemKeyEncapsulation
{
    /// <summary>The bytes of a shared secret, and of each random seed d, z and m.</summary>
    public const int SecretSize = 32;

    private readonly KPke kPke;
    private readonly int hashOffset;

    /// <summary>The ML-KEM built on <paramref name="kPke"/>.</summary>
    public MlKemKeyEncapsulation(KPke kPke)
    {
        this.kPke = kPke;
        hashOffset = kPke.DecryptionKeySize + kPke.EncryptionKeySize;
    }

    /// <summary>The bytes of an encapsulation key: K-PKE's encryption key.</summary>
    public int EncapsulationKeySize => kPke.EncryptionKeySize;

    /// <summary>The bytes of a decapsulation key: dk_PKE || ek || H(ek) || z.</summary>
    public int DecapsulationKeySize => hashOffset + 64;

    /// <summary>The bytes of a ciphertext.</summary>
    public int CiphertextSize => kPke.CiphertextSize;

    /// <summary>ML-KEM.KeyGen_internal (FIPS 203 Algorithm 16).</summary>
    /// <exception cref="ArgumentException">A span is not its size.</exception>
    public void GenerateKey(ReadOnlySpan<byte> d, ReadOnlySpan<byte> z, Span<byte> encapsulationKey, Span<byte> decapsulationKey)
    {
        RequireSize(d, SecretSize, nameof(d));
        RequireSize(z, SecretSize, nameof(z));
        RequireSize(encapsulationKey, EncapsulationKeySize, nameof(encapsulationKey));
        RequireSize(decapsulationKey, DecapsulationKeySize, nameof(decapsulationKey));
        kPke.GenerateKey(d, encapsulationKey, decapsulationKey[..kPke.DecryptionKeySize]);
        encapsulationKey.CopyTo(decapsulationKey[kPke.DecryptionKeySize..]);
        Sha3.Sha3_256(encapsulationKey).CopyTo(decapsulationKey[hashOffset..]);
        z.CopyTo(decapsulationKey[(hashOffset + 32)..]);
    }

    /// <summary>ML-KEM.Encaps_internal (FIPS 203 Algorithm 17) after section 7.2's modulus check.</summary>
    /// <returns>False, writing nothing, when the key fails the modulus check.</returns>
    /// <exception cref="ArgumentException">A span is not its size.</exception>
    public bool TryEncapsulate(ReadOnlySpan<byte> encapsulationKey, ReadOnlySpan<byte> m, Span<byte> ciphertext, Span<byte> sharedSecret)
    {
        RequireSize(encapsulationKey, EncapsulationKeySize, nameof(encapsulationKey));
        RequireSize(m, SecretSize, nameof(m));
        RequireSize(ciphertext, CiphertextSize, nameof(ciphertext));
        RequireSize(sharedSecret, SecretSize, nameof(sharedSecret));
        if (!kPke.HasReducedCoefficients(encapsulationKey))
        {
            return false;
        }

        var keyAndRandomness = Sha3.Sha3_512([.. m, .. Sha3.Sha3_256(encapsulationKey)]);
        kPke.Encrypt(encapsulationKey, m, keyAndRandomness.AsSpan(32), ciphertext);
        keyAndRandomness.AsSpan(0, SecretSize).CopyTo(sharedSecret);
        return true;
    }

    /// <summary>
    /// ML-KEM.Decaps_internal (FIPS 203 Algorithm 18) after section 7.3's hash check. A ciphertext
    /// that does not re-encrypt to itself yields the implicit-rejection secret J(z || c), chosen
    /// without a branch on the comparison.
    /// </summary>
    /// <returns>False, writing nothing, when the key's stored hash is not H of its encapsulation key.</returns>
    /// <exception cref="ArgumentException">A span is not its size.</exception>
    public bool TryDecapsulate(ReadOnlySpan<byte> decapsulationKey, ReadOnlySpan<byte> ciphertext, Span<byte> sharedSecret)
    {
        RequireSize(decapsulationKey, DecapsulationKeySize, nameof(decapsulationKey));
        RequireSize(ciphertext, CiphertextSize, nameof(ciphertext));
        RequireSize(sharedSecret, SecretSize, nameof(sharedSecret));
        var encryptionKey = decapsulationKey[kPke.DecryptionKeySize..hashOffset];
        var hash = decapsulationKey.Slice(hashOffset, 32);
        if (!Sha3.Sha3_256(encryptionKey).AsSpan().SequenceEqual(hash))
        {
            return false;
        }

        Span<byte> m = stackalloc byte[SecretSize];
        kPke.Decrypt(decapsulationKey[..kPke.DecryptionKeySize], ciphertext, m);
        var keyAndRandomness = Sha3.Sha3_512([.. m, .. hash]);
        var rejection = Sha3.Shake256([.. decapsulationKey[(hashOffset + 32)..], .. ciphertext], SecretSize);
        var reencrypted = new byte[CiphertextSize];
        kPke.Encrypt(encryptionKey, m, keyAndRandomness.AsSpan(32), reencrypted);
        var difference = 0;
        for (var index = 0; index < reencrypted.Length; index++)
        {
            difference |= ciphertext[index] ^ reencrypted[index];
        }

        var keep = (byte)((difference - 1) >> 8);
        for (var index = 0; index < SecretSize; index++)
        {
            sharedSecret[index] = (byte)((keyAndRandomness[index] & keep) | (rejection[index] & ~keep));
        }

        return true;
    }

    private static void RequireSize(ReadOnlySpan<byte> span, int size, string name)
    {
        if (span.Length != size)
        {
            throw new ArgumentException($"{name} is {size} bytes, not {span.Length}.", name);
        }
    }
}
