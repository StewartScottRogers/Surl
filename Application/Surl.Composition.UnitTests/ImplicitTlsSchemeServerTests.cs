using Surl.Protocol.Abstractions;

namespace Surl.Composition;

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

    [TestMethod]
    public void CreateRedactor_ServerThatRedacts_GivesTheEngineThatServersRedactor()
    {
        var inner = new RedactingServer();
        var server = new ImplicitTlsSchemeServer(inner, "https");
        var context = new ExchangeRedactionContext(7, "https");

        var redactor = server.CreateRedactor(context);

        Assert.AreSame(inner.Redactor, redactor);
        Assert.AreSame(context, inner.Context);
    }

    [TestMethod]
    public void CreateRedactor_ServerThatDoesNotRedact_PassesEveryByteThrough()
    {
        var redactor = new ImplicitTlsSchemeServer(new RecordingServer(), "https").CreateRedactor(new ExchangeRedactionContext(1, "https"));
        var output = new System.Buffers.ArrayBufferWriter<byte>();

        redactor.ObserveSent("sent"u8);
        redactor.Redact("abc"u8, output);
        redactor.Flush(output);

        CollectionAssert.AreEqual("abc"u8.ToArray(), output.WrittenSpan.ToArray());
    }

    private sealed class StubRedactor : IReceivedBytesRedactor
    {
        public void Redact(ReadOnlySpan<byte> received, System.Buffers.ArrayBufferWriter<byte> output)
        {
        }

        public void ObserveSent(ReadOnlySpan<byte> sent)
        {
        }

        public void Flush(System.Buffers.ArrayBufferWriter<byte> output)
        {
        }
    }

    private sealed class RedactingServer : IConnectionProtocolServer, IExchangeBytesRedactingServer
    {
        public IReceivedBytesRedactor Redactor { get; } = new StubRedactor();

        public ExchangeRedactionContext? Context { get; private set; }

        public IReadOnlyList<string> Schemes { get; } = ["http"];

        public Task ServeAsync(IConnection connection, ExchangeContext context) => Task.CompletedTask;

        public IReceivedBytesRedactor CreateRedactor(ExchangeRedactionContext context)
        {
            Context = context;
            return Redactor;
        }
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
