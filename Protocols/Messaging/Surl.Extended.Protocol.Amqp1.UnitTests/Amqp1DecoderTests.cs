namespace Surl.Extended.Protocol.Amqp1.UnitTests;

/// <summary>
/// Bytes that do not decode as AMQP 1.0 Part 1 says, each refused with <c>amqp:decode-error</c>.
/// </summary>
[TestClass]
public sealed class Amqp1DecoderTests
{
    [TestMethod]
    [DataRow("99", DisplayName = "an unknown format code")]
    [DataRow("01", DisplayName = "a format code from the reserved range")]
    [DataRow("700000", DisplayName = "a uint cut short")]
    [DataRow("", DisplayName = "no bytes at all")]
    [DataRow("5602", DisplayName = "a boolean octet of 2")]
    [DataRow("730000D800", DisplayName = "a char that is a surrogate")]
    [DataRow("A101FF", DisplayName = "a string that is not UTF-8")]
    [DataRow("A30180", DisplayName = "a symbol that is not ASCII")]
    [DataRow("A00501", DisplayName = "a binary longer than the bytes")]
    [DataRow("C0050141", DisplayName = "a list size past the bytes")]
    [DataRow("C002054141", DisplayName = "a list count its size cannot hold")]
    [DataRow("C003014141", DisplayName = "list elements that do not fill its size")]
    [DataRow("C0000000", DisplayName = "a list of size 0")]
    [DataRow("C1020140", DisplayName = "a map with an odd count")]
    [DataRow("E003055001", DisplayName = "an array count its size cannot hold")]
    [DataRow("F0000000050001000140", DisplayName = "a zero-width array claiming 65537 elements")]
    [DataRow("E0020199", DisplayName = "an array of an unknown element format code")]
    [DataRow("E00101", DisplayName = "an array with no element constructor")]
    [DataRow("E004015201FF", DisplayName = "array elements that do not fill its size")]
    public void ReadValue_MalformedBytes_ThrowDecodeError(string hex)
    {
        var decoder = new Amqp1Decoder(Convert.FromHexString(hex));

        var exception = Assert.ThrowsExactly<Amqp1DecodeException>(() => decoder.ReadValue());

        Assert.AreEqual(Amqp1ErrorCondition.DecodeError, exception.Condition);
    }

    [TestMethod]
    public void ReadValue_ZeroWidthArrayOfTheMostElements_IsRead()
    {
        var decoder = new Amqp1Decoder(Convert.FromHexString("F0000000050001000040"));

        var array = (Amqp1Array)decoder.ReadValue()!;

        Assert.HasCount(Amqp1Decoder.MaxZeroWidthArrayElements, array.Elements);
    }

    [TestMethod]
    public void ReadValue_DescribedValuesNestedDeeperThanTheLimit_ThrowDecodeError()
    {
        byte[] bytes = [.. Enumerable.Repeat((byte)0x00, Amqp1Decoder.MaxNestingDepth + 1), .. Enumerable.Repeat((byte)0x40, Amqp1Decoder.MaxNestingDepth + 2)];

        Assert.ThrowsExactly<Amqp1DecodeException>(() => new Amqp1Decoder(bytes).ReadValue());
    }

    [TestMethod]
    public void ReadValue_ListsNestedToTheLimit_AreRead()
    {
        byte[] bytes = [.. Enumerable.Repeat((byte)0x00, Amqp1Decoder.MaxNestingDepth - 1), 0x40, 0xC0, 0x02, 0x01, 0x40, .. Enumerable.Repeat((byte)0x40, Amqp1Decoder.MaxNestingDepth - 2)];

        var value = new Amqp1Decoder(bytes).ReadValue();

        Assert.IsInstanceOfType<Amqp1Described>(value);
    }

    [TestMethod]
    public void ReadValue_TwoValues_AreReadOneAfterTheOther()
    {
        var decoder = new Amqp1Decoder(Convert.FromHexString("415201"));

        var first = decoder.ReadValue();
        var second = decoder.ReadValue();

        Assert.AreEqual(true, first);
        Assert.AreEqual(1u, second);
        Assert.AreEqual(0, decoder.Remaining);
    }

    [TestMethod]
    [DataRow("005310C0020141", DisplayName = "numeric descriptor 0x10")]
    [DataRow("00A30E616D71703A6F70656E3A6C697374C0020141", DisplayName = "symbolic descriptor amqp:open:list")]
    public void ReadValue_DescribedList_ReadsTheDescriptorAndTheList(string hex)
    {
        var described = (Amqp1Described)new Amqp1Decoder(Convert.FromHexString(hex)).ReadValue()!;

        Assert.AreEqual(Amqp1Descriptor.Open, Amqp1Descriptor.CodeOf(described.Descriptor));
        CollectionAssert.AreEqual(new object?[] { true }, ((IReadOnlyList<object?>)described.Value!).ToArray());
    }
}
