namespace Surl.Extended.Protocol.Amqp1.UnitTests;

/// <summary>
/// Descriptors read by code and by name (AMQP 1.0 Part 1 section 1.5).
/// </summary>
[TestClass]
public sealed class Amqp1DescriptorTests
{
    [TestMethod]
    [DataRow("amqp:open:list", Amqp1Descriptor.Open)]
    [DataRow("amqp:sasl-outcome:list", Amqp1Descriptor.SaslOutcome)]
    [DataRow("amqp:amqp-value:*", Amqp1Descriptor.AmqpValue)]
    public void CodeOf_KnownSymbol_IsItsCode(string name, ulong expected)
    {
        Assert.AreEqual(expected, Amqp1Descriptor.CodeOf(new Amqp1Symbol(name)));
    }

    [TestMethod]
    public void CodeOf_Ulong_IsItself()
    {
        Assert.AreEqual(0x1234ul, Amqp1Descriptor.CodeOf(0x1234ul));
    }

    [TestMethod]
    public void CodeOf_UnknownSymbolOrOtherType_IsNull()
    {
        Assert.IsNull(Amqp1Descriptor.CodeOf(new Amqp1Symbol("amqp:nothing:list")));
        Assert.IsNull(Amqp1Descriptor.CodeOf("amqp:open:list"));
        Assert.IsNull(Amqp1Descriptor.CodeOf(null));
    }
}
