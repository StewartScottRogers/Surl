namespace Surl.Protocol.Ssh;

/// <summary>
/// How a cipher used with an HMAC is keyed and made (RFC 4253, section 6.3): its key and IV
/// lengths, which the key exchange derives (section 7.2), and the cipher they make.
/// </summary>
/// <param name="KeyLength">The encryption key's length in bytes.</param>
/// <param name="InitializationVectorLength">The IV's length in bytes; 0 for a stream cipher.</param>
/// <param name="Create">Makes one direction's cipher from its key and IV.</param>
internal sealed record SshCipherAlgorithm(int KeyLength, int InitializationVectorLength, Func<byte[], byte[], ISshPacketCipher> Create)
{
    private static readonly Dictionary<string, SshCipherAlgorithm> Ciphers = new(StringComparer.Ordinal)
    {
        ["aes128-ctr"] = Ctr(16),
        ["aes192-ctr"] = Ctr(24),
        ["aes256-ctr"] = Ctr(32),
        ["aes128-cbc"] = AesCbc(16),
        ["aes192-cbc"] = AesCbc(24),
        ["aes256-cbc"] = AesCbc(32),
        ["rijndael-cbc@lysator.liu.se"] = AesCbc(32),
        ["3des-cbc"] = new(24, 8, SshCbc.TripleDes),
        ["arcfour"] = new(SshArcfour.KeyLength, 0, (key, _) => new SshArcfour(key, 0)),
        ["arcfour128"] = new(SshArcfour.KeyLength, 0, (key, _) => new SshArcfour(key, SshArcfour.Arcfour128Discard)),
        ["blowfish-cbc"] = new(16, 8, SshBlockCbc.Blowfish),
        ["cast128-cbc"] = new(16, 8, SshBlockCbc.Cast128),
    };

    /// <summary>
    /// The cipher named <paramref name="name"/>: <c>aes128-ctr</c>, <c>aes192-ctr</c> and
    /// <c>aes256-ctr</c> (RFC 4344, section 4), and the weak ciphers offered only with
    /// <c>--allow-weak-ssh-algorithms</c>: <c>aes128-cbc</c>, <c>aes192-cbc</c>,
    /// <c>aes256-cbc</c> and its older name <c>rijndael-cbc@lysator.liu.se</c>, <c>3des-cbc</c>,
    /// <c>arcfour</c>, <c>arcfour128</c>, and <c>blowfish-cbc</c> and <c>cast128-cbc</c> (Blowfish and
    /// CAST-128 with 128-bit keys; ADR-0061).
    /// </summary>
    /// <param name="name">The cipher agreed.</param>
    /// <returns>The cipher, or <see langword="null"/> for an AEAD cipher or one not built.</returns>
    public static SshCipherAlgorithm? ForName(string name) => Ciphers.GetValueOrDefault(name);

    private static SshCipherAlgorithm Ctr(int keyLength) => new(keyLength, SshAesCtr.BlockSize, (key, iv) => new SshAesCtr(key, iv));

    private static SshCipherAlgorithm AesCbc(int keyLength) => new(keyLength, 16, SshCbc.Aes);
}
