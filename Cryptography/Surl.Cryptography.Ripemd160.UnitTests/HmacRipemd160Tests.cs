using System.Text;

namespace Surl.Cryptography.Ripemd160;

/// <summary>
/// Pins <see cref="HmacRipemd160" /> to published vectors only (ADR-0003): RFC 2286 section 2's
/// seven test cases, and checks that a message split across
/// <see cref="HmacRipemd160.AppendData" /> calls, and a second message on the same instance,
/// give the same MACs, that <see cref="HmacRipemd160.Verify" /> rejects a flipped bit, and its
/// argument and disposal rules.
/// </summary>
[TestClass]
public sealed class HmacRipemd160Tests
{
    private const string Case1Key = "0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b";

    private const string Case1Mac = "24cb4bd67d20fc1a5d2ed7732dcc39377f0a5668";

    private const string Case2Mac = "dda6c0213a485a9e24f4742064a7f033b43c4069";

    private const string Case7Data = "Test Using Larger Than Block-Size Key and Larger Than One Block-Size Data";

    private const string Case7Mac = "69ea60798d71616cce5fd0871e23754cd75d5a0a";

    // RFC 2286 section 2, test cases 1 to 7: key and data in hexadecimal (xx*n is the byte xx
    // n times, as the RFC writes it), and the listed digest. Case 5's is the full 160 bits
    // ("digest"), not the 96-bit truncation; cases 6 and 7 have an 80-byte key, hashed first.
    [TestMethod]
    [DataRow(Case1Key, "4869205468657265", Case1Mac)]
    [DataRow("4a656665", "7768617420646f2079612077616e7420666f72206e6f7468696e673f", Case2Mac)]
    [DataRow("aa*20", "dd*50", "b0b105360de759960ab4f35298e116e295d8e7c1")]
    [DataRow("0102030405060708090a0b0c0d0e0f10111213141516171819", "cd*50", "d5ca862f4d21d5e610e18b4cf1beb97a4365ecf4")]
    [DataRow("0c*20", "546573742057697468205472756e636174696f6e", "7619693978f91d90539ae786500ff3d8e0518e39")]
    [DataRow("aa*80", "54657374205573696e67204c6172676572205468616e20426c6f636b2d53697a65204b6579202d2048617368204b6579204669727374", "6466ca07ac5eac29e1bd523e5ada7605b791fd8b")]
    [DataRow("aa*80", "54657374205573696e67204c6172676572205468616e20426c6f636b2d53697a65204b657920616e64204c6172676572205468616e204f6e6520426c6f636b2d53697a652044617461", Case7Mac)]
    public void HashData_Rfc2286Section2TestCase_GivesTheListedDigest(string key, string data, string expected)
    {
        var mac = new byte[HmacRipemd160.HashSize];

        HmacRipemd160.HashData(FromRfcHex(key), FromRfcHex(data), mac);

        Assert.AreEqual(expected, Convert.ToHexStringLower(mac));
        Assert.AreEqual(expected, Convert.ToHexStringLower(HmacRipemd160.HashData(FromRfcHex(key), FromRfcHex(data))));
    }

    // RFC 2286 section 2, test case 7, fed five bytes at a time.
    [TestMethod]
    public void AppendData_Rfc2286Section2TestCase7SplitAcrossCalls_GivesTheListedDigest()
    {
        var message = Encoding.ASCII.GetBytes(Case7Data);
        var mac = new byte[HmacRipemd160.HashSize];
        using var hmac = new HmacRipemd160(FromRfcHex("aa*80"));

        for (var offset = 0; offset < message.Length; offset += 5)
        {
            hmac.AppendData(message.AsSpan(offset, Math.Min(5, message.Length - offset)));
        }

        hmac.GetHashAndReset(mac);

        Assert.AreEqual(Case7Mac, Convert.ToHexStringLower(mac));
    }

    // RFC 2286 section 2, test case 1, as the second message under the same key.
    [TestMethod]
    public void GetHashAndReset_Rfc2286Section2TestCase1AsSecondMessage_GivesTheListedDigest()
    {
        var mac = new byte[HmacRipemd160.HashSize];
        using var hmac = new HmacRipemd160(Convert.FromHexString(Case1Key));
        hmac.AppendData("something else"u8);
        hmac.GetHashAndReset(mac);

        hmac.AppendData("Hi There"u8);
        hmac.GetHashAndReset(mac);

        Assert.AreEqual(Case1Mac, Convert.ToHexStringLower(mac));
    }

    // RFC 2286 section 2, test case 2.
    [TestMethod]
    public void Verify_Rfc2286Section2TestCase2Digest_ReturnsTrue()
    {
        var verified = HmacRipemd160.Verify("Jefe"u8, "what do ya want for nothing?"u8, Convert.FromHexString(Case2Mac));

        Assert.IsTrue(verified);
    }

    // RFC 2286 section 2, test case 2, with the digest's last bit flipped.
    [TestMethod]
    public void Verify_Rfc2286Section2TestCase2DigestWithAFlippedBit_ReturnsFalse()
    {
        var verified = HmacRipemd160.Verify("Jefe"u8, "what do ya want for nothing?"u8, Convert.FromHexString("dda6c0213a485a9e24f4742064a7f033b43c4068"));

        Assert.IsFalse(verified);
    }

    [TestMethod]
    [DataRow(12)]
    [DataRow(21)]
    public void Verify_WrongMacLength_Throws(int length)
    {
        Assert.ThrowsExactly<ArgumentException>(() => HmacRipemd160.Verify("Jefe"u8, "x"u8, new byte[length]));
    }

    [TestMethod]
    [DataRow(19)]
    [DataRow(21)]
    public void GetHashAndReset_WrongDestinationLength_Throws(int length)
    {
        using var hmac = new HmacRipemd160("Jefe"u8);

        Assert.ThrowsExactly<ArgumentException>(() => hmac.GetHashAndReset(new byte[length]));
    }

    [TestMethod]
    public void AppendData_AfterDispose_Throws()
    {
        var hmac = new HmacRipemd160("Jefe"u8);
        hmac.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(() => hmac.AppendData("a"u8));
    }

    [TestMethod]
    public void GetHashAndReset_AfterDispose_Throws()
    {
        var hmac = new HmacRipemd160("Jefe"u8);
        hmac.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(() => hmac.GetHashAndReset(new byte[HmacRipemd160.HashSize]));
    }

    /// <summary>Hexadecimal, or <c>xx*n</c> for the byte <c>xx</c> repeated n times as RFC 2286 writes it.</summary>
    private static byte[] FromRfcHex(string text)
    {
        var parts = text.Split('*');
        return parts.Length == 1
            ? Convert.FromHexString(text)
            : Enumerable.Repeat(Convert.FromHexString(parts[0])[0], int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture)).ToArray();
    }
}
