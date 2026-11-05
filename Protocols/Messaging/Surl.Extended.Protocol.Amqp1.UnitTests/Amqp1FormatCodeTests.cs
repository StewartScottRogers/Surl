using System.Text;

namespace Surl.Extended.Protocol.Amqp1.UnitTests;

/// <summary>
/// Each format code of AMQP 1.0 Part 1 section 1.6 read and written. The bytes are written by hand
/// from the specification's encodings table; each is decoded, checked against the value it holds,
/// and encoded back to the same bytes.
/// </summary>
[TestClass]
public sealed class Amqp1FormatCodeTests
{
    private static readonly string Bytes300 = string.Concat(Enumerable.Repeat("AB", 300));
    private static readonly string Ascii256 = string.Concat(Enumerable.Repeat("61", 256));

    public static IEnumerable<object?[]> EachPrimitiveFormatCode =>
    [
        ["40", null],
        ["41", true],
        ["42", false],
        ["43", 0u],
        ["44", 0ul],
        ["50FF", (byte)255],
        ["5180", (sbyte)-128],
        ["5207", 7u],
        ["5307", 7ul],
        ["54FF", -1],
        ["55FF", -1L],
        ["60FFFE", (ushort)0xFFFE],
        ["618000", short.MinValue],
        ["7000000100", 256u],
        ["7100000080", 128],
        ["723FC00000", 1.5f],
        ["730001F600", new Rune(0x1F600)],
        ["7422500001", new Amqp1Decimal32(0x22500001)],
        ["800000000000000100", 256ul],
        ["81FFFFFFFFFFFFFF7F", -129L],
        ["823FF8000000000000", 1.5d],
        ["8300000000000003E8", new Amqp1Timestamp(1000)],
        ["842238000000000001", new Amqp1Decimal64(0x2238000000000001)],
        ["9400000000000000010000000000000002", new Amqp1Decimal128(new UInt128(1, 2))],
        ["9800112233445566778899AABBCCDDEEFF", Guid.Parse("00112233-4455-6677-8899-aabbccddeeff")],
        ["A1026869", "hi"],
        ["A10568C3A96C6C", "héll"],
        ["A305504C41494E", new Amqp1Symbol("PLAIN")],
        ["B100000100" + Ascii256, new string('a', 256)],
        ["B300000100" + Ascii256, new Amqp1Symbol(new string('a', 256))],
    ];

    public static IEnumerable<object[]> EachBinaryFormatCode =>
    [
        ["A003010203"],
        ["B00000012C" + Bytes300],
    ];

    public static IEnumerable<object[]> EachCompositeFormatCode =>
    [
        // list0, list8 [true, false], list32 [300-byte binary]
        ["45"],
        ["C003024142"],
        ["D00000013500000001B00000012C" + Bytes300],

        // map8 {:a -> 1u}, map32 {null -> 300-byte binary}, and an empty map8
        ["C10602A301615201"],
        ["D10000013600000002" + "40B00000012C" + Bytes300],
        ["C10100"],

        // array8 of sym8 [:a, :bc], array8 of one-octet booleans [true, false], array32 of 300 ubytes
        ["E00702A30161026263"],
        ["E00402560100"],
        ["F0000001310000012C50" + Bytes300],

        // array8 whose elements carry a descriptor: 0x00 :foo smalluint, elements 1 and 2
        ["E00A0200A303666F6F520102"],

        // an array8 of three nulls: zero-width elements
        ["E0020340"],
    ];

    [TestMethod]
    [DynamicData(nameof(EachPrimitiveFormatCode))]
    public void ReadValue_EachPrimitiveFormatCode_ReadsTheValueAndWritesTheSameBytes(string hex, object? expected)
    {
        var bytes = Convert.FromHexString(hex);

        var decoder = new Amqp1Decoder(bytes);
        var value = decoder.ReadValue();

        Assert.AreEqual(expected, value);
        Assert.AreEqual(0, decoder.Remaining);
        Assert.AreEqual(bytes.Length, decoder.Position);
        Assert.AreEqual(bytes[0], Amqp1Encoder.FormatCodeFor(value));
        CollectionAssert.AreEqual(bytes, Amqp1Encoder.Encode(value));
    }

