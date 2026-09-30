using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using static Surl.Protocol.Ssh.SshKeyFileBuilder;
using static Surl.Protocol.Ssh.SshTestExchange;
using static Surl.Protocol.Ssh.SshTestKeyExchangeClient;

namespace Surl.Protocol.Ssh;

/// <summary>
/// <see cref="SshHostKeyFile.Read"/>: the formats ADR-0051 decision 4 names, and each of its
/// host-key file refusals.
/// </summary>
[TestClass]
public sealed class SshHostKeyFileTests
{
    private const string Passphrase = "correct horse";

    public static IEnumerable<object[]> ReadableKeys()
    {
        yield return ["PKCS #1 RSA", Utf8(SshTestKeys.Rsa2048.ExportRSAPrivateKeyPem()), SshTestKeys.Rsa2048];
        yield return ["PKCS #8 RSA", Utf8(SshTestKeys.Rsa2048.ExportPkcs8PrivateKeyPem()), SshTestKeys.Rsa2048];
        yield return ["OpenSSH RSA", OpenSshPem(OpenSshBody("ssh-rsa", OpenSshRsaFields(SshTestKeys.Rsa2048.ExportParameters(true)))), SshTestKeys.Rsa2048];
        foreach (var (name, key) in new[] { ("nistp256", SshTestKeys.EcdsaP256), ("nistp384", SshTestKeys.EcdsaP384), ("nistp521", SshTestKeys.EcdsaP521) })
        {
            var parameters = key.ExportParameters(true);
            yield return [$"PKCS #8 {name}", Utf8(key.ExportPkcs8PrivateKeyPem()), key];
            yield return [$"SEC 1 {name}", Utf8(key.ExportECPrivateKeyPem()), key];
            yield return [
                $"OpenSSH {name}",
                OpenSshPem(OpenSshBody("ecdsa-sha2-" + name, OpenSshEcdsaFields(name, Point(parameters), Unsigned(parameters.D!)))),
                key];
        }

        yield return [
            "PKCS #8 RSA encrypted by the BCL, AES-256-CBC, HMAC-SHA-256",
            Utf8(SshTestKeys.Rsa2048.ExportEncryptedPkcs8PrivateKeyPem(Passphrase, new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 1000))),
            SshTestKeys.Rsa2048];
        yield return [
            "PKCS #8 ECDSA encrypted by the BCL, AES-128-CBC, HMAC-SHA-1",
            Utf8(SshTestKeys.EcdsaP256.ExportEncryptedPkcs8PrivateKeyPem(Passphrase, new PbeParameters(PbeEncryptionAlgorithm.Aes128Cbc, HashAlgorithmName.SHA1, 1000))),
            SshTestKeys.EcdsaP256];
        foreach (var (prf, cipher, keyLength) in new (string?, string, bool)[]
        {
            (null, "2.16.840.1.101.3.4.1.2", false),
            ("1.2.840.113549.2.7", "2.16.840.1.101.3.4.1.22", true),
            ("1.2.840.113549.2.10", Aes256CbcOid, false),
            ("1.2.840.113549.2.11", Aes256CbcOid, true),
        })
        {
            yield return [
                $"PKCS #8 encrypted with PRF {prf ?? "(default)"}, cipher {cipher}, key length {(keyLength ? "given" : "left out")}",
                Pem("ENCRYPTED PRIVATE KEY", EncryptedPkcs8(SshTestKeys.Rsa2048.ExportPkcs8PrivateKey(), Passphrase, prf, cipher, keyLength)),
                SshTestKeys.Rsa2048];
        }
    }

    [TestMethod]
    [DynamicData(nameof(ReadableKeys))]
    public void Read_EachFormat_GivesTheKey(string caseName, byte[] file, AsymmetricAlgorithm expected)
    {
        var reading = SshHostKeyFile.Read(file, Passphrase, allowWeakAlgorithms: false);

        Assert.IsNull(reading.Refusal, caseName);
        var expectedKey = expected is RSA rsa ? SshHostKey.FromRsa(rsa) : SshHostKey.FromEcdsa((ECDsa)expected);
        Assert.AreEqual(expectedKey.KeyType, reading.Key!.KeyType, caseName);
        CollectionAssert.AreEqual(expectedKey.PublicKeyBlob.ToArray(), reading.Key.PublicKeyBlob.ToArray(), caseName);
    }

    public static IEnumerable<object[]> ReadableEd25519Keys()
    {
        var seed = SshTestKeys.Ed25519Seed;
        var publicKey = SshTestKeys.Ed25519PublicKey;

        yield return ["PKCS #8 Ed25519", Pem("PRIVATE KEY", Pkcs8(Ed25519Oid, null, Pkcs8Ed25519PrivateKey(seed)))];
        yield return ["PKCS #8 version 2 Ed25519 with its public key", Pem("PRIVATE KEY", Pkcs8Ed25519WithPublicKey(seed, publicKey))];
        yield return ["PKCS #8 Ed25519 encrypted", Pem("ENCRYPTED PRIVATE KEY", EncryptedPkcs8(Pkcs8(Ed25519Oid, null, Pkcs8Ed25519PrivateKey(seed)), Passphrase))];
        yield return ["OpenSSH Ed25519", OpenSshPem(OpenSshBody("ssh-ed25519", OpenSshEd25519Fields(publicKey, [.. seed, .. publicKey])))];
    }

    [TestMethod]
    [DynamicData(nameof(ReadableEd25519Keys))]
    public void Read_EachEd25519Format_GivesTheSeedsKey(string caseName, byte[] file)
    {
        var reading = SshHostKeyFile.Read(file, Passphrase, allowWeakAlgorithms: false);

        Assert.IsNull(reading.Refusal, caseName);
        Assert.AreEqual("ssh-ed25519", reading.Key!.KeyType, caseName);
        CollectionAssert.AreEqual(Concat(String("ssh-ed25519"), Str(SshTestKeys.Ed25519PublicKey)), reading.Key.PublicKeyBlob.ToArray(), caseName);
    }

    [TestMethod]
    public void Read_KeyRead_SignsWhatItsPublicKeyVerifies()
    {
        var parameters = SshTestKeys.EcdsaP384.ExportParameters(true);
        var file = OpenSshPem(OpenSshBody("ecdsa-sha2-nistp384", OpenSshEcdsaFields("nistp384", Point(parameters), Unsigned(parameters.D!))));
        var key = SshHostKeyFile.Read(file, null, allowWeakAlgorithms: false).Key!;

        var signature = new SshWireReader(key.Sign("ecdsa-sha2-nistp384", [1, 2, 3]));

        Assert.AreEqual("ecdsa-sha2-nistp384", Encoding.ASCII.GetString(signature.ReadString().Span));
        var values = new SshWireReader(signature.ReadString());
        byte[] fixedField = [.. Fixed(values.ReadMpint(), 48), .. Fixed(values.ReadMpint(), 48)];
        Assert.IsTrue(SshTestKeys.EcdsaP384.VerifyData(new byte[] { 1, 2, 3 }, fixedField, HashAlgorithmName.SHA384, DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
    }

    [TestMethod]
    public void Read_ShortRsaKey_IsRefusedWithoutWeakAlgorithmsAndReadWithThem()
    {
        var file = Utf8(SshTestKeys.Rsa1024.ExportRSAPrivateKeyPem());

        var refused = SshHostKeyFile.Read(file, null, allowWeakAlgorithms: false);
        var allowed = SshHostKeyFile.Read(file, null, allowWeakAlgorithms: true);

        AssertRefused(refused, SshHostKeyRefusalReason.NeedsWeakAlgorithms, "RSA keys of 1024 bits need --allow-weak-ssh-algorithms");
        Assert.AreEqual("ssh-rsa", allowed.Key!.KeyType);
    }

    [TestMethod]
    [DataRow(false, SshHostKeyRefusalReason.NeedsWeakAlgorithms, "DSA keys of 1024 bits need --allow-weak-ssh-algorithms", DisplayName = "Without weak algorithms")]
    [DataRow(true, SshHostKeyRefusalReason.UnsupportedKeyType, "key type ssh-dss is not supported", DisplayName = "With weak algorithms, until BL-248")]
    public void Read_DsaKey_IsRefused(bool allowWeakAlgorithms, SshHostKeyRefusalReason reason, string text)
    {
        var pkcs8 = SshHostKeyFile.Read(Pem("PRIVATE KEY", Pkcs8Dsa(1024)), null, allowWeakAlgorithms);
        var openSsh = SshHostKeyFile.Read(OpenSshPem(OpenSshBody("ssh-dss", Mpint(BigInteger.One << 1023))), null, allowWeakAlgorithms);

        AssertRefused(pkcs8, reason, text);
        AssertRefused(openSsh, reason, text);
    }

    public static IEnumerable<object[]> UnsupportedKeys()
    {
        yield return ["PKCS #8 X25519", Pem("PRIVATE KEY", Pkcs8("1.3.101.110", null, [4, 32, .. new byte[32]])), "1.3.101.110"];
        yield return [
            "PKCS #8 EC on brainpoolP256r1",
            Pem("PRIVATE KEY", Pkcs8("1.2.840.10045.2.1", writer => writer.WriteObjectIdentifier("1.3.36.3.3.2.8.1.1.7"), [0])),
            "ecdsa on curve 1.3.36.3.3.2.8.1.1.7"];
        yield return ["SEC 1 EC on secp256k1", Pem("EC PRIVATE KEY", Sec1("1.3.132.0.10")), "ecdsa on curve 1.3.132.0.10"];
        yield return ["OpenSSH unknown type", OpenSshPem(OpenSshBody("sk-ssh-ed25519@openssh.com", [])), "sk-ssh-ed25519@openssh.com"];
        yield return ["OpenSSH unknown ECDSA curve", OpenSshPem(OpenSshBody("ecdsa-sha2-nistp999", String("nistp999"))), "ecdsa-sha2-nistp999"];
        yield return ["OpenSSH curve other than the type's", OpenSshPem(OpenSshBody("ecdsa-sha2-nistp256", String("nistp384"))), "ecdsa-sha2-nistp256"];
    }

    [TestMethod]
    [DynamicData(nameof(UnsupportedKeys))]
    public void Read_KeyTypeSurlDoesNotServe_IsRefusedAsNotSupported(string caseName, byte[] file, string keyType)
    {
        AssertRefused(SshHostKeyFile.Read(file, null, allowWeakAlgorithms: true), SshHostKeyRefusalReason.UnsupportedKeyType, $"key type {keyType} is not supported", caseName);
    }

    public static IEnumerable<object[]> UnreadableFiles()
    {
        var rsa = SshTestKeys.Rsa2048.ExportParameters(true);
        var p256 = SshTestKeys.EcdsaP256.ExportParameters(true);
        var point = Point(p256);
        var d = Unsigned(p256.D!);
        var rsaFields = OpenSshRsaFields(rsa);

        yield return ["Not PEM", Ascii("hello\n")];
        yield return ["Empty", Array.Empty<byte>()];
        yield return ["Two PEM blocks", Concat(Utf8(SshTestKeys.Rsa2048.ExportRSAPrivateKeyPem()), Utf8(SshTestKeys.Rsa2048.ExportRSAPrivateKeyPem()))];
        yield return ["A certificate", Pem("CERTIFICATE", [0x30, 0x00])];
        yield return ["Legacy encrypted PEM", Ascii("-----BEGIN RSA PRIVATE KEY-----\nProc-Type: 4,ENCRYPTED\nDEK-Info: AES-128-CBC,00\n\nAAAA\n-----END RSA PRIVATE KEY-----\n")];
        yield return ["PKCS #1 that is not DER", Pem("RSA PRIVATE KEY", [1, 2, 3])];
        yield return ["PKCS #8 that is not DER", Pem("PRIVATE KEY", [1, 2, 3])];
        yield return ["PKCS #8 RSA that is not an RSA key", Pem("PRIVATE KEY", Pkcs8("1.2.840.113549.1.1.1", writer => writer.WriteNull(), [0x30, 0x00]))];
        yield return ["SEC 1 naming no curve", Pem("EC PRIVATE KEY", Sec1(null))];
        yield return ["OpenSSH without its magic", OpenSshPem(Ascii("openssh-key-v2\0"))];
        yield return ["OpenSSH with a KDF but no cipher", OpenSshPem(OpenSshBody("ssh-rsa", rsaFields, kdf: "bcrypt"))];
        yield return ["OpenSSH holding two keys", OpenSshPem(OpenSshBody("ssh-rsa", rsaFields, keyCount: 2))];
        yield return ["OpenSSH check integers that differ", OpenSshPem(OpenSshBody("ssh-rsa", rsaFields, secondCheck: 7))];
        yield return ["OpenSSH cut short", OpenSshPem(OpenSshBody("ssh-rsa", rsaFields)[..60])];
        yield return ["OpenSSH RSA with p = 1", OpenSshPem(OpenSshBody("ssh-rsa", RsaFieldsWith(rsa, prime1: 1)))];
        yield return ["OpenSSH RSA with q = 1", OpenSshPem(OpenSshBody("ssh-rsa", RsaFieldsWith(rsa, prime2: 1)))];
        yield return ["OpenSSH RSA with a wrong modulus", OpenSshPem(OpenSshBody("ssh-rsa", RsaFieldsWith(rsa, modulus: 15)))];
        yield return ["OpenSSH ECDSA point too short", OpenSshPem(OpenSshBody("ecdsa-sha2-nistp256", OpenSshEcdsaFields("nistp256", point[..64], d)))];
        yield return ["OpenSSH ECDSA point compressed", OpenSshPem(OpenSshBody("ecdsa-sha2-nistp256", OpenSshEcdsaFields("nistp256", [2, .. point[1..]], d)))];
        yield return ["OpenSSH ECDSA zero private value", OpenSshPem(OpenSshBody("ecdsa-sha2-nistp256", OpenSshEcdsaFields("nistp256", point, BigInteger.Zero)))];
        yield return ["OpenSSH ECDSA negative private value", OpenSshPem(OpenSshBody("ecdsa-sha2-nistp256", OpenSshEcdsaFields("nistp256", point, BigInteger.MinusOne)))];
        yield return ["OpenSSH ECDSA private value too long", OpenSshPem(OpenSshBody("ecdsa-sha2-nistp256", OpenSshEcdsaFields("nistp256", point, BigInteger.One << 256)))];
        yield return ["Encrypted PKCS #8 under PBES1", Utf8(SshTestKeys.Rsa2048.ExportEncryptedPkcs8PrivateKeyPem(Passphrase, new PbeParameters(PbeEncryptionAlgorithm.TripleDes3KeyPkcs12, HashAlgorithmName.SHA1, 1000)))];
        yield return ["Encrypted PKCS #8 with another KDF", Pem("ENCRYPTED PRIVATE KEY", EncryptedPkcs8([0x30, 0x00], Passphrase, kdfOid: "1.3.6.1.4.1.11591.4.11"))];
        yield return ["Encrypted PKCS #8 with another PRF", Pem("ENCRYPTED PRIVATE KEY", EncryptedPkcs8([0x30, 0x00], Passphrase, prfOid: "1.2.840.113549.2.5"))];
        yield return ["Encrypted PKCS #8 with another cipher", Pem("ENCRYPTED PRIVATE KEY", EncryptedPkcs8([0x30, 0x00], Passphrase, cipherOid: "1.2.840.113549.3.7"))];
        yield return ["Encrypted PKCS #8 with no iterations", Pem("ENCRYPTED PRIVATE KEY", EncryptedPkcs8([0x30, 0x00], Passphrase, iterations: 0))];
        yield return ["Encrypted PKCS #8 with too many iterations", Pem("ENCRYPTED PRIVATE KEY", EncryptedPkcs8([0x30, 0x00], Passphrase, iterations: 1L << 32))];
        yield return ["Encrypted PKCS #8 with an 8-byte IV", Pem("ENCRYPTED PRIVATE KEY", EncryptedPkcs8([0x30, 0x00], Passphrase, ivLength: 8))];
        yield return [
            "OpenSSH ECDSA point of another key",
            OpenSshPem(OpenSshBody("ecdsa-sha2-nistp256", OpenSshEcdsaFields("nistp256", Point(ECDsa.Create(ECCurve.NamedCurves.nistP256).ExportParameters(false)), d)))];
        yield return ["Encrypted PKCS #8 of an empty key", Pem("ENCRYPTED PRIVATE KEY", EncryptedPkcs8([0x30, 0x00], Passphrase))];

        var seed = SshTestKeys.Ed25519Seed;
        var publicKey = SshTestKeys.Ed25519PublicKey;
        var otherPublicKey = (byte[])publicKey.Clone();
        otherPublicKey[0] ^= 1;
        yield return ["PKCS #8 Ed25519 with algorithm parameters", Pem("PRIVATE KEY", Pkcs8(Ed25519Oid, writer => writer.WriteNull(), Pkcs8Ed25519PrivateKey(seed)))];
        yield return ["PKCS #8 Ed25519 whose private key is not an OCTET STRING", Pem("PRIVATE KEY", Pkcs8(Ed25519Oid, null, [0x30, 0x00]))];
        yield return ["PKCS #8 Ed25519 with a 31-byte seed", Pem("PRIVATE KEY", Pkcs8(Ed25519Oid, null, Pkcs8Ed25519PrivateKey(seed[..31])))];
        yield return ["PKCS #8 Ed25519 with bytes after its seed", Pem("PRIVATE KEY", Pkcs8(Ed25519Oid, null, [.. Pkcs8Ed25519PrivateKey(seed), 5, 0]))];
        yield return ["OpenSSH Ed25519 private key of the seed alone", OpenSshPem(OpenSshBody("ssh-ed25519", OpenSshEd25519Fields(publicKey, seed)))];
        yield return ["OpenSSH Ed25519 public key of another seed", OpenSshPem(OpenSshBody("ssh-ed25519", OpenSshEd25519Fields(otherPublicKey, [.. seed, .. publicKey])))];
        yield return ["OpenSSH Ed25519 private key ending in another public key", OpenSshPem(OpenSshBody("ssh-ed25519", OpenSshEd25519Fields(publicKey, [.. seed, .. otherPublicKey])))];
        yield return ["OpenSSH Ed25519 private key running past the section", OpenSshPem(OpenSshBody("ssh-ed25519", Concat(Str(publicKey), UInt32(1000))))];
    }

    [TestMethod]
    [DynamicData(nameof(UnreadableFiles))]
    public void Read_NotOnePrivateKeySurlReads_IsRefused(string caseName, byte[] file)
    {
        AssertRefused(SshHostKeyFile.Read(file, Passphrase, allowWeakAlgorithms: false), SshHostKeyRefusalReason.NotAPrivateKey, "not a private key surl can read", caseName);
    }

    [TestMethod]
    public void Read_EncryptedPkcs8WithoutAPassphrase_IsRefused()
    {
        var file = Utf8(SshTestKeys.Rsa2048.ExportEncryptedPkcs8PrivateKeyPem(Passphrase, new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 1000)));

        AssertRefused(SshHostKeyFile.Read(file, null, allowWeakAlgorithms: false), SshHostKeyRefusalReason.EncryptedWithoutPassphrase, "the key is encrypted; give --pass");
    }

    [TestMethod]
    [DataRow(false, DisplayName = "Bad padding")]
    [DataRow(true, DisplayName = "Good padding around bytes that are not DER")]
    public void Read_EncryptedPkcs8WithTheWrongPassphrase_IsRefused(bool rightPaddingWrongContent)
    {
        var file = rightPaddingWrongContent
            ? Pem("ENCRYPTED PRIVATE KEY", EncryptedPkcs8(Ascii("not a key"), Passphrase))
            : Utf8(SshTestKeys.Rsa2048.ExportEncryptedPkcs8PrivateKeyPem(Passphrase, new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 1000)));
        var passphrase = rightPaddingWrongContent ? Passphrase : "wrong horse";

        AssertRefused(SshHostKeyFile.Read(file, passphrase, allowWeakAlgorithms: false), SshHostKeyRefusalReason.PassphraseDoesNotDecrypt, "--pass does not decrypt the key");
    }

    [TestMethod]
    public void Read_EncryptedDsaKey_IsRefusedForItsTypeNotItsPassphrase()
    {
        var file = Pem("ENCRYPTED PRIVATE KEY", EncryptedPkcs8(Pkcs8Dsa(2048), Passphrase));

        AssertRefused(SshHostKeyFile.Read(file, Passphrase, allowWeakAlgorithms: false), SshHostKeyRefusalReason.NeedsWeakAlgorithms, "DSA keys of 2048 bits need --allow-weak-ssh-algorithms");
    }

    private static byte[] RsaFieldsWith(RSAParameters key, int? prime1 = null, int? prime2 = null, int? modulus = null) => Concat(
        Mpint(modulus ?? Unsigned(key.Modulus!)),
        Mpint(Unsigned(key.Exponent!)),
        Mpint(Unsigned(key.D!)),
        Mpint(Unsigned(key.InverseQ!)),
        Mpint(prime1 ?? Unsigned(key.P!)),
        Mpint(prime2 ?? Unsigned(key.Q!)));

    private static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);

    private static byte[] Fixed(BigInteger value, int length)
    {
        var bytes = value.ToByteArray(isUnsigned: true, isBigEndian: true);

        return [.. new byte[length - bytes.Length], .. bytes];
    }

    private static void AssertRefused(SshHostKeyReading reading, SshHostKeyRefusalReason reason, string text, string? caseName = null)
    {
        Assert.IsNull(reading.Key, caseName);
        Assert.AreEqual(reason, reading.Refusal!.Reason, caseName);
        Assert.AreEqual(text, reading.Refusal.Text, caseName);
    }
}
