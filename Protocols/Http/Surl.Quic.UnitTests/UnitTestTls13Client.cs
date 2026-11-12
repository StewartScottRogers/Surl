using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Surl.Cryptography.Curve25519;
using Surl.Cryptography.MlKem;

namespace Surl.Quic;

/// <summary>
/// The TLS 1.3 client half of <see cref="UnitTestQuicClient"/>, written in the test project from
/// RFC 8446 sections 4.4 and 7.1 (as Surl.Tls13.UnitTests' client is) and sharing no code with
/// Surl.Tls13's key schedule, transcript or messages. It holds an x25519 private key and an
/// ML-KEM-768 decapsulation key (for an X25519MLKEM768 share, draft-ietf-tls-ecdhe-mlkem), derives the
/// handshake secrets from the ServerHello, checks the server's flight - the CertificateVerify
/// signature with the certificate's public key, and the server's Finished - and writes the
/// client's Finished.
/// </summary>
internal sealed class UnitTestTls13Client
{
    private readonly byte[] privateKey = RandomNumberGenerator.GetBytes(X25519.KeySize);
    private readonly byte[] decapsulationKey = new byte[MlKem768.DecapsulationKeySize];
    private readonly List<byte> transcript = [];
    private HashAlgorithmName hash;
    private int hashLength;
    private byte[] handshakeSecret = [];

    public UnitTestTls13Client()
    {
        PublicKey = new byte[X25519.KeySize];
        X25519.ComputePublicKey(privateKey, PublicKey);
        var encapsulationKey = new byte[MlKem768.EncapsulationKeySize];
        MlKem768.GenerateKey(RandomNumberGenerator.GetBytes(32), RandomNumberGenerator.GetBytes(32), encapsulationKey, decapsulationKey);
        X25519MlKem768Share = [.. encapsulationKey, .. PublicKey];
    }

    /// <summary>The client's x25519 key_exchange value.</summary>
    public byte[] PublicKey { get; }

    /// <summary>The client's X25519MLKEM768 key_exchange value: the ML-KEM-768 encapsulation key, then <see cref="PublicKey"/>.</summary>
    public byte[] X25519MlKem768Share { get; }

    /// <summary>The group of the server's key share in the last ServerHello: 0x11EC (X25519MLKEM768) or 0x001D (x25519).</summary>
    public ushort Group { get; private set; }

    public ushort CipherSuite { get; private set; }

    public byte[] ClientHandshakeSecret { get; private set; } = [];

    public byte[] ServerHandshakeSecret { get; private set; } = [];

    public byte[] ClientApplicationSecret { get; private set; } = [];

    public byte[] ServerApplicationSecret { get; private set; } = [];

    public byte[] EncryptedExtensions { get; private set; } = [];

    /// <summary>Takes the ClientHello as sent and the ServerHello, and derives the handshake traffic secrets.</summary>
    public void ReceiveServerHello(byte[] clientHello, byte[] serverHello)
    {
        Assert.AreEqual(2, serverHello[0], "The Initial CRYPTO data is not a ServerHello.");
        var (suite, group, serverShare) = ReadServerHello(serverHello);
        Group = group;
        CipherSuite = suite;
        hash = suite == 0x1302 ? HashAlgorithmName.SHA384 : HashAlgorithmName.SHA256;
        hashLength = suite == 0x1302 ? 48 : 32;
        transcript.AddRange(clientHello);
        transcript.AddRange(serverHello);
        var shared = SharedSecret(group, serverShare);
        var early = HKDF.Extract(hash, new byte[hashLength], new byte[hashLength]);
        handshakeSecret = HKDF.Extract(hash, shared, ExpandLabel(early, "derived", CryptographicOperations.HashData(hash, []), hashLength));
        var helloHash = TranscriptHash();
        ClientHandshakeSecret = ExpandLabel(handshakeSecret, "c hs traffic", helloHash, hashLength);
        ServerHandshakeSecret = ExpandLabel(handshakeSecret, "s hs traffic", helloHash, hashLength);
    }