    [TestMethod]
    [DynamicData(nameof(EachBinaryFormatCode))]
    public void ReadValue_EachBinaryFormatCode_ReadsTheBytesAndWritesTheSameBytes(string hex)
    {
        var bytes = Convert.FromHexString(hex);

        var value = new Amqp1Decoder(bytes).ReadValue();

        var lengthBytes = bytes[0] == Amqp1FormatCode.Binary8 ? 2 : 5;
        CollectionAssert.AreEqual(bytes[lengthBytes..], (byte[])value!);
        CollectionAssert.AreEqual(bytes, Amqp1Encoder.Encode(value));
    }

    [TestMethod]
    [DynamicData(nameof(EachCompositeFormatCode))]
    public void ReadValue_EachCompositeFormatCode_WritesTheSameBytes(string hex)
    {
        var bytes = Convert.FromHexString(hex);

        var value = new Amqp1Decoder(bytes).ReadValue();

        CollectionAssert.AreEqual(bytes, Amqp1Encoder.Encode(value));
    }

    [TestMethod]
    public void ReadValue_List8_ReadsItsElementsInOrder()
    {
        var list = (IReadOnlyList<object?>)new Amqp1Decoder(Convert.FromHexString("C0050341425201")).ReadValue()!;

        CollectionAssert.AreEqual(new object?[] { true, false, 1u }, list.ToArray());
    }

    [TestMethod]
    public void ReadValue_Map8_ReadsItsPairsInOrder()
    {
        var map = (Amqp1Map)new Amqp1Decoder(Convert.FromHexString("C10602A301615201")).ReadValue()!;

        Assert.HasCount(1, map.Entries);
        Assert.AreEqual(new Amqp1Symbol("a"), map.Entries[0].Key);
        Assert.AreEqual(1u, map.Entries[0].Value);
    }

    [TestMethod]
    public void ReadValue_DescribedArray_KeepsTheDescriptorAndTheElementFormatCode()
    {
        var array = (Amqp1Array)new Amqp1Decoder(Convert.FromHexString("E00A0200A303666F6F520102")).ReadValue()!;

        Assert.AreEqual(Amqp1FormatCode.SmallUInt, array.ElementFormatCode);
        Assert.AreEqual(new Amqp1Symbol("foo"), array.ElementDescriptor);
        CollectionAssert.AreEqual(new object?[] { 1u, 2u }, array.Elements.ToArray());
    }

    [TestMethod]
    public void ReadValue_ArrayOfZeroWidthNulls_ReadsEachElement()
    {
        var array = (Amqp1Array)new Amqp1Decoder(Convert.FromHexString("E0020340")).ReadValue()!;

        CollectionAssert.AreEqual(new object?[] { null, null, null }, array.Elements.ToArray());
    }

    [TestMethod]
    [DataRow("5601", true)]
    [DataRow("5600", false)]
    public void ReadValue_OneOctetBoolean_ReadsTheBooleanAndWritesTheZeroWidthForm(string hex, bool expected)
    {
        var value = new Amqp1Decoder(Convert.FromHexString(hex)).ReadValue();

        Assert.AreEqual(expected, value);
        Assert.AreEqual(expected ? Amqp1FormatCode.BooleanTrue : Amqp1FormatCode.BooleanFalse, Amqp1Encoder.FormatCodeFor(value));
    }

    [TestMethod]
    [DataRow("70000000FF", "52FF")]
    [DataRow("7000000000", "43")]
    [DataRow("5200", "43")]
    [DataRow("8000000000000000FF", "53FF")]
    [DataRow("710000007F", "547F")]
    [DataRow("810000000000000001", "5501")]
    [DataRow("B00000000101", "A00101")]
    [DataRow("B10000000161", "A10161")]
    [DataRow("B30000000161", "A30161")]
    [DataRow("C00100", "45")]
    [DataRow("D000000006000000015200", "C0020143")]
    public void ReadValue_WideFormOfASmallValue_IsReadAndWrittenInTheSmallestForm(string wideHex, string smallestHex)
    {
        var value = new Amqp1Decoder(Convert.FromHexString(wideHex)).ReadValue();

        CollectionAssert.AreEqual(Convert.FromHexString(smallestHex), Amqp1Encoder.Encode(value));
    }
}
