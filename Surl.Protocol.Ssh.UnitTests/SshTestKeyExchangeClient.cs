using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using Surl.Cryptography.Curve25519;
using static Surl.Protocol.Ssh.SshTestExchange;

namespace Surl.Protocol.Ssh;

/// <summary>
/// The client's side of one key exchange, built in the test from the BCL's primitives and
/// written out from RFC 4253 section 8, RFC 5656 section 4, RFC 8731, RFC 4419 and RFC 8268, so no test
/// checks the server's exchange hash, signature or key derivation against the server's own
/// code. None of the client's messages depends on the server's answers - the group a group
/// exchange gets is predicted from the request - so the client sends them all at once through
/// an <see cref="Surl.Protocol.Abstractions.InMemoryConnection"/>, then checks what the server wrote.
/// </summary>
internal sealed class SshTestKeyExchangeClient : IDisposable
{
    /// <summary>What the group exchange asks for: the server answers with the 3072-bit group 15.</summary>
    public const uint GroupExchangeMinBits = 2048;

    public const uint GroupExchangePreferredBits = 3072;

    public const uint GroupExchangeMaxBits = 8192;

    private readonly string keyExchange;
    private readonly string hostKeyAlgorithm;
    private readonly ECDiffieHellman? ellipticKey;
    private readonly byte[]? curvePrivateKey;
    private readonly BigInteger privateExponent;
    private readonly BigInteger? prime;
    private readonly bool padClientValue;

    public SshTestKeyExchangeClient(
        string keyExchange,
        string hostKeyAlgorithm,
        bool strict = false,
        bool firstKexPacketFollows = false,
        bool padClientValue = false,
        string cipher = "aes128-ctr",
        string mac = "hmac-sha2-256",
        string? cipherServerToClient = null,
        string? macServerToClient = null,
        bool extensionInfo = false,
        string compression = "none")
    {
        this.keyExchange = keyExchange;
        this.hostKeyAlgorithm = hostKeyAlgorithm;
        this.padClientValue = padClientValue;
        KexInitPayload = ClientKexInitPayload(
            keyExchange: keyExchange + (extensionInfo ? ",ext-info-c" : string.Empty) + (strict ? ",kex-strict-c-v00@openssh.com" : string.Empty),
            hostKey: hostKeyAlgorithm,
            cipher: cipher,
            mac: mac,
            firstKexPacketFollows: firstKexPacketFollows,
            cipherServerToClient: cipherServerToClient,
            macServerToClient: macServerToClient,
            compression: compression);
        ellipticKey = CurveOf(keyExchange) is { } curve ? ECDiffieHellman.Create(curve) : null;
        curvePrivateKey = keyExchange.StartsWith("curve25519-sha256", StringComparison.Ordinal) ? RandomNumberGenerator.GetBytes(X25519.KeySize) : null;
        prime = PrimeOf(keyExchange);
        privateExponent = new BigInteger(RandomNumberGenerator.GetBytes(64), isUnsigned: true, isBigEndian: true);
    }

    public byte[] KexInitPayload { get; }

    /// <summary>
    /// e as sent: an <c>mpint</c>, or with <c>padClientValue</c> the same with a redundant
    /// leading zero byte, as RFC 4251 section 5 says a sender must not write.
    /// </summary>
    private byte[] ClientValueField() => padClientValue
        ? Str([0, .. Mpint(ClientPublicValue)[4..]])
        : Mpint(ClientPublicValue);

    /// <summary>
    /// The method's hash, as its name says.
    /// </summary>
    public HashAlgorithmName HashAlgorithm => keyExchange switch
    {
        "ecdh-sha2-nistp384" => HashAlgorithmName.SHA384,
        "ecdh-sha2-nistp521" or "diffie-hellman-group16-sha512" or "diffie-hellman-group18-sha512" => HashAlgorithmName.SHA512,
        _ when keyExchange.EndsWith("-sha1", StringComparison.Ordinal) => HashAlgorithmName.SHA1,
        _ => HashAlgorithmName.SHA256,
    };

    private bool IsGroupExchange => keyExchange.StartsWith("diffie-hellman-group-exchange-", StringComparison.Ordinal);

    private BigInteger ClientPublicValue => BigInteger.ModPow(2, privateExponent, prime!.Value);

    /// <summary>
    /// The method's messages from the client: the ECDH or DH init, preceded by the group
    /// exchange request for a group exchange.
    /// </summary>
    public byte[] MethodPackets() => Concat([.. MethodPayloads().Select(payload => Packet(payload))]);

