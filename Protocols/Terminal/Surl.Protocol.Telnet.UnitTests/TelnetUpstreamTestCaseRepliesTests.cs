using System.Net;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Telnet;

[TestClass]
public sealed class TelnetUpstreamTestCaseRepliesTests
{
    private const byte Iac = 255;
    private const byte Will = 251;
    private const byte Do = 253;

    // The opening negotiation of ADR-0025, without the banner.
    private static readonly byte[] OpeningWithoutBanner =
        [Iac, Will, 3, Iac, Do, 24, Iac, Do, 35, Iac, Do, 39, Iac, Do, 31];

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Constructor_NonPositiveIdlePeriod_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new TelnetUpstreamTestCaseReplies(TimeSpan.Zero));
    }

    [TestMethod]
    public void Constructor_ServerWithNullReplies_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new TelnetProtocolServer(null!));
    }

    [TestMethod]
    public void IdlePeriod_IsWhatWasGiven()
    {
        Assert.AreEqual(TimeSpan.FromSeconds(2), new TelnetUpstreamTestCaseReplies(TimeSpan.FromSeconds(2)).IdlePeriod);
    }

    [TestMethod]
    public async Task ServeAsync_ClientSendsTest1452AndHalfCloses_EchoesItVerbatimWithNoBannerAndCloses()
    {
        var replies = new TelnetUpstreamTestCaseReplies(TimeSpan.FromSeconds(30));
        var connection = new InMemoryConnection([Bytes("test1452")], peerHalfClosesWhenExhausted: true);

        await ServeAsync(connection, replies, TestContext.CancellationToken);

        CollectionAssert.AreEqual(AfterOpening("test1452"u8), connection.WrittenBytes);
        CollectionAssert.AreEqual("test1452"u8.ToArray(), replies.ReceivedBytes);
    }

    [TestMethod]
    public async Task ServeAsync_ClientSendsAnIacInData_EchoesItDoubled()
    {
        var replies = new TelnetUpstreamTestCaseReplies(TimeSpan.FromSeconds(30));
        var connection = new InMemoryConnection([new byte[] { Iac, Iac }], peerHalfClosesWhenExhausted: true);

        await ServeAsync(connection, replies, TestContext.CancellationToken);

        CollectionAssert.AreEqual(AfterOpening([Iac, Iac]), connection.WrittenBytes);
    }

    [TestMethod]
    public async Task ServeAsync_ClientGoesQuietAfterSendingWithoutHalfClosing_ClosesAfterTheIdlePeriodAndRecordsInArrivalOrder()
    {
        var replies = new TelnetUpstreamTestCaseReplies(TimeSpan.FromMilliseconds(50));
        var connection = new InMemoryConnection([Bytes("one "), Bytes("two")], peerHalfClosesWhenExhausted: false);

        await ServeAsync(connection, replies, TestContext.CancellationToken);

        CollectionAssert.AreEqual(AfterOpening("one two"u8), connection.WrittenBytes);
        CollectionAssert.AreEqual("one two"u8.ToArray(), replies.ReceivedBytes);
    }

    [TestMethod]
    public async Task ServeAsync_ClientSendsNothing_WaitsWithoutClosingForTheIdlePeriod()
    {
        var replies = new TelnetUpstreamTestCaseReplies(TimeSpan.FromMilliseconds(1));
        var connection = new InMemoryConnection([], peerHalfClosesWhenExhausted: false);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        cancellation.CancelAfter(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => ServeAsync(connection, replies, cancellation.Token));
        CollectionAssert.AreEqual(OpeningWithoutBanner, connection.WrittenBytes);
    }

    [TestMethod]
    public async Task ServeAsync_CancelledWhileWaitingForTheIdlePeriod_Throws()
    {
        var replies = new TelnetUpstreamTestCaseReplies(TimeSpan.FromHours(1));
        var connection = new InMemoryConnection([Bytes("x")], peerHalfClosesWhenExhausted: false);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        cancellation.CancelAfter(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => ServeAsync(connection, replies, cancellation.Token));
        CollectionAssert.AreEqual("x"u8.ToArray(), replies.ReceivedBytes);
    }

    [TestMethod]
    public async Task ServeAsync_WithoutTheSeam_StillSendsTheBannerAndEchoesWithCrlf()
    {
        var connection = new InMemoryConnection([Bytes("hi\r\n")], peerHalfClosesWhenExhausted: true);

        await new TelnetProtocolServer().ServeAsync(connection, Context(CancellationToken.None));

        var text = Encoding.ASCII.GetString(connection.WrittenBytes);
        StringAssert.Contains(text, "surl TELNET server");
        StringAssert.EndsWith(text, "hi\r\n");
    }

    private static byte[] AfterOpening(ReadOnlySpan<byte> echoed) => [.. OpeningWithoutBanner, .. echoed];

    private static ReadOnlyMemory<byte> Bytes(string text) => Encoding.ASCII.GetBytes(text);

    private static Task ServeAsync(InMemoryConnection connection, TelnetUpstreamTestCaseReplies replies, CancellationToken cancellationToken) =>
        new TelnetProtocolServer(replies).ServeAsync(connection, Context(cancellationToken));

    private static ExchangeContext Context(CancellationToken cancellationToken) => new(
        1,
        new ListenUrl("telnet", "127.0.0.1", 18623).WithBoundPort(18623),
        new IPEndPoint(IPAddress.Loopback, 18623),
        new IPEndPoint(IPAddress.Loopback, 50000),
        new RecordingExchangeLog(),
        TimeProvider.System,
        cancellationToken);
}
