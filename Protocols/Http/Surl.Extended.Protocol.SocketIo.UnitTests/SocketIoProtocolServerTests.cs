using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.SocketIo;

/// <summary>
/// The server's schemes and its arguments (ADR-0120, decision 0).
/// </summary>
[TestClass]
public sealed class SocketIoProtocolServerTests
{
    [TestMethod]
    public void Schemes_AreSocketIoAndSocketIos()
    {
        var harness = new SocketIoServerHarness();

        CollectionAssert.AreEqual(new[] { "socketio", "socketios" }, harness.Server.Schemes.ToArray());
    }

    [TestMethod]
    public void Constructor_NullArgument_Throws()
    {
        var namespaces = SocketIoServedNamespaces.FromListenPath("/");
        var sessions = new SocketIoSessions();
        var policy = new UnitTestPasswordPolicy(false);

        Assert.ThrowsExactly<ArgumentNullException>(() => new SocketIoProtocolServer(null!, sessions, policy));
        Assert.ThrowsExactly<ArgumentNullException>(() => new SocketIoProtocolServer(namespaces, null!, policy));
        Assert.ThrowsExactly<ArgumentNullException>(() => new SocketIoProtocolServer(namespaces, sessions, null!));
    }

    [TestMethod]
    public async Task ServeAsync_NullArgument_Throws()
    {
        var harness = new SocketIoServerHarness();

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => harness.Server.ServeAsync(null!, harness.Context()));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => harness.Server.ServeAsync(new InMemoryConnection([]), null!));
    }

    [TestMethod]
    public void CreateRedactor_AuthorizationAndApiKeyFields_AreLoggedAsStars()
    {
        var redactor = new SocketIoServerHarness().Server.CreateRedactor(new ExchangeRedactionContext(1, "s"));
        var output = new System.Buffers.ArrayBufferWriter<byte>();

        redactor.Redact(System.Text.Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\nHost: h\r\nAuthorization: Bearer sekret-token\r\nX-Api-Key: sekret-key\r\n\r\n"), output);
        redactor.Flush(output);
        var logged = System.Text.Encoding.ASCII.GetString(output.WrittenSpan);

        Assert.DoesNotContain("sekret", logged);
        Assert.Contains("Authorization: Bearer ****", logged);
    }
}
