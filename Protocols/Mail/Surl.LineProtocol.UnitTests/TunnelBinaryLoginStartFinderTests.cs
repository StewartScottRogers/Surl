using System.Text;

namespace Surl.LineProtocol;

[TestClass]
public sealed class TunnelBinaryLoginStartFinderTests
{
    private const string ProxyLine = "PROXY TCP4 127.0.0.1 127.0.0.1 52097 18780\r\n";

    [TestMethod]
    [DataRow("\u0010(", DisplayName = "MQTT")]
    [DataRow("0\u0084", DisplayName = "LDAP")]
    public void TryFind_ALoginByteFirst_FindsItAtTheStart(string received)
    {
        var finder = new TunnelBinaryLoginStartFinder();

        Assert.IsTrue(finder.TryFind(Bytes(received), out var loginStart, out var redactor));
        Assert.AreEqual(0, loginStart);
        Assert.IsNotNull(redactor);
    }

    [TestMethod]
    [DataRow("PROXY TCP4 1.2.3.4 5.6.7.8 1 2\r\n", DisplayName = "one PROXY line")]
    [DataRow("PROXY UNKNOWN\n", DisplayName = "a bare line feed")]
    [DataRow("PROXY UNKNOWN\r\nPROXY UNKNOWN\r\n", DisplayName = "two PROXY lines")]
    public void TryFind_ALoginByteAfterProxyLines_FindsItThere(string lines)
    {
        var finder = new TunnelBinaryLoginStartFinder();

        Assert.IsTrue(finder.TryFind(Bytes(lines + "\u0010("), out var loginStart, out var redactor));
        Assert.AreEqual(lines.Length, loginStart);
        Assert.IsInstanceOfType<TunnelMqttConnectRedactor>(redactor);
    }

    [TestMethod]
    public void TryFind_AProxyLineSplitAtEveryByte_FindsTheLoginAfterIt()
    {
        var sent = Bytes(ProxyLine + "0\u0084");
        for (var split = 0; split <= ProxyLine.Length; split++)
        {
            var finder = new TunnelBinaryLoginStartFinder();

            Assert.IsFalse(finder.TryFind(sent.AsSpan(0, split), out _, out _), $"split at {split}");
            Assert.IsTrue(finder.TryFind(sent.AsSpan(split), out var loginStart, out var redactor), $"split at {split}");
            Assert.AreEqual(ProxyLine.Length - split, loginStart, $"split at {split}");
            Assert.IsInstanceOfType<TunnelLdapBindRedactor>(redactor);
        }
    }

    [TestMethod]
    [DataRow("GET / HTTP/1.1\r\n\u0010(", DisplayName = "another first line")]
    [DataRow("PROXY\r\n\u0010(", DisplayName = "a first line too short")]
    [DataRow("\u0016\u0003\u0001\n\u0010(", DisplayName = "TLS")]
    [DataRow("PROXY UNKNOWN\r\nGET /\r\n\u0010(", DisplayName = "a login two lines after PROXY")]
    [DataRow("PROXY UNKNOWN \u0010(", DisplayName = "an unfinished PROXY line")]
    public void TryFind_NoLoginWhereOneMayStart_FindsNone(string received)
    {
        var finder = new TunnelBinaryLoginStartFinder();

        Assert.IsFalse(finder.TryFind(Bytes(received), out _, out var redactor));
        Assert.IsNull(redactor);
    }

    [TestMethod]
    public void TryFind_AfterItFoundALogin_FindsNoSecondOne()
    {
        var finder = new TunnelBinaryLoginStartFinder();
        finder.TryFind(Bytes("\u0010("), out _, out _);

        Assert.IsFalse(finder.TryFind(Bytes("\u0010("), out _, out _));
    }

    [TestMethod]
    public void TryFind_NoBytes_LeavesTheTunnelsStartAwaited()
    {
        var finder = new TunnelBinaryLoginStartFinder();

        Assert.IsFalse(finder.TryFind([], out _, out _));
        Assert.IsTrue(finder.TryFind(Bytes("\u0010("), out _, out _));
    }

    private static byte[] Bytes(string text) => Encoding.Latin1.GetBytes(text);
}
