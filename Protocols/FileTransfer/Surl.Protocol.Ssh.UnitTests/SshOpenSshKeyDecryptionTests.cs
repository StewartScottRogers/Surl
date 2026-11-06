using System.Security.Cryptography;
using System.Text;
using static Surl.Protocol.Ssh.SshKeyFileBuilder;
using static Surl.Protocol.Ssh.SshTestExchange;
using static Surl.Protocol.Ssh.SshTestKeyExchangeClient;

namespace Surl.Protocol.Ssh;

/// <summary>
/// <see cref="SshHostKeyFile.Read"/> of encrypted <c>openssh-key-v1</c> keys: the keys in
/// <c>Fixtures/openssh-encrypted-keys</c>, written by <c>ssh-keygen</c> from OpenSSH_10.3p1 (Git
/// for Windows) with <c>-a 2</c> and the passphrase <c>correct horse</c> (see the fixtures'
/// README), and ADR-0051's refusals of the keys it cannot decrypt.
/// </summary>
[TestClass]
public sealed class SshOpenSshKeyDecryptionTests
{
    private const string FixtureFolder = "openssh-encrypted-keys";

    private const string Passphrase = "correct horse";

    [TestMethod]
    [DataRow("ed25519-aes128-ctr", "ssh-ed25519")]
    [DataRow("ed25519-aes192-ctr", "ssh-ed25519")]
    [DataRow("ed25519-aes256-ctr", "ssh-ed25519")]
    [DataRow("ed25519-aes128-gcm@openssh.com", "ssh-ed25519")]
    [DataRow("ed25519-aes256-gcm@openssh.com", "ssh-ed25519")]
    [DataRow("ed25519-chacha20-poly1305@openssh.com", "ssh-ed25519")]
    [DataRow("ed25519-aes256-cbc", "ssh-ed25519")]
    [DataRow("rsa-aes256-ctr", "rsa-sha2-256")]
    [DataRow("ecdsa-nistp384-aes256-ctr", "ecdsa-sha2-nistp384")]
    public void Read_EncryptedFixtureWithItsPassphrase_SignsWhatItsPublicKeyVerifies(string fixture, string signatureAlgorithm)
    {
        var publicKeyBlob = FixturePublicKeyBlob(fixture);
        byte[] exchangeHash = SHA256.HashData(Encoding.ASCII.GetBytes("an exchange hash for " + fixture));

        var reading = SshHostKeyFile.Read(FixtureKey(fixture), Passphrase, allowWeakAlgorithms: false);

        Assert.IsNull(reading.Refusal, fixture);
        CollectionAssert.AreEqual(publicKeyBlob, reading.Key!.PublicKeyBlob.ToArray(), fixture);
        Assert.IsTrue(SignatureVerifies(publicKeyBlob, reading.Key.Sign(signatureAlgorithm, exchangeHash), exchangeHash), fixture);
    }

    [TestMethod]
    [DataRow("ed25519-aes256-ctr", "wrong horse", DisplayName = "CTR: the check integers differ")]
    [DataRow("ed25519-aes256-cbc", "wrong horse", DisplayName = "CBC: the check integers differ")]
    [DataRow("ed25519-aes256-gcm@openssh.com", "wrong horse", DisplayName = "AES-GCM: the tag does not verify")]
    [DataRow("ed25519-chacha20-poly1305@openssh.com", "wrong horse", DisplayName = "ChaCha20-Poly1305: the tag does not verify")]
    [DataRow("ed25519-aes256-ctr", "", DisplayName = "An empty passphrase")]
    public void Read_EncryptedFixtureWithTheWrongPassphrase_IsRefused(string fixture, string passphrase)
    {
        AssertRefused(SshHostKeyFile.Read(FixtureKey(fixture), passphrase, allowWeakAlgorithms: false), SshHostKeyRefusalReason.PassphraseDoesNotDecrypt, "--pass does not decrypt the key");
    }

    [TestMethod]
    public void Read_EncryptedFixtureWithoutAPassphrase_IsRefused()
    {
        AssertRefused(SshHostKeyFile.Read(FixtureKey("ed25519-aes256-ctr"), null, allowWeakAlgorithms: false), SshHostKeyRefusalReason.EncryptedWithoutPassphrase, "the key is encrypted; give --pass");
    }

