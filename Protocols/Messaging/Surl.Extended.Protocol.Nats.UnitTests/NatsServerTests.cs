using System.Net;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Nats.UnitTests;

[TestClass]
public sealed class NatsServerTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Constructor_NullPolicy_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new NatsServer(null!, UnitTestMessageBus.Create()));
    }

    [TestMethod]
    public void Constructor_NullBus_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new NatsServer(new UnitTestNatsAuthenticationPolicy(), null!));
    }

    [TestMethod]
    public async Task ServeAsync_BusBoundBelowMaxMessage_IsTheInfoMaxPayload()
    {
        var server = new NatsServer(new UnitTestNatsAuthenticationPolicy(), new Surl.MessageBus.MessageBus(TimeProvider.System, maxMessageBytes: 1000));
        var connection = new InMemoryConnection([], new IPEndPoint(IPAddress.Loopback, 4222));

        await server.ServeAsync(connection, Context());

        Assert.Contains("\"max_payload\":1000,", Encoding.UTF8.GetString(connection.WrittenBytes));
    }

    [TestMethod]
    public void Schemes_Default_AreNatsAndNatss()
    {
        CollectionAssert.AreEqual(new[] { "nats", "natss" }, new NatsServer(new UnitTestNatsAuthenticationPolicy(), UnitTestMessageBus.Create()).Schemes.ToArray());
    }

    [TestMethod]
    public void ServerId_TwoServers_AreEach22UpperCaseLettersAndDigitsAndDiffer()
    {
        var first = new NatsServer(new UnitTestNatsAuthenticationPolicy(), UnitTestMessageBus.Create()).ServerId;
        var second = new NatsServer(new UnitTestNatsAuthenticationPolicy(), UnitTestMessageBus.Create()).ServerId;

        Assert.MatchesRegex("^[A-Z0-9]{22}$", first);
        Assert.MatchesRegex("^[A-Z0-9]{22}$", second);
        Assert.AreNotEqual(first, second);
    }

    [TestMethod]
    public async Task ServeAsync_NullArguments_Throw()
    {
        var server = new NatsServer(new UnitTestNatsAuthenticationPolicy(), UnitTestMessageBus.Create());

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.ServeAsync(null!, Context()));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.ServeAsync(new InMemoryConnection([]), null!));
    }

    [TestMethod]
    public async Task ServeAsync_TwoConnections_SendInfoFirstWithClientIdsInAcceptOrder()
    {
        var server = new NatsServer(new UnitTestNatsAuthenticationPolicy(), UnitTestMessageBus.Create());
        var first = new InMemoryConnection([], new IPEndPoint(IPAddress.Loopback, 4222));
        var second = new InMemoryConnection([], new IPEndPoint(IPAddress.Loopback, 4222));

        await server.ServeAsync(first, Context());
        await server.ServeAsync(second, Context());

        Assert.AreEqual(Info(server.ServerId, 1, authRequired: true), Encoding.UTF8.GetString(first.WrittenBytes));
        Assert.AreEqual(Info(server.ServerId, 2, authRequired: true), Encoding.UTF8.GetString(second.WrittenBytes));
    }

    [TestMethod]
    public async Task ServeAsync_AllowsAnonymous_InfoSaysNoAuthRequired()
    {
        var server = new NatsServer(new UnitTestNatsAuthenticationPolicy(), UnitTestMessageBus.Create(), new NatsServerOptions { AllowsAnonymous = true });
        var connection = new InMemoryConnection([], new IPEndPoint(IPAddress.Loopback, 4222));

        await server.ServeAsync(connection, Context());

        Assert.AreEqual(Info(server.ServerId, 1, authRequired: false), Encoding.UTF8.GetString(connection.WrittenBytes));
    }

    [TestMethod]
    [DataRow(ConnectionRefusal.TooManyConnections)]
    [DataRow(ConnectionRefusal.TooManyConnectionsFromAddress)]
    public async Task WriteRefusalAsync_EitherLimit_IsMaximumConnectionsExceededWithNoInfoThenClosed(ConnectionRefusal refusal)
    {
        var connection = new InMemoryConnection([]);

        await new NatsServer(new UnitTestNatsAuthenticationPolicy(), UnitTestMessageBus.Create()).WriteRefusalAsync(connection, refusal, TestContext.CancellationToken);

        Assert.AreEqual("-ERR 'Maximum Connections Exceeded'\r\n", Encoding.UTF8.GetString(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
    }

    [TestMethod]
    public async Task WriteRefusalAsync_NullConnection_Throws()
    {
        var server = new NatsServer(new UnitTestNatsAuthenticationPolicy(), UnitTestMessageBus.Create());

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => server.WriteRefusalAsync(null!, ConnectionRefusal.TooManyConnections, TestContext.CancellationToken).AsTask());
    }

    // ADR-0113 decision 1, written by hand from its table.
    internal static string Info(string serverId, int clientId, bool authRequired) =>
        $"INFO {{\"server_id\":\"{serverId}\",\"server_name\":\"{serverId}\",\"version\":\"2.10.0\",\"proto\":1,"
        + $"\"host\":\"127.0.0.1\",\"port\":4222,\"headers\":true,\"max_payload\":1048576,\"client_id\":{clientId},"
        + $"\"client_ip\":\"127.0.0.1\",\"auth_required\":{(authRequired ? "true" : "false")},"
        + "\"tls_required\":false,\"tls_verify\":false,\"tls_available\":false}\r\n";

    private ExchangeContext Context() => new(
        1,
        new ListenUrl("nats", "127.0.0.1", 4222).WithBoundPort(4222),
        new IPEndPoint(IPAddress.Loopback, 4222),
        new IPEndPoint(IPAddress.Loopback, 50000),
        new RecordingExchangeLog(),
        new ManualTimeProvider(),
        TestContext.CancellationToken);
}
