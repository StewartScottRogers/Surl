namespace Surl.Protocol.HttpProxy;

[TestClass]
public sealed class ProxyTargetTests
{
    [TestMethod]
    [DataRow("http://origin.test/a?b", "http", "origin.test", "origin.test", 80, "/a?b")]
    [DataRow("HTTP://Origin.test:8080", "http", "Origin.test:8080", "Origin.test", 8080, "/")]
    [DataRow("http://origin.test?q", "http", "origin.test", "origin.test", 80, "/?q")]
    [DataRow("http://[::1]:81/x", "http", "[::1]:81", "::1", 81, "/x")]
    [DataRow("ftp://origin.test/a.txt", "ftp", "origin.test", "origin.test", 21, "/a.txt")]
    public void FromAbsoluteForm_AbsoluteTarget_IsRead(string requestTarget, string scheme, string authority, string host, int port, string originForm)
    {
        var target = ProxyTarget.FromAbsoluteForm(requestTarget);

        Assert.AreEqual(new ProxyTarget(scheme, authority, host, port, originForm), target);
    }

    [TestMethod]
    [DataRow("/a")]
    [DataRow("://origin.test/")]
    [DataRow("http:///a")]
    [DataRow("http://user@origin.test/")]
    [DataRow("http://origin.test:/")]
    [DataRow("http://origin.test:99999/")]
    [DataRow("http://origin.test:+80/")]
    [DataRow("http://[::1/")]
    [DataRow("http://[::1]x/")]
    public void FromAbsoluteForm_OtherTarget_IsNone(string requestTarget)
    {
        Assert.IsNull(ProxyTarget.FromAbsoluteForm(requestTarget));
    }

    [TestMethod]
    [DataRow("ftp://user:pass@origin.test/a.txt", "user:pass")]
    [DataRow("FTP://a@b@origin.test/a.txt", "a@b")]
    public void FromAbsoluteForm_FtpUserInfo_IsKeptOutOfTheAuthority(string requestTarget, string userInfo)
    {
        var target = ProxyTarget.FromAbsoluteForm(requestTarget);

        Assert.AreEqual(new ProxyTarget("ftp", "origin.test", "origin.test", 21, "/a.txt", userInfo), target);
    }

    [TestMethod]
    [DataRow("origin.test:443", "origin.test", 443)]
    [DataRow("[::1]:8080", "::1", 8080)]
    [DataRow("127.0.0.1:1", "127.0.0.1", 1)]
    public void FromAuthorityForm_HostAndPort_IsRead(string requestTarget, string host, int port)
    {
        var target = ProxyTarget.FromAuthorityForm(requestTarget);

        Assert.AreEqual(new ProxyTarget(string.Empty, requestTarget, host, port, string.Empty), target);
    }

    [TestMethod]
    [DataRow("origin.test")]
    [DataRow("origin.test:0")]
    [DataRow(":443")]
    [DataRow("[::1]")]
    public void FromAuthorityForm_NoHostOrNoPort_IsNone(string requestTarget)
    {
        Assert.IsNull(ProxyTarget.FromAuthorityForm(requestTarget));
    }
}
