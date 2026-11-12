using Surl.Protocol.Abstractions;

namespace Surl.Cli;

[TestClass]
public sealed class DnsTransportOptionTests
{
    private static SurlCommandLine Served(params string[] arguments) =>
        CommandLineParser.Parse(arguments, ["dns"]).CommandLine!;

    [TestMethod]
    public void Parse_DnsTransportUdpTcpTls_ReadsTheListInTheOrderGiven() =>
        CollectionAssert.AreEqual(
            new[] { "udp", "tcp", "tls" },
            Served("--dns-transport", "udp,tcp,tls", "dns://127.0.0.1:5300").DnsTransports.ToArray());

    [TestMethod]
    public void Parse_DnsTransportInAnyCase_IsLowerCased() =>
        CollectionAssert.AreEqual(new[] { "tls" }, Served("--dns-transport=TLS", "dns://127.0.0.1:5300").DnsTransports.ToArray());

    [TestMethod]
    public void Parse_NoDnsTransport_IsUdpThenTcp() =>
        CollectionAssert.AreEqual(new[] { "udp", "tcp" }, Served("dns://127.0.0.1:5300").DnsTransports.ToArray());

    [TestMethod]
    [DataRow("bogus")]
    [DataRow("")]
    [DataRow("tcp,tcp")]
    [DataRow("udp,,tcp")]
    [DataRow("https")]
    public void Parse_BadDnsTransport_IsRefusedFailedInitWithTheOptionErrorText(string value)
    {
        var result = CommandLineParser.Parse(["--dns-transport", value, "dns://127.0.0.1:5300"], ["dns"]);

        Assert.AreEqual(SurlExitCode.FailedInit, result.Failure!.ExitCode);
        StringAssert.Contains(result.Failure.Message, "--dns-transport");
    }

    [TestMethod]
    public void ReadDnsTransports_Empty_IsBlankAndAWordNotListedIsBadlyUsed()
    {
        Assert.AreEqual(OptionArgumentReader.Blank, OptionArgumentReader.ReadDnsTransports(string.Empty, out _));
        Assert.AreEqual(OptionArgumentReader.BadlyUsed, OptionArgumentReader.ReadDnsTransports("https", out _));
    }

    [TestMethod]
    public void Parse_DnsTransportUdpTcpQuic_ReadsTheListAndTheAllowedValuesListQuic()
    {
        CollectionAssert.AreEqual(
            new[] { "udp", "tcp", "quic" },
            Served("--dns-transport", "udp,tcp,quic", "dns://127.0.0.1:5300").DnsTransports.ToArray());
        StringAssert.Contains(OptionArgumentReader.DnsTransports.Type.AllowedValues, "quic");
    }

    [TestMethod]
    [DataRow("dns://127.0.0.1:5300", true)]
    [DataRow("dns://[::1]:5300/", true)]
    [DataRow("dns://127.0.0.1", false)]
    [DataRow("dns://[::1]", false)]
    [DataRow("dns://127.0.0.1/", false)]
    public void Parse_DnsListenUrl_IsRecordedOnlyWhenItGivesAPort(string listenUrl, bool givesPort) =>
        Assert.AreEqual(givesPort ? 1 : 0, Served(listenUrl).DnsListenUrlsWithPort.Count);

    [TestMethod]
    public void Parse_NonDnsListenUrlWithAPort_IsNotRecorded() =>
        Assert.IsEmpty(CommandLineParser.Parse(["tftp://127.0.0.1:69"]).CommandLine!.DnsListenUrlsWithPort);
}