    public static IEnumerable<object[]> KeysNotReadable()
    {
        var gcmBody = FixtureBody("ed25519-aes256-gcm@openssh.com");
        var ctrBody = FixtureBody("ed25519-aes256-ctr");

        yield return ["Another KDF", OpenSshPem(EncryptedBody(kdf: "pbkdf2"))];
        yield return ["A cipher not read", OpenSshPem(EncryptedBody(cipher: "twofish256-cbc"))];
        yield return ["A cipher without a KDF", OpenSshPem(EncryptedBody(kdf: "none"))];
        yield return ["An empty salt", OpenSshPem(EncryptedBody(kdfOptions: Concat(Str([]), UInt32(2))))];
        yield return ["A salt longer than bcrypt_pbkdf takes", OpenSshPem(EncryptedBody(kdfOptions: Concat(Str(new byte[(1 << 20) + 1]), UInt32(2))))];
        yield return ["No rounds", OpenSshPem(EncryptedBody(kdfOptions: Concat(Str(new byte[16]), UInt32(0))))];
        yield return ["More rounds than an int holds", OpenSshPem(EncryptedBody(kdfOptions: Concat(Str(new byte[16]), UInt32(0x80000000))))];
        yield return ["KDF options cut short", OpenSshPem(EncryptedBody(kdfOptions: Str(new byte[16])))];
        yield return ["Two keys", OpenSshPem(EncryptedBody(keyCount: 2))];
        yield return ["An empty encrypted section", OpenSshPem(EncryptedBody(encrypted: []))];
        yield return ["An encrypted section not whole blocks", OpenSshPem(EncryptedBody(encrypted: new byte[15]))];
        yield return ["An AEAD key without its tag", OpenSshPem(gcmBody[..^1])];
        yield return ["A key cut short in its encrypted section", OpenSshPem(ctrBody[..(ctrBody.Length - 40)])];
    }

    [TestMethod]
    [DynamicData(nameof(KeysNotReadable))]
    public void Read_EncryptedKeyNotWellFormed_IsRefusedAsNotAPrivateKey(string caseName, byte[] file)
    {
        AssertRefused(SshHostKeyFile.Read(file, Passphrase, allowWeakAlgorithms: false), SshHostKeyRefusalReason.NotAPrivateKey, "not a private key surl can read", caseName);
    }

    // An encrypted openssh-key-v1 body whose fields a test damages one at a time: the salt and
    // round count, a public key, and an encrypted section of one AES block with no tag.
    private static byte[] EncryptedBody(
        string cipher = "aes256-ctr",
        string kdf = "bcrypt",
        byte[]? kdfOptions = null,
        uint keyCount = 1,
        byte[]? encrypted = null) => Concat(
            Ascii("openssh-key-v1\0"),
            String(cipher),
            String(kdf),
            Str(kdfOptions ?? Concat(Str(new byte[16]), UInt32(2))),
            UInt32(keyCount),
            Str(String("ssh-ed25519")),
            Str(encrypted ?? new byte[16]));

    private static byte[] FixtureKey(string fixture) => RecordedFixture.ReadBytes(FixtureFolder, fixture + ".key");

    private static byte[] FixtureBody(string fixture)
    {
        var text = Encoding.ASCII.GetString(FixtureKey(fixture));
        var fields = PemEncoding.Find(text);

        return Convert.FromBase64String(text[fields.Base64Data]);
    }

    // The .pub file: the key type, the base64 public key blob, then the comment.
    private static byte[] FixturePublicKeyBlob(string fixture) =>
        Convert.FromBase64String(Encoding.ASCII.GetString(RecordedFixture.ReadBytes(FixtureFolder, fixture + ".key.pub")).Split(' ')[1]);

    private static void AssertRefused(SshHostKeyReading reading, SshHostKeyRefusalReason reason, string text, string? caseName = null)
    {
        Assert.IsNull(reading.Key, caseName);
        Assert.AreEqual(reason, reading.Refusal!.Reason, caseName);
        Assert.AreEqual(text, reading.Refusal.Text, caseName);
    }
}
