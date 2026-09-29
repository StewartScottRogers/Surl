using System.Buffers;

namespace Surl.Protocol.Telnet;

[TestClass]
public sealed class TelnetOptionNegotiatorTests
{
    private const byte Iac = 255;
    private const byte Will = 251;
    private const byte Wont = 252;
    private const byte Do = 253;
    private const byte Dont = 254;

    [TestMethod]
    public void WriteOpening_OffersSuppressGoAheadAndAsksForTheClientOptions()
    {
        var negotiator = new TelnetOptionNegotiator();
        var outbound = new ArrayBufferWriter<byte>();

        negotiator.WriteOpening(outbound);

        CollectionAssert.AreEqual(
            new byte[] { Iac, Will, 3, Iac, Do, 24, Iac, Do, 35, Iac, Do, 39, Iac, Do, 31 },
            outbound.WrittenSpan.ToArray());
    }

    [TestMethod]
    [DataRow(Will, (byte)24, (int)TelnetClientOptionChange.TurnedOn, new byte[0])]
    [DataRow(Wont, (byte)24, (int)TelnetClientOptionChange.TurnedOff, new byte[0])]
    [DataRow(Will, (byte)0, (int)TelnetClientOptionChange.TurnedOn, new byte[] { Iac, Do, 0 })]
    [DataRow(Wont, (byte)0, (int)TelnetClientOptionChange.None, new byte[0])]
    [DataRow(Will, (byte)5, (int)TelnetClientOptionChange.None, new byte[] { Iac, Dont, 5 })]
    [DataRow(Do, (byte)3, (int)TelnetClientOptionChange.None, new byte[0])]
    [DataRow(Do, (byte)5, (int)TelnetClientOptionChange.None, new byte[] { Iac, Wont, 5 })]
    [DataRow(Dont, (byte)3, (int)TelnetClientOptionChange.None, new byte[0])]
    public void Answer_AfterTheOpening_FollowsRfc1143(byte verb, byte option, int change, byte[] answer)
    {
        var negotiator = new TelnetOptionNegotiator();
        negotiator.WriteOpening(new ArrayBufferWriter<byte>());
        var outbound = new ArrayBufferWriter<byte>();

        var result = negotiator.Answer(verb, option, outbound);

        Assert.AreEqual((TelnetClientOptionChange)change, result);
        CollectionAssert.AreEqual(answer, outbound.WrittenSpan.ToArray());
    }
}