    /// <summary>Checks the server's Handshake-level flight and returns the client's Finished; the application secrets follow.</summary>
    public byte[] ReceiveServerFlight(byte[] handshake)
    {
        var flight = SplitMessages(handshake);
        ReceivedCertificateRequest = flight.Count > 1 && flight[1][0] == 13;
        var messages = ReceivedCertificateRequest ? flight.Where((_, index) => index != 1).ToList() : flight;
        CollectionAssert.AreEqual(new byte[] { 8, 11, 15, 20 }, messages.Select(message => message[0]).ToArray());
        EncryptedExtensions = messages[0];
        transcript.AddRange(messages[0]);
        if (ReceivedCertificateRequest)
        {
            transcript.AddRange(flight[1]);
        }

        transcript.AddRange(messages[1]);
        VerifyCertificateVerify(messages[1], messages[2], TranscriptHash());
        transcript.AddRange(messages[2]);
        var expected = CryptographicOperations.HmacData(hash, ExpandLabel(ServerHandshakeSecret, "finished", [], hashLength), TranscriptHash());
        CollectionAssert.AreEqual(expected, messages[3][4..], "The server's Finished does not verify.");
        transcript.AddRange(messages[3]);
        var serverFinishedHash = TranscriptHash();
        var master = HKDF.Extract(hash, new byte[hashLength], ExpandLabel(handshakeSecret, "derived", CryptographicOperations.HashData(hash, []), hashLength));
        ClientApplicationSecret = ExpandLabel(master, "c ap traffic", serverFinishedHash, hashLength);
        ServerApplicationSecret = ExpandLabel(master, "s ap traffic", serverFinishedHash, hashLength);
        byte[] clientAuthentication = ReceivedCertificateRequest ? ClientAuthenticationMessages() : [];
        var verifyData = CryptographicOperations.HmacData(hash, ExpandLabel(ClientHandshakeSecret, "finished", [], hashLength), TranscriptHash());
        return [.. clientAuthentication, 20, 0, 0, (byte)hashLength, .. verifyData];
    }

    /// <summary>
    /// The certificate, with its RSA private key, the client answers a CertificateRequest with
    /// (RFC 8446 section 4.4.2); null answers with an empty Certificate.
    /// </summary>
    public X509Certificate2? ClientCertificate { get; set; }

    /// <summary>Whether the server's flight carried a CertificateRequest.</summary>
    public bool ReceivedCertificateRequest { get; private set; }

