using System.Text;

namespace Surl.Conformance;

/// <summary>
/// The pinned Curl port's <c>dns</c> client against a live <c>surl dns://</c> over each transport it has - UDP, TCP,
/// DNS over TLS, DNS over HTTPS and DNS over QUIC (BL-486, ADR-0083 decisions 1 to 3). Each asks for an A, an AAAA,
/// an MX and a missing name of the zone surl is given. Each expected value comes from the zone and the RFCs as
/// <c>Documentation/Conformance/Extended/DnsQuery.md</c> lists them - an authoritative answer carries the zone's
/// RRset and AA (RFC 1034 section 4.3.2), a missing name is NXDOMAIN with the zone's SOA in authority (RFC 2308) -
/// never from what the port printed beyond the bytes the server was required to send. Inconclusive when the pinned
/// client is absent.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class CurlPortClientQueriesSurlOverDnsTests
{
    private const string Zone = """
        $ORIGIN example.test.
        $TTL 300
        @    IN SOA ns.example.test. hostmaster.example.test. 1 7200 900 1209600 60
        @    IN NS  ns.example.test.
        ns   IN A   192.0.2.53
        www  IN A   192.0.2.10
        www  IN AAAA 2001:db8::10
        @    IN MX  10 mail.example.test.
        mail IN A   192.0.2.25
        """;

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [Description("DnsQuery:RFC1035-3.4.1-1, DnsQuery:RFC1034-4.3.2-1, DnsQuery:RFC1035-4.1.1-2: an A query is answered with the zone's address and AA")]
    [DataRow("udp")]
    [DataRow("tcp")]
    [DataRow("tls")]
    [DataRow("https")]
    [DataRow("quic")]
    public async Task A_IsAnsweredWithTheZonesAddress(string transport)
    {
        var (text, diagnostics) = await QueryAsync(transport, "www.example.test?type=A");

        StringAssert.Contains(text, "status: NOERROR", diagnostics);
        StringAssert.Contains(text, " aa", diagnostics);
        StringAssert.Contains(text, "www.example.test.\t300\tIN\tA\t192.0.2.10", diagnostics);
    }

    [TestMethod]
    [Description("DnsQuery:RFC3596-2.2-1, DnsQuery:RFC1034-4.3.2-1: an AAAA query is answered with the zone's IPv6 address")]
    [DataRow("udp")]
    [DataRow("tcp")]
    [DataRow("tls")]
    [DataRow("https")]
    [DataRow("quic")]
    public async Task Aaaa_IsAnsweredWithTheZonesAddress(string transport)
    {
        var (text, diagnostics) = await QueryAsync(transport, "www.example.test?type=AAAA");

        StringAssert.Contains(text, "status: NOERROR", diagnostics);
        StringAssert.Contains(text, "www.example.test.\t300\tIN\tAAAA\t2001:db8::10", diagnostics);
    }

    [TestMethod]
    [Description("DnsQuery:RFC1035-3.3-1, DnsQuery:RFC1035-3.3.9-1: an MX query is answered with the preference and exchange, the exchange's address in additional")]
    [DataRow("udp")]
    [DataRow("tcp")]
    [DataRow("tls")]
    [DataRow("https")]
    [DataRow("quic")]
    public async Task Mx_IsAnsweredWithTheZonesExchangeAndItsAddress(string transport)
    {
        var (text, diagnostics) = await QueryAsync(transport, "example.test?type=MX");

        StringAssert.Contains(text, "status: NOERROR", diagnostics);
        StringAssert.Contains(text, "example.test.\t300\tIN\tMX\t10 mail.example.test.", diagnostics);
        StringAssert.Contains(text, "mail.example.test.\t300\tIN\tA\t192.0.2.25", diagnostics);
    }

    [TestMethod]
    [Description("DnsQuery:RFC2308-2.1-1, DnsQuery:RFC2308-3-1, DnsQuery:RFC2308-3-2: a missing name is NXDOMAIN with the zone's SOA in authority, its TTL the lower of 300 and the minimum 60")]
    [DataRow("udp")]
    [DataRow("tcp")]
    [DataRow("tls")]
    [DataRow("https")]
    [DataRow("quic")]
    public async Task MissingName_IsNxdomainWithTheZonesSoa(string transport)
    {
        var (text, diagnostics) = await QueryAsync(transport, "missing.example.test?type=A");

        StringAssert.Contains(text, "status: NXDOMAIN", diagnostics);
        StringAssert.Contains(text, "ANSWER: 0", diagnostics);
        StringAssert.Contains(text, "example.test.\t60\tIN\tSOA\tns.example.test. hostmaster.example.test. 1 7200 900 1209600 60", diagnostics);
    }

    private async Task<(string Text, string Diagnostics)> QueryAsync(string transport, string nameAndType)
    {
        await using var surl = await SurlOnLoopback.StartAsync(
            "dns",
            new Dictionary<string, byte[]> { [".surl/dns/example.test.zone"] = Encoding.UTF8.GetBytes(Zone.ReplaceLineEndings("\n")) },
            [".surl", ".surl/dns"],
            transport is "udp" or "tcp" ? ["--dns-transport", transport] : ["--dns-transport", transport, "--self-signed"],
            TestContext.CancellationToken);

        var result = await PinnedCurlPortClientOnThisMachine.RunAsync(
            TestContext, "-sS", "-k", "-m", "30", "--dns-transport", transport, $"dns://127.0.0.1:{surl.BaseUrl.Port}/{nameAndType}");

        var diagnostics = $"{result}; stderr: {result.StandardError}; surl: {surl.Log}";
        Assert.AreEqual(0, result.ExitCode, diagnostics);
        return (Encoding.UTF8.GetString(result.StandardOutput), diagnostics);
    }
}
