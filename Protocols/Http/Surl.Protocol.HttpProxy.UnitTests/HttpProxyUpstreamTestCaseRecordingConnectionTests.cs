using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.HttpProxy;

[TestClass]
public sealed class HttpProxyUpstreamTestCaseRecordingConnectionTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ReadAsync_RecordsWhatWasReadAndWrapsEverythingElse()
    {
        var local = new IPEndPoint(IPAddress.Loopback, 1);
        var remote = new IPEndPoint(IPAddress.Loopback, 2);
        var inner = new InMemoryConnection([new byte[] { 1, 2, 3 }], local, remote);
        var replies = new HttpProxyUpstreamTestCaseReplies(new Dictionary<string, byte[]>(), []);
        var connection = new HttpProxyUpstreamTestCaseRecordingConnection(inner, replies);
        var buffer = new byte[8];

        var read = await connection.ReadAsync(buffer, TestContext.CancellationToken);
        await connection.WriteAsync(new byte[] { 9 }, TestContext.CancellationToken);
        var tls = await connection.UpgradeToTlsAsync(TestContext.CancellationToken);
        await connection.CompleteWritesAsync(TestContext.CancellationToken);
        connection.Abort();
        await connection.DisposeAsync();

        Assert.AreEqual(3, read);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, replies.ReceivedBytes);
        CollectionAssert.AreEqual(new byte[] { 9 }, inner.WrittenBytes);
        Assert.AreSame(local, connection.LocalEndPoint);
        Assert.AreSame(remote, connection.RemoteEndPoint);
        Assert.AreSame(tls, connection.TlsSession);
        Assert.IsTrue(inner.WritesCompleted);
        Assert.IsTrue(inner.Aborted);
        Assert.IsTrue(inner.Disposed);
    }

    [TestMethod]
    public async Task ReadAsync_AfterStopRecording_RecordsNothingMore()
    {
        var inner = new InMemoryConnection([new byte[] { 1, 2 }, new byte[] { 3, 4 }]);
        var replies = new HttpProxyUpstreamTestCaseReplies(new Dictionary<string, byte[]>(), []);
        var connection = new HttpProxyUpstreamTestCaseRecordingConnection(inner, replies);
        var buffer = new byte[2];

        await connection.ReadAsync(buffer, TestContext.CancellationToken);
        connection.StopRecording();
        var read = await connection.ReadAsync(buffer, TestContext.CancellationToken);

        Assert.AreEqual(2, read);
        CollectionAssert.AreEqual(new byte[] { 1, 2 }, replies.ReceivedBytes);
    }
}
