using System.Net;

namespace Surl.Cli;

[TestClass]
public sealed class OptionArgumentReaderTests
{
    [TestMethod]
    [DataRow("*", DisplayName = "Every address")]
    public void ReadProxyAllowedOrigin_Star_ReadsEveryAddress(string argument)
    {
        var refusal = OptionArgumentReader.ReadProxyAllowedOrigin(argument, out var origin);

        Assert.IsNull(refusal);
        Assert.AreSame(ProxyAllowedOrigin.EveryAddress, origin);
    }

    [TestMethod]
    [DataRow("192.0.2.7", "192.0.2.7", 32, DisplayName = "IPv4 address alone is the one address")]
    [DataRow("10.0.0.0/8", "10.0.0.0", 8, DisplayName = "IPv4 range")]
    [DataRow("0.0.0.0/0", "0.0.0.0", 0, DisplayName = "Prefix 0")]
    [DataRow("010.000.000.001/032", "10.0.0.1", 32, DisplayName = "Leading zeros are decimal")]
    [DataRow("2001:db8::1", "2001:db8::1", 128, DisplayName = "IPv6 address alone is the one address")]
    [DataRow("2001:DB8::/32", "2001:db8::", 32, DisplayName = "IPv6 range, upper-case hex")]
    [DataRow("::ffff:192.0.2.7", "192.0.2.7", 32, DisplayName = "IPv4-mapped IPv6 is IPv4")]
    [DataRow("::ffff:192.0.2.0/24", "192.0.2.0", 24, DisplayName = "IPv4-mapped range takes IPv4's prefix")]
    public void ReadProxyAllowedOrigin_AddressOrRange_ReadsTheRange(string argument, string network, int prefixLength)
    {
        var refusal = OptionArgumentReader.ReadProxyAllowedOrigin(argument, out var origin);

        Assert.IsNull(refusal);
        Assert.AreEqual(ProxyAllowedOrigin.Range(IPAddress.Parse(network), prefixLength), origin);
    }

    [TestMethod]
    [DataRow("", OptionArgumentReader.Blank, DisplayName = "Empty is blank")]
    [DataRow("localhost", OptionArgumentReader.ExpectedOriginAddress, DisplayName = "A host name")]
    [DataRow("127.1", OptionArgumentReader.ExpectedOriginAddress, DisplayName = "IPv4 shorthand")]
    [DataRow("1.2.3.4.5", OptionArgumentReader.ExpectedOriginAddress, DisplayName = "Five IPv4 parts")]
    [DataRow("1.2.3.256", OptionArgumentReader.ExpectedOriginAddress, DisplayName = "IPv4 part past 255")]
    [DataRow("1.2.3.-4", OptionArgumentReader.ExpectedOriginAddress, DisplayName = "IPv4 part with a sign")]
    [DataRow("1.2..4", OptionArgumentReader.ExpectedOriginAddress, DisplayName = "Empty IPv4 part")]
    [DataRow("1.2.3.4:80", OptionArgumentReader.ExpectedOriginAddress, DisplayName = "IPv4 with a port")]
    [DataRow("[::1]", OptionArgumentReader.ExpectedOriginAddress, DisplayName = "Bracketed IPv6")]
    [DataRow("fe80::1%eth0", OptionArgumentReader.ExpectedOriginAddress, DisplayName = "IPv6 with a zone")]
    [DataRow("2001:db8::g", OptionArgumentReader.ExpectedOriginAddress, DisplayName = "Not hex")]
    [DataRow("2001:db8:::1", OptionArgumentReader.ExpectedOriginAddress, DisplayName = "Three colons")]
    [DataRow("**", OptionArgumentReader.ExpectedOriginAddress, DisplayName = "Not exactly a star")]
    [DataRow("/8", OptionArgumentReader.ExpectedOriginAddress, DisplayName = "Prefix without address")]
    [DataRow("10.0.0.0/", OptionArgumentReader.PrefixLengthOutOfRange, DisplayName = "Empty prefix")]
    [DataRow("10.0.0.0/33", OptionArgumentReader.PrefixLengthOutOfRange, DisplayName = "IPv4 prefix past 32")]
    [DataRow("10.0.0.0/+8", OptionArgumentReader.PrefixLengthOutOfRange, DisplayName = "Signed prefix")]
    [DataRow("10.0.0.0/8/8", OptionArgumentReader.PrefixLengthOutOfRange, DisplayName = "Two prefixes")]
    [DataRow("10.0.0.0/99999999999", OptionArgumentReader.PrefixLengthOutOfRange, DisplayName = "Prefix past int")]
    [DataRow("2001:db8::/129", OptionArgumentReader.PrefixLengthOutOfRange, DisplayName = "IPv6 prefix past 128")]
    [DataRow("::ffff:10.0.0.0/104", OptionArgumentReader.PrefixLengthOutOfRange, DisplayName = "IPv4-mapped prefix is IPv4's")]
    public void ReadProxyAllowedOrigin_Malformed_IsRefused(string argument, string reason)
    {
        var refusal = OptionArgumentReader.ReadProxyAllowedOrigin(argument, out _);

        Assert.AreEqual(reason, refusal);
    }

