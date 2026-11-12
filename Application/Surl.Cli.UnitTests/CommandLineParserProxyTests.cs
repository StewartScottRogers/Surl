using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Cli;

/// <summary>
/// <c>-x</c>/<c>--proxy</c>, <c>-U</c>/<c>--proxy-user</c>, <c>--proxy-user-file</c> and
/// <c>--proxy-allow-origin</c> read into <see cref="SurlCommandLine"/>, and their refusals
/// (ADR-0088 decision 7, BL-1004).
/// </summary>
[TestClass]
public sealed class CommandLineParserProxyTests
{
    private const string Url = "http://127.0.0.1:8080/";

    [TestMethod]
    public void Parse_NoProxyOption_ServesNoProxy()
    {
        var commandLine = Served(Url);

        Assert.IsEmpty(commandLine.ProxyListenUrls);
        Assert.IsEmpty(commandLine.ProxyAccounts);
        Assert.IsNull(commandLine.ProxyUserFile);
        Assert.IsEmpty(commandLine.ProxyAllowedOrigins);
    }

    [TestMethod]
    [DataRow("-x")]
    [DataRow("--proxy")]
    public void Parse_ProxyHttp_IsAnHttpProxyListenUrlAndNeedsNoOtherListenUrl(string option)
    {
        var commandLine = Served(option, "http://127.0.0.1:3128");

        CollectionAssert.AreEqual(new[] { new ListenUrl("http", "127.0.0.1", 3128) { IsProxy = true } }, commandLine.ProxyListenUrls.ToArray());
        Assert.IsEmpty(commandLine.ListenUrls);
    }

    [TestMethod]
    public void Parse_ProxyHttps_IsAnHttpsProxyListenUrl() =>
        CollectionAssert.AreEqual(
            new[] { new ListenUrl("https", "127.0.0.1", 3129) { IsProxy = true } },
            Served("--proxy=https://127.0.0.1:3129", "--cert", "c.pem", "--key", "k.pem").ProxyListenUrls.ToArray());

    [TestMethod]
    [DataRow("http://127.0.0.1", 1080)]
    [DataRow("https://127.0.0.1", 443)]
    public void Parse_ProxyWithoutAPort_UsesCurlsDefaultPort(string argument, int port) =>
        Assert.AreEqual(port, Served("-x", argument, "--self-signed").ProxyListenUrls[0].Port);

    [TestMethod]
    public void Parse_ProxyBesideAListenUrl_KeepsBothAndTheBareUrlIsNotAProxy()
    {
        var commandLine = Served(Url, "-x", "http://127.0.0.1:3128", "-x", "http://127.0.0.1:3129");

        Assert.AreEqual(1, commandLine.ListenUrls.Count);
        Assert.IsFalse(commandLine.ListenUrls[0].IsProxy);
        CollectionAssert.AreEqual(new[] { 3128, 3129 }, commandLine.ProxyListenUrls.Select(listenUrl => listenUrl.Port).ToArray());
    }

    [TestMethod]

    [DataRow("ftp://127.0.0.1:21", "ftp")]
    public void Parse_ProxyWithAnotherScheme_IsUnsupportedProtocol(string argument, string scheme) =>
        AssertRefused(["-x", argument, Url], SurlExitCode.UnsupportedProtocol, $"(1) Protocol \"{scheme}\" not supported");

    [TestMethod]
    public void Parse_MalformedProxy_IsMalformedUrl()
    {
        var result = CommandLineParser.Parse(["-x", "http://127.0.0.1:3128/path", Url]);

        Assert.AreEqual(CommandLineOutcome.Refused, result.Outcome);
        Assert.AreEqual(SurlExitCode.MalformedUrl, result.Failure?.ExitCode);
    }

    [TestMethod]
    public void Parse_ProxyWithoutAnArgument_RequiresParameter() =>
        AssertRefused(["-x"], SurlExitCode.FailedInit, "option -x: requires parameter", followedByTryHelpLine: true);

    [TestMethod]
    [DataRow("--unix-socket", "--unix-socket")]
    [DataRow("--abstract-unix-socket", "--abstract-unix-socket")]
    public void Parse_ProxyWithAUnixSocket_IsRefused(string option, string reported) =>
        AssertRefused(
            [option, "s", "-x", "http://127.0.0.1:3128"],
            SurlExitCode.FailedInit,
            $"option {reported}: cannot be used with --proxy",
            followedByTryHelpLine: true);