    /// <summary>
    /// The payloads of <see cref="MethodPackets"/>, for a client that protects them itself.
    /// </summary>
    public byte[][] MethodPayloads() => ellipticKey is not null || curvePrivateKey is not null
        ? [Concat([30], Str(ClientPoint()))]
        : IsGroupExchange
            ? [
                Concat([34], UInt32(GroupExchangeMinBits), UInt32(GroupExchangePreferredBits), UInt32(GroupExchangeMaxBits)),
                Concat([32], ClientValueField()),
            ]
            : [Concat([30], ClientValueField())];

    /// <summary>
    /// Everything the client sends, in order: its identification line, its <c>KEXINIT</c>, the
    /// method's messages and <c>NEWKEYS</c>.
    /// </summary>
    public byte[] InboundBytes() => Concat(Ascii(ClientLine), Packet(KexInitPayload), MethodPackets(), Packet(21));

    /// <summary>
    /// Checks what the server wrote: its identification line, its <c>KEXINIT</c>, the method's
    /// answers and <c>NEWKEYS</c>; that <c>K_S</c> is <paramref name="expectedHostKeyBlob"/>;
    /// and that the signature verifies over the H the client computes.
    /// </summary>
    /// <returns>K and H as the client computes them.</returns>
    public (BigInteger SharedSecret, byte[] ExchangeHash) CheckServerAnswer(byte[] writtenBytes, ReadOnlySpan<byte> expectedHostKeyBlob)
    {
        CollectionAssert.AreEqual(Ascii(ServerLine), writtenBytes[..ServerLine.Length]);

        return CheckKeyExchange(ServerPackets(writtenBytes[ServerLine.Length..]), expectedHostKeyBlob);
    }

    /// <summary>
    /// Checks the server's payloads of one key exchange, its <c>KEXINIT</c> first and
    /// <c>NEWKEYS</c> last, as <see cref="CheckServerAnswer"/> does.
    /// </summary>
    /// <returns>K and H as the client computes them.</returns>
    public (BigInteger SharedSecret, byte[] ExchangeHash) CheckKeyExchange(List<byte[]> packets, ReadOnlySpan<byte> expectedHostKeyBlob)
    {
        Assert.AreEqual(20, packets[0][0]);
        CollectionAssert.AreEqual(new byte[] { 21 }, packets[^1]);
        var serverKexInit = packets[0];
        var hashFields = Concat(
            Str(Ascii(ClientLine.TrimEnd())),
            Str(Ascii(ServerLine.TrimEnd())),
            Str(KexInitPayload),
            Str(serverKexInit));

        var (reply, methodFields, sharedSecret) = ReadMethodAnswer(packets);
        var hostKeyBlob = reply.ReadString().ToArray();
        CollectionAssert.AreEqual(expectedHostKeyBlob.ToArray(), hostKeyBlob);
        var (serverValueField, secret) = sharedSecret(reply);
        var signature = reply.ReadString().ToArray();

        var exchangeHash = CryptographicOperations.HashData(
            HashAlgorithm,
            Concat(hashFields, Str(hostKeyBlob), methodFields, serverValueField, Mpint(secret)));
        Assert.IsTrue(SignatureVerifies(hostKeyBlob, signature, exchangeHash), "The server's signature over H does not verify.");

        return (secret, exchangeHash);
    }

    /// <summary>
    /// Derives one key as RFC 4253 section 7.2 writes it, the session identifier being
    /// <paramref name="sessionIdentifier"/>, or this exchange's H for the first exchange.
    /// </summary>
    public byte[] DeriveKey(BigInteger sharedSecret, byte[] exchangeHash, char letter, int length, byte[]? sessionIdentifier = null)
    {
        var prefix = Concat(Mpint(sharedSecret), exchangeHash);
        var key = CryptographicOperations.HashData(HashAlgorithm, Concat(prefix, [(byte)letter], sessionIdentifier ?? exchangeHash));
        while (key.Length < length)
        {
            key = Concat(key, CryptographicOperations.HashData(HashAlgorithm, Concat(prefix, key)));
        }

        return key[..length];
    }

    public void Dispose() => ellipticKey?.Dispose();

    public static byte[] Str(byte[] bytes) => Concat(UInt32((uint)bytes.Length), bytes);

    public static byte[] Mpint(BigInteger value) => Str(value.IsZero ? [] : value.ToByteArray(isUnsigned: false, isBigEndian: true));

