using System.Text;

namespace Surl.Protocol.Ssh;

/// <summary>
/// One OpenSSH host certificate the SSH server serves for one of its host keys (ADR-0051,
/// decisions 2 and 4; <c>--hostcert</c>): the <c>*-cert.pub</c> file <c>ssh-keygen -s &lt;ca&gt; -h</c>
/// writes, one line of the certificate type, the base64 certificate blob and a comment. The blob
/// is OpenSSH <c>PROTOCOL.certkeys</c>'s: <c>string</c> the certificate type, <c>string</c> nonce,
/// the key's public fields, <c>uint64</c> serial, <c>uint32</c> type (2, a host certificate),
/// <c>string</c> key id, <c>string</c> valid principals, <c>uint64</c> valid after and valid
/// before, <c>string</c> critical options, extensions and reserved, <c>string</c> signature key,
/// <c>string</c> signature.
/// </summary>
/// <remarks>
/// The certificate types read are <c>ssh-rsa-cert-v01@openssh.com</c>,
/// <c>ecdsa-sha2-nistp256-cert-v01@openssh.com</c>, <c>-nistp384-</c>, <c>-nistp521-</c> and
/// <c>ssh-ed25519-cert-v01@openssh.com</c>, the ones for the host keys surl serves that upstream
/// curl's host-key lists name. The server judges neither the validity period nor the CA's
/// signature: the client does, against the certificate authority it trusts (ADR-0051, decision 4).
/// </remarks>
public sealed class SshHostCertificate
{
    /// <summary>
    /// What a certificate type and each certificate host-key algorithm add to their key's name
    /// (OpenSSH <c>PROTOCOL.certkeys</c>).
    /// </summary>
    public const string CertificateSuffix = "-cert-v01@openssh.com";

    // PROTOCOL.certkeys: SSH2_CERT_TYPE_HOST.
    private const uint HostCertificateType = 2;

    // The public fields each certified key type's blob holds after its name, every one an SSH
    // string: e and n (RFC 4253 section 6.6), the curve and Q (RFC 5656 section 3.1), or the
    // 32-byte key (RFC 8709 section 4).
    private static readonly Dictionary<string, int> PublicFieldCountsByKeyType = new(StringComparer.Ordinal)
    {
        [SshRsaHostKey.RsaKeyType] = 2,
        ["ecdsa-sha2-nistp256"] = 2,
        ["ecdsa-sha2-nistp384"] = 2,
        ["ecdsa-sha2-nistp521"] = 2,
        [SshEd25519HostKey.Ed25519KeyType] = 1,
    };

    private readonly byte[] blob;

    private readonly byte[] certifiedPublicKeyBlob;

    private SshHostCertificate(string keyType, byte[] blob, byte[] certifiedPublicKeyBlob)
    {
        KeyType = keyType;
        this.blob = blob;
        this.certifiedPublicKeyBlob = certifiedPublicKeyBlob;
    }

    /// <summary>
    /// The certificate type, e.g. <c>ssh-ed25519-cert-v01@openssh.com</c>.
    /// </summary>
    public string CertificateType => KeyType + CertificateSuffix;

    /// <summary>
    /// The type of the key the certificate certifies, e.g. <c>ssh-ed25519</c>.
    /// </summary>
    public string KeyType { get; }

    /// <summary>
    /// The certificate blob, <c>K_S</c> in a key exchange that agrees a certificate host-key
    /// algorithm.
    /// </summary>
    public ReadOnlyMemory<byte> Blob => blob;

    /// <summary>
    /// The public key blob of the key the certificate certifies (RFC 4253, section 6.6).
    /// </summary>
    public ReadOnlyMemory<byte> CertifiedPublicKeyBlob => certifiedPublicKeyBlob;

    /// <summary>
    /// Reads one host certificate from a <c>--hostcert</c> file's bytes. Reading the file from
    /// disk is <c>Surl.Console</c>'s.
    /// </summary>
    /// <param name="fileBytes">The file's bytes.</param>
    /// <returns>The certificate, or the refusal.</returns>
    public static SshHostCertificateReading Read(ReadOnlySpan<byte> fileBytes)
    {
        try
        {
            return new SshHostCertificateReading(Parse(Encoding.Latin1.GetString(fileBytes)), null);
        }
        catch (Exception malformed) when (malformed is FormatException or SshDisconnectRequiredException)
        {
            return new SshHostCertificateReading(null, SshHostCertificateRefusal.NotAHostCertificate);
        }
    }

    /// <summary>
    /// Whether this certificate certifies <paramref name="hostKey"/>: the key it names is that
    /// key's public key, byte for byte.
    /// </summary>
    /// <param name="hostKey">The host key.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    public bool Certifies(SshHostKey hostKey)
    {
        ArgumentNullException.ThrowIfNull(hostKey);

        return hostKey.PublicKeyBlob.Span.SequenceEqual(certifiedPublicKeyBlob);
    }

    // One line: the certificate type, the base64 blob, and an optional comment.
    private static SshHostCertificate Parse(string text)
    {
        var line = text.Trim();
        var words = line.Split([' ', '\t'], 3, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length < 2 || line.Contains('\n', StringComparison.Ordinal))
        {
            throw new FormatException("A host certificate file is one line: its type, then its base64 blob.");
        }

        return ParseBlob(words[0], Convert.FromBase64String(words[1]));
    }

    private static SshHostCertificate ParseBlob(string lineType, byte[] blob)
    {
        var reader = new SshWireReader(blob);
        var certificateType = Encoding.Latin1.GetString(reader.ReadString().Span);
        var keyType = KeyTypeOf(certificateType);
        if (certificateType != lineType)
        {
            throw new FormatException("The certificate line's type is not its blob's.");
        }

        reader.ReadString();
        var publicFieldsStart = reader.Position;
        for (var field = 0; field < PublicFieldCountsByKeyType[keyType]; field++)
        {
            reader.ReadString();
        }

        var certifiedPublicKeyBlob = new SshWireWriter();
        certifiedPublicKeyBlob.WriteString(keyType);
        certifiedPublicKeyBlob.WriteBytes(blob.AsSpan(publicFieldsStart..reader.Position));
        ReadHostCertificateFields(reader, blob.Length);

        return new SshHostCertificate(keyType, blob, certifiedPublicKeyBlob.ToArray());
    }

    // The key type a certificate type certifies, when it is one this reads.
    private static string KeyTypeOf(string certificateType)
    {
        var keyType = certificateType.EndsWith(CertificateSuffix, StringComparison.Ordinal) ? certificateType[..^CertificateSuffix.Length] : certificateType;

        return keyType != certificateType && PublicFieldCountsByKeyType.ContainsKey(keyType)
            ? keyType
            : throw new FormatException($"{certificateType} is not a certificate type surl reads.");
    }

    // Serial, type, key id, principals, the validity period, critical options, extensions,
    // reserved, signature key and signature, ending the blob.
    private static void ReadHostCertificateFields(SshWireReader reader, int blobLength)
    {
        reader.ReadUInt64();
        if (reader.ReadUInt32() != HostCertificateType)
        {
            throw new FormatException("The certificate is not a host certificate.");
        }

        reader.ReadString();
        reader.ReadString();
        reader.ReadUInt64();
        reader.ReadUInt64();
        for (var field = 0; field < 5; field++)
        {
            reader.ReadString();
        }

        if (reader.Position != blobLength)
        {
            throw new FormatException("The certificate blob runs on past its signature.");
        }
    }
}
