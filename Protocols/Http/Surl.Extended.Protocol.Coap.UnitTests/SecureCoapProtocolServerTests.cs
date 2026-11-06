using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Surl.Dtls;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Coap;

/// <summary>
/// The <c>coaps</c> server (RFC 7252 section 9.1; ADR-0097 decisions 0, 4, 5 and 6; ADR-0087)
/// driven by <see cref="UnitTestDtlsClient"/>, a DTLS 1.2 client written from RFC 6347, carrying
/// CoAP messages written by hand from RFC 7252 section 3 as application data.
/// </summary>
[TestClass]
public sealed class SecureCoapProtocolServerTests
{
    private static readonly TimeSpan PatienceLimit = TimeSpan.FromSeconds(10);

    private static readonly CoapAccessRules Anonymous = new(AllowsAnonymous: true, HasAccounts: false);

    private static readonly CoapAccessRules Private = new(AllowsAnonymous: false, HasAccounts: true);

    // RFC 7252 section 3: CON GET, Message ID 0x0102, token 0x7A, Uri-Path "a.txt" (delta 11, length 5).
    private static readonly byte[] GetATxt = [0x41, 0x01, 0x01, 0x02, 0x7A, 0xB5, (byte)'a', (byte)'.', (byte)'t', (byte)'x', (byte)'t'];

    private static readonly CoapDtlsCredentials AliceOnly = new(Certificate: null, PreSharedKeys: Lookup);

    [TestMethod]
    public void Schemes_IsCoaps() =>
        CollectionAssert.AreEqual(
            new[] { "coaps" },
            new SecureCoapProtocolServer(new CoapTestStore().Store, Anonymous, new CoapObservations(), AliceOnly).Schemes.ToArray());

