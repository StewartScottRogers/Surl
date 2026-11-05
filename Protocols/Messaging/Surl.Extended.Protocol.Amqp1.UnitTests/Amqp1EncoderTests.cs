using System.Text;

namespace Surl.Extended.Protocol.Amqp1.UnitTests;

/// <summary>
/// Values written: described values, lists of every .NET list type, and values AMQP 1.0 cannot encode.
/// </summary>
[TestClass]
public sealed class Amqp1EncoderTests
{
    [TestMethod]
    public void WriteValue_DescribedList_WritesTheDescriptorThenTheList()
    {
        var encoder = new Amqp1Encoder();

        encoder.WriteValue(new Amqp1Described(Amqp1Descriptor.Open, new List<object?> { true }));
        encoder.WriteValue(null);

        CollectionAssert.AreEqual(Convert.FromHexString("005310C002014140"), encoder.Written.ToArray());
    }

    [TestMethod]
    public void FormatCodeFor_ListWhoseContentPassesTheOneOctetSize_IsAList32()
    {
        var bytes = Amqp1Encoder.Encode(Enumerable.Repeat<object?>(true, 254).ToArray());

        Assert.AreEqual(Amqp1FormatCode.List8, bytes[0]);
        Assert.AreEqual(Amqp1FormatCode.List32, Amqp1Encoder.FormatCodeFor(Enumerable.Repeat<object?>(true, 255).ToArray()));
    }

    [TestMethod]
    public void Encode_MapWith128Pairs_IsAMap32()
    {
        var entries = Enumerable.Range(0, 128).Select(_ => new KeyValuePair<object?, object?>(null, null)).ToArray();

        Assert.AreEqual(Amqp1FormatCode.Map32, Amqp1Encoder.FormatCodeFor(new Amqp1Map(entries)));
    }

    [TestMethod]
    public void Encode_ArrayOf256Elements_IsAnArray32()
    {
        var array = new Amqp1Array(Amqp1FormatCode.Null, new object?[256]);

        Assert.AreEqual(Amqp1FormatCode.Array32, Amqp1Encoder.FormatCodeFor(array));
    }

    [TestMethod]
    public void FormatCodeFor_TypeWithNoEncoding_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() => Amqp1Encoder.FormatCodeFor(1.5m));
        Assert.ThrowsExactly<ArgumentException>(() => Amqp1Encoder.Encode(new Amqp1Map([new(1.5m, null)])));
    }

    [TestMethod]
    public void Encode_ArrayOfAnUnknownElementFormatCode_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() => Amqp1Encoder.Encode(new Amqp1Array(0x99, [1u])));
    }

    [TestMethod]
    public void Encode_ArrayElementTooLargeForItsFormatCode_Throws()
    {
        Assert.ThrowsExactly<OverflowException>(() => Amqp1Encoder.Encode(new Amqp1Array(Amqp1FormatCode.SmallUInt, [300u])));
    }

    [TestMethod]
    public void Encode_SymbolThatIsNotAscii_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() => Amqp1Encoder.Encode(new Amqp1Symbol("é")));
    }

    [TestMethod]
    public void Encode_ArrayOfDescribedElements_WritesTheDescriptorOnce()
    {
        var array = new Amqp1Array(Amqp1FormatCode.SmallUInt, [1u, 2u], new Amqp1Symbol("foo"));

        CollectionAssert.AreEqual(Convert.FromHexString("E00A0200A303666F6F520102"), Amqp1Encoder.Encode(array));
    }

    [TestMethod]
    public void Encode_EachWidthOfEachFixedWidthType_RoundTrips()
    {
        object?[] values = [(byte)1, (sbyte)-1, (ushort)2, (short)-2, 1.25f, 2.5d, new Rune('A'), new Amqp1Decimal32(1), new Amqp1Decimal64(2), new Amqp1Decimal128(3), new Amqp1Timestamp(-1), Guid.NewGuid(), 200, -200L];

        foreach (var value in values)
        {
            Assert.AreEqual(value, new Amqp1Decoder(Amqp1Encoder.Encode(value)).ReadValue());
        }
    }

    [TestMethod]
    public void Encode_ArrayOfEveryOneOctetSmallForm_RoundTrips()
    {
        object?[] arrays =
        [
            new Amqp1Array(Amqp1FormatCode.SmallULong, [255ul]),
            new Amqp1Array(Amqp1FormatCode.SmallInt, [-1]),
            new Amqp1Array(Amqp1FormatCode.SmallLong, [-1L]),
            new Amqp1Array(Amqp1FormatCode.UInt0, [0u]),
            new Amqp1Array(Amqp1FormatCode.ULong0, [0ul]),
            new Amqp1Array(Amqp1FormatCode.List0, [Array.Empty<object?>()]),
            new Amqp1Array(Amqp1FormatCode.BooleanTrue, [true]),
            new Amqp1Array(Amqp1FormatCode.BooleanFalse, [false]),
            new Amqp1Array(Amqp1FormatCode.List8, [new object?[] { 1u }]),
            new Amqp1Array(Amqp1FormatCode.List32, [new object?[] { 1u }]),
            new Amqp1Array(Amqp1FormatCode.Map8, [new Amqp1Map([])]),
            new Amqp1Array(Amqp1FormatCode.Map32, [new Amqp1Map([])]),
            new Amqp1Array(Amqp1FormatCode.Array8, [new Amqp1Array(Amqp1FormatCode.Null, [null])]),
            new Amqp1Array(Amqp1FormatCode.Array32, [new Amqp1Array(Amqp1FormatCode.Null, [null])]),
            new Amqp1Array(Amqp1FormatCode.Binary8, [new byte[] { 1 }]),
            new Amqp1Array(Amqp1FormatCode.String8, ["a"]),
            new Amqp1Array(Amqp1FormatCode.String32, ["a"]),
        ];

        foreach (var array in arrays)
        {
            var bytes = Amqp1Encoder.Encode(array);

            CollectionAssert.AreEqual(bytes, Amqp1Encoder.Encode(new Amqp1Decoder(bytes).ReadValue()));
        }
    }
}
