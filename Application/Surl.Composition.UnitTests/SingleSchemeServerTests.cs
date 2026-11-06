using Surl.Protocol.Abstractions;

namespace Surl.Composition;

[TestClass]
public sealed class SingleSchemeServerTests
{
    [TestMethod]
    public void Schemes_OneOfTheServersSchemes_IsExactlyThatScheme()
    {
        var server = new SingleSchemeServer(new RecordingServer(), "sftp");

        CollectionAssert.AreEqual(new[] { "sftp" }, server.Schemes.ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_Exchange_HandsTheConnectionToTheServerItRegisters()
    {
        var inner = new RecordingServer();
        var server = new SingleSchemeServer(inner, "scp");
        await using var connection = new InMemoryConnection([]);

        await server.ServeAsync(connection, null!);

        Assert.AreSame(connection, inner.ServedConnection);
    }

    [TestMethod]
    public void Constructor_NullArgument_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new SingleSchemeServer(null!, "scp"));
        Assert.ThrowsExactly<ArgumentNullException>(() => new SingleSchemeServer(new RecordingServer(), null!));
    }

    [TestMethod]
    public void Constructor_ASchemeTheServerDoesNotAnswer_ThrowsArgumentException() =>
        Assert.ThrowsExactly<ArgumentException>(() => new SingleSchemeServer(new RecordingServer(), "ftp"));

    private sealed class RecordingServer : IConnectionProtocolServer
    {
        public IConnection? ServedConnection { get; private set; }

        public IReadOnlyList<string> Schemes { get; } = ["scp", "sftp"];

        public Task ServeAsync(IConnection connection, ExchangeContext context)
        {
            ServedConnection = connection;
            return Task.CompletedTask;
        }
    }
}
