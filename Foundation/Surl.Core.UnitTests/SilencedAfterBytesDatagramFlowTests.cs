using System.Net;
using System.Text;

namespace Surl.Core;

[TestClass]
public sealed class SilencedAfterBytesDatagramFlowTests
{
    [TestMethod]
    public async Task EveryMemberButSend_PassesThrough()
    {
        var local = new IPEndPoint(IPAddress.Loopback, 69);
        var remote = new IPEndPoint(IPAddress.Loopback, 50001);
        var inner = new FakeDatagramFlow("rrq"u8.ToArray(), local, remote);
        inner.Arrive("ack"u8.ToArray());
        var flow = new SilencedAfterBytesDatagramFlow(inner, 10, () => { });

        var endPoints = (flow.LocalEndPoint, flow.RemoteEndPoint);
        var received = await flow.ReceiveAsync(CancellationToken.None);
        await flow.MoveToNewLocalPortAsync(CancellationToken.None);
        await flow.DisposeAsync();

        Assert.AreEqual((local, remote), endPoints);
        Assert.AreEqual("rrq", Encoding.ASCII.GetString(flow.FirstDatagram.Span));
        Assert.AreEqual("ack", Encoding.ASCII.GetString(received.Span));
        Assert.AreEqual(new IPEndPoint(IPAddress.Loopback, 40001), flow.LocalEndPoint);
        Assert.IsTrue(inner.Disposed);
    }

    [TestMethod]
    public async Task SendAsync_SendsWhileWithinTheCount_ThenDiscardsEveryDatagram_AndIsSilencedOnce()
    {
        var inner = new FakeDatagramFlow([]);
        var silenced = 0;
        var flow = new SilencedAfterBytesDatagramFlow(inner, 6, () => silenced++);

        await flow.SendAsync("abc"u8.ToArray(), CancellationToken.None);
        await flow.SendAsync("def"u8.ToArray(), CancellationToken.None);
        await flow.SendAsync("g"u8.ToArray(), CancellationToken.None);
        await flow.SendAsync("h"u8.ToArray(), CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "abc", "def" }, inner.Sent.Select(sent => Encoding.ASCII.GetString(sent)).ToArray());
        Assert.AreEqual(1, silenced);
    }
}
