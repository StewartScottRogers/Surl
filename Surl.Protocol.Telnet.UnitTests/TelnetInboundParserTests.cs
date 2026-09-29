namespace Surl.Protocol.Telnet;

[TestClass]
public sealed class TelnetInboundParserTests
{
    [TestMethod]
    public void Take_NegotiationOneByteAtATime_CompletesOnTheOption()
    {
        var parser = new TelnetInboundParser(8192);

        var first = parser.Take(255);
        var second = parser.Take(253);
        var third = parser.Take(24);

        Assert.AreEqual(TelnetInboundKind.Nothing, first.Kind);
        Assert.AreEqual(TelnetInboundKind.Nothing, second.Kind);
        Assert.AreEqual(new TelnetInbound(TelnetInboundKind.Negotiation, 253, 24, ReadOnlyMemory<byte>.Empty), third);
        Assert.IsFalse(parser.IsPartWayThroughCommand);
    }

    [TestMethod]
    public void Take_SubnegotiationWithIacIac_CompletesWithTheByteOnce()
    {
        var parser = new TelnetInboundParser(8192);

        var results = new byte[] { 255, 250, 24, 0, 255, 255, 255, 240 }.Select(parser.Take).ToArray();

        Assert.AreEqual(TelnetInboundKind.Subnegotiation, results[^1].Kind);
        Assert.AreEqual(24, results[^1].Option);
        CollectionAssert.AreEqual(new byte[] { 0, 255 }, results[^1].Payload.ToArray());
    }

    [TestMethod]
    public void Take_OtherCommand_IsACommand()
    {
        var parser = new TelnetInboundParser(8192);

        parser.Take(255);
        var result = parser.Take(246);

        Assert.AreEqual(new TelnetInbound(TelnetInboundKind.Command, 246, 0, ReadOnlyMemory<byte>.Empty), result);
    }

    [TestMethod]
    public void Take_SubnegotiationPastTheLimit_IsTooLong()
    {
        var parser = new TelnetInboundParser(2);

        var results = new byte[] { 255, 250, 24, 0, 1, 2 }.Select(parser.Take).ToArray();

        Assert.AreEqual(TelnetInboundKind.SubnegotiationTooLong, results[^1].Kind);
        Assert.AreEqual(24, results[^1].Option);
    }
}
