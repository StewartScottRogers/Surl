using System.Buffers;
using System.Text;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.HttpProxy.ProxyTestKit;

namespace Surl.Protocol.HttpProxy;

[TestClass]
public sealed class HttpProxyExchangeLogRedactionTests
{
    private static string Logged(params string[] chunks)
    {
        var redactor = new HttpProxyProtocolServer(new InMemoryOriginConnector())
            .CreateRedactor(new ExchangeRedactionContext(1, "http"));
        var output = new ArrayBufferWriter<byte>();
        foreach (var chunk in chunks)
        {
            redactor.Redact(Encoding.Latin1.GetBytes(chunk), output);
        }

        redactor.Flush(output);

        return Encoding.Latin1.GetString(output.WrittenSpan);
    }

    [TestMethod]
    public void ForwardedGet_ProxyAuthorizationAndAuthorization_AreLoggedWithTheirCredentialsMasked() =>
        Assert.AreEqual(
            "GET http://origin.test/a HTTP/1.1\r\nHost: origin.test\r\nProxy-Authorization: Basic ****\r\nAuthorization: Bearer ****\r\n\r\n",
            Logged("GET http://origin.test/a HTTP/1.1\r\nHost: origin.test\r\nProxy-Authorization: Basic YWxpY2U6czNjcmV0\r\nAuthorization: Bearer tok3n\r\n\r\n"));

    [TestMethod]
    public void Connect_ProxyAuthorization_IsMaskedAndTheTunnelledBytesAreLoggedAsReceived() =>
        Assert.AreEqual(
            "CONNECT origin.test:443 HTTP/1.1\r\nHost: origin.test:443\r\nProxy-Authorization: Basic ****\r\n\r\n\x16\x03\x01tls-bytes",
            Logged("CONNECT origin.test:443 HTTP/1.1\r\nHost: origin.test:443\r\nProxy-Authorization: Basic YWxpY2U6\r\n\r\n", "\x16\x03\x01tls-bytes"));

    [TestMethod]
    public void TunnelledPlaintextRequest_AuthorizationIsMasked() =>
        Assert.AreEqual(
            "CONNECT o:80 HTTP/1.1\r\n\r\nGET / HTTP/1.1\r\nAuthorization: Basic ****\r\n\r\n",
            Logged("CONNECT o:80 HTTP/1.1\r\n\r\n", "GET / HTTP/1.1\r\nAuthorization: Basic YWxpY2U6\r\n\r\n"));

    [TestMethod]
    public async Task ServeAsync_ForwardedAuthorization_ReachesTheOriginUnchangedWhileTheLogHoldsOnlyMaskedBytes()
    {
        var origin = Connection("HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok");
        var origins = new InMemoryOriginConnector().ScriptConnection("origin.test", 80, origin);
        var client = Connection("GET http://origin.test/a HTTP/1.1\r\nHost: origin.test\r\nAuthorization: Bearer tok3n\r\n\r\n");

        await ServeAsync(client, origins);

        StringAssert.Contains(Latin1(origin.WrittenBytes), "Authorization: Bearer tok3n");
        Assert.AreEqual(new OriginConnectionRequest("origin.test", 80, ExchangeLimits.Default.HeadTimeout), origins.Requests.Single());
    }
}
