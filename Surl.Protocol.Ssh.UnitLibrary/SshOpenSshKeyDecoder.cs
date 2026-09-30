using System.Formats.Asn1;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace Surl.Protocol.Ssh;

/// <summary>
/// Decodes OpenSSH's own key format, <c>openssh-key-v1</c> (<c>OPENSSH PRIVATE KEY</c>,
/// OpenSSH's <c>PROTOCOL.key</c>): the magic, the cipher and KDF names and options, one
/// public key, and the private section, whose two check integers must match and which holds
/// the key's type and fields.
/// </summary>
/// <remarks>
/// Only an unencrypted key (cipher and KDF <c>none</c>) is read; an encrypted one is refused
/// as not available until BL-223 reads it (ADR-0051, decision 4).
/// </remarks>
internal static class SshOpenSshKeyDecoder
{
    private const string EcdsaKeyTypePrefix = "ecdsa-sha2-";

    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("openssh-key-v1\0");

    /// <summary>
    /// Decodes the body of an <c>OPENSSH PRIVATE KEY</c> block.
    /// </summary>
    /// <param name="body">The block's decoded body.</param>
    /// <param name="allowWeakAlgorithms">Whether <c>--allow-weak-ssh-algorithms</c> was given.</param>
    /// <returns>The host key.</returns>
    /// <exception cref="SshHostKeyRefusedException">The key is refused.</exception>
    /// <exception cref="SshDisconnectRequiredException">A field runs past the body's end.</exception>
    /// <exception cref="CryptographicException">The key's values do not form a key.</exception>
    public static SshHostKey Decode(byte[] body, bool allowWeakAlgorithms)
    {
        if (!body.AsSpan().StartsWith(Magic))
        {
            throw NotAPrivateKey();
        }

        var reader = new SshWireReader(body.AsMemory(Magic.Length));
        var cipher = ReadName(reader);
        var kdf = ReadName(reader);
        reader.ReadString();
        if (cipher != "none")
        {
            throw new SshHostKeyRefusedException(SshHostKeyRefusal.EncryptedOpenSshKey);
        }

        if (kdf != "none" || reader.ReadUInt32() != 1)
        {
            throw NotAPrivateKey();
        }

        reader.ReadString();

        return ReadPrivateSection(new SshWireReader(reader.ReadString()), allowWeakAlgorithms);
    }

    private static SshHostKey ReadPrivateSection(SshWireReader section, bool allowWeakAlgorithms)
    {
        if (section.ReadUInt32() != section.ReadUInt32())
        {
            throw NotAPrivateKey();
        }

        var keyType = ReadName(section);

        return keyType switch
        {
            SshRsaHostKey.RsaKeyType => ReadRsa(section, allowWeakAlgorithms),
            "ssh-dss" => throw SshHostKeyFile.DsaRefusal(section.ReadMpint().GetBitLength(), allowWeakAlgorithms),
            _ when keyType.StartsWith(EcdsaKeyTypePrefix, StringComparison.Ordinal) => ReadEcdsa(section, keyType),
            _ => throw new SshHostKeyRefusedException(SshHostKeyRefusal.UnsupportedKeyType(keyType)),
        };
    }

    // n, e, d, iqmp, p, q; the CRT exponents d mod (p - 1) and d mod (q - 1) are computed.
    private static SshHostKey ReadRsa(SshWireReader section, bool allowWeakAlgorithms)
    {
        var modulus = section.ReadMpint();
        var exponent = section.ReadMpint();
        var privateExponent = section.ReadMpint();
        var coefficient = section.ReadMpint();
        var prime1 = section.ReadMpint();
        var prime2 = section.ReadMpint();
        if (prime1 <= BigInteger.One || prime2 <= BigInteger.One)
        {
            throw new CryptographicException("An RSA prime must be greater than one.");
        }

        var writer = new AsnWriter(AsnEncodingRules.DER);
        using (writer.PushSequence())
        {
            writer.WriteInteger(0);
            writer.WriteInteger(modulus);
            writer.WriteInteger(exponent);
            writer.WriteInteger(privateExponent);
            writer.WriteInteger(prime1);
            writer.WriteInteger(prime2);
            writer.WriteInteger(privateExponent % (prime1 - 1));
            writer.WriteInteger(privateExponent % (prime2 - 1));
            writer.WriteInteger(coefficient);
        }

        using var rsa = RSA.Create();
        rsa.ImportRSAPrivateKey(writer.Encode(), out _);

        return SshHostKeyFile.FromRsa(rsa, allowWeakAlgorithms);
    }

    // The curve's SSH name, Q, then d.
    private static SshHostKey ReadEcdsa(SshWireReader section, string keyType)
    {
        var curveName = ReadName(section);
        var curve = SshNistCurve.All.FirstOrDefault(candidate => SshEcdsaHostKey.KeyTypeOn(candidate) == keyType && candidate.Identifier == curveName)
            ?? throw new SshHostKeyRefusedException(SshHostKeyRefusal.UnsupportedKeyType(keyType));
        var point = section.ReadString().ToArray();
        var d = PrivateValueBytes(section.ReadMpint(), curve);

        // Q is computed from d and must be the uncompressed Q the file holds, so a damaged file
        // is refused alike on every platform rather than serving a point its signatures do not match.
        using var ecdsa = ECDsa.Create(new ECParameters { Curve = curve.Curve, D = d });
        var parameters = ecdsa.ExportParameters(includePrivateParameters: true);
        byte[] computedPoint = [0x04, .. parameters.Q.X!, .. parameters.Q.Y!];
        if (!point.AsSpan().SequenceEqual(computedPoint))
        {
            throw new CryptographicException("The ECDSA key's public point is not its private value's.");
        }

        return new SshEcdsaHostKey(curve, parameters);
    }

    // d, positive and no longer than the curve's field, as the field's fixed-length bytes.
    private static byte[] PrivateValueBytes(BigInteger privateKey, SshNistCurve curve)
    {
        if (privateKey.Sign <= 0 || privateKey.GetByteCount(isUnsigned: true) > curve.FieldLength)
        {
            throw new CryptographicException("The ECDSA key's private value does not fit its curve.");
        }

        var d = new byte[curve.FieldLength];
        privateKey.TryWriteBytes(d.AsSpan(curve.FieldLength - privateKey.GetByteCount(isUnsigned: true)), out _, isUnsigned: true, isBigEndian: true);

        return d;
    }

    private static string ReadName(SshWireReader reader) => Encoding.Latin1.GetString(reader.ReadString().Span);

    private static SshHostKeyRefusedException NotAPrivateKey() => new(SshHostKeyRefusal.NotAPrivateKey);
}
