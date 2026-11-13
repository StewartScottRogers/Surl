using System.Net;
using System.Net.Sockets;
using System.Security.Authentication;
using Surl.Authentication;
using Surl.Networking;

namespace Surl.Composition;

/// <summary>
/// <see cref="TlsClientLdapDirectoryConnector"/> over loopback sockets (ADR-0149 decision 11; BL-673): the clear
/// connection, an unreachable directory as an <see cref="IOException"/> and a refused handshake as an
/// <see cref="AuthenticationException"/>. They open sockets, so they are integration tests.
/// </summary>
[TestClass]
public sealed class TlsClientLdapDirectoryConnectorTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [TestCategory("Integration")]
    public async Task ConnectAsync_AClearDirectory_GivesItsStream()
    {
        using var listener = Listen(out var port);
        var accepting = listener.AcceptTcpClientAsync(TestContext.CancellationToken).AsTask();

        await using var connection = await Connector().ConnectAsync(new LdapDirectoryEndpoint("127.0.0.1", port, UseTls: false), TestContext.CancellationToken);
        using var server = await accepting;
        await connection.Stream.WriteAsync(new byte[] { 7 }, TestContext.CancellationToken);
        var received = new byte[1];
        await server.GetStream().ReadExactlyAsync(received, TestContext.CancellationToken);

        Assert.AreEqual(7, received[0]);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task ConnectAsync_NothingListening_IsAnIOException()
    {
        using var listener = Listen(out var port);
        listener.Stop();

        await Assert.ThrowsAsync<IOException>(
            async () => await Connector().ConnectAsync(new LdapDirectoryEndpoint("127.0.0.1", port, UseTls: false), TestContext.CancellationToken));
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task ConnectAsync_ADirectoryThatRefusesTheHandshake_IsAnAuthenticationException()
    {
        using var listener = Listen(out var port);
        var closing = CloseNextConnectionAsync(listener);

        await Assert.ThrowsExactlyAsync<AuthenticationException>(
            async () => await Connector().ConnectAsync(new LdapDirectoryEndpoint("127.0.0.1", port, UseTls: true), TestContext.CancellationToken));
        await closing;
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task StartTlsAsync_ADirectoryThatRefusesTheHandshake_IsAnAuthenticationException()
    {
        using var listener = Listen(out var port);
        var closing = CloseNextConnectionAsync(listener);
        await using var connection = await Connector().ConnectAsync(new LdapDirectoryEndpoint("127.0.0.1", port, UseTls: false), TestContext.CancellationToken);

        await Assert.ThrowsExactlyAsync<AuthenticationException>(async () => await connection.StartTlsAsync(TestContext.CancellationToken));
        await closing;
    }

    [TestMethod]
    public async Task ConnectAsync_NullEndpoint_Throws() =>
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            async () => await Connector().ConnectAsync(null!, TestContext.CancellationToken));

    private static TlsClientLdapDirectoryConnector Connector() =>
        new(new TlsClientConnector([]), TimeSpan.FromSeconds(10));

    private static TcpListener Listen(out int port)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        port = ((IPEndPoint)listener.LocalEndpoint).Port;
        return listener;
    }

    private async Task CloseNextConnectionAsync(TcpListener listener)
    {
        using var client = await listener.AcceptTcpClientAsync(TestContext.CancellationToken);
    }
}
