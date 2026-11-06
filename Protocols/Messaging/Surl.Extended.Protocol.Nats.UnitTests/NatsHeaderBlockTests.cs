using System.Text;
using Surl.MessageBus;

namespace Surl.Extended.Protocol.Nats.UnitTests;

/// <summary>The header block, written by hand from the headers ADR (NATS ADR-4).</summary>
[TestClass]
public sealed class NatsHeaderBlockTests
{
    [TestMethod]
    public void Parse_VersionStatusAndHeaders_GivesTheHeadersCasePreservedAndTrimmed()
    {
        var headers = NatsHeaderBlock.Parse(Encoding.UTF8.GetBytes("NATS/1.0 503 No Responders\r\nX-A:  1 \r\nx-a:2\r\n\r\n"));

        CollectionAssert.AreEqual(new[] { new BusMessageHeader("X-A", "1"), new BusMessageHeader("x-a", "2") }, headers.ToArray());
    }

    [TestMethod]
    [DataRow("NATS/1.0\r\nX-A: 1\r\n")]
    [DataRow("NATS/2.0\r\nX-A: 1\r\n\r\n")]
    [DataRow("NATS/1.0x\r\n\r\n")]
    [DataRow("NATS/1.0\r\nno colon\r\n\r\n")]
    [DataRow("NATS/1.0\r\n: empty name\r\n\r\n")]
    public void Parse_BlockThatDoesNotParse_GivesNoHeaders(string block)
    {
        Assert.IsEmpty(NatsHeaderBlock.Parse(Encoding.UTF8.GetBytes(block)));
    }

    [TestMethod]
    public void Parse_VersionOnly_GivesNoHeaders()
    {
        Assert.IsEmpty(NatsHeaderBlock.Parse("NATS/1.0\r\n\r\n"u8));
    }

    [TestMethod]
    public void Encode_HeadersItCanCarry_WritesTheBlockInOrderLeavingOutTheRest()
    {
        var block = NatsHeaderBlock.Encode([new("A", "1"), new(string.Empty, "x"), new("B:C", "x"), new("D\r", "x"), new("E\n", "x"), new("F", "x\r"), new("G", "x\n"), new("H", "2")]);

        Assert.AreEqual("NATS/1.0\r\nA: 1\r\nH: 2\r\n\r\n", Encoding.UTF8.GetString(block!));
    }

    [TestMethod]
    public void Encode_NoHeaders_IsNull()
    {
        Assert.IsNull(NatsHeaderBlock.Encode([]));
    }

    [TestMethod]
    public void NoResponders_IsTheSixteenByte503Block()
    {
        Assert.AreEqual("NATS/1.0 503\r\n\r\n", Encoding.UTF8.GetString(NatsHeaderBlock.NoResponders.Span));
        Assert.AreEqual(16, NatsHeaderBlock.NoResponders.Length);
    }
}
