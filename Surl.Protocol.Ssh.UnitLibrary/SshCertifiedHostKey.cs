namespace Surl.Protocol.Ssh;

/// <summary>
/// A host key served under its certificate: <c>K_S</c> is the certificate blob, the host-key
/// algorithms are the key's with <see cref="SshHostCertificate.CertificateSuffix"/> added
/// (<c>rsa-sha2-512-cert-v01@openssh.com</c> for <c>rsa-sha2-512</c>, and so on), and each signs
/// as the key does, the signature blob naming the key's own algorithm (OpenSSH
/// <c>PROTOCOL.certkeys</c> and <c>PROTOCOL</c>, section 3.1).
/// </summary>
internal sealed class SshCertifiedHostKey : SshHostKey
{
    private readonly SshHostKey certifiedKey;

    /// <summary>
    /// Creates the certified host key.
    /// </summary>
    /// <param name="certificate">The certificate.</param>
    /// <param name="certifiedKey">The key it certifies.</param>
    public SshCertifiedHostKey(SshHostCertificate certificate, SshHostKey certifiedKey)
        : base(
            certificate.CertificateType,
            certificate.Blob.ToArray(),
            [.. certifiedKey.SignatureAlgorithms.Select(algorithm => algorithm + SshHostCertificate.CertificateSuffix)])
    {
        Certificate = certificate;
        this.certifiedKey = certifiedKey;
    }

    /// <summary>
    /// The certificate.
    /// </summary>
    public SshHostCertificate Certificate { get; }

    /// <inheritdoc/>
    internal override byte[] SignRaw(string algorithm, byte[] data) => certifiedKey.SignRaw(SignatureName(algorithm), data);

    /// <inheritdoc/>
    private protected override string SignatureName(string algorithm) => algorithm[..^SshHostCertificate.CertificateSuffix.Length];
}