    [TestMethod]
    public async Task Constructor_And_ServeAsync_NullArgumentsOrNoCredential_Throw()
    {
        var store = new CoapTestStore().Store;
        var observations = new CoapObservations();
        var server = new SecureCoapProtocolServer(store, Anonymous, observations, AliceOnly);
        await using var flow = new InMemoryDatagramFlow(new byte[] { 0x16 });

        Assert.ThrowsExactly<ArgumentNullException>(() => new SecureCoapProtocolServer(null!, Anonymous, observations, AliceOnly));
        Assert.ThrowsExactly<ArgumentNullException>(() => new SecureCoapProtocolServer(store, null!, observations, AliceOnly));
        Assert.ThrowsExactly<ArgumentNullException>(() => new SecureCoapProtocolServer(store, Anonymous, null!, AliceOnly));
        Assert.ThrowsExactly<ArgumentNullException>(() => new SecureCoapProtocolServer(store, Anonymous, observations, null!));
        Assert.ThrowsExactly<ArgumentException>(() => new SecureCoapProtocolServer(store, Anonymous, observations, new CoapDtlsCredentials(null, null)));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.ServeAsync(null!, Context(new RecordingExchangeLog(), new ManualTimeProvider(), default)));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.ServeAsync(flow, null!));
    }

    [TestMethod]
    public async Task ServeAsync_PskHandshakeThenGet_IsAnswered205AsAnAuthenticatedPeerAndNotesThePskLogin()
    {
        var store = new CoapTestStore().WithFile("a.txt", "hello");
        var server = new SecureCoapProtocolServer(store.Store, Private, new CoapObservations(), AliceOnly);
        await using var served = Served.Start(server, new UnitTestDtlsClient { CipherSuites = [0x00A8] });

        await served.WaitUntilEstablishedAsync();
        served.Flow.SendCoap(GetATxt);
        var response = await served.ReceiveCoapAsync(1);

        Assert.AreEqual(CoapMessageType.Acknowledgement, response.Type);
        Assert.AreEqual(CoapCode.Content, response.Code);
        Assert.AreEqual((ushort)0x0102, response.MessageId);
        CollectionAssert.AreEqual(new byte[] { 0x7A }, response.Token.ToArray());
        Assert.AreEqual("hello", Encoding.UTF8.GetString(response.Payload.Span));
        CollectionAssert.Contains(served.Log.Notes.ToList(), "Login accepted: psk alice");
    }

    [TestMethod]
    public async Task ServeAsync_CertificateHandshakeThenBlockwiseGet_IsAnsweredIn1024ByteBlocks()
    {
        var body = CoapTestExchange.Body(2500);
        var store = new CoapTestStore().WithFile("a.txt", body);
        await using var served = Served.Start(CertificateServer(store, Anonymous), new UnitTestDtlsClient());

        await served.WaitUntilEstablishedAsync();
        var blocks = new List<CoapMessage>();
        for (uint number = 0; number < 3; number++)
        {
            served.Flow.SendCoap(Get((ushort)(0x0200 + number), "a.txt", CoapTestExchange.Block(CoapOptionNumber.Block2, $"{number}/0/1024")));
            blocks.Add(await served.ReceiveCoapAsync((int)number + 1));
        }

        Assert.AreEqual(DtlsCipherSuite.AllSuitesInServerOrder[0].Code, served.Flow.Client.ServerHello!.CipherSuite);
        CollectionAssert.AreEqual(
            new[] { "0/1/1024", "1/1/1024", "2/0/1024" },
            blocks.Select(block => BlockOf(block, CoapOptionNumber.Block2)).ToArray());
        Assert.AreEqual(2500u, UIntOf(blocks[0], CoapOptionNumber.Size2));
        Assert.AreEqual(body, string.Concat(blocks.Select(block => Encoding.UTF8.GetString(block.Payload.Span))));
    }

    [TestMethod]
    public async Task ServeAsync_ClientMaxFragmentLength512_ShrinksBlocksTo256Bytes()
    {
        var store = new CoapTestStore().WithFile("a.txt", CoapTestExchange.Body(600));
        await using var served = Served.Start(CertificateServer(store, Anonymous), new UnitTestDtlsClient { MaxFragmentLength = [1] });

        await served.WaitUntilEstablishedAsync();
        served.Flow.SendCoap(GetATxt);
        var response = await served.ReceiveCoapAsync(1);

        Assert.AreEqual("0/1/256", BlockOf(response, CoapOptionNumber.Block2));
        Assert.AreEqual(256, response.Payload.Length);
    }

    [TestMethod]
    public async Task ServeAsync_CertificateHandshakeWithoutClientCertificate_IsUnauthenticatedAndRefused401OnAPrivateServer()
    {
        var store = new CoapTestStore().WithFile("a.txt", "hello");
        await using var served = Served.Start(CertificateServer(store, Private), new UnitTestDtlsClient());

        await served.WaitUntilEstablishedAsync();
        served.Flow.SendCoap(GetATxt);
        var response = await served.ReceiveCoapAsync(1);

        Assert.AreEqual(CoapCode.Unauthorized, response.Code);
        Assert.IsFalse(served.Log.Notes.Any(note => note.StartsWith("Login accepted", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ServeAsync_VerifiedClientCertificate_IsAuthenticatedAndServedOnAPrivateServer()
    {
        using var clientKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var store = new CoapTestStore().WithFile("a.txt", "hello");
        var credentials = new CoapDtlsCredentials(UnitTestCertificates.EcdsaP256, null, [UnitTestCertificates.ClientAuthority]);
        var server = new SecureCoapProtocolServer(store.Store, Private, new CoapObservations(), credentials, new UnitTestCoapRandomSource(), cookieSecrets: null);
        var client = new UnitTestDtlsClient
        {
            ClientCertificateChain = [UnitTestCertificates.IssueClientCertificate(clientKey)],
            ClientCertificateKey = clientKey,
        };
        await using var served = Served.Start(server, client);

        await served.WaitUntilEstablishedAsync();
        served.Flow.SendCoap(GetATxt);
        var response = await served.ReceiveCoapAsync(1);

        Assert.AreEqual(CoapCode.Content, response.Code);
        Assert.AreEqual("hello", Encoding.UTF8.GetString(response.Payload.Span));
    }

    [TestMethod]
    public async Task ServeAsync_ObservationOverDtls_IsNotifiedOfAPutOverPlainCoap()
    {
        var store = new CoapTestStore(CoapTestStore.Uploads).WithFile("a.txt", "one");
        var observations = new CoapObservations();
        var server = new SecureCoapProtocolServer(store.Store, Anonymous, observations, AliceOnly, new UnitTestCoapRandomSource(), cookieSecrets: null);
        await using var served = Served.Start(server, new UnitTestDtlsClient { CipherSuites = [0x00A8] });
        await served.WaitUntilEstablishedAsync();
        served.Flow.SendCoap(Get(0x0301, "a.txt", CoapOption.FromUInt(CoapOptionNumber.Observe, 0)));
        Assert.AreEqual(1u, UIntOf(await served.ReceiveCoapAsync(1), CoapOptionNumber.Observe));
        var plain = new CoapProtocolServer(store.Store, Anonymous, observations);
        using var plainExchange = new CancellationTokenSource();
        await using var plainFlow = new InMemoryDatagramFlow(CoapMessageWriter.Write(
            new CoapMessage(CoapMessageType.Confirmable, CoapCode.Put, 0x0401, new byte[] { 0x41 }, [CoapOption.FromString(CoapOptionNumber.UriPath, "a.txt")], "two"u8.ToArray())));
        var plainServing = plain.ServeAsync(plainFlow, Context(new RecordingExchangeLog(), served.Clock, plainExchange.Token));

        var notification = await served.ReceiveCoapAsync(2);
        // RFC 7252 section 3: an Empty ACK echoing the notification's Message ID.
        served.Flow.SendCoap([0x60, 0x00, (byte)(notification.MessageId >> 8), (byte)notification.MessageId]);

        Assert.AreEqual(CoapMessageType.Confirmable, notification.Type);
        Assert.AreEqual(CoapCode.Content, notification.Code);
        Assert.AreEqual(2u, UIntOf(notification, CoapOptionNumber.Observe));
        Assert.AreEqual("two", Encoding.UTF8.GetString(notification.Payload.Span));
        await plainExchange.CancelAsync();
        await Assert.ThrowsAsync<OperationCanceledException>(() => plainServing);
    }

    [TestMethod]
    public async Task ServeAsync_PlaintextCoapBeforeTheHandshakeFinishes_IsIgnored()
    {
        var store = new CoapTestStore().WithFile("a.txt", "hello");
        UnitTestDtlsClient client = new() { CipherSuites = [0x00A8], BeforeFinalFlight = _ => [GetATxt] };
        var server = new SecureCoapProtocolServer(store.Store, Anonymous, new CoapObservations(), AliceOnly, new UnitTestCoapRandomSource(), cookieSecrets: null);
        await using var served = Served.Start(server, client, firstDatagram: GetATxt);

        await served.WaitUntilEstablishedAsync();
        served.Flow.SendCoap(Get(0x0501, "a.txt"));
        var response = await served.ReceiveCoapAsync(1);

        Assert.AreEqual((ushort)0x0501, response.MessageId);
        Assert.HasCount(1, served.Flow.ReceivedCoap);
        Assert.IsTrue(
            served.Flow.Sent.All(datagram => datagram[0] is >= 20 and <= 23 && datagram[1] == 0xFE),
            "Every datagram the server sent is a DTLS record, none a CoAP message.");
    }

    [TestMethod]
    public async Task ServeAsync_UnknownPskIdentity_IsRefusedWithDecryptErrorAndServesNothing()
    {
        var store = new CoapTestStore().WithFile("a.txt", "hello");
        var server = new SecureCoapProtocolServer(store.Store, Anonymous, new CoapObservations(), AliceOnly, new UnitTestCoapRandomSource(), cookieSecrets: null);
        await using var served = Served.Start(server, new UnitTestDtlsClient { CipherSuites = [0x00A8], PskIdentity = "mallory"u8.ToArray() });

        await served.Serving;

        Assert.IsFalse(served.Flow.IsEstablished);
        Assert.AreEqual(new DtlsAlert(IsFatal: true, DtlsAlertDescription.DecryptError), served.Flow.ReceivedAlerts.Single());
        CollectionAssert.Contains(served.Log.Notes.ToList(), "The DTLS handshake ended: AlertSent (DecryptError).");
        Assert.IsEmpty(served.Flow.ReceivedCoap);
    }

    [TestMethod]
    public async Task ServeAsync_HandshakePastTheHeadTimeout_EndsWithNoAlert()
    {
        var server = new SecureCoapProtocolServer(new CoapTestStore().Store, Anonymous, new CoapObservations(), AliceOnly, new UnitTestCoapRandomSource(), cookieSecrets: null);
        await using var served = Served.Start(server, new UnitTestDtlsClient { CipherSuites = [0x00A8], SendsFinalFlight = false });

        await WaitUntilAsync(() => served.Clock.PendingTimers >= 1, "the handshake timers");
        served.Clock.Advance(ExchangeLimits.Default.HeadTimeout + TimeSpan.FromSeconds(1));
        await served.Serving;

        CollectionAssert.Contains(served.Log.Notes.ToList(), "The DTLS handshake ended: TimedOut.");
        Assert.IsEmpty(served.Flow.ReceivedAlerts);
    }

    [TestMethod]
    public async Task ServeAsync_CloseNotifyBeforeAnyCoap_EndsTheFlow()
    {
        var server = new SecureCoapProtocolServer(new CoapTestStore().Store, Anonymous, new CoapObservations(), AliceOnly, new UnitTestCoapRandomSource(), cookieSecrets: null);
        await using var served = Served.Start(server, new UnitTestDtlsClient { CipherSuites = [0x00A8] });

        await served.WaitUntilEstablishedAsync();
        served.Flow.SendProtected(DtlsContentType.Alert, [1, 0]);
        await served.Serving;

        CollectionAssert.Contains(served.Log.Notes.ToList(), "The client ended the DTLS association.");
        Assert.IsEmpty(served.Flow.ReceivedCoap);
    }

    [TestMethod]
    public async Task ServeAsync_CloseNotifyAfterAGet_EndsTheFlow()
    {
        var store = new CoapTestStore().WithFile("a.txt", "hello");
        var server = new SecureCoapProtocolServer(store.Store, Anonymous, new CoapObservations(), AliceOnly, new UnitTestCoapRandomSource(), cookieSecrets: null);
        await using var served = Served.Start(server, new UnitTestDtlsClient { CipherSuites = [0x00A8] });
        await served.WaitUntilEstablishedAsync();
        served.Flow.SendCoap(GetATxt);
        await served.ReceiveCoapAsync(1);

        served.Flow.SendProtected(DtlsContentType.Alert, [1, 0]);
        await served.Serving;

        CollectionAssert.Contains(served.Log.Notes.ToList(), "The client ended the DTLS association.");
    }

    [TestMethod]
    public async Task ServeAsync_CancelledForALimit_SendsAWarningCloseNotify()
    {
        var store = new CoapTestStore().WithFile("a.txt", "hello");
        var server = new SecureCoapProtocolServer(store.Store, Anonymous, new CoapObservations(), AliceOnly, new UnitTestCoapRandomSource(), cookieSecrets: null);
        await using var served = Served.Start(server, new UnitTestDtlsClient { CipherSuites = [0x00A8] });
        await served.WaitUntilEstablishedAsync();
        served.Flow.SendCoap(GetATxt);
        await served.ReceiveCoapAsync(1);

        await served.Exchange.CancelAsync();
        await Assert.ThrowsAsync<OperationCanceledException>(() => served.Serving);

        Assert.AreEqual(new DtlsAlert(IsFatal: false, DtlsAlertDescription.CloseNotify), served.Flow.ReceivedAlerts.Single());
    }

    [TestMethod]
    public async Task ServeAsync_CancelledAtShutdown_SendsNoAlert()
    {
        var store = new CoapTestStore().WithFile("a.txt", "hello");
        var server = new SecureCoapProtocolServer(store.Store, Anonymous, new CoapObservations(), AliceOnly, new UnitTestCoapRandomSource(), cookieSecrets: null);
        await using var served = Served.Start(server, new UnitTestDtlsClient { CipherSuites = [0x00A8] });
        await served.WaitUntilEstablishedAsync();
        served.Flow.SendCoap(GetATxt);
        await served.ReceiveCoapAsync(1);

        await served.Shutdown.CancelAsync();
        await served.Exchange.CancelAsync();
        await Assert.ThrowsAsync<OperationCanceledException>(() => served.Serving);

        Assert.IsEmpty(served.Flow.ReceivedAlerts);
    }

    [TestMethod]
    [DataRow(1163, 6)]
    [DataRow(1171, 6)]
    [DataRow(1088, 6)]
    [DataRow(1087, 5)]
    [DataRow(512, 4)]
    public void BlockSizeExponentFor_RecordRoom_IsTheLargestBlockThatFitsWithItsHeader(int room, int expected) =>
        Assert.AreEqual(expected, SecureCoapProtocolServer.BlockSizeExponentFor(room));

    internal static ExchangeContext Context(IExchangeLog log, TimeProvider clock, CancellationToken cancellationToken, CancellationToken shutdownToken = default) =>
        new(
            1,
            new ListenUrl("coaps", "127.0.0.1", 5684).WithBoundPort(5684),
            new IPEndPoint(IPAddress.Loopback, 5684),
            new IPEndPoint(IPAddress.Loopback, 50000),
            log,
            clock,
            cancellationToken)
        {
            ShutdownToken = shutdownToken,
        };

    private static byte[]? Lookup(ReadOnlySpan<byte> identity) =>
        identity.SequenceEqual("alice"u8) ? "secret"u8.ToArray() : null;

    private static SecureCoapProtocolServer CertificateServer(CoapTestStore store, CoapAccessRules rules) =>
        new(store.Store, rules, new CoapObservations(), new CoapDtlsCredentials(UnitTestCertificates.EcdsaP256, null), new UnitTestCoapRandomSource(), cookieSecrets: null);

    private static byte[] Get(ushort messageId, string path, params CoapOption[] options) =>
        CoapMessageWriter.Write(new CoapMessage(
            CoapMessageType.Confirmable,
            CoapCode.Get,
            messageId,
            new byte[] { 0x7B },
            [CoapOption.FromString(CoapOptionNumber.UriPath, path), .. options],
            ReadOnlyMemory<byte>.Empty));

    private static string? BlockOf(CoapMessage message, int number) =>
        message.Options.SingleOrDefault(option => option.Number == number) is { } option ? CoapBlockOption.Read(option).ToString() : null;

    private static uint? UIntOf(CoapMessage message, int number) =>
        message.Options.SingleOrDefault(option => option.Number == number)?.ReadUInt();

    private static async Task WaitUntilAsync(Func<bool> condition, string what)
    {
        var started = DateTime.UtcNow;

        while (!condition())
        {
            Assert.IsLessThan(PatienceLimit, DateTime.UtcNow - started, $"Waited for {what}.");
            await Task.Delay(5);
        }
    }

    private sealed class Served : IAsyncDisposable
    {
        private Served(UnitTestDtlsDatagramFlow flow)
        {
            Flow = flow;
        }

        public UnitTestDtlsDatagramFlow Flow { get; }

        public RecordingExchangeLog Log { get; } = new();

        public ManualTimeProvider Clock { get; } = new();

        public CancellationTokenSource Exchange { get; } = new();

        public CancellationTokenSource Shutdown { get; } = new();

        public Task Serving { get; private set; } = Task.CompletedTask;

        public static Served Start(SecureCoapProtocolServer server, UnitTestDtlsClient client, byte[]? firstDatagram = null)
        {
            var served = new Served(new UnitTestDtlsDatagramFlow(client, firstDatagram));
            served.Serving = Task.Run(() => server.ServeAsync(
                served.Flow, Context(served.Log, served.Clock, served.Exchange.Token, served.Shutdown.Token)));
            return served;
        }

        public Task WaitUntilEstablishedAsync() => WaitUntilAsync(() => Flow.IsEstablished, "the handshake");

        public async Task<CoapMessage> ReceiveCoapAsync(int count)
        {
            await WaitUntilAsync(() => Flow.ReceivedCoap.Count >= count, $"{count} CoAP messages");
            return CoapMessageReader.Read(Flow.ReceivedCoap[count - 1]).Message!;
        }

        public async ValueTask DisposeAsync()
        {
            await Exchange.CancelAsync();
            try
            {
                await Serving;
            }
            catch (OperationCanceledException)
            {
                // The exchange is over; a test that cares has asserted how it ended.
            }

            await Flow.DisposeAsync();
            Exchange.Dispose();
            Shutdown.Dispose();
        }
    }
}
