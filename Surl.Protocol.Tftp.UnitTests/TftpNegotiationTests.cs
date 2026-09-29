using System.Text;

namespace Surl.Protocol.Tftp;

[TestClass]
public sealed class TftpNegotiationTests
{
    [TestMethod]
    public void ForRead_NoOptions_UsesTheDefaultsAndSendsNoOptionAcknowledgement()
    {
        var negotiation = TftpNegotiation.ForRead([], 17);

        Assert.AreEqual(512, negotiation.BlockSize);
        Assert.AreEqual(TimeSpan.FromSeconds(5), negotiation.RetransmissionTimeout);
        Assert.IsNull(negotiation.OptionAcknowledgement);
    }

    [TestMethod]
    [DataRow("8", "8")]
    [DataRow("1024", "1024")]
    [DataRow("65464", "65464")]
    [DataRow("65465", "65464")]
    [DataRow("99999999999", "65464")]
    public void ForRead_BlockSize_IsAcceptedAndCappedAtTheRfc2348Maximum(string asked, string answered)
    {
        var negotiation = TftpNegotiation.ForRead([new TftpOption("blksize", asked)], 17);

        Assert.AreEqual(int.Parse(answered, System.Globalization.CultureInfo.InvariantCulture), negotiation.BlockSize);
        Assert.AreEqual($"blksize\0{answered}\0", Oack(negotiation));
    }

    [TestMethod]
    [DataRow("blksize", "7")]
    [DataRow("blksize", "abc")]
    [DataRow("blksize", "-9")]
    [DataRow("blksize", " 512")]
    [DataRow("timeout", "0")]
    [DataRow("timeout", "256")]
    [DataRow("windowsize", "4")]
    public void ForRead_UnacceptableOption_IsLeftOut(string name, string value)
    {
        var negotiation = TftpNegotiation.ForRead([new TftpOption(name, value)], 17);

        Assert.IsNull(negotiation.OptionAcknowledgement);
        Assert.AreEqual(512, negotiation.BlockSize);
        Assert.AreEqual(TimeSpan.FromSeconds(5), negotiation.RetransmissionTimeout);
    }

    [TestMethod]
    [DataRow("1")]
    [DataRow("255")]
    public void ForRead_Timeout_IsEchoedAndBecomesTheRetransmissionTimeout(string seconds)
    {
        var negotiation = TftpNegotiation.ForRead([new TftpOption("timeout", seconds)], 17);

        Assert.AreEqual(TimeSpan.FromSeconds(int.Parse(seconds, System.Globalization.CultureInfo.InvariantCulture)), negotiation.RetransmissionTimeout);
        Assert.AreEqual($"timeout\0{seconds}\0", Oack(negotiation));
    }

    [TestMethod]
    public void ForRead_TransferSize_IsAnsweredWithTheFileLength()
    {
        var negotiation = TftpNegotiation.ForRead([new TftpOption("tsize", "0")], 5000000000);

        Assert.AreEqual("tsize\u00005000000000\0", Oack(negotiation));
    }

    [TestMethod]
    public void ForRead_OptionNames_AreMatchedInAnyCaseAnsweredInLowerCaseInTheClientsOrder()
    {
        var negotiation = TftpNegotiation.ForRead(
            [new TftpOption("TIMEOUT", "6"), new TftpOption("windowsize", "4"), new TftpOption("BlkSize", "1024"), new TftpOption("tSize", "0")],
            17);

        Assert.AreEqual("timeout\u00006\0blksize\u00001024\0tsize\u000017\0", Oack(negotiation));
        Assert.AreEqual(1024, negotiation.BlockSize);
    }

    [TestMethod]
    public void ForRead_RepeatedOption_OnlyTheFirstCounts()
    {
        var negotiation = TftpNegotiation.ForRead([new TftpOption("blksize", "7"), new TftpOption("blksize", "1024")], 17);

        Assert.IsNull(negotiation.OptionAcknowledgement);
        Assert.AreEqual(512, negotiation.BlockSize);
    }

    private static string Oack(TftpNegotiation negotiation)
    {
        var packet = negotiation.OptionAcknowledgement!;
        Assert.AreEqual(0, packet[0]);
        Assert.AreEqual(TftpPacket.OptionAcknowledgement, packet[1]);

        return Encoding.ASCII.GetString(packet, 2, packet.Length - 2);
    }
}
