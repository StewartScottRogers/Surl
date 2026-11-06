using System.Security.Cryptography;

namespace Surl.Cryptography;

/// <summary>
/// Pins <see cref="Des" /> to published DES blocks, and to the base class library's DES under
/// every key it accepts, never to blocks the code under test computed.
/// </summary>
[TestClass]
public sealed class DesTests
{
    // J. Orlin Grabbe, "The DES Algorithm Illustrated": the worked example.
    [TestMethod]
    [DataRow("133457799BBCDFF1", "0123456789ABCDEF", "85E813540F0AB405", DisplayName = "The DES Algorithm Illustrated")]
    // NIST SP 800-17, table A.1, variable plaintext known answer test: the weak key 01...01 that
    // the base class library refuses.
    [DataRow("0101010101010101", "8000000000000000", "95F8A5E5DD31D900", DisplayName = "SP 800-17 A.1, first entry")]
    [DataRow("0101010101010101", "0000000000000001", "166B40B44ABA4BD6", DisplayName = "SP 800-17 A.1, last entry")]
    public void EncryptBlock_PublishedVector_GivesThePublishedCiphertext(string key, string plaintext, string expected)
    {
        byte[] ciphertext = Des.EncryptBlock(Convert.FromHexString(key), Convert.FromHexString(plaintext));

        Assert.AreEqual(expected, Convert.ToHexString(ciphertext));
    }

    [TestMethod]
    public void EncryptBlock_KeysTheBaseClassLibraryAccepts_MatchesItsDesEcb()
    {
        var random = new Random(291);
        using var bclDes = DES.Create();
        for (int trial = 0; trial < 200; trial++)
        {
            byte[] key = new byte[Des.KeyLength];
            byte[] plaintext = new byte[Des.BlockLength];
            random.NextBytes(key);
            random.NextBytes(plaintext);
            if (DES.IsWeakKey(key) || DES.IsSemiWeakKey(key))
            {
                continue;
            }

            bclDes.Key = key;
            CollectionAssert.AreEqual(bclDes.EncryptEcb(plaintext, PaddingMode.None), Des.EncryptBlock(key, plaintext));
        }
    }

    [TestMethod]
    public void EncryptBlock_KeyParityBitsFlipped_IsTheSameCiphertext()
    {
        byte[] key = Convert.FromHexString("133457799BBCDFF1");
        byte[] flipped = [.. key.Select(b => (byte)(b ^ 1))];
        byte[] plaintext = Convert.FromHexString("0123456789ABCDEF");

        CollectionAssert.AreEqual(Des.EncryptBlock(key, plaintext), Des.EncryptBlock(flipped, plaintext));
    }

    [TestMethod]
    public void EncryptBlock_KeyNotEightBytes_Throws()
    {
        var exception = Assert.ThrowsExactly<ArgumentException>(() => Des.EncryptBlock(new byte[7], new byte[8]));

        Assert.AreEqual("key", exception.ParamName);
    }

    [TestMethod]
    public void EncryptBlock_BlockNotEightBytes_Throws()
    {
        var exception = Assert.ThrowsExactly<ArgumentException>(() => Des.EncryptBlock(new byte[8], new byte[9]));

        Assert.AreEqual("plaintext", exception.ParamName);
    }
}
