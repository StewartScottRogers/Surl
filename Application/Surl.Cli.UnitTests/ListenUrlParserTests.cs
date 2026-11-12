using Surl.Protocol.Abstractions;

namespace Surl.Cli;

[TestClass]
public sealed class ListenUrlParserTests
{
    [TestMethod]
    [DataRow("http://127.0.0.1:8080/", "http", "127.0.0.1", 8080, DisplayName = "IPv4 host and port")]
    [DataRow("http://127.0.0.1:8080", "http", "127.0.0.1", 8080, DisplayName = "No trailing slash")]
    [DataRow("HTTP://127.0.0.1:8080/", "http", "127.0.0.1", 8080, DisplayName = "Scheme lower-cased")]
    [DataRow("HtTpS://127.0.0.1:8443/", "https", "127.0.0.1", 8443, DisplayName = "Mixed-case scheme lower-cased")]
    [DataRow("http://[::1]:8080/", "http", "::1", 8080, DisplayName = "Bracketed IPv6, brackets removed")]
    [DataRow("HTTP://[::1]:8080", "http", "::1", 8080, DisplayName = "ADR example: HTTP://[::1]:8080")]
    [DataRow("http://[::1]/", "http", "::1", 80, DisplayName = "Bracketed IPv6, default port")]
    [DataRow("http://[fe80::1%25eth0]:8080/", "http", "fe80::1%eth0", 8080, DisplayName = "IPv6 zone decoded")]
    [DataRow("http://[::]:0/", "http", "::", 0, DisplayName = "IPv6 any address")]
    [DataRow("http://localhost/", "http", "localhost", 80, DisplayName = "Host name, default port")]
    [DataRow("http://LocalHost/", "http", "LocalHost", 80, DisplayName = "Host name kept as written")]
    [DataRow("http://my-host.example/", "http", "my-host.example", 80, DisplayName = "Host name with hyphen and dots")]
    [DataRow("http://127.0.0.1:/", "http", "127.0.0.1", 80, DisplayName = "Empty port is the default port")]
    [DataRow("http://127.0.0.1:0/", "http", "127.0.0.1", 0, DisplayName = "Port 0 asks for an ephemeral port")]
    [DataRow("http://127.0.0.1:0080/", "http", "127.0.0.1", 80, DisplayName = "Leading zeros allowed")]
    [DataRow("http://127.0.0.1:65535/", "http", "127.0.0.1", 65535, DisplayName = "Highest port")]
    [DataRow("http://255.255.255.255/", "http", "255.255.255.255", 80, DisplayName = "Highest IPv4 parts")]
    [DataRow("http://1.2.3/", "http", "1.2.3", 80, DisplayName = "Three numbers is a host name")]
    [DataRow("http://1..3.4/", "http", "1..3.4", 80, DisplayName = "Four parts, one empty, is a host name")]
    [DataRow("http://a.b.c.d/", "http", "a.b.c.d", 80, DisplayName = "Four labels, not numbers, is a host name")]
    [DataRow("tftp://0.0.0.0:0", "tftp", "0.0.0.0", 0, DisplayName = "ADR example: tftp any address")]
    public void Parse_AcceptedListenUrl_ReturnsListenUrl(string argument, string scheme, string host, int port)
    {
        var result = ListenUrlParser.Parse(argument);

        Assert.IsTrue(result.Succeeded);
        Assert.IsNull(result.Failure);
        Assert.AreEqual(new ListenUrl(scheme, host, port), result.ListenUrl);
    }

    [TestMethod]
    [DataRow("dict", 2628)]
    [DataRow("ftp", 21)]
    [DataRow("ftps", 990)]
    [DataRow("gopher", 70)]
    [DataRow("gophers", 70)]
    [DataRow("http", 80)]
    [DataRow("https", 443)]
    [DataRow("imap", 143)]
    [DataRow("imaps", 993)]
    [DataRow("ldap", 389)]
    [DataRow("ldaps", 636)]
    [DataRow("mqtt", 1883)]
    [DataRow("mqtts", 8883)]
    [DataRow("pop3", 110)]
    [DataRow("pop3s", 995)]
    [DataRow("rtsp", 554)]
    [DataRow("scp", 22)]
    [DataRow("sftp", 22)]
    [DataRow("smb", 445)]
    [DataRow("smbs", 445)]
    [DataRow("smtp", 25)]
    [DataRow("smtps", 465)]
    [DataRow("socks4", 1080)]
    [DataRow("socks4a", 1080)]
    [DataRow("socks5", 1080)]
    [DataRow("socks5h", 1080)]
    [DataRow("telnet", 23)]
    [DataRow("tftp", 69)]
    [DataRow("ws", 80)]
    [DataRow("wss", 443)]
    public void Parse_AcceptedSchemeWithoutPort_UsesSchemesDefaultPort(string scheme, int defaultPort)
    {
        var result = ListenUrlParser.Parse($"{scheme}://127.0.0.1/");

        Assert.AreEqual(new ListenUrl(scheme, "127.0.0.1", defaultPort), result.ListenUrl);
    }

