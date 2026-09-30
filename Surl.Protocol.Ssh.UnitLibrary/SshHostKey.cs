using System.Security.Cryptography;

namespace Surl.Protocol.Ssh;

/// <summary>
/// One host key the SSH server holds: its public key blob (RFC 4253, section 6.6), the
/// host-key algorithms it signs with, and the signing of the exchange hash (ADR-0051,
/// decisions 2 and 4). An RSA key signs <c>rsa-sha2-512</c> and <c>rsa-sha2-256</c> (RFC 8332);
/// an ECDSA key on P-256, P-384 or P-521 its curve's <c>ecdsa-sha2-*</c> (RFC 5656, section 3).
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
    /// The key type the public key blob starts with: <c>ssh-rsa</c>, or
    /// <c>ecdsa-sha2-nistp256</c>, <c>-nistp384</c> or <c>-nistp521</c>. The server holds at
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
