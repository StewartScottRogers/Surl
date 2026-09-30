using System.Security.Cryptography;

namespace Surl.Protocol.Ssh;

/// <summary>
/// One host key the SSH server holds: its public key blob (RFC 4253, section 6.6), the
/// host-key algorithms it signs with, and the signing of the exchange hash (ADR-0051,
/// decisions 2 and 4). An RSA key signs <c>rsa-sha2-512</c> and <c>rsa-sha2-256</c> (RFC 8332)
/// and the SHA-1 <c>ssh-rsa</c>; an ECDSA key on P-256, P-384 or P-521 its curve's
/// <c>ecdsa-sha2-*</c> (RFC 5656, section 3); an Ed25519 key <c>ssh-ed25519</c> (RFC 8709); a
/// DSA key <c>ssh-dss</c> (RFC 4253, section 6.6). <c>ssh-rsa</c> and <c>ssh-dss</c> are
/// offered only with <c>--allow-weak-ssh-algorithms</c>.
/// </summary>
public abstract class SshHostKey
{
    private readonly byte[] publicKeyBlob;

    private protected SshHostKey(string keyType, byte[] publicKeyBlob, IReadOnlyList<string> signatureAlgorithms)
    {
        KeyType = keyType;
        this.publicKeyBlob = publicKeyBlob;
        SignatureAlgorithms = signatureAlgorithms;
    }

    /// <summary>
    /// The key type the public key blob starts with: <c>ssh-rsa</c>, <c>ssh-ed25519</c>,
    /// <c>ssh-dss</c>, or <c>ecdsa-sha2-nistp256</c>, <c>-nistp384</c> or <c>-nistp521</c>. The server holds at
    /// most one key of each type (ADR-0051, decision 4).
    /// </summary>
    public string KeyType { get; }

    /// <summary>
    /// The public key blob, <c>K_S</c> in the key exchange (RFC 4253, section 6.6).
    /// </summary>
    public ReadOnlyMemory<byte> PublicKeyBlob => publicKeyBlob;

    /// <summary>
    /// The host-key algorithms this key signs with, in ADR-0051 decision 2's order.
    /// </summary>
    public IReadOnlyList<string> SignatureAlgorithms { get; }

    /// <summary>
    /// A host key holding <paramref name="rsa"/>'s private key, which is copied.
    /// </summary>
    /// <param name="rsa">The key.</param>
    /// <returns>The host key.</returns>
    public static SshHostKey FromRsa(RSA rsa)
    {
        ArgumentNullException.ThrowIfNull(rsa);

        return new SshRsaHostKey(rsa.ExportParameters(includePrivateParameters: true));
    }

    /// <summary>
    /// A host key holding <paramref name="dsa"/>'s private key, which is copied: an
    /// <c>ssh-dss</c> key, which <see cref="SshAlgorithmOffer.Default"/> offers only with
    /// <c>--allow-weak-ssh-algorithms</c>.
    /// </summary>
    /// <param name="dsa">The key, with a 160-bit q, as every 1024-bit DSA key has.</param>
    /// <returns>The host key.</returns>
    /// <exception cref="ArgumentException">q is not 160 bits, so <c>ssh-dss</c>'s 40-byte signature cannot hold r and s.</exception>
    public static SshHostKey FromDsa(DSA dsa)
    {
        ArgumentNullException.ThrowIfNull(dsa);

        var parameters = dsa.ExportParameters(includePrivateParameters: true);

        return parameters.Q!.Length == SshDsaHostKey.SubgroupLength
            ? new SshDsaHostKey(parameters)
            : throw new ArgumentException("An ssh-dss host key has a 160-bit q; this one's is longer.", nameof(dsa));
    }

    /// <summary>
    /// A host key holding <paramref name="ecdsa"/>'s private key, which is copied.
    /// </summary>
    /// <param name="ecdsa">The key, on P-256, P-384 or P-521.</param>
    /// <returns>The host key.</returns>
    /// <exception cref="ArgumentException">The key is on another curve.</exception>
    /// <remarks>
    /// The curve is told by its public point, which must lie on P-256, P-384 or P-521, rather
    /// than by the name the platform exports, which differs between platforms.
    /// </remarks>
    public static SshHostKey FromEcdsa(ECDsa ecdsa)
    {
        ArgumentNullException.ThrowIfNull(ecdsa);

        var parameters = ecdsa.ExportParameters(includePrivateParameters: true);
        var curve = SshNistCurve.All.FirstOrDefault(candidate => candidate.Holds(parameters.Q))
            ?? throw new ArgumentException("An SSH ECDSA host key is on P-256, P-384 or P-521; this one is on another curve.", nameof(ecdsa));

        return new SshEcdsaHostKey(curve, parameters);
    }

    /// <summary>
    /// Signs <paramref name="data"/> with <paramref name="algorithm"/> and wraps the signature
    /// as the key exchange reply carries it: <c>string</c> the algorithm, <c>string</c> the
    /// signature.
    /// </summary>
    /// <param name="algorithm">One of <see cref="SignatureAlgorithms"/>.</param>
    /// <param name="data">What is signed: the exchange hash H.</param>
    /// <returns>The signature blob.</returns>
    internal byte[] Sign(string algorithm, byte[] data)
    {
        var blob = new SshWireWriter();
        blob.WriteString(algorithm);
        blob.WriteString(SignRaw(algorithm, data));

        return blob.ToArray();
    }

    /// <summary>
    /// The signature itself, without the algorithm name before it.
    /// </summary>
    /// <param name="algorithm">One of <see cref="SignatureAlgorithms"/>.</param>
    /// <param name="data">What is signed.</param>
    /// <returns>The signature.</returns>
    private protected abstract byte[] SignRaw(string algorithm, byte[] data);
}
