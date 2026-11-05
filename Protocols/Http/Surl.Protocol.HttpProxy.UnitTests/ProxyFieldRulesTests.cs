using Surl.HttpMessage;

namespace Surl.Protocol.HttpProxy;

/// <summary>
/// Each header rule of ADR-0088 decision 3: the fields removed and added in each direction.
/// </summary>
[TestClass]
public sealed class ProxyFieldRulesTests
{
    private static List<HttpRequestField> Fields(params string[] lines) =>
        lines.Select(line => line.Split(": ", 2)).Select(parts => new HttpRequestField(parts[0], parts[1])).ToList();

    private static string[] Lines(IEnumerable<HttpRequestField> fields) =>
        fields.Select(field => $"{field.Name}: {field.Value}").ToArray();

    private static string[] Forwarded(params string[] lines) =>
        Lines(ProxyFieldRules.ForwardedRequestFields(Fields(lines), "origin.test", decrementsMaxForwards: false, sendsChunkedBody: false));

    private static string[] Relayed(params string[] lines) =>
        Lines(ProxyFieldRules.RelayedResponseFields(Fields(lines), relaysChunkedBody: false, closesClientConnection: false));

    [TestMethod]
    public void ForwardedRequestFields_CurlsProxyRequest_DropsProxyConnectionAndAddsVia()
    {
        var forwarded = Forwarded("Host: origin.test", "User-Agent: curl/8.21.0", "Accept: */*", "Proxy-Connection: Keep-Alive");

        CollectionAssert.AreEqual(new[] { "Host: origin.test", "User-Agent: curl/8.21.0", "Accept: */*", "Via: 1.1 surl" }, forwarded);
    }

    [TestMethod]
    public void ForwardedRequestFields_ConnectionAndTheFieldsItNames_AreRemoved()
    {
        var forwarded = Forwarded("Host: h", "Connection: X-Hop, close", "X-Hop: 1", "X-Kept: 2");

        CollectionAssert.AreEqual(new[] { "Host: origin.test", "X-Kept: 2", "Via: 1.1 surl" }, forwarded);
    }

    [TestMethod]
    [DataRow("Keep-Alive: timeout=5")]
    [DataRow("TE: trailers")]
    [DataRow("Trailer: X-Sum")]
    [DataRow("Transfer-Encoding: chunked")]
    [DataRow("Upgrade: websocket")]
    [DataRow("Proxy-Authorization: Basic dTpw")]
    [DataRow("proxy-connection: close")]
    public void ForwardedRequestFields_HopByHopOrProxyField_IsRemoved(string field)
    {
        var forwarded = Forwarded("Host: h", field);

        CollectionAssert.AreEqual(new[] { "Host: origin.test", "Via: 1.1 surl" }, forwarded);
    }

    [TestMethod]
    public void ForwardedRequestFields_Upgrade_KeepsUpgradeAndSaysConnectionUpgrade()
    {
        var forwarded = Forwarded("Host: h", "Connection: Upgrade", "Upgrade: websocket");

        CollectionAssert.AreEqual(new[] { "Host: origin.test", "Via: 1.1 surl", "Connection: Upgrade", "Upgrade: websocket" }, forwarded);
    }

    [TestMethod]
    public void ForwardedRequestFields_HostDifferingFromTheTarget_IsReplacedInPlace()
    {
        var forwarded = Forwarded("Accept: */*", "Host: elsewhere.test");

        CollectionAssert.AreEqual(new[] { "Accept: */*", "Host: origin.test", "Via: 1.1 surl" }, forwarded);
    }

    [TestMethod]
    public void ForwardedRequestFields_NoHost_GeneratesItFirst()
    {
        var forwarded = Forwarded("Accept: */*");

        CollectionAssert.AreEqual(new[] { "Host: origin.test", "Accept: */*", "Via: 1.1 surl" }, forwarded);
    }

    [TestMethod]
    [DataRow("Max-Forwards: 3", "Max-Forwards: 2")]
    [DataRow("Max-Forwards: 0", "Max-Forwards: 0")]
    [DataRow("Max-Forwards: many", "Max-Forwards: many")]
    public void ForwardedRequestFields_MaxForwardsWhenDecrementing_GoesOnOneLess(string field, string expected)
    {
        var forwarded = Lines(ProxyFieldRules.ForwardedRequestFields(Fields("Host: h", field), "origin.test", decrementsMaxForwards: true, sendsChunkedBody: false));

        CollectionAssert.AreEqual(new[] { "Host: origin.test", expected, "Via: 1.1 surl" }, forwarded);
    }

    [TestMethod]
    public void ForwardedRequestFields_ChunkedBody_EndsWithTransferEncodingChunked()
    {
        var forwarded = Lines(ProxyFieldRules.ForwardedRequestFields(Fields("Host: h", "Transfer-Encoding: chunked"), "origin.test", decrementsMaxForwards: false, sendsChunkedBody: true));

        CollectionAssert.AreEqual(new[] { "Host: origin.test", "Via: 1.1 surl", "Transfer-Encoding: chunked" }, forwarded);
    }

    [TestMethod]
    [DataRow("Proxy-Authenticate: Basic realm=\"surl\"")]
    [DataRow("Proxy-Authentication-Info: nextnonce=\"a\"")]
    [DataRow("Keep-Alive: timeout=5")]
    [DataRow("Transfer-Encoding: chunked")]
    [DataRow("Connection: keep-alive")]
    public void RelayedResponseFields_HopByHopOrProxyField_IsRemoved(string field)
    {
        var relayed = Relayed("Content-Length: 2", field);

        CollectionAssert.AreEqual(new[] { "Content-Length: 2", "Via: 1.1 surl" }, relayed);
    }

    [TestMethod]
    public void RelayedResponseFields_ChunkedAndClosing_EndWithTheirFields()
    {
        var relayed = Lines(ProxyFieldRules.RelayedResponseFields(Fields("Server: origin"), relaysChunkedBody: true, closesClientConnection: true));

        CollectionAssert.AreEqual(new[] { "Server: origin", "Via: 1.1 surl", "Transfer-Encoding: chunked", "Connection: close" }, relayed);
    }

    [TestMethod]
    [DataRow(new[] { "Max-Forwards: 7" }, 7L)]
    [DataRow(new[] { "Max-Forwards: -1" }, null)]
    [DataRow(new[] { "Accept: */*" }, null)]
    public void MaxForwards_Fields_IsTheNumberOrNone(string[] lines, long? expected)
    {
        Assert.AreEqual(expected, ProxyFieldRules.MaxForwards(Fields(lines)));
    }

    [TestMethod]
    [DataRow(new[] { "Upgrade: h2c", "Connection: keep-alive, Upgrade" }, true)]
    [DataRow(new[] { "Upgrade: h2c" }, false)]
    [DataRow(new[] { "Connection: upgrade" }, false)]
    public void IsUpgrade_Fields_NeedsUpgradeAndConnectionUpgrade(string[] lines, bool expected)
    {
        Assert.AreEqual(expected, ProxyFieldRules.IsUpgrade(Fields(lines)));
    }
}
