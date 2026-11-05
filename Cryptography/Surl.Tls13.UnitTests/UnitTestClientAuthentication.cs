using System.Security.Cryptography;
using System.Text;

namespace Surl.Tls13;

/// <summary>
/// The client's half of certificate authentication (RFC 8446 sections 4.4.2 to 4.4.4), written in
/// the test project from the specification and sharing no code with the library: the client's
/// Certificate, its CertificateVerify signature over the transcript, and the Finished that follows.
/// </summary>
internal static class UnitTestClientAuthentication
{
    /// <summary>A Certificate message with the context bytes given and one entry, without extensions, per certificate.</summary>
    public static byte[] CertificateMessage(byte[] context, params byte[][] chain)
    {
        byte[] list = [.. chain.SelectMany(der => (byte[])[.. Length24(der.Length), .. der, 0, 0])];
        byte[] body = [(byte)context.Length, .. context, .. Length24(list.Length), .. list];
        return [11, .. Length24(body.Length), .. body];
    }

    /// <summary>A CertificateVerify message with the scheme and signature given.</summary>
    public static byte[] CertificateVerify(ushort scheme, byte[] signature)
    {
        byte[] body = [(byte)(scheme >> 8), (byte)scheme, (byte)(signature.Length >> 8), (byte)signature.Length, .. signature];
        return [15, .. Length24(body.Length), .. body];
    }

    /// <summary>The content a client's CertificateVerify signs after <paramref name="certificateMessage"/>.</summary>
    public static byte[] SignedContent(UnitTestVerifiedFlight flight, byte[] certificateMessage) =>
    [
        .. Enumerable.Repeat((byte)0x20, 64), .. Encoding.ASCII.GetBytes("TLS 1.3, client CertificateVerify"), 0,
        .. CryptographicOperations.HashData(flight.Hash, [.. flight.TranscriptThroughServerFinished, .. certificateMessage]),
    ];

    /// <summary>Signs <paramref name="content"/> with <paramref name="key"/> as <paramref name="scheme"/> asks.</summary>
    public static byte[] Sign(AsymmetricAlgorithm key, ushort scheme, byte[] content)
    {
        var hash = scheme switch
        {
            0x0804 or 0x0403 or 0x0401 => HashAlgorithmName.SHA256,
            0x0805 or 0x0503 => HashAlgorithmName.SHA384,
            _ => HashAlgorithmName.SHA512,
        };
        return key is RSA rsa
            ? rsa.SignData(content, hash, RSASignaturePadding.Pss)
            : ((ECDsa)key).SignData(content, hash, DSASignatureFormat.Rfc3279DerSequence);
    }

    /// <summary>The client's Finished over the transcript through the server's Finished and then <paramref name="clientMessages"/>.</summary>
    public static byte[] Finished(UnitTestVerifiedFlight flight, params byte[][] clientMessages)
    {
        var hashLength = flight.HandshakeSecrets.Client.Length;
        var finishedKey = UnitTestTlsClient.ExpandLabel(flight.Hash, flight.HandshakeSecrets.Client, "finished", [], hashLength);
        var transcriptHash = CryptographicOperations.HashData(
            flight.Hash, [.. flight.TranscriptThroughServerFinished, .. clientMessages.SelectMany(message => message)]);
        return [20, 0, 0, (byte)hashLength, .. UnitTestTlsClient.Hmac(flight.Hash, finishedKey, transcriptHash)];
    }

    private static byte[] Length24(int length) => [(byte)(length >> 16), (byte)(length >> 8), (byte)length];
}
