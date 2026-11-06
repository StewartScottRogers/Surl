using Surl.Output;

namespace Surl.Mcp;

[TestClass]
public sealed class SurlMcpCommandLineTests
{
    [TestMethod]
    public void Parse_NoWordsAndNoEnvironment_ServesWithTheDefaultSettings()
    {
        var result = SurlMcpCommandLine.Parse([], null);

        Assert.AreEqual(SurlMcpCommandLineAction.Serve, result.Action);
        Assert.AreEqual(new SurlMcpHostSettings(), result.Settings);
    }

    [TestMethod]
    public void Parse_EveryOption_SetsEverySetting()
    {
        var result = SurlMcpCommandLine.Parse(
            ["--root", "work", "--allow-non-loopback", "--max-servers", "64", "--log-level", "VERBOSE"], null);

        Assert.AreEqual(
            new SurlMcpHostSettings { Root = "work", AllowNonLoopback = true, MaxServers = 64, LogLevel = LogLevel.Verbose },
            result.Settings);
    }

    [TestMethod]
    [DataRow(null, null)]
    [DataRow("", null)]
    [DataRow("from-environment", "from-environment")]
    public void Parse_RootFromTheEnvironment_IsTheRootWhenNotEmpty(string? environment, string? expectedRoot)
    {
        var result = SurlMcpCommandLine.Parse([], environment);

        Assert.AreEqual(expectedRoot, result.Settings!.Root);
    }

    [TestMethod]
    public void Parse_RootOptionAndEnvironment_TakesTheOption()
    {
        var result = SurlMcpCommandLine.Parse(["--root", "from-option"], "from-environment");

        Assert.AreEqual("from-option", result.Settings!.Root);
    }

    [TestMethod]
    public void Parse_AnOptionTwice_TakesTheLast()
    {
        var result = SurlMcpCommandLine.Parse(["--max-servers", "2", "--max-servers", "3"], null);

        Assert.AreEqual(3, result.Settings!.MaxServers);
    }

    [TestMethod]
    public void Parse_Help_AnswersAtOnceIgnoringLaterWords()
    {
        var result = SurlMcpCommandLine.Parse(["--allow-non-loopback", "--help", "--version", "--bogus"], null);

        Assert.AreEqual(SurlMcpCommandLineAction.ShowHelp, result.Action);
    }

    [TestMethod]
    public void Parse_Version_AnswersAtOnceIgnoringLaterWords()
    {
        var result = SurlMcpCommandLine.Parse(["--allow-non-loopback", "--version", "--help", "--bogus"], null);

        Assert.AreEqual(SurlMcpCommandLineAction.ShowVersion, result.Action);
    }

    [TestMethod]
    [DataRow(new[] { "--bogus" }, "surl-mcp: option --bogus: is unknown")]
    [DataRow(new[] { "work" }, "surl-mcp: option work: is unknown")]
    [DataRow(new[] { "--root" }, "surl-mcp: option --root: needs an argument")]
    [DataRow(new[] { "--max-servers" }, "surl-mcp: option --max-servers: needs an argument")]
    [DataRow(new[] { "--log-level" }, "surl-mcp: option --log-level: needs an argument")]
    [DataRow(new[] { "--root", "" }, "surl-mcp: option --root: needs a directory, not the empty string")]
    [DataRow(new[] { "--max-servers", "0" }, "surl-mcp: option --max-servers: expected a number from 1 to 64, got '0'")]
    [DataRow(new[] { "--max-servers", "65" }, "surl-mcp: option --max-servers: expected a number from 1 to 64, got '65'")]
    [DataRow(new[] { "--max-servers", "" }, "surl-mcp: option --max-servers: expected a number from 1 to 64, got ''")]
    [DataRow(new[] { "--max-servers", "+5" }, "surl-mcp: option --max-servers: expected a number from 1 to 64, got '+5'")]
    [DataRow(new[] { "--max-servers", "99999999999" }, "surl-mcp: option --max-servers: expected a number from 1 to 64, got '99999999999'")]
    [DataRow(new[] { "--log-level", "loud" }, "surl-mcp: option --log-level: expected none, error, info, verbose or trace, got 'loud'")]
    public void Parse_AWordSurlMcpDoesNotAccept_IsRefusedNamingIt(string[] arguments, string expectedRefusal)
    {
        var result = SurlMcpCommandLine.Parse(arguments, null);

        Assert.AreEqual(SurlMcpCommandLineAction.Refuse, result.Action);
        Assert.AreEqual(expectedRefusal, result.Refusal);
        Assert.IsNull(result.Settings);
    }

    [TestMethod]
    [DataRow("none", LogLevel.None)]
    [DataRow("error", LogLevel.Error)]
    [DataRow("info", LogLevel.Info)]
    [DataRow("verbose", LogLevel.Verbose)]
    [DataRow("trace", LogLevel.Trace)]
    public void Parse_LogLevel_TakesSurlsWords(string word, LogLevel expected)
    {
        var result = SurlMcpCommandLine.Parse(["--log-level", word], null);

        Assert.AreEqual(expected, result.Settings!.LogLevel);
    }
}