    [TestMethod]
    public void Parse_ProxyUser_AddsAProxyAccountApartFromTheServerAccounts()
    {
        var commandLine = Served("-U", "alice:secret", "--proxy-user=bob:pw", "-u", "carol:x", Url);

        CollectionAssert.AreEqual(new[] { "alice", "bob" }, commandLine.ProxyAccounts.Select(account => account.UserName).ToArray());
        CollectionAssert.AreEqual(new[] { "secret", "pw" }, commandLine.ProxyAccounts.Select(account => account.Password).ToArray());
        CollectionAssert.AreEqual(new[] { "carol" }, commandLine.Accounts.Select(account => account.UserName).ToArray());
    }

    [TestMethod]
    public void Parse_ProxyUserWithoutAColon_IsRefusedWithoutTheText() =>
        AssertRefused(["-U", "alicesecret", Url], SurlExitCode.FailedInit, "option -U: " + OptionArgumentReader.ReadAccount("alicesecret", out _), followedByTryHelpLine: true);

    [TestMethod]
    public void Parse_ProxyUserFile_IsKeptAsGiven()
    {
        Assert.AreEqual("proxy-users.txt", Served("--proxy-user-file", "proxy-users.txt", Url).ProxyUserFile);
        Assert.IsEmpty(Served("--proxy-user-file", "proxy-users.txt", Url).Accounts);
    }

    [TestMethod]
    public void Parse_ProxyUserFileWithoutAnArgument_RequiresParameter() =>
        AssertRefused(["--proxy-user-file"], SurlExitCode.FailedInit, "option --proxy-user-file: requires parameter", followedByTryHelpLine: true);

    [TestMethod]
    public void Parse_NoProxyAuth_AcceptsDigestAndBasicForTheProxy()
    {
        var commandLine = Served("-x", "http://127.0.0.1:3128", "-U", "u:p");

        Assert.IsNull(commandLine.GivenProxyAuthenticationMethods);
        CollectionAssert.AreEqual(new[] { "digest", "basic" }, commandLine.AcceptedProxyAuthenticationMethods.ToArray());
    }

    [TestMethod]
    [DataRow("basic,ntlm", new[] { "ntlm", "basic" }, DisplayName = "Two words, in ADR-0032 section 3's order")]
    [DataRow("NTLM,Negotiate", new[] { "negotiate", "ntlm" }, DisplayName = "Any case")]
    [DataRow("digest", new[] { "digest" }, DisplayName = "One word")]
    public void Parse_ProxyAuth_ReadsItsWordsIntoTheProxysMethods(string argument, string[] methods)
    {
        var commandLine = Served("--proxy", "http://127.0.0.1:3128", "-U", "u:p", "--proxy-auth", argument);

        CollectionAssert.AreEqual(methods, commandLine.GivenProxyAuthenticationMethods!.ToArray());
        CollectionAssert.AreEqual(methods, commandLine.AcceptedProxyAuthenticationMethods.ToArray());
    }

    [TestMethod]
    public void Parse_ProxyAuthGivenTwice_TheLastOneWins() =>
        CollectionAssert.AreEqual(
            new[] { "ntlm" },
            Served("--proxy-auth", "basic", "--proxy-auth=ntlm", Url).AcceptedProxyAuthenticationMethods.ToArray());

    [TestMethod]
    public void Parse_AuthAndProxyAuth_StayIndependent()
    {
        var both = Served("--auth", "bearer", "--proxy-auth", "ntlm", Url);
        var authOnly = Served("--auth", "bearer", Url);
        var proxyAuthOnly = Served("--proxy-auth", "ntlm", Url);

        CollectionAssert.AreEqual(new[] { "bearer" }, both.AcceptedAuthenticationMethods.ToArray());
        CollectionAssert.AreEqual(new[] { "ntlm" }, both.AcceptedProxyAuthenticationMethods.ToArray());
        CollectionAssert.AreEqual(new[] { "digest", "basic" }, authOnly.AcceptedProxyAuthenticationMethods.ToArray());
        Assert.IsNull(proxyAuthOnly.GivenAuthenticationMethods);
    }

