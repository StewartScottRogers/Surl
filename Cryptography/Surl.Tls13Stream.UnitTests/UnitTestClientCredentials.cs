using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Surl.Tls13Stream;

/// <summary>
/// What a scripted client answers a CertificateRequest with (RFC 8446 sections 4.4.2 and 4.4.3),
/// written from the specification: a Certificate message, and a CertificateVerify signed with
/// ecdsa_secp256r1_sha256 over the transcript, which can be corrupted or left out.
/// </summary>
internal sealed class UnitTestClientCredentials
{
    private readonly ECDsa key;

    private UnitTestClientCredentials(ECDsa key, byte[][] chain, bool corruptSignature)
    {
        this.key = key;
        Chain = chain;
        CorruptSignature = corruptSignature;
    }

    /// <summary>The DER certificates sent, end-entity certificate first; empty sends an empty Certificate and no CertificateVerify.</summary>
    public byte[][] Chain { get; }

    /// <summary>Whether the CertificateVerify signature is damaged.</summary>
    public bool CorruptSignature { get; }

    /// <summary>A fresh self-signed ECDSA P-256 client certificate, signed for correctly unless <paramref name="corruptSignature"/>.</summary>
    public static UnitTestClientCredentials SelfSigned(bool corruptSignature = false)
    {
        var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest("CN=client", key, HashAlgorithmName.SHA256);
        var now = DateTimeOffset.UtcNow;
        using var certificate = request.CreateSelfSigned(now.AddDays(-1), now.AddDays(1));
        return new UnitTestClientCredentials(key, [certificate.Export(X509ContentType.Cert)], corruptSignature);
    }

    /// <summary>Credentials that answer with an empty Certificate (RFC 8446 section 4.4.2.4).</summary>
    public static UnitTestClientCredentials None() => new(ECDsa.Create(ECCurve.NamedCurves.nistP256), [], false);

    /// <summary>The Certificate and, when there is a certificate, CertificateVerify messages to send after the server's Finished.</summary>
    /// <param name="transcriptThroughServerFinished">The handshake messages from the ClientHello through the server's Finished.</param>
    public byte[][] Messages(byte[] transcriptThroughServerFinished)
    {
        byte[] list = [.. Chain.SelectMany(der => (byte[])[.. Length24(der.Length), .. der, 0, 0])];
        byte[] body = [0, .. Length24(list.Length), .. list];
        byte[] certificate = [11, .. Length24(body.Length), .. body];
        if (Chain.Length == 0)
        {
            return [certificate];
        }

        byte[] content =
        [
            .. Enumerable.Repeat((byte)0x20, 64), .. Encoding.ASCII.GetBytes("TLS 1.3, client CertificateVerify"), 0,
            .. SHA256.HashData([.. transcriptThroughServerFinished, .. certificate]),
        ];
        var signature = key.SignData(content, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);
        signature[^1] ^= CorruptSignature ? (byte)1 : (byte)0;
        byte[] verifyBody = [0x04, 0x03, (byte)(signature.Length >> 8), (byte)signature.Length, .. signature];
        return [certificate, [15, .. Length24(verifyBody.Length), .. verifyBody]];
    }

    private static byte[] Length24(int length) => [(byte)(length >> 16), (byte)(length >> 8), (byte)length];
}
