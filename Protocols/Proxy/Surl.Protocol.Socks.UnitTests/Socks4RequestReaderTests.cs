using System.Net;
using static Surl.Protocol.Socks.SocksTestKit;

namespace Surl.Protocol.Socks;

/// <summary>
/// Reading a SOCKS4 or SOCKS4a request after its version byte (ADR-0121 decisions 2 and 6).
/// </summary>
[TestClass]
public sealed class Socks4RequestReaderTests
{
    [TestMethod]
    [DataRow(new byte[] { 0, 0, 0, 1 }, true)]
    [DataRow(new byte[] { 0, 0, 0, 255 }, true)]
    [DataRow(new byte[] { 0, 0, 0, 0 }, false)]
    [DataRow(new byte[] { 0, 0, 1, 1 }, false)]
    [DataRow(new byte[] { 127, 0, 0, 1 }, false)]
    public void IsSocks4aAddress_Address_SaysWhetherAHostNameFollows(byte[] address, bool expected)
    {
        Assert.AreEqual(expected, Socks4RequestReader.IsSocks4aAddress(address));
    }

    [TestMethod]
    public async Task ReadAsync_RecordedSocks4aRequest_ReadsEveryField()
    {
        var recorded = Fixture("socks4a-host-name", "request.bin");
        var reader = new SocksClientReader(ConnectionOf(recorded[1..21]));

        var (outcome, request) = await Socks4RequestReader.ReadAsync(reader, CancellationToken.None);

        Assert.AreEqual(Socks4ReadOutcome.Read, outcome);
        Assert.AreEqual(new Socks4Request(0x01, 8080, null, string.Empty, "origin.test"), request);
    }

    [TestMethod]
    public async Task ReadAsync_RecordedSocks4Request_ReadsTheAddressAndUserId()
    {
        var recorded = Fixture("socks4-user-id", "request.bin");
        var reader = new SocksClientReader(ConnectionOf(recorded[1..14]));

        var (outcome, request) = await Socks4RequestReader.ReadAsync(reader, CancellationToken.None);

        Assert.AreEqual(Socks4ReadOutcome.Read, outcome);
        Assert.AreEqual(new Socks4Request(0x01, 8080, IPAddress.Loopback, "alice", null), request);
    }

    [TestMethod]
    public async Task ReadAsync_HostNameEndsEarly_IsTruncated()
    {
        var reader = new SocksClientReader(ConnectionOf([0x01, 0x1F, 0x90, 0, 0, 0, 1, 0x00, (byte)'o']));

        var (outcome, request) = await Socks4RequestReader.ReadAsync(reader, CancellationToken.None);

        Assert.AreEqual(Socks4ReadOutcome.Truncated, outcome);
        Assert.IsNull(request);
    }
}