    [TestMethod]
    public void Schemes_AreTheThirtyWireSchemes()
    {
        var schemes = SchemeDefaultPorts.Schemes;

        Assert.HasCount(30, schemes);
    }

    [TestMethod]
    [DataRow("file:///tmp", "file", DisplayName = "file has no wire")]
    [DataRow("nosuch://127.0.0.1/", "nosuch", DisplayName = "Unknown scheme")]
    [DataRow("NOSUCH://127.0.0.1/", "nosuch", DisplayName = "Unknown scheme lower-cased in the message")]
    [DataRow("ipfs://127.0.0.1/", "ipfs", DisplayName = "ipfs is a gateway path")]
    [DataRow("ipns://127.0.0.1/", "ipns", DisplayName = "ipns is a gateway path")]
    public void Parse_SchemeNotAccepted_RefusesAsUnsupportedProtocol(string argument, string scheme)
    {
        var result = ListenUrlParser.Parse(argument);

        AssertRefused(result, SurlExitCode.UnsupportedProtocol, $"(1) Protocol \"{scheme}\" not supported");
    }

    [TestMethod]
    [DataRow("not a url", "Malformed input to a URL function", DisplayName = "Not a URL")]
    [DataRow("", "Malformed input to a URL function", DisplayName = "Empty text")]
    [DataRow("127.0.0.1:8080", "Malformed input to a URL function", DisplayName = "No scheme is not guessed")]
    [DataRow("://127.0.0.1/", "Malformed input to a URL function", DisplayName = "Empty scheme")]
    [DataRow("1http://127.0.0.1/", "Malformed input to a URL function", DisplayName = "Scheme starting with a digit")]
    [DataRow("ht_tp://127.0.0.1/", "Malformed input to a URL function", DisplayName = "Scheme with a bad character")]
    [DataRow("http://user@127.0.0.1/", "A listen URL cannot have a user name or password", DisplayName = "User name")]
    [DataRow("socks5h://alice@127.0.0.1/", "A listen URL cannot have a user name or password", DisplayName = "SOCKS user name")]
    [DataRow("http://user:secret@127.0.0.1:8080/", "A listen URL cannot have a user name or password", DisplayName = "User name and password")]
    [DataRow("http://[::1/", "Bad IPv6 address", DisplayName = "No closing bracket")]
    [DataRow("http://[127.0.0.1]/", "Bad IPv6 address", DisplayName = "IPv4 inside brackets")]
    [DataRow("http://[nonsense]/", "Bad IPv6 address", DisplayName = "Not an address inside brackets")]
    [DataRow("http://[]/", "Bad IPv6 address", DisplayName = "Empty brackets")]
    [DataRow("http://[fe80::1%eth0]/", "Bad IPv6 address", DisplayName = "Zone not encoded as %25")]
    [DataRow("http://[nonsense%25eth0]/", "Bad IPv6 address", DisplayName = "Not an address before a zone")]
    [DataRow("http://[fe80::1%25]/", "Bad IPv6 address", DisplayName = "Empty zone")]
    [DataRow("http://[fe80::1%25e/th]/", "Bad IPv6 address", DisplayName = "Zone cut by a slash")]
    [DataRow("http://[fe80::1%25e!h]/", "Bad IPv6 address", DisplayName = "Zone with a bad character")]
    [DataRow("http://[::1]x/", "Malformed input to a URL function", DisplayName = "Text after the closing bracket")]
    [DataRow("http://::1/", "Malformed input to a URL function", DisplayName = "Unbracketed IPv6 address")]
    [DataRow("http://127.0.0.1:80:80/", "Malformed input to a URL function", DisplayName = "Second colon")]
    [DataRow("http://256.0.0.1/", "Bad IPv4 address", DisplayName = "IPv4 part above 255")]
    [DataRow("http://1.2.3.99999999999/", "Bad IPv4 address", DisplayName = "IPv4 part too long for a number")]
    [DataRow("http://host_name/", "Malformed input to a URL function", DisplayName = "Host name with an underscore")]
    [DataRow("http://héte/", "Malformed input to a URL function", DisplayName = "Host name with a non-ASCII letter")]
    [DataRow("http://:8080/", "No host part in the URL", DisplayName = "Missing host")]
    [DataRow("http:///", "No host part in the URL", DisplayName = "Empty authority")]
    [DataRow("http://", "No host part in the URL", DisplayName = "Nothing after the scheme")]
    [DataRow("http://127.0.0.1:65536/", "Port number was not a decimal number between 0 and 65535", DisplayName = "Port above 65535")]
    [DataRow("http://127.0.0.1:99999999999/", "Port number was not a decimal number between 0 and 65535", DisplayName = "Port too long for a number")]
    [DataRow("http://127.0.0.1:abc/", "Port number was not a decimal number between 0 and 65535", DisplayName = "Non-numeric port")]
    [DataRow("http://127.0.0.1:-1/", "Port number was not a decimal number between 0 and 65535", DisplayName = "Signed port")]
    [DataRow("http://[::1]:abc/", "Port number was not a decimal number between 0 and 65535", DisplayName = "Non-numeric port after IPv6")]
    [DataRow("http://127.0.0.1:8080/x", "A listen URL cannot have a path, query or fragment", DisplayName = "Path")]
    [DataRow("socks5://127.0.0.1:1080/x", "A listen URL cannot have a path, query or fragment", DisplayName = "SOCKS path")]
    [DataRow("socks4://127.0.0.1:1080?q", "A listen URL cannot have a path, query or fragment", DisplayName = "SOCKS query")]
    [DataRow("http://127.0.0.1:8080//", "A listen URL cannot have a path, query or fragment", DisplayName = "Two slashes")]
    [DataRow("http://127.0.0.1:8080?q", "A listen URL cannot have a path, query or fragment", DisplayName = "Query")]
    [DataRow("http://127.0.0.1:8080/#f", "A listen URL cannot have a path, query or fragment", DisplayName = "Fragment")]
    public void Parse_MalformedListenUrl_RefusesAsMalformedUrl(string argument, string reason)
    {
        var result = ListenUrlParser.Parse(argument);

        AssertRefused(result, SurlExitCode.MalformedUrl, $"(3) URL rejected: {reason}");
    }

