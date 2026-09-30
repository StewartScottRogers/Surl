using System.Formats.Asn1;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using static Surl.Protocol.Ssh.SshTestExchange;
using static Surl.Protocol.Ssh.SshTestKeyExchangeClient;

namespace Surl.Protocol.Ssh;

/// <summary>
/// Key files the tests build by hand, field by field from their specifications, for the
/// formats and cases the BCL does not write: OpenSSH's <c>openssh-key-v1</c>
/// (<c>PROTOCOL.key</c>), PKCS #8 for DSA, Ed25519 and other algorithms, and PBES2-encrypted
/// PKCS #8 with each optional field chosen.
/// </summary>
internal static class SshKeyFileBuilder
{
    public const string Pbes2Oid = "1.2.840.113549.1.5.13";

    public const string Pbkdf2Oid = "1.2.840.113549.1.5.12";

    public const string Aes256CbcOid = "2.16.840.1.101.3.4.1.42";

    public const string HmacSha256Oid = "1.2.840.113549.2.9";

    public const string Ed25519Oid = "1.3.101.112";

    public static byte[] Pem(string label, byte[] der) =>
        Encoding.ASCII.GetBytes(PemEncoding.WriteString(label, der) + "\n");

    /// <summary>
    /// An <c>openssh-key-v1</c> body: magic, cipher, KDF, KDF options, key count, public key,
    /// then the private section - two check integers, the key type and the key's fields,
    /// comment and padding.
    /// </summary>
    public static byte[] OpenSshBody(
        string keyType,
        byte[] keyFields,
        string cipher = "none",
        string kdf = "none",
        uint keyCount = 1,
        uint secondCheck = 0x01020304)
    {
        var section = Concat(UInt32(0x01020304), UInt32(secondCheck), String(keyType), keyFields, String("comment"), [1, 2, 3]);

        return Concat(
            Ascii("openssh-key-v1\0"),
            String(cipher),
            String(kdf),
            Str([]),
            UInt32(keyCount),
            Str(String(keyType)),
            Str(section));
    }

    public static byte[] OpenSshPem(byte[] body) => Pem("OPENSSH PRIVATE KEY", body);

    /// <summary>The RSA fields of <c>openssh-key-v1</c>: n, e, d, iqmp, p, q.</summary>
    public static byte[] OpenSshRsaFields(RSAParameters key) => Concat(
        Mpint(Unsigned(key.Modulus!)),
        Mpint(Unsigned(key.Exponent!)),
        Mpint(Unsigned(key.D!)),
        Mpint(Unsigned(key.InverseQ!)),
        Mpint(Unsigned(key.P!)),
        Mpint(Unsigned(key.Q!)));

    /// <summary>The ECDSA fields of <c>openssh-key-v1</c>: the curve name, Q, d.</summary>
    public static byte[] OpenSshEcdsaFields(string curveName, byte[] point, BigInteger privateValue) =>
        Concat(String(curveName), Str(point), Mpint(privateValue));

    /// <summary>The Ed25519 fields of <c>openssh-key-v1</c>: the public key, then the private key (the seed and the public key).</summary>
    public static byte[] OpenSshEd25519Fields(byte[] publicKey, byte[] privateKey) => Concat(Str(publicKey), Str(privateKey));

