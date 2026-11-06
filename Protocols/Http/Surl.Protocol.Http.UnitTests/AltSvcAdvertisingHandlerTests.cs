using System.Net;
using System.Text;
using Surl.Content;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Http;

/// <summary>
/// <c>--http3</c>'s <c>Alt-Svc</c> (ADR-0082 decision 6): every answer on an https connection,
/// over HTTP/1.x and HTTP/2, names HTTP/3 on the connection's own port; nothing else changes.
/// The requests are pinned upstream curl's recorded <c>get-file</c> and
/// <c>h2-prior-knowledge-get-file</c>.
/// </summary>
[TestClass]
public sealed class AltSvcAdvertisingHandlerTests
{
    private const string AltSvcLine = "Alt-Svc: h3=\":8443\"; ma=86400\r\n";
    private static readonly IPEndPoint Local = new(IPAddress.Loopback, 8443);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ServeAsync_Http11OverTlsWithHttp3_AddsAltSvcAfterTheRecordedFields()
    {
        var connection = await ServeAsync("get-file", advertisesHttp3: true, InMemoryConnection.DefaultUpgradeTlsSession with { ApplicationProtocol = "http/1.1" });

        var expected = Encoding.ASCII.GetString(RecordedFixture.ReadBytes("get-file", "response.bin"))
            .Replace("Content-Length: 17\r\n", "Content-Length: 17\r\n" + AltSvcLine, StringComparison.Ordinal);
        Assert.AreEqual(expected, Encoding.ASCII.GetString(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task ServeAsync_Http2OverTlsWithHttp3_AddsAltSvcToTheHeaderBlock()
    {
        var connection = await ServeAsync("h2-prior-knowledge-get-file", advertisesHttp3: true, InMemoryConnection.DefaultUpgradeTlsSession with { ApplicationProtocol = "h2" });

        var response = Http2TestClient.Responses(connection.WrittenBytes)[1];
        Assert.AreEqual("200", response.Status);
        Assert.AreEqual("h3=\":8443\"; ma=86400", response.Fields.Single(field => field.Name == "alt-svc").Value);
    }

    [TestMethod]
    public async Task ServeAsync_CleartextWithHttp3_SendsNoAltSvc()
    {
        var connection = await ServeAsync("get-file", advertisesHttp3: true, tlsSession: null);

        CollectionAssert.AreEqual(RecordedFixture.ReadBytes("get-file", "response.bin"), connection.WrittenBytes);
    }

    [TestMethod]
    public async Task ServeAsync_TlsWithoutHttp3_SendsNoAltSvc()
    {
        var connection = await ServeAsync("get-file", advertisesHttp3: false, InMemoryConnection.DefaultUpgradeTlsSession);

        CollectionAssert.AreEqual(RecordedFixture.ReadBytes("get-file", "response.bin"), connection.WrittenBytes);
    }

    private async Task<InMemoryConnection> ServeAsync(string fixture, bool advertisesHttp3, TlsSession? tlsSession)
    {
        var store = new ContentStore(HttpServerHarness.Root, HttpServerHarness.StandardFileSystem(), ContentExposureOptions.ServeEverythingInsideTheRoot);
        var server = new HttpProtocolServer(store, new AnonymousAuthenticationPolicy(), advertisesHttp3);
        var connection = new InMemoryConnection(RecordedFixture.Whole(RecordedFixture.ReadRequestBytes(fixture)), Local, initialTlsSession: tlsSession);

        await server.ServeAsync(connection, HttpServerHarness.Context(new RecordingExchangeLog(), new ManualTimeProvider(HttpServerHarness.Now), TestContext.CancellationToken));

        return connection;
    }
}
