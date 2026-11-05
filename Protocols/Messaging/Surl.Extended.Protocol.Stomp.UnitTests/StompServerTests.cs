using System.Net;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Stomp.UnitTests;

/// <summary>The broker's schemes, argument guards and connection refusal (ADR-0118 decision 8).</summary>
[TestClass]
public sealed class StompServerTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Constructor_NullPolicy_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new StompServer(null!));
    }

    [TestMethod]
    public void Schemes_IsStomp()
    {
        CollectionAssert.AreEqual(new[] { "stomp" }, new StompServer(new UnitTestStompAuthenticationPolicy()).Schemes.ToArray());
    }

    [TestMethod]
    public void ServeAsync_NullArgument_Throws()
    {
        var server = new StompServer(new UnitTestStompAuthenticationPolicy());
        var context = new ExchangeContext(
            1,
            new ListenUrl("stomp", "127.0.0.1", 61613).WithBoundPort(61613),
            new IPEndPoint(IPAddress.Loopback, 61613),
            new IPEndPoint(IPAddress.Loopback, 50000),
            new RecordingExchangeLog(),
            TimeProvider.System,
            CancellationToken.None);

        Assert.ThrowsExactly<ArgumentNullException>(() => server.ServeAsync(null!, context));
        Assert.ThrowsExactly<ArgumentNullException>(() => server.ServeAsync(new InMemoryConnection([]), null!));
    }

    [TestMethod]
    public async Task WriteRefusalAsync_AnyRefusal_IsTooManyConnectionsAndClosed()
    {
        var connection = new InMemoryConnection([]);
        var server = new StompServer(new UnitTestStompAuthenticationPolicy(), new StompServerOptions());

        await server.WriteRefusalAsync(connection, ConnectionRefusal.TooManyConnections, TestContext.CancellationToken);

        Assert.AreEqual("ERROR\nmessage:Too many connections\n\n\0", Encoding.UTF8.GetString(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
    }

    [TestMethod]
    public async Task WriteRefusalAsync_NullConnection_Throws()
    {
        var server = new StompServer(new UnitTestStompAuthenticationPolicy());

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => server.WriteRefusalAsync(null!, ConnectionRefusal.TooManyConnections, TestContext.CancellationToken).AsTask());
    }
}
