using static Surl.Hpack.Rfc7541Hex;

namespace Surl.Hpack;

[TestClass]
public sealed class HpackEncoderTests
{
    [TestMethod]
    public void Encode_NeverIndexedFieldMatchingAStaticEntry_SendsALiteralNamingIt()
    {
        var encoder = new HpackEncoder(stringCoding: HpackStringCoding.Raw);

        var encoded = encoder.Encode([new HpackHeaderField(":method", "GET", HpackFieldIndexing.NeverIndexed)]);

        Assert.AreEqual("1203474554", Hex(encoded));
        Assert.AreEqual(0, encoder.TableSize);
    }

    [TestMethod]
    public void Encode_ValueNotInTheStaticTable_NamesTheFirstStaticEntryWithTheName()
    {
        var encoder = new HpackEncoder(stringCoding: HpackStringCoding.Raw);

        var encoded = encoder.Encode([new HpackHeaderField(":method", "PUT")]);

        Assert.AreEqual("4203505554", Hex(encoded));
    }

    [TestMethod]
    public void Encode_NeverIndexedFieldMatchingADynamicEntry_NamesTheNewestDynamicEntryWithTheName()
    {
        var encoder = new HpackEncoder(stringCoding: HpackStringCoding.Raw);
        encoder.Encode([new HpackHeaderField("custom-key", "a"), new HpackHeaderField("custom-key", "b")]);

        var encoded = encoder.Encode([new HpackHeaderField("custom-key", "b", HpackFieldIndexing.NeverIndexed)]);

        Assert.AreEqual("1f2f0162", Hex(encoded));
    }

    [TestMethod]
    public void Encode_ValueNotInTheDynamicTable_NamesTheNewestDynamicEntryWithTheName()
    {
        var encoder = new HpackEncoder(stringCoding: HpackStringCoding.Raw);
        encoder.Encode([new HpackHeaderField("custom-key", "a"), new HpackHeaderField("custom-key", "b")]);

        var encoded = encoder.Encode([new HpackHeaderField("custom-key", "c", HpackFieldIndexing.WithoutIndexing)]);

        Assert.AreEqual("0f2f0163", Hex(encoded));
    }

    [TestMethod]
    [DataRow("custom-key", "408825a849e95ba97d7f00", DisplayName = "Huffman shorter")]
    [DataRow("\0", "40010000", DisplayName = "raw shorter")]
    public void Encode_WhenShorter_ChoosesTheShorterString(string name, string expected)
    {
        var encoder = new HpackEncoder();

        var encoded = encoder.Encode([new HpackHeaderField(name, string.Empty)]);

        Assert.AreEqual(expected, Hex(encoded));
    }

    [TestMethod]
    public void Encode_AfterPeerLowersAndRaisesTheTableSize_OpensWithBothSizeUpdates()
    {
        var encoder = new HpackEncoder(maximumTableSize: 256);
        encoder.SetPeerMaximumTableSize(0);
        encoder.SetPeerMaximumTableSize(4096);

        var first = encoder.Encode([]);
        var second = encoder.Encode([]);

        Assert.AreEqual("203fe101", Hex(first));
        Assert.IsEmpty(second);
        Assert.AreEqual(256, encoder.MaximumTableSize);
    }

    [TestMethod]
    public void Encode_AfterPeerRaisesTheTableSize_OpensWithOneSizeUpdate()
    {
        var encoder = new HpackEncoder();
        encoder.SetPeerMaximumTableSize(100_000);

        var encoded = encoder.Encode([]);

        Assert.AreEqual("3fe11f", Hex(encoded));
    }

    [TestMethod]
    public void Encode_IncrementalFieldLargerThanTheTable_EmptiesTheTable()
    {
        var encoder = new HpackEncoder(maximumTableSize: 50, stringCoding: HpackStringCoding.Raw);
        encoder.Encode([new HpackHeaderField("a", "b")]);

        encoder.Encode([new HpackHeaderField("a", new string('x', 20))]);

        Assert.AreEqual(0, encoder.TableSize);
        Assert.IsEmpty(encoder.DynamicTableEntries);
    }

    [TestMethod]
    public void Encode_EncodedBlock_DecodesToTheSameHeaderList()
    {
        HpackHeaderField[] fields =
        [
            new(":status", "200"), new("content-type", "text/plain"), new("x-ÿ", "\u0001þ"),
            new("authorization", "secret", HpackFieldIndexing.NeverIndexed),
            new("content-length", "2", HpackFieldIndexing.WithoutIndexing),
        ];
        var encoder = new HpackEncoder();
        var decoder = new HpackDecoder(HpackDecoder.DefaultMaximumTableSize, 1 << 20);

        var first = decoder.Decode(encoder.Encode(fields));
        var second = decoder.Decode(encoder.Encode(fields));

        CollectionAssert.AreEqual(fields, first.Fields.ToArray());
        CollectionAssert.AreEqual(fields, second.Fields.ToArray());
    }

    [TestMethod]
    public void Constructor_NegativeTableSize_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new HpackEncoder(-1));
    }

    [TestMethod]
    public void SetPeerMaximumTableSize_Negative_Throws()
    {
        var encoder = new HpackEncoder();

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => encoder.SetPeerMaximumTableSize(-1));
    }

    [TestMethod]
    public void Encode_NullFields_Throws()
    {
        var encoder = new HpackEncoder();

        Assert.ThrowsExactly<ArgumentNullException>(() => encoder.Encode(null!));
    }
}