    /// <summary>
    /// Splits the server's unencrypted packets into their payloads.
    /// </summary>
    public static List<byte[]> ServerPackets(byte[] bytes)
    {
        var payloads = new List<byte[]>();
        var reader = new SshWireReader(bytes);
        while (true)
        {
            try
            {
                var body = reader.ReadString();
                payloads.Add(body[1..(body.Length - body.Span[0])].ToArray());
            }
            catch (SshDisconnectRequiredException)
            {
                return payloads;
            }
        }
    }

    private static ECCurve? CurveOf(string keyExchange) => keyExchange switch
    {
        "ecdh-sha2-nistp256" => ECCurve.NamedCurves.nistP256,
        "ecdh-sha2-nistp384" => ECCurve.NamedCurves.nistP384,
        "ecdh-sha2-nistp521" => ECCurve.NamedCurves.nistP521,
        _ => null,
    };

    private static BigInteger? PrimeOf(string keyExchange) => keyExchange switch
    {
        "diffie-hellman-group14-sha256" or "diffie-hellman-group14-sha1" => SshModpGroup.Group14.Prime,
        "diffie-hellman-group1-sha1" => SshModpGroup.Oakley2.Prime,
        "diffie-hellman-group16-sha512" => SshModpGroup.Group16.Prime,
        "diffie-hellman-group18-sha512" => SshModpGroup.Group18.Prime,
        "diffie-hellman-group-exchange-sha256" or "diffie-hellman-group-exchange-sha1" => SshModpGroup.Group15.Prime,
        _ => null,
    };

    private static bool SignatureVerifies(byte[] hostKeyBlob, byte[] signatureBlob, byte[] exchangeHash)
    {
        var key = new SshWireReader(hostKeyBlob);
        var keyType = Encoding.ASCII.GetString(key.ReadString().Span);
        var signature = new SshWireReader(signatureBlob);
        var algorithm = Encoding.ASCII.GetString(signature.ReadString().Span);
        var signatureBytes = signature.ReadString().ToArray();

        return keyType switch
        {
            "ssh-rsa" => VerifyRsa(key, algorithm, signatureBytes, exchangeHash),
            "ssh-ed25519" => VerifyEd25519(key, algorithm, signatureBytes, exchangeHash),
            "ssh-dss" => VerifyDsa(key, algorithm, signatureBytes, exchangeHash),
            _ => VerifyEcdsa(key, keyType, algorithm, signatureBytes, exchangeHash),
        };
    }