    [TestMethod]
    [DataRow("kerberos", DisplayName = "An unknown word")]
    [DataRow("basic,,digest", DisplayName = "An empty item")]
    [DataRow("", DisplayName = "Empty argument")]
    public void Parse_ProxyAuthWithABadWord_IsRefusedAsFailedInit(string argument) =>
        AssertRefused(
            ["--proxy-auth", argument, Url],
            SurlExitCode.FailedInit,
            "option --proxy-auth: " + OptionArgumentReader.ReadAuthenticationMethods(argument, out _),
            followedByTryHelpLine: true);

    [TestMethod]
    public void Parse_ProxyAuthWithoutAnArgument_RequiresParameter() =>
        AssertRefused(["--proxy-auth"], SurlExitCode.FailedInit, "option --proxy-auth: requires parameter", followedByTryHelpLine: true);

    [TestMethod]
    public void Parse_ProxyAllowOrigin_AddsEachRangeInOrder()
    {
        var origins = Served("--proxy-allow-origin", "10.0.0.0/8", "--proxy-allow-origin=*", "--proxy-allow-origin", "::1", Url).ProxyAllowedOrigins;

        Assert.AreEqual(ProxyAllowedOrigin.Range(IPAddress.Parse("10.0.0.0"), 8), origins[0]);
        Assert.IsTrue(origins[1].AdmitsEveryAddress);
        Assert.AreEqual(ProxyAllowedOrigin.Range(IPAddress.IPv6Loopback, 128), origins[2]);
    }

    [TestMethod]
    [DataRow("example.com", "expected an IPv4 or IPv6 address, an address/prefix, or *")]
    [DataRow("10.0.0.0/33", "the prefix length is not 0 to 32 for IPv4 or 0 to 128 for IPv6")]
    public void Parse_BadProxyAllowOrigin_IsRefused(string argument, string reason) =>
        AssertRefused(["--proxy-allow-origin", argument, Url], SurlExitCode.FailedInit, $"option --proxy-allow-origin: {reason}", followedByTryHelpLine: true);

    [TestMethod]
    public void Parse_ProxyOptionsFromAConfigFile_EqualTheSameOptionsOnTheCommandLine()
    {
        var fromFile = CommandLineParser.Parse(
            ["-K", "a.cfg"],
            [],
            new FakeConfigFileSource(("a.cfg", "proxy = http://127.0.0.1:3128\nproxy-user = alice:secret\nproxy-allow-origin = 10.0.0.0/8\n")));
        var fromCommandLine = Served("-x", "http://127.0.0.1:3128", "-U", "alice:secret", "--proxy-allow-origin", "10.0.0.0/8");

        Assert.AreEqual(CommandLineOutcome.Serve, fromFile.Outcome, fromFile.Failure?.Message);
        CollectionAssert.AreEqual(fromCommandLine.ProxyListenUrls.ToArray(), fromFile.CommandLine!.ProxyListenUrls.ToArray());
        CollectionAssert.AreEqual(fromCommandLine.ProxyAccounts.ToArray(), fromFile.CommandLine.ProxyAccounts.ToArray());
        CollectionAssert.AreEqual(fromCommandLine.ProxyAllowedOrigins.ToArray(), fromFile.CommandLine.ProxyAllowedOrigins.ToArray());
    }

    [TestMethod]
    public void Parse_NoListenUrlAtAll_StillNeedsOne() =>
        AssertRefused(["-U", "alice:secret"], SurlExitCode.FailedInit, CommandLineParser.NoUrlSpecified, followedByTryHelpLine: true);

    private static void AssertRefused(string[] arguments, SurlExitCode exitCode, string message, bool followedByTryHelpLine = false)
    {
        var result = CommandLineParser.Parse(arguments);

        Assert.AreEqual(CommandLineOutcome.Refused, result.Outcome);
        Assert.AreEqual(new CommandLineFailure(exitCode, message, followedByTryHelpLine), result.Failure);
    }

    private static SurlCommandLine Served(params string[] arguments)
    {
        var result = CommandLineParser.Parse(arguments);

        Assert.AreEqual(CommandLineOutcome.Serve, result.Outcome, result.Failure?.Message);
        Assert.IsNotNull(result.CommandLine);
        return result.CommandLine;
    }
}
