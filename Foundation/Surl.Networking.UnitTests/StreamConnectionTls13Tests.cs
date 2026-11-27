using System.Net;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using Surl.Protocol.Abstractions;

namespace Surl.Networking;

/// <summary>
/// What a <see cref="StreamConnection"/> secured by the hand-built TLS 1.3 stream writes to its
/// transport when it closes its write half (ADR-0157; RFC 8446 section 6.1): a real
/// <see cref="SslStream"/> client over an in-memory pipe, and a log of the server's writes and
/// half-close in order. The TLS 1.3-only <see cref="SslStream"/> client cannot run on macOS.
/// </summary>
[TestClass]
public sealed class StreamConnectionTls13Tests
{
    // A protected alert: 2 bytes of alert, 1 of inner content type and a 16-byte AEAD tag.
    private const string ProtectedAlertRecord = "record of type 23 with 19 bytes";
    private const string ShutdownSendCalled = "ShutdownSend";

    private static readonly IPEndPoint Local = new(IPAddress.Loopback, 443);
    private static readonly IPEndPoint Remote = new(IPAddress.Loopback, 50000);
    private static readonly FixedTimeProvider Time = new(TestCertificates.Now);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.OSX)]
    public async Task CompleteWritesAsync_HandBuiltTls13Connection_SendsCloseNotifyBeforeFin()
    {
        using var certificate = TestCertificates.CreateEcdsaServerCertificate();
        using var settings = new ServerTlsSettings(certificate, [], [], Time);
        var (serverStream, clientStream) = InMemoryDuplexStream.CreatePair();
        var transport = new RecordingTransport(serverStream);
        await using var connection = CreateConnection(transport, settings);
        await using var client = await HandshakeAsync(connection, clientStream);
        transport.Events.Clear();

        await connection.CompleteWritesAsync(TestContext.CancellationToken);

        CollectionAssert.AreEqual(new[] { ProtectedAlertRecord, ShutdownSendCalled }, transport.Events);
        Assert.AreEqual(0, await client.ReadAsync(new byte[16], TestContext.CancellationToken));
        clientStream.CompleteWrites();
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.OSX)]
    public async Task CompleteWritesAsync_HandBuiltTls13ConnectionTwiceThenDisposed_WritesNoFurtherRecord()
    {
        using var certificate = TestCertificates.CreateEcdsaServerCertificate();
        using var settings = new ServerTlsSettings(certificate, [], [], Time);
        var (serverStream, clientStream) = InMemoryDuplexStream.CreatePair();
        var transport = new RecordingTransport(serverStream);
        var connection = CreateConnection(transport, settings);
        await using var client = await HandshakeAsync(connection, clientStream);
        transport.Events.Clear();

        await connection.CompleteWritesAsync(TestContext.CancellationToken);
        await connection.CompleteWritesAsync(TestContext.CancellationToken);
        clientStream.CompleteWrites();
        await connection.DisposeAsync();

        CollectionAssert.AreEqual(new[] { ProtectedAlertRecord, ShutdownSendCalled }, transport.Events);
    }

    private static StreamConnection CreateConnection(RecordingTransport transport, ServerTlsSettings settings) =>
        new(transport, Local, Remote, transport, new ServerTlsHandshake(settings, [], new FakeTls13Support(true)));

    // A client offering TLS 1.3 alone, which a platform without TLS 1.3 in SslStream serves by the hand-built stream.
    private async Task<SslStream> HandshakeAsync(StreamConnection connection, InMemoryDuplexStream clientStream)
    {
        var upgrade = connection.UpgradeToTlsAsync(TestContext.CancellationToken).AsTask();
        var client = new SslStream(clientStream, leaveInnerStreamOpen: true);
        var options = new SslClientAuthenticationOptions
        {
            TargetHost = "localhost",
            EnabledSslProtocols = SslProtocols.Tls13,
            RemoteCertificateValidationCallback = (_, _, _, _) => true,
            CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
        };

        await client.AuthenticateAsClientAsync(options, TestContext.CancellationToken);
        await upgrade;

        return client;
    }

    private sealed record FakeTls13Support(bool SslStreamLacksTls13Server) : ITls13Support;

    /// <summary>
    /// The server's transport and its control: passes everything to an in-memory pipe and logs each
    /// write, attempted ones after the half-close included, and each <see cref="ShutdownSend"/>.
    /// </summary>
    private sealed class RecordingTransport(InMemoryDuplexStream inner) : Stream, IConnectionTransportControl
    {
        public List<string> Events { get; } = [];

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public void ShutdownSend()
        {
            Events.Add(ShutdownSendCalled);
            inner.CompleteWrites();
        }

        public void ResetAndClose() => inner.Dispose();

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Events.Add(Describe(buffer.Span));

            return inner.WriteAsync(buffer, cancellationToken);
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            inner.ReadAsync(buffer, cancellationToken);

        public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        // One whole TLS record is named by its outer type and length; anything else by its byte count.
        private static string Describe(ReadOnlySpan<byte> written) =>
            written.Length >= 5 && written.Length == 5 + ((written[3] << 8) | written[4])
                ? $"record of type {written[0]} with {written.Length - 5} bytes"
                : $"{written.Length} bytes that are not one record";
    }
}