    // RFC 4253 section 6.6: mpint p, q, g, y; the signature is r and s as 160-bit unsigned
    // integers, 40 bytes, over SHA-1.
    private static bool VerifyDsa(SshWireReader key, string algorithm, byte[] signature, byte[] exchangeHash)
    {
        Assert.AreEqual("ssh-dss", algorithm);
        Assert.HasCount(40, signature);
        var p = key.ReadMpint();
        var length = p.ToByteArray(isUnsigned: true, isBigEndian: true).Length;
        using var dsa = DSA.Create(new DSAParameters
        {
            P = Fixed(p, length),
            Q = Fixed(key.ReadMpint(), 20),
            G = Fixed(key.ReadMpint(), length),
            Y = Fixed(key.ReadMpint(), length),
        });

        return dsa.VerifyData(exchangeHash, signature, HashAlgorithmName.SHA1, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
    }

    // RFC 8709: string the 32-byte public key; the signature is RFC 8032's 64 bytes.
    private static bool VerifyEd25519(SshWireReader key, string algorithm, byte[] signature, byte[] exchangeHash)
    {
        Assert.AreEqual("ssh-ed25519", algorithm);

        return Surl.Cryptography.Ed25519.Ed25519.Verify(key.ReadString().Span, exchangeHash, signature);
    }

    private static bool VerifyRsa(SshWireReader key, string algorithm, byte[] signature, byte[] exchangeHash)
    {
        var exponent = key.ReadMpint().ToByteArray(isUnsigned: true, isBigEndian: true);
        var modulus = key.ReadMpint().ToByteArray(isUnsigned: true, isBigEndian: true);
        using var rsa = RSA.Create(new RSAParameters { Exponent = exponent, Modulus = modulus });
        var hash = algorithm switch
        {
            "rsa-sha2-512" => HashAlgorithmName.SHA512,
            "rsa-sha2-256" => HashAlgorithmName.SHA256,
            "ssh-rsa" => HashAlgorithmName.SHA1,
            _ => throw new AssertFailedException($"An RSA key signed with {algorithm}."),
        };

        return signature.Length == modulus.Length && rsa.VerifyData(exchangeHash, signature, hash, RSASignaturePadding.Pkcs1);
    }

    private static bool VerifyEcdsa(SshWireReader key, string keyType, string algorithm, byte[] signature, byte[] exchangeHash)
    {
        Assert.AreEqual(keyType, algorithm);
        var (curve, hash, fieldLength) = Encoding.ASCII.GetString(key.ReadString().Span) switch
        {
            "nistp256" => (ECCurve.NamedCurves.nistP256, HashAlgorithmName.SHA256, 32),
            "nistp384" => (ECCurve.NamedCurves.nistP384, HashAlgorithmName.SHA384, 48),
            _ => (ECCurve.NamedCurves.nistP521, HashAlgorithmName.SHA512, 66),
        };
        var point = key.ReadString().ToArray();
        using var ecdsa = ECDsa.Create(new ECParameters
        {
            Curve = curve,
            Q = new ECPoint { X = point[1..(1 + fieldLength)], Y = point[(1 + fieldLength)..] },
        });
        var values = new SshWireReader(signature);
        var fixedField = Concat(Fixed(values.ReadMpint(), fieldLength), Fixed(values.ReadMpint(), fieldLength));

        return ecdsa.VerifyData(exchangeHash, fixedField, hash, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
    }

    private static byte[] Fixed(BigInteger value, int length)
    {
        var bytes = value.ToByteArray(isUnsigned: true, isBigEndian: true);

        return Concat(new byte[length - bytes.Length], bytes);
    }

    private byte[] ClientPoint()
    {
        if (curvePrivateKey is not null)
        {
            var publicKey = new byte[X25519.KeySize];
            X25519.ComputePublicKey(curvePrivateKey, publicKey);

            return publicKey;
        }

        var q = ellipticKey!.ExportParameters(includePrivateParameters: false).Q;

        return Concat([4], q.X!, q.Y!);
    }

    // The reply packet, positioned at K_S; the fields H holds between K_S and the server's
    // value; and a reader of the server's value that returns its H field and K.
    private (SshWireReader Reply, byte[] MethodFields, Func<SshWireReader, (byte[], BigInteger)> SharedSecret) ReadMethodAnswer(List<byte[]> packets)
    {
        if (curvePrivateKey is not null)
        {
            Assert.HasCount(3, packets);
            return (Reply(packets[1], 31), Str(ClientPoint()), reply =>
            {
                var serverPublicKey = reply.ReadString().ToArray();
                Assert.HasCount(X25519.KeySize, serverPublicKey);
                var secret = new byte[X25519.KeySize];
                X25519.ScalarMultiply(curvePrivateKey, serverPublicKey, secret);

                // RFC 8731 section 3.1: the 32 bytes read as an unsigned big-endian integer.
                return (Str(serverPublicKey), new BigInteger(secret, isUnsigned: true, isBigEndian: true));
            }
            );
        }

        if (ellipticKey is not null)
        {
            Assert.HasCount(3, packets);
            return (Reply(packets[1], 31), Str(ClientPoint()), reply =>
            {
                var serverPoint = reply.ReadString().ToArray();
                var fieldLength = (serverPoint.Length - 1) / 2;
                using var serverKey = ECDiffieHellman.Create(new ECParameters
                {
                    Curve = ellipticKey.ExportParameters(false).Curve,
                    Q = new ECPoint { X = serverPoint[1..(1 + fieldLength)], Y = serverPoint[(1 + fieldLength)..] },
                });
                var secret = ellipticKey.DeriveRawSecretAgreement(serverKey.PublicKey);

                return (Str(serverPoint), new BigInteger(secret, isUnsigned: true, isBigEndian: true));
            }
            );
        }

        var methodFields = ClientValueField();
        var replyPacket = packets[1];
        if (IsGroupExchange)
        {
            Assert.HasCount(4, packets);
            CollectionAssert.AreEqual(Concat([31], Mpint(prime!.Value), Mpint(2)), packets[1]);
            methodFields = Concat(
                UInt32(GroupExchangeMinBits),
                UInt32(GroupExchangePreferredBits),
                UInt32(GroupExchangeMaxBits),
                Mpint(prime.Value),
                Mpint(2),
                methodFields);
            replyPacket = packets[2];
        }

        return (Reply(replyPacket, IsGroupExchange ? (byte)33 : (byte)31), methodFields, reply =>
        {
            var serverPublicValue = reply.ReadMpint();

            return (Mpint(serverPublicValue), BigInteger.ModPow(serverPublicValue, privateExponent, prime!.Value));
        }
        );
    }

    private static SshWireReader Reply(byte[] payload, byte messageNumber)
    {
        Assert.AreEqual(messageNumber, payload[0]);
        var reader = new SshWireReader(payload);
        reader.ReadByte();

        return reader;
    }
}
