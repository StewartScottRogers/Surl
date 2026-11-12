namespace Surl.Cryptography.MlKem;

/// <summary>
/// ML-KEM-1024 (FIPS 203): ML-KEM.KeyGen_internal, ML-KEM.Encaps_internal and
/// ML-KEM.Decaps_internal (section 6) with the input checks of section 7, with k = 4,
/// eta_1 = eta_2 = 2, d_u = 11 and d_v = 5. The caller draws the random seeds; this class never
/// does. Hand-built because the base class library's <c>MLKem</c> is missing on macOS and needs
/// OpenSSL 3.5 on Linux (ADR-0082 decision 2).
/// </summary>
public static class MlKem1024
{
    /// <summary>The bytes of an encapsulation key: 1568.</summary>
    public const int EncapsulationKeySize = 1568;

    /// <summary>The bytes of a decapsulation key: 3168.</summary>
    public const int DecapsulationKeySize = 3168;

    /// <summary>The bytes of a ciphertext: 1568.</summary>
    public const int CiphertextSize = 1568;

    /// <summary>The bytes of a shared secret: 32.</summary>
    public const int SharedSecretSize = MlKemKeyEncapsulation.SecretSize;

    /// <summary>The bytes of each random seed: d and z for key generation, m for encapsulation.</summary>
    public const int SeedSize = MlKemKeyEncapsulation.SecretSize;

    private static readonly MlKemKeyEncapsulation KeyEncapsulation = new(KPke.ForMlKem1024);

    /// <summary>ML-KEM.KeyGen_internal (FIPS 203 Algorithm 16) from the seeds <paramref name="d"/> and <paramref name="z"/>.</summary>
    /// <param name="d">The 32-byte seed of K-PKE's key.</param>
    /// <param name="z">The 32-byte implicit-rejection seed.</param>
    /// <param name="encapsulationKey">Receives the 1568-byte encapsulation key.</param>
    /// <param name="decapsulationKey">Receives the 3168-byte decapsulation key.</param>
    /// <exception cref="ArgumentException">A span is not its size.</exception>
    public static void GenerateKey(ReadOnlySpan<byte> d, ReadOnlySpan<byte> z, Span<byte> encapsulationKey, Span<byte> decapsulationKey) =>
        KeyEncapsulation.GenerateKey(d, z, encapsulationKey, decapsulationKey);

    /// <summary>
    /// ML-KEM.Encaps_internal (FIPS 203 Algorithm 17) with the randomness <paramref name="m"/>,
    /// after section 7.2's modulus check of <paramref name="encapsulationKey"/>.
    /// </summary>
    /// <param name="encapsulationKey">The peer's 1568-byte encapsulation key.</param>
    /// <param name="m">32 random bytes.</param>
    /// <param name="ciphertext">Receives the 1568-byte ciphertext.</param>
    /// <param name="sharedSecret">Receives the 32-byte shared secret.</param>
    /// <returns>False, writing nothing, when the key fails the modulus check.</returns>
    /// <exception cref="ArgumentException">A span is not its size.</exception>
    public static bool TryEncapsulate(ReadOnlySpan<byte> encapsulationKey, ReadOnlySpan<byte> m, Span<byte> ciphertext, Span<byte> sharedSecret) =>
        KeyEncapsulation.TryEncapsulate(encapsulationKey, m, ciphertext, sharedSecret);

    /// <summary>
    /// ML-KEM.Decaps_internal (FIPS 203 Algorithm 18), after section 7.3's hash check of
    /// <paramref name="decapsulationKey"/>. A ciphertext that does not re-encrypt to itself yields
    /// the implicit-rejection secret J(z || c), chosen without a branch on the comparison.
    /// </summary>
    /// <param name="decapsulationKey">The 3168-byte decapsulation key.</param>
    /// <param name="ciphertext">The 1568-byte ciphertext.</param>
    /// <param name="sharedSecret">Receives the 32-byte shared secret.</param>
    /// <returns>False, writing nothing, when the key's stored hash is not H of its encapsulation key.</returns>
    /// <exception cref="ArgumentException">A span is not its size.</exception>
    public static bool TryDecapsulate(ReadOnlySpan<byte> decapsulationKey, ReadOnlySpan<byte> ciphertext, Span<byte> sharedSecret) =>
        KeyEncapsulation.TryDecapsulate(decapsulationKey, ciphertext, sharedSecret);
}
