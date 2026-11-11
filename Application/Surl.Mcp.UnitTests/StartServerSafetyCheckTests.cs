using Surl.Cli;
using Surl.Protocol.Abstractions;

namespace Surl.Mcp;

[TestClass]
public sealed class StartServerSafetyCheckTests
{
    [TestMethod]
    [DataRow("localhost")]
    [DataRow("LocalHost")]
    [DataRow("127.0.0.1")]
    [DataRow("127.8.9.10")]
    [DataRow("::1")]
    public void IsLoopback_ALoopbackHost_IsTrue(string host) =>
        Assert.IsTrue(StartServerSafetyCheck.IsLoopback(host));

    [TestMethod]
    [DataRow("0.0.0.0")]
    [DataRow("::")]
    [DataRow("10.0.0.1")]
    [DataRow("localhost.example.com")]
    public void IsLoopback_AnyOtherHost_IsFalse(string host) =>
        Assert.IsFalse(StartServerSafetyCheck.IsLoopback(host));

    [TestMethod]
    public void IsLocal_AUnixSocketListenUrlOnAnyHost_IsTrue() =>
        Assert.IsTrue(StartServerSafetyCheck.IsLocal(
            new ListenUrl("http", "example.com", 80) { UnixSocket = new UnixSocketAddress("/run/surl.sock", isAbstract: false) }));

    [TestMethod]
    public void FindListenHostRefusal_ANonLoopbackProxyListenUrl_IsRefused()
    {
        var check = new StartServerSafetyCheck(new SurlMcpHostSettings(), new PathConfinement(null));
        var commandLine = new SurlCommandLine
        {
            ListenUrls = [new ListenUrl("http", "127.0.0.1", 0)],
            ProxyListenUrls = [new ListenUrl("http", "0.0.0.0", 3128) { IsProxy = true }],
        };

        Assert.AreEqual(
            "the listen host 0.0.0.0 is not loopback; only whoever starts surl-mcp can allow it, with --allow-non-loopback",
            check.FindListenHostRefusal(commandLine));
    }

    [TestMethod]
    public void FindListenHostRefusal_ALoopbackProxyListenUrl_IsAccepted()
    {
        var check = new StartServerSafetyCheck(new SurlMcpHostSettings(), new PathConfinement(null));
        var commandLine = new SurlCommandLine { ProxyListenUrls = [new ListenUrl("https", "::1", 3129) { IsProxy = true }] };

        Assert.IsNull(check.FindListenHostRefusal(commandLine));
    }

    [TestMethod]
    public void FindListenHostRefusal_ANonLoopbackProxyListenUrlTheHostAllows_IsAccepted()
    {
        var check = new StartServerSafetyCheck(new SurlMcpHostSettings { AllowNonLoopback = true }, new PathConfinement(null));
        var commandLine = new SurlCommandLine { ProxyListenUrls = [new ListenUrl("http", "192.0.2.1", 3128) { IsProxy = true }] };

        Assert.IsNull(check.FindListenHostRefusal(commandLine));
    }

    [TestMethod]
    [DataRow("--config=surl.conf")]
    [DataRow("-Ksurl.conf")]
    public void FindRefusal_AConfigFileSpelledAttached_IsRefused(string word)
    {
        var check = new StartServerSafetyCheck(new SurlMcpHostSettings(), new PathConfinement(null));

        Assert.AreEqual(StartServerSafetyCheck.ConfigFileRule, check.FindRefusal(["http://127.0.0.1:0", word]));
    }

    [TestMethod]
    public void FindRefusal_WordsSurlRefuses_AreLeftToSurlApi()
    {
        var check = new StartServerSafetyCheck(new SurlMcpHostSettings(), new PathConfinement(null));

        Assert.IsNull(check.FindRefusal(["http://0.0.0.0:0", "--no-such-option"]));
    }

    [TestMethod]
    public void FindRefusal_AnExtendedSchemeOnANonLoopbackHost_IsRefused()
    {
        var check = new StartServerSafetyCheck(new SurlMcpHostSettings(), new PathConfinement(null));
        var scheme = Cli.ExtendedSchemeDefaultPorts.Schemes.First();

        Assert.IsNotNull(check.FindRefusal([$"{scheme}://0.0.0.0:0"]));
    }
}
