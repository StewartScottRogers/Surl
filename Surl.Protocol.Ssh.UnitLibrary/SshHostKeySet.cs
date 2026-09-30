using System.Diagnostics.CodeAnalysis;

namespace Surl.Protocol.Ssh;

/// <summary>
/// The host keys the SSH server serves, at most one of each key type, and the host
/// certificates (<c>--hostcert</c>) it serves for them, at most one of each certificate type;
/// the client's host-key list picks one (ADR-0051, decision 4).
/// </summary>
public sealed class SshHostKeySet
{
    private readonly List<SshHostKey> keys = [];

    private readonly List<SshCertifiedHostKey> certifiedKeys = [];

    /// <summary>
    /// The keys held, in the order added.
    /// </summary>
    public IReadOnlyList<SshHostKey> Keys => keys;

    /// <summary>
    /// The certificates held, in the order added.
    /// </summary>
    public IEnumerable<SshHostCertificate> Certificates => certifiedKeys.Select(certifiedKey => certifiedKey.Certificate);

    /// <summary>
    /// The host-key algorithms the keys held sign with, then those of the certificates held, for
    /// <see cref="SshAlgorithmOffer.Default"/>.
    /// </summary>
    public IEnumerable<string> SignatureAlgorithms => AllKeys.SelectMany(key => key.SignatureAlgorithms);

    private IEnumerable<SshHostKey> AllKeys => keys.Concat(certifiedKeys);

    /// <summary>
    /// Adds <paramref name="key"/> unless a key of its type is already held.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="heldKey">The key of the same type already held, when it is not added; otherwise <see langword="null"/>.</param>
    /// <returns>Whether the key was added.</returns>
    public bool TryAdd(SshHostKey key, [NotNullWhen(false)] out SshHostKey? heldKey)
    {
        ArgumentNullException.ThrowIfNull(key);

        heldKey = keys.Find(held => held.KeyType == key.KeyType);
        if (heldKey is not null)
        {
            return false;
        }

        keys.Add(key);

        return true;
    }

    /// <summary>
    /// Whether one of the keys held is the key <paramref name="certificate"/> certifies.
    /// </summary>
    /// <param name="certificate">The certificate.</param>
    /// <returns><see langword="true"/> when one is.</returns>
    public bool HoldsKeyCertifiedBy(SshHostCertificate certificate)
    {
        ArgumentNullException.ThrowIfNull(certificate);

        return keys.Exists(certificate.Certifies);
    }

    /// <summary>
    /// Adds <paramref name="certificate"/>, served for the key it certifies, unless a certificate
    /// of its type is already held.
    /// </summary>
    /// <param name="certificate">The certificate, which certifies a key held (<see cref="HoldsKeyCertifiedBy"/>).</param>
    /// <param name="heldCertificate">The certificate of the same type already held, when it is not added; otherwise <see langword="null"/>.</param>
    /// <returns>Whether the certificate was added.</returns>
    /// <exception cref="ArgumentException">No key held is the key it certifies.</exception>
    public bool TryAdd(SshHostCertificate certificate, [NotNullWhen(false)] out SshHostCertificate? heldCertificate)
    {
        ArgumentNullException.ThrowIfNull(certificate);

        var certifiedKey = keys.Find(certificate.Certifies)
            ?? throw new ArgumentException("The certificate certifies no host key held.", nameof(certificate));
        heldCertificate = certifiedKeys.Find(held => held.KeyType == certificate.CertificateType)?.Certificate;
        if (heldCertificate is not null)
        {
            return false;
        }

        certifiedKeys.Add(new SshCertifiedHostKey(certificate, certifiedKey));

        return true;
    }

    /// <summary>
    /// The key that signs with <paramref name="algorithm"/>, the host-key algorithm the
    /// negotiation agreed: a certificate host-key algorithm's key is the key served under its
    /// certificate.
    /// </summary>
    /// <param name="algorithm">The host-key algorithm.</param>
    /// <returns>The key.</returns>
    /// <exception cref="InvalidOperationException">No key held signs with it: the offer named an algorithm of a key the server does not hold.</exception>
    internal SshHostKey ForSignatureAlgorithm(string algorithm) =>
        AllKeys.FirstOrDefault(key => key.SignatureAlgorithms.Contains(algorithm))
            ?? throw new InvalidOperationException($"The SSH server offered the host-key algorithm {algorithm}, but holds no key that signs with it.");
}
