using System.Net;
using Surl.Protocol.Abstractions;
using static Surl.Extended.Protocol.Mqtt5.UnitTests.Mqtt5BrokerExchange;
using static Surl.Extended.Protocol.Mqtt5.UnitTests.Mqtt5WebSocketExchange;

namespace Surl.Extended.Protocol.Mqtt5.UnitTests;

/// <summary>
/// The framed connection the broker reads and writes on <c>mqtt5+ws</c> (BL-500; ADR-0093
/// decision 7): what it passes through to the transport and what it refuses.
/// </summary>
[TestClass]
public sealed class Mqtt5WebSocketConnectionTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Members_PassTheTransportsEndpointsAndTlsSessionThrough()
    {
        var local = new IPEndPoint(IPAddress.Loopback, 8083);
        var remote = new IPEndPoint(IPAddress.Loopback, 50001);
        var transport = new InMemoryConnection([], local, remote);

        var framed = Framed(transport);

        Assert.AreSame(local, framed.LocalEndPoint);
        Assert.AreSame(remote, framed.RemoteEndPoint);
        Assert.IsNull(framed.TlsSession);
    }

    [TestMethod]
    public void Abort_AbortsTheTransport()
    {
        var transport = new InMemoryConnection([]);

        Framed(transport).Abort();

        Assert.IsTrue(transport.Aborted);
    }

    [TestMethod]
    public async Task UpgradeToTlsAsync_Throws()
    {
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () => await Framed(new InMemoryConnection([])).UpgradeToTlsAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task DisposeAsync_LeavesTheTransportToTheEngine()
    {
        var transport = new InMemoryConnection([]);

        await Framed(transport).DisposeAsync();

        Assert.IsFalse(transport.Disposed);
    }

    [TestMethod]
    public async Task ReadAsync_EmptyBuffer_Throws()
    {
        await Assert.ThrowsExactlyAsync<ArgumentException>(async () => await Framed(new InMemoryConnection([])).ReadAsync(Memory<byte>.Empty, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ReadAsync_SmallBuffer_HandsAFramesPayloadOverInPieces()
    {
        var framed = Framed(new InMemoryConnection([BinaryFrame(1, 2, 3)]));
        var buffer = new byte[2];

        var first = await framed.ReadAsync(buffer, TestContext.CancellationToken);
        var firstBytes = buffer[..first];
        var second = await framed.ReadAsync(buffer, TestContext.CancellationToken);

        CollectionAssert.AreEqual(new byte[] { 1, 2 }, firstBytes);
        Assert.AreEqual(1, second);
        Assert.AreEqual(3, buffer[0]);
    }

    [TestMethod]
    public async Task WriteAsync_AfterACloseWasSent_WritesNothing()
    {
        var transport = new InMemoryConnection([ClientFrame(Text, [])]);
        var framed = Framed(transport);
        var read = await framed.ReadAsync(new byte[1], TestContext.CancellationToken);

        await framed.WriteAsync(new byte[] { 0xD0, 0x00 }, TestContext.CancellationToken);
        await framed.CompleteWritesAsync(TestContext.CancellationToken);

        Assert.AreEqual(0, read);
        Assert.IsTrue(framed.CloseSent);
        CollectionAssert.AreEqual(ServerClose(1003), transport.WrittenBytes);
        Assert.IsTrue(transport.WritesCompleted);
        Assert.AreEqual(0, await framed.ReadAsync(new byte[1], TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task WriteAsync_SendsOneUnmaskedBinaryFrame()
    {
        var transport = new InMemoryConnection([]);

        await Framed(transport).WriteAsync(new byte[] { 0xD0, 0x00 }, TestContext.CancellationToken);

        CollectionAssert.AreEqual(new byte[] { 0x82, 0x02, 0xD0, 0x00 }, transport.WrittenBytes);
    }

    private Mqtt5WebSocketConnection Framed(InMemoryConnection transport) =>
        new(transport, transport.ReadAsync, Context(new ManualTimeProvider(), TestContext.CancellationToken));
}
