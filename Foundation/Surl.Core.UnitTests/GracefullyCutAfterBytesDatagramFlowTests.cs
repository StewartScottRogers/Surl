using System.Net;
using System.Text;

namespace Surl.Core;

[TestClass]
public sealed class GracefullyCutAfterBytesDatagramFlowTests
{
    [TestMethod]
    public async Task EveryMemberButTheSends_PassesThrough()
    {
        var local = new IPEndPoint(IPAddress.Loopback, 443);
        var remote = new IPEndPoint(IPAddress.Loopback, 50001);
        var inner = new FakeDatagramFlow("initial"u8.ToArray(), local, remote);
        inner.Arrive("ack"u8.ToArray());
        var flow = new GracefullyCutAfterBytesDatagramFlow(inner, 10, () => { });

        var endPoints = (flow.LocalEndPoint, flow.RemoteEndPoint);
        var received = await flow.ReceiveAsync(CancellationToken.None);
        await flow.MoveToNewLocalPortAsync(CancellationToken.None);
        await flow.DisposeAsync();

        Assert.AreEqual((local, remote), endPoints);
        Assert.AreEqual("initial", Encoding.ASCII.GetString(flow.FirstDatagram.Span));
        Assert.AreEqual("ack", Encoding.ASCII.GetString(received.Span));
        Assert.AreEqual(new IPEndPoint(IPAddress.Loopback, 40001), flow.LocalEndPoint);
        Assert.IsTrue(inner.Disposed);
    }

    [TestMethod]
    public async Task SendAsync_PastTheCount_CutsOnceAndDiscards_WhileClosingDatagramsAreStillSent()
    {
        var inner = new FakeDatagramFlow([]);
        var cuts = 0;
        var flow = new GracefullyCutAfterBytesDatagramFlow(inner, 3, () => cuts++);

        await flow.SendAsync("abc"u8.ToArray(), CancellationToken.None);
        var cutAtTheCount = flow.IsCut;
        await flow.SendAsync("d"u8.ToArray(), CancellationToken.None);
        await flow.SendClosingDatagramAsync("close"u8.ToArray(), CancellationToken.None);
        await flow.SendAsync("e"u8.ToArray(), CancellationToken.None);

        Assert.IsFalse(cutAtTheCount);
        Assert.IsTrue(flow.IsCut);
        Assert.AreEqual(1, cuts);
        CollectionAssert.AreEqual(new[] { "abc", "close" }, inner.Sent.Select(sent => Encoding.ASCII.GetString(sent)).ToArray());
    }
}