    [TestMethod]
    public void AllowedOrigin_ReadsWithReadProxyAllowedOrigin()
    {
        var refusal = OptionArgumentReader.AllowedOrigin.Read("::1", out var origin);

        Assert.IsNull(refusal);
        Assert.AreEqual(ProxyAllowedOrigin.Range(IPAddress.IPv6Loopback, 128), origin);
        Assert.AreEqual("address[/prefix]", OptionArgumentReader.AllowedOrigin.Type.Name);
    }

    [TestMethod]
    public void ProxyListenUrlText_KeepsTheTextForParseProxy()
    {
        var refusal = OptionArgumentReader.ProxyListenUrlText.Read("https://127.0.0.1:3129", out var text);

        Assert.IsNull(refusal);
        Assert.AreEqual("https://127.0.0.1:3129", text);
        Assert.AreEqual(OptionArgumentReader.Blank, OptionArgumentReader.ProxyListenUrlText.Read(string.Empty, out _));
    }

    [TestMethod]
    [DataRow("proxyuser:proxy-secret", "proxyuser", "proxy-secret", DisplayName = "Proxy account")]
    [DataRow("proxyuser:a:b", "proxyuser", "a:b", DisplayName = "Split at the first colon")]
    public void ReadAccount_ProxyUserArgument_ReadsTheProxyAccount(string argument, string userName, string password)
    {
        var refusal = OptionArgumentReader.ReadAccount(argument, out var account);

        Assert.IsNull(refusal);
        Assert.AreEqual(new CommandLineAccount(userName, password), account);
        Assert.DoesNotContain(password, account.ToString());
    }

    [TestMethod]
    [DataRow("", OptionArgumentReader.Blank, DisplayName = "Empty is blank")]
    [DataRow("proxy-secret", OptionArgumentReader.ExpectedUserColonPassword, DisplayName = "No colon")]
    [DataRow("proxyuser:", OptionArgumentReader.EmptyPassword, DisplayName = "Empty password")]
    [DataRow("proxy\u0001user:proxy-secret", OptionArgumentReader.ControlCharacterInUserName, DisplayName = "Control character in the name")]
    public void ReadAccount_MalformedProxyUserArgument_IsRefusedWithoutThePassword(string argument, string reason)
    {
        var refusal = OptionArgumentReader.ReadAccount(argument, out _);

        Assert.AreEqual(reason, refusal);
        Assert.DoesNotContain("proxy-secret", refusal!);
    }

    [TestMethod]
    public void ReadPath_ProxyUserFile_KeepsThePath()
    {
        var refusal = OptionArgumentReader.ReadPath("proxy-users.txt", out var path);

        Assert.IsNull(refusal);
        Assert.AreEqual("proxy-users.txt", path);
    }
}
