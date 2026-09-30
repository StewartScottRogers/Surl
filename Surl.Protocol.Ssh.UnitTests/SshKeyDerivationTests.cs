using System.Numerics;
using System.Security.Cryptography;

namespace Surl.Protocol.Ssh;

/// <summary>
/// RFC 4253 section 7.2 has no published test vector of its own, so each expected key here is
/// computed by hand from the formula: <c>K1 = HASH(K || H || X || session_id)</c>,
/// <c>K2 = HASH(K || H || K1)</c>, the key being <c>K1 || K2 || ...</c> cut to length, with K
/// written out byte for byte as an <c>mpint</c>.
/// </summary>
[TestClass]
public sealed class SshKeyDerivationTests
{
    // K = 0x80 0x01 ... 0x1F: its top bit is set, so its mpint carries a leading zero byte.
    private static readonly byte[] SharedSecretMagnitude = [0x80, .. Enumerable.Range(1, 31).Select(value => (byte)value)];

    private static readonly byte[] SharedSecretAsMpint = [0x00, 0x00, 0x00, 0x21, 0x00, .. SharedSecretMagnitude];

    private static readonly byte[] ExchangeHash = [.. Enumerable.Range(0x40, 32).Select(value => (byte)value)];

    private static readonly byte[] SessionIdentifier = [.. Enumerable.Range(0xA0, 32).Select(value => (byte)value)];

    [TestMethod]
    [DataRow('A')]
    [DataRow('F')]
    public void DeriveKey_NoLongerThanTheHash_IsTheFirstBlockCut(char letter)
    {
        var expected = SHA256.HashData([.. SharedSecretAsMpint, .. ExchangeHash, (byte)letter, .. SessionIdentifier])[..16];

        CollectionAssert.AreEqual(expected, Derivation(HashAlgorithmName.SHA256).DeriveKey(letter, 16));
    }

    [TestMethod]
    public void DeriveKey_LongerThanTheHash_IsExtendedWithHashesOfWhatCameBefore()
    {
        var first = SHA256.HashData([.. SharedSecretAsMpint, .. ExchangeHash, (byte)'C', .. SessionIdentifier]);
        var second = SHA256.HashData([.. SharedSecretAsMpint, .. ExchangeHash, .. first]);
        var third = SHA256.HashData([.. SharedSecretAsMpint, .. ExchangeHash, .. first, .. second]);

        CollectionAssert.AreEqual(
            (byte[])[.. first, .. second, .. third[..8]],
            Derivation(HashAlgorithmName.SHA256).DeriveKey('C', 72));
    }

    [TestMethod]
    public void DeriveKey_UsesTheMethodsHash()
    {
        var expected = SHA512.HashData([.. SharedSecretAsMpint, .. ExchangeHash, (byte)'D', .. SessionIdentifier]);

        CollectionAssert.AreEqual(expected, Derivation(HashAlgorithmName.SHA512).DeriveKey('D', 64));
    }

    [TestMethod]
    public void Letters_AreAToFInOrder()
    {
        CollectionAssert.AreEqual("ABCDEF".ToCharArray(), SshKeyDerivation.Letters.ToArray());
    }

    private static SshKeyDerivation Derivation(HashAlgorithmName hash) => new(
        hash,
        new BigInteger(SharedSecretMagnitude, isUnsigned: true, isBigEndian: true),
        ExchangeHash,
        SessionIdentifier);
}
