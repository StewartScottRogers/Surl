using Surl.Protocol.Abstractions;

namespace Surl.Cli;

/// <summary>
/// <c>--unix-socket</c> and <c>--abstract-unix-socket</c> read into <see cref="SurlCommandLine.UnixSocket"/>
/// and the one listen URL's <see cref="ListenUrl.UnixSocket"/>, and their refusals (ADR-0094,
/// decisions 1 and 2, BL-548).
/// </summary>
[TestClass]
public sealed class CommandLineParserUnixSocketTests
{
    private const string Url = "http://localhost/";
    private const string SocketPath = "/tmp/surl.sock";

    [TestMethod]
    public void Parse_NoUnixSocket_ListensOnTcp()
    {
        var commandLine = Served(Url);

        Assert.IsNull(commandLine.UnixSocket);
        Assert.IsNull(commandLine.ListenUrls[0].UnixSocket);
    }

    [TestMethod]
    public void Parse_UnixSocket_SetsTheSocketFileOnTheListenUrl()
    {
        var commandLine = Served("--unix-socket", SocketPath, Url);

        var expected = new UnixSocketAddress(SocketPath, isAbstract: false);
        Assert.AreEqual(expected, commandLine.UnixSocket);
        Assert.AreEqual(new ListenUrl("http", "localhost", 80) { UnixSocket = expected }, commandLine.ListenUrls.Single());
    }

    [TestMethod]
    public void Parse_UnixSocketAfterTheListenUrl_SetsItAsWell() =>
        Assert.AreEqual(new UnixSocketAddress(SocketPath, isAbstract: false), Served(Url, $"--unix-socket={SocketPath}").ListenUrls[0].UnixSocket);

    [TestMethod]
    public void Parse_AbstractUnixSocket_SetsTheAbstractNameOnTheListenUrl() =>
        Assert.AreEqual(new UnixSocketAddress("surl-test", isAbstract: true), Served("--abstract-unix-socket", "surl-test", Url).ListenUrls[0].UnixSocket);

    [TestMethod]
    [DataRow(new[] { "--unix-socket", "a.sock", "--abstract-unix-socket", "b" }, "b", true, DisplayName = "Abstract given last")]
    [DataRow(new[] { "--abstract-unix-socket", "b", "--unix-socket", "a.sock" }, "a.sock", false, DisplayName = "File given last")]
    public void Parse_BothOptions_TheLastGivenWins(string[] options, string name, bool isAbstract) =>
        Assert.AreEqual(new UnixSocketAddress(name, isAbstract), Served([.. options, Url]).UnixSocket);

    [TestMethod]
    [DataRow("--unix-socket")]
    [DataRow("--abstract-unix-socket")]
    public void Parse_UnixSocketWithoutItsArgument_IsRefused(string option) =>
        AssertRefused([Url, option], $"option {option}: {CommandLineParser.RequiresParameter}");

    [TestMethod]
    [DataRow("--unix-socket")]
    [DataRow("--abstract-unix-socket")]
    public void Parse_UnixSocketBlankArgument_IsRefused(string option) =>
        AssertRefused([option, string.Empty, Url], $"option {option}: {OptionArgumentReader.Blank}");

    [TestMethod]
    [DataRow("--unix-socket", "a.sock")]
    [DataRow("--abstract-unix-socket", "a")]
    public void Parse_UnixSocketWithTwoListenUrls_IsRefused(string option, string argument) =>
        AssertRefused([option, argument, Url, "https://localhost/"], $"option {option}: serves one listen URL, and 2 were given");

    [TestMethod]
    public void Parse_UnixSocketWithTftp_IsRefusedAsUdp() =>
        AssertRefused(["--unix-socket", SocketPath, "tftp://localhost/"], "option --unix-socket: tftp listens on UDP");

    [TestMethod]
    [DataRow("coap")]
    [DataRow("coaps")]
    [DataRow("dns")]
    public void Parse_UnixSocketWithAnExtendedUdpScheme_IsRefusedAsUdp(string scheme)
    {
        var failure = Refused(["--unix-socket", SocketPath, $"{scheme}://localhost/"], [scheme]);

        Assert.AreEqual($"option --unix-socket: {scheme} listens on UDP", failure.Message);
    }

    [TestMethod]
    public void Parse_UnixSocketWithHttp3_IsRefusedAsUdp() =>
        AssertRefused(["--http3", "--unix-socket", SocketPath, "https://localhost/"], "option --unix-socket: --http3 listens on UDP");

    [TestMethod]
    [DataRow("ftp")]
    [DataRow("ftps")]
    public void Parse_UnixSocketWithFtp_ServesTheControlConnectionOnTheSocket(string scheme)
    {
        var commandLine = Served("--unix-socket", SocketPath, $"{scheme}://localhost/");

        var listenUrl = commandLine.ListenUrls.Single();
        Assert.AreEqual(scheme, listenUrl.Scheme);
        Assert.AreEqual(new UnixSocketAddress(SocketPath, isAbstract: false), listenUrl.UnixSocket);
    }

    [TestMethod]
    [DataRow("--unix-socket", "socks5", false)]
    [DataRow("--unix-socket", "socks5h", false)]
    [DataRow("--abstract-unix-socket", "socks5h", true)]
    public void Parse_UnixSocketWithASocks5ListenUrl_ServesSocks5OnTheSocket(string option, string scheme, bool isAbstract)
    {
        var commandLine = Served(option, "surl-socks.sock", $"{scheme}://localhost");

        var expected = new ListenUrl(scheme, "localhost", 1080) { UnixSocket = new UnixSocketAddress("surl-socks.sock", isAbstract) };
        Assert.AreEqual(expected, commandLine.ListenUrls.Single());
    }

    [TestMethod]
    [DataRow("socks4")]
    [DataRow("socks4a")]
    public void Parse_UnixSocketWithASocks4ListenUrl_ServesSocks4OnTheSocket(string scheme) =>
        Assert.AreEqual(
            new UnixSocketAddress(SocketPath, isAbstract: false),
            Served("--unix-socket", SocketPath, $"{scheme}://localhost").ListenUrls.Single().UnixSocket);

    [TestMethod]
    public void Parse_UnixSocketWithASocks5ProxyOptionUrl_IsRefusedAsAProxy() =>
        AssertRefused(["--unix-socket", SocketPath, "-x", "socks5h://localhost"], "option --unix-socket: cannot be used with --proxy");

    [TestMethod]
    public void Parse_UnixSocketWithNoListenUrl_IsRefusedForTheMissingUrl() =>
        Assert.AreEqual(CommandLineParser.NoUrlSpecified, Refused(["--unix-socket", SocketPath], []).Message);

    private static SurlCommandLine Served(params string[] arguments)
    {
        var result = CommandLineParser.Parse(arguments);

        Assert.AreEqual(CommandLineOutcome.Serve, result.Outcome, result.Failure?.Message);
        return result.CommandLine!;
    }

    private static CommandLineFailure Refused(string[] arguments, string[] extendedSchemes)
    {
        var result = CommandLineParser.Parse(arguments, extendedSchemes);

        Assert.AreEqual(CommandLineOutcome.Refused, result.Outcome);
        Assert.IsNotNull(result.Failure);
        Assert.AreEqual(SurlExitCode.FailedInit, result.Failure.ExitCode);
        Assert.IsTrue(result.Failure.FollowedByTryHelpLine);
        return result.Failure;
    }

    private static void AssertRefused(string[] arguments, string message) =>
        Assert.AreEqual(message, Refused(arguments, []).Message);
}
