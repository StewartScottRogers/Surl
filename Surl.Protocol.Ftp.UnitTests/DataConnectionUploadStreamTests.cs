using System.Net;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Ftp.FtpTestExchange;

namespace Surl.Protocol.Ftp;

[TestClass]
public sealed class DataConnectionUploadStreamTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ReadAsync_ArrayOverload_OpensTheDataConnectionOnceAndReadsIt()
    {
        var dataConnection = new InMemoryConnection(Ascii("abc"));
        var openingReplies = 0;
        await using var stream = await StreamOverAsync(dataConnection, () => openingReplies++);
        var buffer = new byte[8];

        var first = await stream.ReadAsync(buffer, 1, 7, TestContext.CancellationToken);
        var second = await stream.ReadAsync(buffer, 0, 8, TestContext.CancellationToken);

        Assert.AreEqual(3, first);
        Assert.AreEqual((byte)'a', buffer[1]);
        Assert.AreEqual(0, second);
        Assert.AreEqual(1, openingReplies);
    }

    [TestMethod]
    public async Task DisposeAsync_BeforeAnyRead_OpensNothing()
    {
        var dataConnection = new InMemoryConnection([]);
        var stream = await StreamOverAsync(dataConnection, () => { });

        await stream.DisposeAsync();
        stream.AbortConnection();

        Assert.IsFalse(dataConnection.Disposed);
        Assert.IsFalse(dataConnection.Aborted);
    }

    [TestMethod]
    public async Task Members_ThatAConnectionCannotHonour_Throw()
    {
        await using var stream = await StreamOverAsync(new InMemoryConnection([]), () => { });

        Assert.IsTrue(stream.CanRead);
        Assert.IsFalse(stream.CanSeek);
        Assert.IsFalse(stream.CanWrite);
        Assert.IsFalse(stream.ConnectionReadFailed);
        stream.Flush();
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Length);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position = 0);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Read(new byte[1], 0, 1));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.SetLength(0));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Write(new byte[1], 0, 1));
    }

    private async Task<DataConnectionUploadStream> StreamOverAsync(IConnection dataConnection, Action onOpeningReply)
    {
        var passiveEndPoint = new IPEndPoint(IPAddress.Loopback, 50100);
        var context = Context(
            new ManualTimeProvider(),
            TestContext.CancellationToken,
            dataConnections: new InMemoryDataConnections().ScriptPassiveListener(passiveEndPoint, dataConnection));
        var dataConnections = new FtpDataConnections(new InMemoryConnection([]), context);
        await dataConnections.AnswerExtendedPassiveAsync(null);

        return new DataConnectionUploadStream(dataConnections, () =>
        {
            onOpeningReply();
            return ValueTask.CompletedTask;
        });
    }
}
