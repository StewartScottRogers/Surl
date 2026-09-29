using Surl.Protocol.Abstractions;

namespace Surl.Console;

[TestClass]
public sealed class ImplicitTlsSchemeServerTests
{
    [TestMethod]
    public void Schemes_Https_IsExactlyHttps()
    {
        var server = new ImplicitTlsSchemeServer(new RecordingServer(), "https");

        CollectionAssert.AreEqual(new[] { "https" }, server.Schemes.ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_Exchange_HandsTheConnectionAndContextToTheServerItRegisters()
    {
        var inner = new RecordingServer();
        var server = new ImplicitTlsSchemeServer(inner, "https");
        await using var connection = new InMemoryConnection([]);

        await server.ServeAsync(connection, null!);

        Assert.AreSame(connection, inner.ServedConnection);
    }

    [TestMethod]
    public void Constructor_NullServer_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new ImplicitTlsSchemeServer(null!, "https"));
    }

    [TestMethod]
    public void Constructor_PlaintextScheme_ThrowsArgumentException()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new ImplicitTlsSchemeServer(new RecordingServer(), "http"));
    }

    private sealed class RecordingServer : IConnectionProtocolServer
    {
        public IConnection? ServedConnection { get; private set; }

        public IReadOnlyList<string> Schemes { get; } = ["http"];

        public Task ServeAsync(IConnection connection, ExchangeContext context)
        {
            ServedConnection = connection;
            return Task.CompletedTask;
        }
    }
}
