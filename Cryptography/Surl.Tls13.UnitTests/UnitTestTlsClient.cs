using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Surl.Cryptography.Curve25519;

namespace Surl.Tls13;

/// <summary>
/// The client side of a TLS 1.3 handshake, written in the test project from RFC 8446 sections
/// 4.4 and 7.1 and sharing no code with the server's key schedule, transcript or messages. It
/// holds one key share's private key, checks the server's flight - the ServerHello, the
/// CertificateVerify signature with the certificate's public key, and the server's Finished -
/// derives every secret on its own, and writes the client's Finished.
/// </summary>
internal sealed class UnitTestTlsClient
{
    private readonly Func<byte[], byte[]> agree;

    private UnitTestTlsClient(ushort group, byte[] publicShare, Func<byte[], byte[]> agree)
    {
        Group = group;
        PublicShare = publicShare;
        this.agree = agree;
    }

    /// <summary>The group of the client's key share.</summary>
    public ushort Group { get; }

    /// <summary>The client's key_exchange value.</summary>
    public byte[] PublicShare { get; }

    public static UnitTestTlsClient WithX25519Share() => WithX25519PrivateKey(RandomNumberGenerator.GetBytes(X25519.KeySize));

    /// <summary>The client of RFC 8448 section 3, with that trace's x25519 private key.</summary>
    public static UnitTestTlsClient WithRfc8448ClientShare() => WithX25519PrivateKey(Rfc8448.ClientEphemeralPrivateKey);

    public static UnitTestTlsClient WithEllipticCurveShare(ushort group) => WithEllipticCurveShare(group, CurveOf(group));

    public static ECCurve CurveOf(ushort group) => group switch
    {
        0x0017 => ECCurve.NamedCurves.nistP256,
        0x0018 => ECCurve.NamedCurves.nistP384,
        _ => ECCurve.NamedCurves.nistP521,
    };

    private static UnitTestTlsClient WithX25519PrivateKey(byte[] privateKey)
    {
        var publicKey = new byte[X25519.KeySize];
        X25519.ComputePublicKey(privateKey, publicKey);
        return new UnitTestTlsClient(0x001D, publicKey, serverShare =>
        {
            var shared = new byte[X25519.KeySize];
            X25519.ScalarMultiply(privateKey, serverShare, shared);
            return shared;
        });
    }

    public static UnitTestTlsClient WithEllipticCurveShare(ushort group, ECCurve curve)
    {
        var key = ECDiffieHellman.Create(curve);
        var point = key.ExportParameters(false).Q;
        return new UnitTestTlsClient(group, [0x04, .. point.X!, .. point.Y!], serverShare =>
        {
            var length = (serverShare.Length - 1) / 2;
            using var peer = ECDiffieHellman.Create(new ECParameters
            {
                Curve = curve,
                Q = new ECPoint { X = serverShare[1..(1 + length)], Y = serverShare[(1 + length)..] },
            });
            return key.DeriveRawSecretAgreement(peer.PublicKey);
        });
    }

    /// <summary>Replaces <paramref name="share"/>'s bytes in a recorded ClientHello with this client's public share, which is as long.</summary>
    public byte[] WithShareReplaced(byte[] clientHello, ReadOnlySpan<byte> share)
    {
        Assert.HasCount(share.Length, PublicShare);
        var copy = clientHello.ToArray();
        var index = copy.AsSpan().IndexOf(share);
        Assert.IsGreaterThanOrEqualTo(0, index);
        PublicShare.CopyTo(copy, index);
        return copy;
    }

