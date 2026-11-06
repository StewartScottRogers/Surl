using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.OciRegistry;

/// <summary>
/// The Bearer challenge and its realm (Token "How to authenticate"; RFC 6750, section 3;
/// ADR-0115, decisions 4.1 and 4.2).
/// </summary>
[TestClass]
public sealed class OciChallengeTests
{
    private static ExchangeContext Context(ListenUrl listenUrl, EndPoint local) =>
        new(1, listenUrl, local, new IPEndPoint(IPAddress.Loopback, 50000), new RecordingExchangeLog(), TimeProvider.System, CancellationToken.None);

    [TestMethod]
    public void Realm_ListenHostIsADnsName_IsThatNameWithTheBoundPort()
    {
        var context = Context(new ListenUrl("oci", "registry.test", 0).WithBoundPort(5443), new IPEndPoint(IPAddress.Loopback, 5443));

        var realm = OciChallenge.Realm(context, isEncrypted: true);

        Assert.AreEqual("https://registry.test:5443/token", realm);
    }

    [TestMethod]
    [DataRow("0.0.0.0", "10.0.0.7", "http://10.0.0.7:5000/token")]
    [DataRow("::", "::ffff:10.0.0.7", "http://10.0.0.7:5000/token")]
    [DataRow("::", "::1", "http://[::1]:5000/token")]
    public void Realm_ListenHostIsAnAddress_IsTheLocalAddressTheConnectionArrivedOn(string listenHost, string localAddress, string expected)
    {
        var context = Context(new ListenUrl("oci", listenHost, 5000), new IPEndPoint(IPAddress.Parse(localAddress), 5000));

        var realm = OciChallenge.Realm(context, isEncrypted: false);

        Assert.AreEqual(expected, realm);
    }

    [TestMethod]
    public void Realm_LocalEndPointIsNoAddress_IsTheListenHost()
    {
        var context = Context(new ListenUrl("oci", "127.0.0.1", 5000), new DnsEndPoint("localhost", 5000));

        var realm = OciChallenge.Realm(context, isEncrypted: false);

        Assert.AreEqual("http://127.0.0.1:5000/token", realm);
    }

    [TestMethod]
    [DataRow(null, null, "Bearer realm=\"http://registry.test:5000/token\",service=\"surl\"")]
    [DataRow("repository:app:pull", null, "Bearer realm=\"http://registry.test:5000/token\",service=\"surl\",scope=\"repository:app:pull\"")]
    [DataRow("repository:app:pull", "invalid_token", "Bearer realm=\"http://registry.test:5000/token\",service=\"surl\",scope=\"repository:app:pull\",error=\"invalid_token\"")]
    [DataRow(null, "invalid_token", "Bearer realm=\"http://registry.test:5000/token\",service=\"surl\",error=\"invalid_token\"")]
    public void Bearer_ScopeAndError_AreAddedWhenGiven(string? scope, string? error, string expected)
    {
        var context = Context(new ListenUrl("oci", "registry.test", 5000), new IPEndPoint(IPAddress.Loopback, 5000));

        var challenge = OciChallenge.Bearer(context, isEncrypted: false, scope, error);

        Assert.AreEqual(expected, challenge);
    }
}
