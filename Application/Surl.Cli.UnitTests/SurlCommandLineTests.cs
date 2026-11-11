using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Cli;

[TestClass]
public sealed class SurlCommandLineTests
{
    [TestMethod]
    public void New_ProxyMembers_DefaultToNone()
    {
        var commandLine = new SurlCommandLine();

        Assert.IsEmpty(commandLine.ProxyListenUrls);
        Assert.IsEmpty(commandLine.ProxyAccounts);
        Assert.IsNull(commandLine.ProxyUserFile);
        Assert.IsEmpty(commandLine.ProxyAllowedOrigins);
    }

    [TestMethod]
    public void With_ProxyMembers_KeepsEachProxyValue()
    {
        var commandLine = new SurlCommandLine
        {
            ProxyListenUrls = [new ListenUrl("https", "127.0.0.1", 3129) { IsProxy = true }],
            ProxyAccounts = [new CommandLineAccount("proxyuser", "proxy-secret")],
            ProxyUserFile = "proxy-users.txt",
            ProxyAllowedOrigins = [ProxyAllowedOrigin.Range(IPAddress.Parse("10.0.0.0"), 8), ProxyAllowedOrigin.EveryAddress],
        };

        Assert.IsTrue(commandLine.ProxyListenUrls[0].IsProxy);
        Assert.AreEqual("proxyuser", commandLine.ProxyAccounts[0].UserName);
        Assert.AreEqual("proxy-users.txt", commandLine.ProxyUserFile);
        Assert.AreEqual("10.0.0.0/8", commandLine.ProxyAllowedOrigins[0].ToString());
        Assert.DoesNotContain("proxy-secret", commandLine.ToString());
    }
}