    /// <summary>
    /// Checks the server's flight against the transcript so far - the ClientHello, or after a
    /// HelloRetryRequest the message_hash, the HelloRetryRequest and the second ClientHello - and
    /// returns what the client derived.
    /// </summary>
    public UnitTestVerifiedFlight VerifyServerFlight(IReadOnlyList<byte[]> transcriptBeforeServerHello, byte[] initial, byte[] handshake)
    {
        var (suite, group, serverShare) = ReadServerHello(initial);
        Assert.AreEqual(Group, group, "The ServerHello's key share is not for the client's group.");
        var hash = suite == 0x1302 ? HashAlgorithmName.SHA384 : HashAlgorithmName.SHA256;
        var hashLength = suite == 0x1302 ? 48 : 32;
        var transcript = new List<byte>(transcriptBeforeServerHello.SelectMany(message => message));
        transcript.AddRange(initial);
        var sharedSecret = agree(serverShare);

        var early = HKDF.Extract(hash, new byte[hashLength], new byte[hashLength]);
        var emptyHash = CryptographicOperations.HashData(hash, []);
        var handshakeSecret = HKDF.Extract(hash, sharedSecret, ExpandLabel(hash, early, "derived", emptyHash, hashLength));
        var helloHash = CryptographicOperations.HashData(hash, [.. transcript]);
        var clientHandshake = ExpandLabel(hash, handshakeSecret, "c hs traffic", helloHash, hashLength);
        var serverHandshake = ExpandLabel(hash, handshakeSecret, "s hs traffic", helloHash, hashLength);

        var messages = SplitMessages(handshake);
        CollectionAssert.AreEqual(new byte[] { 8, 11, 15, 20 }, messages.Select(message => message[0]).ToArray());
        transcript.AddRange(messages[0]);
        transcript.AddRange(messages[1]);
        VerifyCertificateVerify(messages[1], messages[2], CryptographicOperations.HashData(hash, [.. transcript]));
        transcript.AddRange(messages[2]);
        var expectedServerVerifyData = Hmac(hash, ExpandLabel(hash, serverHandshake, "finished", [], hashLength), CryptographicOperations.HashData(hash, [.. transcript]));
        CollectionAssert.AreEqual(expectedServerVerifyData, messages[3][4..], "The server's Finished does not verify.");
        transcript.AddRange(messages[3]);

        var serverFinishedHash = CryptographicOperations.HashData(hash, [.. transcript]);
        var master = HKDF.Extract(hash, new byte[hashLength], ExpandLabel(hash, handshakeSecret, "derived", emptyHash, hashLength));
        var clientVerifyData = Hmac(hash, ExpandLabel(hash, clientHandshake, "finished", [], hashLength), CryptographicOperations.HashData(hash, [.. transcript]));
        return new UnitTestVerifiedFlight(
            suite,
            messages[0],
            messages[1],
            messages[2],
            new TlsTrafficSecrets(clientHandshake, serverHandshake),
            new TlsTrafficSecrets(
                ExpandLabel(hash, master, "c ap traffic", serverFinishedHash, hashLength),
                ExpandLabel(hash, master, "s ap traffic", serverFinishedHash, hashLength)),
            [20, 0, 0, (byte)hashLength, .. clientVerifyData]);
    }

    /// <summary>The message_hash message that replaces ClientHello1 in the transcript (RFC 8446 section 4.4.1).</summary>
    public static byte[] MessageHash(HashAlgorithmName hash, byte[] clientHello1)
    {
        var digest = CryptographicOperations.HashData(hash, clientHello1);
        return [254, 0, 0, (byte)digest.Length, .. digest];
    }

    private static (ushort Suite, ushort Group, byte[] Share) ReadServerHello(byte[] message)
    {
        Assert.AreEqual(2, message[0], "The Initial bytes are not a ServerHello.");
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
        var scheme = BinaryPrimitives.ReadUInt16BigEndian(certificateVerify.AsSpan(4));
        var signature = certificateVerify[8..];
        byte[] content = [.. Enumerable.Repeat((byte)0x20, 64), .. Encoding.ASCII.GetBytes("TLS 1.3, server CertificateVerify"), 0, .. transcriptHash];
        var verified = scheme switch
        {
            0x0804 => certificate.GetRSAPublicKey()!.VerifyData(content, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pss),
            0x0403 => VerifyEcdsa(certificate, content, signature, HashAlgorithmName.SHA256),
            0x0503 => VerifyEcdsa(certificate, content, signature, HashAlgorithmName.SHA384),
            0x0603 => VerifyEcdsa(certificate, content, signature, HashAlgorithmName.SHA512),
            _ => false,
        };
        Assert.IsTrue(verified, $"The CertificateVerify signature (scheme 0x{scheme:X4}) does not verify with the certificate's public key.");
    }

    private static bool VerifyEcdsa(X509Certificate2 certificate, byte[] content, byte[] signature, HashAlgorithmName hash) =>
        certificate.GetECDsaPublicKey()!.VerifyData(content, signature, hash, DSASignatureFormat.Rfc3279DerSequence);

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

    private static byte[] ExpandLabel(HashAlgorithmName hash, byte[] secret, string label, byte[] context, int length)
    {
        var fullLabel = Encoding.ASCII.GetBytes("tls13 " + label);
        byte[] info = [(byte)(length >> 8), (byte)length, (byte)fullLabel.Length, .. fullLabel, (byte)context.Length, .. context];
        return HKDF.Expand(hash, secret, length, info);
    }

    private static byte[] Hmac(HashAlgorithmName hash, byte[] key, byte[] data) => CryptographicOperations.HmacData(hash, key, data);
}
