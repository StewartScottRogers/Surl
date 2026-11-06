using Surl.Protocol.Abstractions;

namespace Surl.Cli;

/// <summary>
/// <c>--control</c> and <c>--control-token-file</c> read into <see cref="SurlCommandLine.ControlListenUrl"/>
/// and <see cref="SurlCommandLine.ControlTokenFile"/>, and their refusals (ADR-0124 decision 7, BL-815).
/// </summary>
[TestClass]
public sealed class CommandLineParserControlTests
{
    private const string Url = "http://127.0.0.1:8080/";
    private const string TokenFile = "t";

    [TestMethod]
    public void Parse_NoControl_ServesNoControlEndpoint()
    {
        var commandLine = Served(Url);

        Assert.IsNull(commandLine.ControlListenUrl);
        Assert.IsNull(commandLine.ControlTokenFile);
    }

    [TestMethod]
    public void Parse_ControlOnLoopbackWithTokenFile_IsRead()
    {
        var commandLine = Served("--control", "http://127.0.0.1:0", "--control-token-file", TokenFile, Url);

        Assert.AreEqual(new ListenUrl("http", "127.0.0.1", 0), commandLine.ControlListenUrl);
        Assert.AreEqual(TokenFile, commandLine.ControlTokenFile);
        CollectionAssert.AreEqual(new[] { new ListenUrl("http", "127.0.0.1", 8080) }, commandLine.ListenUrls.ToArray());
    }

    [TestMethod]
    [DataRow("http://localhost:0", "localhost", 0)]
    [DataRow("HTTP://LocalHost:9000/", "LocalHost", 9000)]
    [DataRow("http://127.255.255.254:1", "127.255.255.254", 1)]
    [DataRow("http://[::1]:0", "::1", 0)]
    [DataRow("http://127.0.0.1", "127.0.0.1", 80)]
    public void Parse_ControlOnEachLoopbackHost_IsRead(string argument, string host, int port) =>
        Assert.AreEqual(
            new ListenUrl("http", host, port),
            Served($"--control={argument}", "--control-token-file", TokenFile, Url).ControlListenUrl);

    [TestMethod]
    public void Parse_ControlGivenTwice_TheLastWins() =>
        Assert.AreEqual(
            new ListenUrl("http", "localhost", 2),
            Served("--control", "http://127.0.0.1:1", "--control", "http://localhost:2", "--control-token-file", TokenFile, Url).ControlListenUrl);

    [TestMethod]
    [DataRow("http://0.0.0.0:0")]
    [DataRow("http://192.168.1.1:0")]
    [DataRow("http://128.0.0.1:0")]
    [DataRow("http://[::]:0")]
    [DataRow("http://[::ffff:127.0.0.1]:0")]
    [DataRow("http://[::1%25eth0]:0")]
    [DataRow("http://127.1:0")]
    [DataRow("http://example.com:0")]
    public void Parse_ControlNotOnLoopback_IsRefused(string argument) =>
        AssertRefused(
            ["--control", argument, "--control-token-file", TokenFile, Url],
            SurlExitCode.FailedInit,
            $"(2) --control must listen on loopback (127.0.0.0/8, ::1 or localhost): {argument}");

    [TestMethod]
    [DataRow("https://127.0.0.1:0", "https")]
    [DataRow("FTP://127.0.0.1:0", "ftp")]
    [DataRow("ws://localhost:0", "ws")]
    [DataRow("nosuch://127.0.0.1:0", "nosuch")]
    public void Parse_ControlNotHttp_IsRefused(string argument, string scheme) =>
        AssertRefused(
            ["--control", argument, "--control-token-file", TokenFile, Url],
            SurlExitCode.FailedInit,
            $"(2) --control needs http, not {scheme}");

    [TestMethod]
    [DataRow("127.0.0.1:0")]
    [DataRow("http://127.0.0.1:0/path")]
    [DataRow("http://user@127.0.0.1:0")]
    public void Parse_MalformedControl_IsRefusedAsAMalformedListenUrl(string argument)
    {
        var result = CommandLineParser.Parse(["--control", argument, "--control-token-file", TokenFile, Url]);

        Assert.AreEqual(CommandLineOutcome.Refused, result.Outcome);
        Assert.AreEqual(SurlExitCode.MalformedUrl, result.Failure?.ExitCode);
        Assert.StartsWith("(3) URL rejected: ", result.Failure!.Message);
    }

    [TestMethod]
    public void Parse_ControlWithoutTokenFile_IsRefused() =>
        AssertRefused(["--control", "http://127.0.0.1:0", Url], SurlExitCode.FailedInit, "(2) --control needs --control-token-file");

    [TestMethod]
    public void Parse_TokenFileWithoutControl_IsRefused() =>
        AssertRefused(["--control-token-file", TokenFile, Url], SurlExitCode.FailedInit, "(2) --control-token-file without --control");

    [TestMethod]
    public void Parse_ControlWithoutAnArgument_RequiresParameter() =>
        AssertRefused(["--control"], SurlExitCode.FailedInit, "option --control: requires parameter", followedByTryHelpLine: true);

    [TestMethod]
    public void Parse_NegatedControl_CannotBeReversed() =>
        AssertRefused(
            ["--no-control", Url],
            SurlExitCode.FailedInit,
            "option --no-control: the given option cannot be reversed with a --no- prefix",
            followedByTryHelpLine: true);

    [TestMethod]
    public void Parse_ControlFromAConfigFile_EqualsTheSameOptionsOnTheCommandLine()
    {
        var fromFile = CommandLineParser.Parse(
            ["-K", "a.cfg", Url],
            [],
            new FakeConfigFileSource(("a.cfg", "control = \"http://[::1]:0\"\ncontrol-token-file = token.txt\n")));
        var fromCommandLine = Served("--control", "http://[::1]:0", "--control-token-file", "token.txt", Url);

        Assert.AreEqual(CommandLineOutcome.Serve, fromFile.Outcome, fromFile.Failure?.Message);
        Assert.IsNotNull(fromFile.CommandLine);
        Assert.AreEqual(fromCommandLine.ControlListenUrl, fromFile.CommandLine.ControlListenUrl);
        Assert.AreEqual(fromCommandLine.ControlTokenFile, fromFile.CommandLine.ControlTokenFile);
    }

    [TestMethod]
    public void Parse_ControlRefusedInAConfigFile_IsLocated()
    {
        var result = CommandLineParser.Parse(
            ["-K", "a.cfg", Url], [], new FakeConfigFileSource(("a.cfg", "verbose\ncontrol = http://0.0.0.0:0\n")));

        Assert.AreEqual(CommandLineOutcome.Refused, result.Outcome);
        Assert.AreEqual(
            "a.cfg:2: (2) --control must listen on loopback (127.0.0.0/8, ::1 or localhost): http://0.0.0.0:0",
            result.Failure?.Message);
    }

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
