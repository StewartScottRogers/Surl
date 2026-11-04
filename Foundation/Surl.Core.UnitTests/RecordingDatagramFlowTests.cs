using System.Net;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Core;

[TestClass]
public sealed class RecordingDatagramFlowTests
{
    [TestMethod]
    public void EndPointsAndFirstDatagram_AreTheFlows_AndTheFirstDatagramIsNotLoggedHere()
    {
        var local = new IPEndPoint(IPAddress.IPv6Loopback, 69);
        var remote = new IPEndPoint(IPAddress.IPv6Loopback, 50001);
        var log = new RecordingExchangeLog();
        var recording = new RecordingDatagramFlow(new FakeDatagramFlow("rrq"u8.ToArray(), local, remote), log);

        Assert.AreEqual(local, recording.LocalEndPoint);
        Assert.AreEqual(remote, recording.RemoteEndPoint);
        Assert.AreEqual("rrq", Encoding.ASCII.GetString(recording.FirstDatagram.Span));
        Assert.IsEmpty(log.Entries);
    }

    [TestMethod]
    public async Task ReceiveAsync_AndSendAsync_LogEachDatagram()
    {
        var log = new RecordingExchangeLog();
        var flow = new FakeDatagramFlow([]);
        flow.Arrive("ack"u8.ToArray());
        var recording = new RecordingDatagramFlow(flow, log);

        await recording.SendAsync("data"u8.ToArray(), CancellationToken.None);
        var received = await recording.ReceiveAsync(CancellationToken.None);

        Assert.AreEqual("ack", Encoding.ASCII.GetString(received.Span));
        Assert.AreEqual("data", Encoding.ASCII.GetString(flow.Sent.Single()));
        var entries = log.Entries.Select(entry => $"{entry.Kind}:{Encoding.ASCII.GetString(entry.Bytes)}").ToArray();
        CollectionAssert.AreEqual(new[] { "BytesSent:data", "BytesReceived:ack" }, entries);
    }

    [TestMethod]
    public async Task MoveToNewLocalPortAsync_AndDisposeAsync_ReachTheFlow()
    {
        var flow = new FakeDatagramFlow([]);
        var recording = new RecordingDatagramFlow(flow, new RecordingExchangeLog());

        await recording.MoveToNewLocalPortAsync(CancellationToken.None);
        Assert.AreEqual(new IPEndPoint(IPAddress.Loopback, 40001), recording.LocalEndPoint);

        await recording.DisposeAsync();
        Assert.IsTrue(flow.Disposed);
    }
}
