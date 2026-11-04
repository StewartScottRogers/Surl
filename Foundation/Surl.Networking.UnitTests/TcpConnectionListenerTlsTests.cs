using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Networking;

/// <summary>
/// Server-side TLS on a connection accepted from a real TCP socket on <c>127.0.0.1</c>, port 0,
/// with an <see cref="SslStream"/> client (ADR-0010). What upstream curl does against it is
/// proven by the conformance follow-up, not here.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class TcpConnectionListenerTlsTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task AcceptAsync_HttpsWithTlsSettings_UpgradesAgreesOnHttp11AndExchangesBytes()
    {
        using var certificate = ThrowawayServerCertificate.Create(TimeProvider.System, ["127.0.0.1"]);
        using var settings = new ServerTlsSettings(certificate, [], [], TimeProvider.System);
        await using var listener = await TcpConnectionListener.StartAsync(
            new ListenUrl("https", "127.0.0.1", 0), settings, TestContext.CancellationToken);
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, listener.ListenUrl.BoundPort!.Value, TestContext.CancellationToken);
        await using var connection = await listener.AcceptAsync(TestContext.CancellationToken);
        await using var clientTls = new SslStream(client.GetStream());

        var upgrade = connection.UpgradeToTlsAsync(TestContext.CancellationToken).AsTask();
        await clientTls.AuthenticateAsClientAsync(
            new SslClientAuthenticationOptions
            {
                TargetHost = "127.0.0.1",
                ApplicationProtocols = [SslApplicationProtocol.Http11],
                RemoteCertificateValidationCallback = (_, _, _, _) => true,
            },
            TestContext.CancellationToken);
        var session = await upgrade;
        await clientTls.WriteAsync(Encoding.ASCII.GetBytes("ping"), TestContext.CancellationToken);
        var received = new byte[4];
        await ReadExactlyAsync(connection, received);
        await connection.WriteAsync(Encoding.ASCII.GetBytes("pong"), TestContext.CancellationToken);
        await connection.CompleteWritesAsync(TestContext.CancellationToken);
        using var reader = new StreamReader(clientTls, Encoding.ASCII);
        var reply = await reader.ReadToEndAsync(TestContext.CancellationToken);

        Assert.AreEqual("http/1.1", session.ApplicationProtocol);
        Assert.AreEqual("ping", Encoding.ASCII.GetString(received));
        Assert.AreEqual("pong", reply);
    }

    [TestMethod]
    public async Task AcceptAsync_NoTlsSettings_RefusesAnUpgrade()
    {
        await using var listener = await TcpConnectionListener.StartAsync(
            new ListenUrl("https", "127.0.0.1", 0), TestContext.CancellationToken);
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, listener.ListenUrl.BoundPort!.Value, TestContext.CancellationToken);
        await using var connection = await listener.AcceptAsync(TestContext.CancellationToken);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => connection.UpgradeToTlsAsync(TestContext.CancellationToken).AsTask());
    }

    private async Task ReadExactlyAsync(IConnection connection, byte[] buffer)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var count = await connection.ReadAsync(buffer.AsMemory(offset), TestContext.CancellationToken);
            Assert.AreNotEqual(0, count);
            offset += count;
        }
    }
}
