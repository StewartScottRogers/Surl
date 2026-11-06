namespace Surl.Protocol.Tftp;

[TestClass]
public sealed class WriteDelayingDatagramFlowTests
{
    [TestMethod]
    public async Task SendAsync_AnAckAndADataPacket_DelaysOnlyTheDataPacketAndWrapsEverythingElse()
    {
        var clock = new ManualTimeProvider();
        var inner = new ScriptedDatagramFlow([0, 1], clock, [ScriptedDatagramFlow.Datagram([0, 4, 0, 1])]);
        var flow = new WriteDelayingDatagramFlow(inner, TimeSpan.FromSeconds(1), clock);

        await flow.MoveToNewLocalPortAsync(CancellationToken.None);
        await flow.SendAsync(new byte[] { 0, 4, 0, 0 }, CancellationToken.None);
        var data = flow.SendAsync(new byte[] { 0, 3, 0, 1 }, CancellationToken.None);
        var sentBeforeTheDelay = inner.Sent.Count;
        clock.Advance(TimeSpan.FromSeconds(1));
        await data;
        var received = await flow.ReceiveAsync(CancellationToken.None);
        await flow.DisposeAsync();

        Assert.AreEqual(1, sentBeforeTheDelay);
        Assert.HasCount(2, inner.Sent);
        CollectionAssert.AreEqual(new byte[] { 0, 4, 0, 1 }, received.ToArray());
        CollectionAssert.AreEqual(new byte[] { 0, 1 }, flow.FirstDatagram.ToArray());
        Assert.AreSame(inner.LocalEndPoint, flow.LocalEndPoint);
        Assert.AreSame(inner.RemoteEndPoint, flow.RemoteEndPoint);
    }
}