    /// <summary>The PKCS #8 private key octets of an Ed25519 key: <c>CurvePrivateKey</c>, an OCTET STRING of the seed (RFC 8410, section 7).</summary>
    public static byte[] Pkcs8Ed25519PrivateKey(byte[] seed)
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);
        writer.WriteOctetString(seed);

        return writer.Encode();
    }

    /// <summary>
    /// A version 2 <c>OneAsymmetricKey</c> (RFC 5958) of an Ed25519 key, with its public key in
    /// <c>[1]</c> after the private key, as RFC 8410 section 10.3's second example has.
    /// </summary>
    public static byte[] Pkcs8Ed25519WithPublicKey(byte[] seed, byte[] publicKey)
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);
        using (writer.PushSequence())
        {
            writer.WriteInteger(1);
            using (writer.PushSequence())
            {
                writer.WriteObjectIdentifier(Ed25519Oid);
            }

            writer.WriteOctetString(Pkcs8Ed25519PrivateKey(seed));
            writer.WriteBitString(publicKey, tag: new Asn1Tag(TagClass.ContextSpecific, 1));
        }

        return writer.Encode();
    }

    public static byte[] Point(ECParameters key) => Concat([4], key.Q.X!, key.Q.Y!);

    public static BigInteger Unsigned(byte[] bigEndian) => new(bigEndian, isUnsigned: true, isBigEndian: true);

    /// <summary>
    /// A PKCS #8 <c>PrivateKeyInfo</c> naming <paramref name="algorithmOid"/>, with the given
    /// algorithm parameters and private key octets.
    /// </summary>
    public static byte[] Pkcs8(string algorithmOid, Action<AsnWriter>? writeParameters, byte[] privateKey)
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);
        using (writer.PushSequence())
        {
            writer.WriteInteger(0);
            using (writer.PushSequence())
            {
                writer.WriteObjectIdentifier(algorithmOid);
                writeParameters?.Invoke(writer);
            }

            writer.WriteOctetString(privateKey);
        }

        return writer.Encode();
    }

    /// <summary>
    /// A PKCS #8 DSA key whose prime p has <paramref name="bits"/> bits; only p's size is read.
    /// </summary>
    public static byte[] Pkcs8Dsa(int bits) => Pkcs8(
        "1.2.840.10040.4.1",
        writer =>
        {
            using (writer.PushSequence())
            {
                writer.WriteInteger(BigInteger.One << (bits - 1));
                writer.WriteInteger(3);
                writer.WriteInteger(2);
            }
        },
        [2, 1, 5]);

    /// <summary>
    /// A SEC 1 <c>ECPrivateKey</c> naming <paramref name="curveOid"/>, or no curve.
    /// </summary>
    public static byte[] Sec1(string? curveOid)
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);
        using (writer.PushSequence())
        {
            writer.WriteInteger(1);
            writer.WriteOctetString(new byte[32]);
            if (curveOid is not null)
            {
                using (writer.PushSequence(new Asn1Tag(TagClass.ContextSpecific, 0)))
                {
                    writer.WriteObjectIdentifier(curveOid);
                }
            }
        }

        return writer.Encode();
    }

    /// <summary>
    /// A PBES2 <c>EncryptedPrivateKeyInfo</c> of <paramref name="plaintext"/> under
    /// <paramref name="passphrase"/>, PBKDF2 with the PRF named (or none, meaning HMAC-SHA-1),
    /// an optional key length, and AES-CBC with the key length the cipher's OID names.
    /// </summary>
    public static byte[] EncryptedPkcs8(
        byte[] plaintext,
        string passphrase,
        string? prfOid = HmacSha256Oid,
        string cipherOid = Aes256CbcOid,
        bool withKeyLength = false,
        long iterations = 1000,
        string schemeOid = Pbes2Oid,
        string kdfOid = Pbkdf2Oid,
        int ivLength = 16)
    {
        var salt = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        var iv = new byte[16];
        var keyLength = cipherOid switch
        {
            "2.16.840.1.101.3.4.1.2" => 16,
            "2.16.840.1.101.3.4.1.22" => 24,
            _ => 32,
        };
        var prf = prfOid switch
        {
            null or "1.2.840.113549.2.7" => HashAlgorithmName.SHA1,
            "1.2.840.113549.2.10" => HashAlgorithmName.SHA384,
            "1.2.840.113549.2.11" => HashAlgorithmName.SHA512,
            _ => HashAlgorithmName.SHA256,
        };
        using var aes = Aes.Create();
        aes.Key = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(passphrase), salt, (int)Math.Clamp(iterations, 1, 1000), prf, keyLength);
        var encrypted = aes.EncryptCbc(plaintext, iv);

        var writer = new AsnWriter(AsnEncodingRules.DER);
        using (writer.PushSequence())
        {
            using (writer.PushSequence())
            {
                writer.WriteObjectIdentifier(schemeOid);
                using (writer.PushSequence())
                {
                    using (writer.PushSequence())
                    {
                        writer.WriteObjectIdentifier(kdfOid);
                        using (writer.PushSequence())
                        {
                            writer.WriteOctetString(salt);
                            writer.WriteInteger(iterations);
                            if (withKeyLength)
                            {
                                writer.WriteInteger(keyLength);
                            }

                            if (prfOid is not null)
                            {
                                using (writer.PushSequence())
                                {
                                    writer.WriteObjectIdentifier(prfOid);
                                    writer.WriteNull();
                                }
                            }
                        }
                    }

                    using (writer.PushSequence())
                    {
                        writer.WriteObjectIdentifier(cipherOid);
                        writer.WriteOctetString(new byte[ivLength]);
                    }
                }
            }

            writer.WriteOctetString(encrypted);
        }

        return writer.Encode();
    }
}