    /// <summary>
    /// The client's Certificate - empty, or <see cref="ClientCertificate"/> alone - and, with a
    /// certificate, its CertificateVerify by rsa_pss_rsae_sha256 (RFC 8446 sections 4.4.2 and 4.4.3),
    /// each appended to the transcript.
    /// </summary>
    private byte[] ClientAuthenticationMessages()
    {
        byte[] entries = ClientCertificate is { } certificate
            ? [.. Length24(certificate.RawData.Length), .. certificate.RawData, 0, 0]
            : [];
        byte[] certificateMessage = WithHeader(11, [0, .. Length24(entries.Length), .. entries]);
        transcript.AddRange(certificateMessage);
        if (ClientCertificate is null)
        {
            return certificateMessage;
        }

        byte[] content = [.. Enumerable.Repeat((byte)0x20, 64), .. Encoding.ASCII.GetBytes("TLS 1.3, client CertificateVerify"), 0, .. TranscriptHash()];
        var signature = ClientCertificate.GetRSAPrivateKey()!.SignData(content, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        byte[] verifyMessage = WithHeader(15, [0x08, 0x04, (byte)(signature.Length >> 8), (byte)signature.Length, .. signature]);
        transcript.AddRange(verifyMessage);
        return [.. certificateMessage, .. verifyMessage];
    }

    private static byte[] Length24(int length) => [(byte)(length >> 16), (byte)(length >> 8), (byte)length];

    private static byte[] WithHeader(byte type, byte[] body) => [type, .. Length24(body.Length), .. body];

    /// <summary>The value of the quic_transport_parameters extension (0x39) in EncryptedExtensions.</summary>
    public byte[] ServerTransportParameters() => EncryptedExtension(0x0039);

    /// <summary>The ALPN protocol the server selected in EncryptedExtensions (RFC 7301 section 3.1): a list of one name.</summary>
    public string ApplicationProtocol => System.Text.Encoding.ASCII.GetString(EncryptedExtension(0x0010).AsSpan(3));

    private byte[] EncryptedExtension(ushort wanted)
    {
        var extensions = EncryptedExtensions.AsSpan(6);
        while (!extensions.IsEmpty)
        {
            var type = BinaryPrimitives.ReadUInt16BigEndian(extensions);
            var length = BinaryPrimitives.ReadUInt16BigEndian(extensions[2..]);
            if (type == wanted)
            {
                return extensions.Slice(4, length).ToArray();
            }

            extensions = extensions[(4 + length)..];
        }

        Assert.Fail($"EncryptedExtensions carries no extension 0x{wanted:x4}.");
        return [];
    }

    /// <summary>
    /// x25519's secret, or for X25519MLKEM768 the server's share split into the ML-KEM-768
    /// ciphertext and its x25519 key, and the secret ML-KEM's then x25519's.
    /// </summary>
    private byte[] SharedSecret(ushort group, byte[] serverShare)
    {
        if (group == 0x001D)
        {
            return X25519Secret(serverShare);
        }

        Assert.AreEqual((ushort)0x11EC, group, "The ServerHello's key share is neither X25519MLKEM768 nor x25519.");
        Assert.HasCount(MlKem768.CiphertextSize + X25519.KeySize, serverShare);
        var mlKemSecret = new byte[MlKem768.SharedSecretSize];
        Assert.IsTrue(MlKem768.TryDecapsulate(decapsulationKey, serverShare.AsSpan(0, MlKem768.CiphertextSize), mlKemSecret));
        return [.. mlKemSecret, .. X25519Secret(serverShare[MlKem768.CiphertextSize..])];
    }

    private byte[] X25519Secret(byte[] serverPublicKey)
    {
        var shared = new byte[X25519.KeySize];
        X25519.ScalarMultiply(privateKey, serverPublicKey, shared);
        return shared;
    }

    private byte[] TranscriptHash() => CryptographicOperations.HashData(hash, [.. transcript]);

    private static (ushort Suite, ushort Group, byte[] Share) ReadServerHello(byte[] message)
    {
        var body = message.AsSpan(4);
        var sessionIdLength = body[34];
        var afterSessionId = body[(35 + sessionIdLength)..];
        var suite = BinaryPrimitives.ReadUInt16BigEndian(afterSessionId);
        var extensions = afterSessionId[5..];
        while (!extensions.IsEmpty)
        {
            var type = BinaryPrimitives.ReadUInt16BigEndian(extensions);
            var length = BinaryPrimitives.ReadUInt16BigEndian(extensions[2..]);
            if (type == 0x0033)
            {
                var data = extensions.Slice(4, length);
                return (suite, BinaryPrimitives.ReadUInt16BigEndian(data), data[4..].ToArray());
            }

            extensions = extensions[(4 + length)..];
        }

        Assert.Fail("The ServerHello has no key_share.");
        return default;
    }

    private static void VerifyCertificateVerify(byte[] certificateMessage, byte[] certificateVerify, byte[] transcriptHash)
    {
        var contextLength = certificateMessage[4];
        var entryStart = 4 + 1 + contextLength + 3;
        var derLength = (certificateMessage[entryStart] << 16) | (certificateMessage[entryStart + 1] << 8) | certificateMessage[entryStart + 2];
        using var certificate = X509CertificateLoader.LoadCertificate(certificateMessage.AsSpan(entryStart + 3, derLength));
        Assert.AreEqual((ushort)0x0804, BinaryPrimitives.ReadUInt16BigEndian(certificateVerify.AsSpan(4)));
        byte[] content = [.. Enumerable.Repeat((byte)0x20, 64), .. Encoding.ASCII.GetBytes("TLS 1.3, server CertificateVerify"), 0, .. transcriptHash];
        Assert.IsTrue(
            certificate.GetRSAPublicKey()!.VerifyData(content, certificateVerify[8..], HashAlgorithmName.SHA256, RSASignaturePadding.Pss),
            "The CertificateVerify signature does not verify with the certificate's public key.");
    }

    private static List<byte[]> SplitMessages(byte[] bytes)
    {
        var messages = new List<byte[]>();
        var offset = 0;
        while (offset < bytes.Length)
        {
            var length = (bytes[offset + 1] << 16) | (bytes[offset + 2] << 8) | bytes[offset + 3];
            messages.Add(bytes[offset..(offset + 4 + length)]);
            offset += 4 + length;
        }

        return messages;
    }

    private byte[] ExpandLabel(byte[] secret, string label, byte[] context, int length)
    {
        var fullLabel = Encoding.ASCII.GetBytes("tls13 " + label);
        byte[] info = [(byte)(length >> 8), (byte)length, (byte)fullLabel.Length, .. fullLabel, (byte)context.Length, .. context];
        return HKDF.Expand(hash, secret, length, info);
    }
}
