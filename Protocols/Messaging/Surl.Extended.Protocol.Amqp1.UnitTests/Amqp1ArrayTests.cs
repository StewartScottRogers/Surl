namespace Surl.Extended.Protocol.Amqp1.UnitTests;

/// <summary>
/// Arrays of symbols, as a field with <c>multiple="true"</c> carries them (AMQP 1.0 Part 1 section 1.4).
/// </summary>
[TestClass]
public sealed class Amqp1ArrayTests
{
    [TestMethod]
    public void OfSymbols_ShortSymbols_AreSym8Elements()
    {
        var array = Amqp1Array.OfSymbols([new("PLAIN"), new("ANONYMOUS")]);

        Assert.AreEqual(Amqp1FormatCode.Symbol8, array.ElementFormatCode);
        Assert.IsNull(array.ElementDescriptor);
        CollectionAssert.AreEqual(new object?[] { new Amqp1Symbol("PLAIN"), new Amqp1Symbol("ANONYMOUS") }, array.Elements.ToArray());
    }

    [TestMethod]
    public void OfSymbols_ASymbolOver255Octets_AreSym32Elements()
    {
        var array = Amqp1Array.OfSymbols([new("a"), new(new string('b', 256))]);

        Assert.AreEqual(Amqp1FormatCode.Symbol32, array.ElementFormatCode);
        CollectionAssert.AreEqual(Amqp1Encoder.Encode(array), Amqp1Encoder.Encode(new Amqp1Decoder(Amqp1Encoder.Encode(array)).ReadValue()));
    }
}