    [TestMethod]
    public void Parse_NullArgument_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => ListenUrlParser.Parse(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => ListenUrlParser.Parse(null!, []));
        Assert.ThrowsExactly<ArgumentNullException>(() => ListenUrlParser.Parse("s3://127.0.0.1/", null!));
    }

    [TestMethod]
    [DataRow("s3://127.0.0.1/", "s3", 443)]
    [DataRow("S3://127.0.0.1/", "s3", 443, DisplayName = "Scheme lower-cased before the lookup")]
    [DataRow("redis://127.0.0.1/", "redis", 6379)]
    public void Parse_ExtendedSchemeARegisteredServerClaims_UsesItsExtendedDefaultPort(string argument, string scheme, int port)
    {
        var result = ListenUrlParser.Parse(argument, ["s3", "redis"]);

        Assert.AreEqual(new ListenUrl(scheme, "127.0.0.1", port), result.ListenUrl);
    }

    [TestMethod]
    public void Parse_ExtendedSchemeWithPort_UsesThePortGiven()
    {
        var result = ListenUrlParser.Parse("s3://127.0.0.1:0/", ["s3"]);

        Assert.AreEqual(new ListenUrl("s3", "127.0.0.1", 0), result.ListenUrl);
    }

    [TestMethod]
    [DataRow("s3", new string[0], DisplayName = "No extended server registered")]
    [DataRow("s3", new[] { "redis" }, DisplayName = "Another extended scheme registered")]
    public void Parse_ExtendedSchemeNotAccepted_IsRefusedAsAnUnknownSchemeIs(string scheme, string[] extendedSchemes)
    {
        var result = ListenUrlParser.Parse($"{scheme}://127.0.0.1/", extendedSchemes);

        AssertRefused(result, SurlExitCode.UnsupportedProtocol, $"(1) Protocol \"{scheme}\" not supported");
    }

    [TestMethod]
    [DataRow("mcp+stdio:")]
    [DataRow("mcp+stdio://")]
    [DataRow("MCP+STDIO:")]
    public void Parse_McpStdioClaimed_IsAListenUrlWithNoHostAndNoPort(string argument)
    {
        var result = ListenUrlParser.Parse(argument, ["mcp+stdio"]);

        Assert.AreEqual(new ListenUrl("mcp+stdio", string.Empty, 0), result.ListenUrl);
    }

    [TestMethod]
    [DataRow("mcp+stdio://localhost/")]
    [DataRow("mcp+stdio:surl")]
    [DataRow("mcp+stdio://:80")]
    public void Parse_McpStdioWithAPart_IsMalformed(string argument)
    {
        var result = ListenUrlParser.Parse(argument, ["mcp+stdio"]);

        AssertRefused(result, SurlExitCode.MalformedUrl, "(3) URL rejected: mcp+stdio: names no host, port, path, query or fragment");
    }

    [TestMethod]
    [DataRow("mcp+stdio:", new string[0])]
    [DataRow("mcp+stdio://", new[] { "mcp" })]
    public void Parse_McpStdioNotClaimed_IsRefusedAsAnUnknownSchemeIs(string argument, string[] extendedSchemes)
    {
        var result = ListenUrlParser.Parse(argument, extendedSchemes);

        AssertRefused(result, SurlExitCode.UnsupportedProtocol, "(1) Protocol \"mcp+stdio\" not supported");
    }

    [TestMethod]
    public void Parse_OriginalSchemeAlsoListedAsExtended_KeepsUpstreamCurlsDefaultPort()
    {
        var result = ListenUrlParser.Parse("mqtt://127.0.0.1/", ["mqtt"]);

        Assert.AreEqual(new ListenUrl("mqtt", "127.0.0.1", 1883), result.ListenUrl);
    }

    [TestMethod]
    [DataRow("http://127.0.0.1:3128", "http", "127.0.0.1", 3128, DisplayName = "ADR example: the HTTP proxy")]
    [DataRow("https://127.0.0.1:3129", "https", "127.0.0.1", 3129, DisplayName = "ADR example: the HTTPS proxy")]
    [DataRow("http://127.0.0.1", "http", "127.0.0.1", 1080, DisplayName = "http proxy default port is curl's 1080")]
    [DataRow("HTTPS://[::1]/", "https", "::1", 443, DisplayName = "https proxy default port is 443, scheme lower-cased")]
    [DataRow("http://localhost:0", "http", "localhost", 0, DisplayName = "Port 0 asks for an ephemeral port")]
    [DataRow("socks5://127.0.0.1", "socks5", "127.0.0.1", 1080, DisplayName = "socks5 proxy default port is curl's 1080")]
    [DataRow("SOCKS4A://127.0.0.1:1081", "socks4a", "127.0.0.1", 1081, DisplayName = "socks4a, scheme lower-cased")]
    public void ParseProxy_HttpOrHttpsListenUrl_ReturnsProxyListenUrl(string argument, string scheme, string host, int port)
    {
        var result = ListenUrlParser.ParseProxy(argument);

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual(new ListenUrl(scheme, host, port) { IsProxy = true }, result.ListenUrl);
        Assert.IsTrue(result.ListenUrl.IsProxy);
    }

    [TestMethod]
    [DataRow("ftp://127.0.0.1/", "ftp", DisplayName = "An origin scheme is no proxy")]
    [DataRow("ws://127.0.0.1/", "ws", DisplayName = "ws is no proxy")]
    public void ParseProxy_OtherScheme_RefusesAsUnsupportedProtocol(string argument, string scheme)
    {
        var result = ListenUrlParser.ParseProxy(argument);

        AssertRefused(result, SurlExitCode.UnsupportedProtocol, $"(1) Protocol \"{scheme}\" not supported");
    }

    [TestMethod]
    [DataRow("127.0.0.1:3128", "Malformed input to a URL function", DisplayName = "No scheme")]
    [DataRow("http://user:secret@127.0.0.1:3128", "A listen URL cannot have a user name or password", DisplayName = "User and password")]
    [DataRow("http://127.0.0.1:65536", "Port number was not a decimal number between 0 and 65535", DisplayName = "Port too high")]
    [DataRow("https://127.0.0.1:3129/proxy", "A listen URL cannot have a path, query or fragment", DisplayName = "Path")]
    public void ParseProxy_MalformedListenUrl_RefusesAsMalformedUrl(string argument, string reason)
    {
        var result = ListenUrlParser.ParseProxy(argument);

        AssertRefused(result, SurlExitCode.MalformedUrl, $"(3) URL rejected: {reason}");
        Assert.DoesNotContain("secret", result.Failure!.Message);
    }

    [TestMethod]
    public void ParseProxy_NullArgument_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => ListenUrlParser.ParseProxy(null!));

    [TestMethod]
    public void Parse_HttpListenUrl_IsNotAProxy() =>
        Assert.IsFalse(ListenUrlParser.Parse("http://127.0.0.1:3128").ListenUrl!.IsProxy);

    private static void AssertRefused(ListenUrlParseResult result, SurlExitCode exitCode, string message)
    {
        Assert.IsFalse(result.Succeeded);
        Assert.IsNull(result.ListenUrl);
        Assert.AreEqual(new CommandLineFailure(exitCode, message), result.Failure);
    }
}
