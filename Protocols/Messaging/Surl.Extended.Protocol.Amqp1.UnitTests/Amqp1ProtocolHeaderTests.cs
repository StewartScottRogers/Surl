namespace Surl.Extended.Protocol.Amqp1.UnitTests;

/// <summary>
/// The protocol headers of AMQP 1.0 Part 2 section 2.2 and Part 5 sections 5.2.1 and 5.3.1,
/// written by hand from the specification.
/// </summary>
[TestClass]
public sealed class Amqp1ProtocolHeaderTests
{
    [TestMethod]
    [DataRow(Amqp1ProtocolId.Amqp, "414D515000010000")]
    [DataRow(Amqp1ProtocolId.Tls, "414D515002010000")]
    [DataRow(Amqp1ProtocolId.Sasl, "414D515003010000")]
    public void Of_EachProtocolId_IsAmqpThenTheIdThenVersion100(Amqp1ProtocolId protocolId, string expectedHex)
    {
        var header = Amqp1ProtocolHeader.Of(protocolId);

        CollectionAssert.AreEqual(Convert.FromHexString(expectedHex), header);
        Assert.AreEqual(protocolId, Amqp1ProtocolHeader.Identify(header));
    }

    [TestMethod]
    [DataRow("414D515001010000", DisplayName = "protocol id 1")]
    [DataRow("414D515004010000", DisplayName = "protocol id 4")]
    [DataRow("414D515000010100", DisplayName = "version 1.1.0")]
    [DataRow("414D515003020000", DisplayName = "version 2.0.0 SASL")]
    [DataRow("414D515000000901", DisplayName = "AMQP 0-9-1")]
    [DataRow("474554202F20485454", DisplayName = "HTTP")]
    public void Identify_AnyOtherHeader_IsNull(string hex)
    {
        var bytes = Convert.FromHexString(hex).AsSpan(0, Amqp1ProtocolHeader.Bytes);

        var protocolId = Amqp1ProtocolHeader.Identify(bytes);

        Assert.IsNull(protocolId);
    }
}
