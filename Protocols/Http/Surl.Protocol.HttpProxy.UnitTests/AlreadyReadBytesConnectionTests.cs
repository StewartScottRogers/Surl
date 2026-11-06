using System.Net;
using System.Net.Security;
using System.Security.Authentication;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.HttpProxy;

[TestClass]
public sealed class AlreadyReadBytesConnectionTests
{
    private static readonly TlsSession Session = new(SslProtocols.Tls12, TlsCipherSuite.TLS_ECDHE_RSA_WITH_AES_256_GCM_SHA384, "h2", null, null);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ReadAsync_BytesAlreadyRead_ReturnsThemFirstThenTheConnections()
    {
        var inner = new InMemoryConnection([(ReadOnlyMemory<byte>)"cd"u8.ToArray()]);
        var connection = new AlreadyReadBytesConnection(inner, "ab"u8.ToArray());
        var buffer = new byte[1];

        var first = await connection.ReadAsync(buffer, TestContext.CancellationToken);
        var firstByte = buffer[0];
        await connection.ReadAsync(buffer, TestContext.CancellationToken);
        var secondByte = buffer[0];
        var rest = new byte[4];
        var fromInner = await connection.ReadAsync(rest, TestContext.CancellationToken);

        Assert.AreEqual(1, first);
        Assert.AreEqual((byte)'a', firstByte);
        Assert.AreEqual((byte)'b', secondByte);
        Assert.AreEqual(2, fromInner);
        Assert.AreEqual("cd", ProxyTestKit.Latin1(rest[..2]));
    }

    [TestMethod]
    public async Task EveryOtherMember_GoesToTheConnection()
    {
        var local = new IPEndPoint(IPAddress.Loopback, 3129);
        var inner = new InMemoryConnection([], local, initialTlsSession: Session);
        var connection = new AlreadyReadBytesConnection(inner, ReadOnlyMemory<byte>.Empty);

        await connection.WriteAsync("x"u8.ToArray(), TestContext.CancellationToken);
        await connection.CompleteWritesAsync(TestContext.CancellationToken);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => connection.UpgradeToTlsAsync(TestContext.CancellationToken).AsTask());
        connection.Abort();
        await connection.DisposeAsync();

        Assert.AreEqual(local, connection.LocalEndPoint);
        Assert.AreEqual(inner.RemoteEndPoint, connection.RemoteEndPoint);
        Assert.AreSame(Session, connection.TlsSession);
        Assert.AreEqual("x", ProxyTestKit.Latin1(inner.WrittenBytes));
        Assert.IsTrue(inner.WritesCompleted);
        Assert.IsTrue(inner.Aborted);
        Assert.IsTrue(inner.Disposed);
    }
}
