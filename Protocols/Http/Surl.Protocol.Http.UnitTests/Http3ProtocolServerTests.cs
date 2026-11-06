using System.Security.Authentication;
using System.Security.Cryptography;
using Surl.Content;
using Surl.Protocol.Abstractions;
using Surl.Quic;
using static Surl.Protocol.Http.HttpServerHarness;

namespace Surl.Protocol.Http;

/// <summary>
/// The HTTP/3 server <c>surl --http3</c> registers for https's UDP side (ADR-0082 decisions 5 and
/// 6), driven from pinned upstream curl's recorded Initials by <see cref="Http3TestClient"/>.
/// </summary>
[TestClass]
public sealed class Http3ProtocolServerTests
{
    public TestContext TestContext { get; set; } = null!;

    private CancellationToken Token => TestContext.CancellationToken;

    [TestMethod]
    public void Schemes_IsHttpsOnly()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var server = new Http3ProtocolServer(Store(), new AnonymousAuthenticationPolicy(), [], key, TimeSpan.Zero);

        CollectionAssert.AreEqual(new[] { "https" }, server.Schemes.ToArray());
    }

    [TestMethod]
    public void Constructor_NullArgument_Throws()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var policy = new AnonymousAuthenticationPolicy();

        Assert.ThrowsExactly<ArgumentNullException>(() => new Http3ProtocolServer(null!, policy, [], key, TimeSpan.Zero));
        Assert.ThrowsExactly<ArgumentNullException>(() => new Http3ProtocolServer(Store(), null!, [], key, TimeSpan.Zero));
        Assert.ThrowsExactly<ArgumentNullException>(() => new Http3ProtocolServer(Store(), policy, null!, key, TimeSpan.Zero));
        Assert.ThrowsExactly<ArgumentNullException>(() => new Http3ProtocolServer(Store(), policy, [], null!, TimeSpan.Zero));
    }

    [TestMethod]
    public async Task ServeAsync_NullArgument_Throws()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var server = new Http3ProtocolServer(Store(), new AnonymousAuthenticationPolicy(), [], key, TimeSpan.Zero);
        var context = Context(new RecordingExchangeLog(), new ManualTimeProvider(Now), Token);
        await using var flow = new InMemoryDatagramFlow(new byte[1], []);

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.ServeAsync(null!, context));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.ServeAsync(flow, null!));
    }

    [TestMethod]
    public async Task ServeAsync_GetOfAContentStoreFile_AnswersItOverHttp3()
    {
        using var client = new Http3TestClient { ProtocolServerPolicy = new AnonymousAuthenticationPolicy() };
        await client.StartAsync(Token);

        client.Send(0, client.Request("GET", "/file.txt"), fin: true);
        await client.UntilAsync(() => client.HasEnded(0), Token);

        var response = client.Response(0);
        Assert.AreEqual("200", response.Status);
        Assert.AreEqual(FileBody, response.BodyText);
        Assert.IsTrue(response.Fin);
    }

    [TestMethod]
    public async Task ServeAsync_ExchangeCancelledBeforeTheHandshakeCompletes_StopsTheConnection()
    {
        using var client = new Http3TestClient { ProtocolServerPolicy = new AnonymousAuthenticationPolicy() };
        var serving = client.Serve(Token);

        await client.Exchange.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => serving.WaitAsync(TimeSpan.FromSeconds(30), Token));
    }

    [TestMethod]
    public async Task ServeAsync_ExchangeCancelled_ClosesWithH3NoErrorAndReturns()
    {
        using var client = new Http3TestClient { ProtocolServerPolicy = new AnonymousAuthenticationPolicy() };
        await client.StartAsync(Token);
        client.Send(0, client.Request("GET", "/file.txt"), fin: true);
        await client.UntilAsync(() => client.HasEnded(0), Token);

        await client.Exchange.CancelAsync();
        await client.FinishAsync(Token);

        Assert.AreEqual(0x100UL, client.CloseCode);
    }

    [TestMethod]
    public async Task ServeAsync_Request_IsJudgedAsOnAnEncryptedConnectionWithTheTls13SessionQuicAgreed()
    {
        var policy = new UnitTestAuthenticationPolicy(_ => new HttpAuthenticationVerdict(HttpAuthenticationOutcome.Proceed, [], null));
        using var client = new Http3TestClient { ProtocolServerPolicy = policy };
        await client.StartAsync(Token);

        client.Send(0, client.Request("GET", "/file.txt"), fin: true);
        await client.UntilAsync(() => client.HasEnded(0), Token);

        var session = policy.StartedTlsSessions.Single();
        Assert.IsNotNull(session);
        Assert.AreEqual(SslProtocols.Tls13, session.Protocol);
        Assert.AreEqual((System.Net.Security.TlsCipherSuite)client.Quic.Tls.CipherSuite, session.CipherSuite);
        Assert.AreEqual("h3", session.ApplicationProtocol);
        Assert.IsNull(session.ClientCertificate);
    }

    [TestMethod]
    public async Task ServeAsync_NoHeadTimeout_StillCompletesTheHandshake()
    {
        using var client = new Http3TestClient { ProtocolServerPolicy = new AnonymousAuthenticationPolicy(), IdleTimeout = TimeSpan.Zero };
        await client.StartAsync(Token, limits: ExchangeLimits.Default with { HeadTimeout = Timeout.InfiniteTimeSpan });

        client.Send(0, client.Request("GET", "/file.txt"), fin: true);
        await client.UntilAsync(() => client.HasEnded(0), Token);

        Assert.AreEqual("200", client.Response(0).Status);
    }

    private static ContentStore Store() => new(Root, StandardFileSystem(), ContentExposureOptions.ServeEverythingInsideTheRoot);
}
