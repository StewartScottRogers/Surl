namespace Surl.Cryptography.Rc4;

/// <summary>
/// Pins <see cref="Rc4" /> to published vectors only (ADR-0003): RFC 6229 section 2, the
/// keystream of the keys 0x0102...; for 40, 128 and 256 bits, with RFC 4345 section 4's
/// 1536-byte discard shown against RFC 6229's offset-1536 row.
/// </summary>
[TestClass]
public sealed class Rc4Tests
{
    // RFC 6229 section 2, "Key length: 40 bits.", key 0x0102030405.
    private const string Key40 = "0102030405";

    // RFC 6229 section 2, "Key length: 128 bits.", key 0x0102030405060708090a0b0c0d0e0f10.
    private const string Key128 = "0102030405060708090a0b0c0d0e0f10";

    // RFC 6229 section 2, "Key length: 256 bits.",
    // key 0x0102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f20.
    private const string Key256 = "0102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f20";

    // RFC 6229 section 2, 128-bit key, "DEC 1536 HEX 600".
    private const string Key128Offset1536 = "ffa0b514647ec04f6306b892ae661181";

    // RFC 6229 section 2, 256-bit key, "DEC 1536 HEX 600".
    private const string Key256Offset1536 = "3e34135c79db010200767651cf263073";

    private const int ArcfourDiscardLength = 1536;

    // Each expected row is RFC 6229 section 2's 16 bytes at that offset for that key.
    [TestMethod]
    [DataRow(Key40, 0, "b2396305f03dc027ccc3524a0a1118a8")]
    [DataRow(Key40, 16, "6982944f18fc82d589c403a47a0d0919")]
    [DataRow(Key40, 1536, "d8729db41882259bee4f825325f5a130")]
    [DataRow(Key40, 4096, "ff25b58995996707e51fbdf08b34d875")]
    [DataRow(Key128, 0, "9ac7cc9a609d1ef7b2932899cde41b97")]
    [DataRow(Key128, 16, "5248c4959014126a6e8a84f11d1a9e1c")]
    [DataRow(Key128, 1536, Key128Offset1536)]
    [DataRow(Key128, 4096, "a36a4c301ae8ac13610ccbc12256cacc")]
    [DataRow(Key256, 0, "eaa6bd25880bf93d3f5d1e4ca2611d91")]
    [DataRow(Key256, 16, "cfa45c9f7e714b54bdfa80027cb14380")]
    [DataRow(Key256, 1536, Key256Offset1536)]
    [DataRow(Key256, 4096, "f3e4c0a2e02d1d01f7f0a74618af2b48")]
    public void ApplyKeyStream_Rfc6229KeyNothingDiscarded_GivesTheRfcKeyStreamAtTheOffset(
        string key, int offset, string expected)
    {
        var rc4 = new Rc4(Convert.FromHexString(key), 0);
        var keyStream = KeyStream(rc4, offset + 16);

        Assert.AreEqual(expected, Convert.ToHexStringLower(keyStream.AsSpan(offset, 16)));
    }

    // RFC 4345 section 4: arcfour128 and arcfour256 discard the first 1536 bytes, so their
    // first byte is RFC 6229's offset-1536 row.
    [TestMethod]
    [DataRow(Key128, Key128Offset1536)]
    [DataRow(Key256, Key256Offset1536)]
    public void ApplyKeyStream_1536BytesDiscarded_StartsAtTheRfc6229Offset1536Row(string key, string expected)
    {
        var rc4 = new Rc4(Convert.FromHexString(key), ArcfourDiscardLength);

        Assert.AreEqual(expected, Convert.ToHexStringLower(KeyStream(rc4, 16)));
    }

    [TestMethod]
    public void ApplyKeyStream_SeveralCalls_GiveTheSameKeyStreamAsOneCall()
    {
        var whole = KeyStream(new Rc4(Convert.FromHexString(Key128), 0), 4112);
        var pieces = new Rc4(Convert.FromHexString(Key128), 0);
        var joined = new List<byte>();

        foreach (var length in new[] { 1, 15, 0, 100, 1420, 2576 })
        {
            joined.AddRange(KeyStream(pieces, length));
        }

        CollectionAssert.AreEqual(whole, joined.ToArray());
    }

    [TestMethod]
    public void ApplyKeyStream_SourceIsDestination_DecryptsWhatItEncrypted()
    {
        var plaintext = "arcfour"u8.ToArray();
        var buffer = plaintext.ToArray();

        new Rc4(Convert.FromHexString(Key128), 0).ApplyKeyStream(buffer, buffer);
        CollectionAssert.AreNotEqual(plaintext, buffer);
        new Rc4(Convert.FromHexString(Key128), 0).ApplyKeyStream(buffer, buffer);

        CollectionAssert.AreEqual(plaintext, buffer);
    }

    [TestMethod]
    public void Constructor_EmptyKey_ThrowsArgumentException()
    {
        var exception = Assert.ThrowsExactly<ArgumentException>(() => new Rc4([], 0));

        Assert.AreEqual("key", exception.ParamName);
    }

    [TestMethod]
    public void Constructor_257ByteKey_ThrowsArgumentException()
    {
        var exception = Assert.ThrowsExactly<ArgumentException>(() => new Rc4(new byte[257], 0));

        Assert.AreEqual("key", exception.ParamName);
    }

    [TestMethod]
    public void Constructor_256ByteKey_IsAccepted()
    {
        var rc4 = new Rc4(new byte[Rc4.MaximumKeySize], 0);

        Assert.HasCount(1, KeyStream(rc4, 1));
    }

    [TestMethod]
    public void Constructor_NegativeDiscardLength_ThrowsArgumentOutOfRangeException()
    {
        var exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new Rc4(Convert.FromHexString(Key40), -1));

        Assert.AreEqual("discardedKeyStreamLength", exception.ParamName);
    }

    [TestMethod]
    public void ApplyKeyStream_DestinationShorterThanSource_ThrowsArgumentException()
    {
        var rc4 = new Rc4(Convert.FromHexString(Key40), 0);

        var exception = Assert.ThrowsExactly<ArgumentException>(() => rc4.ApplyKeyStream(new byte[2], new byte[1]));

        Assert.AreEqual("destination", exception.ParamName);
    }

    // The keystream is what RC4 exclusive-ors into zero bytes.
    private static byte[] KeyStream(Rc4 rc4, int length)
    {
        var keyStream = new byte[length];
        rc4.ApplyKeyStream(keyStream, keyStream);

        return keyStream;
    }
}
