using Surl.Protocol.Abstractions;

namespace Surl.Cli;

/// <summary><c>--mqtt-hold-subscription &lt;count&gt;</c> (ADR-0014 Amendment 1, BL-1721).</summary>
[TestClass]
public sealed class MqttHoldSubscriptionOptionTests
{
    private static SurlCommandLine Served(params string[] arguments) =>
        CommandLineParser.Parse(arguments).CommandLine!;

    [TestMethod]
    public void Parse_NoOption_HoldsNone() =>
        Assert.IsNull(Served("mqtt://127.0.0.1:0").MqttHoldSubscriptionCount);

    [TestMethod]
    [DataRow("3", 3)]
    [DataRow("0", 0)]
    [DataRow("2147483647", int.MaxValue)]
    public void Parse_ANonNegativeWholeNumber_IsTheCount(string argument, int expected) =>
        Assert.AreEqual(expected, Served("--mqtt-hold-subscription", argument, "mqtt://127.0.0.1:0").MqttHoldSubscriptionCount);

    [TestMethod]
    public void Parse_GivenTwice_LaterWins() =>
        Assert.AreEqual(
            5,
            Served("--mqtt-hold-subscription", "3", "--mqtt-hold-subscription", "5", "mqtt://127.0.0.1:0").MqttHoldSubscriptionCount);

    [TestMethod]
    [DataRow("-1")]
    [DataRow("three")]
    [DataRow("1.5")]
    [DataRow("")]
    [DataRow("2147483648")]
    public void Parse_NotANonNegativeWholeNumber_IsRefusedWithExit2(string argument)
    {
        var result = CommandLineParser.Parse(["--mqtt-hold-subscription", argument, "mqtt://127.0.0.1:0"]);

        Assert.IsNull(result.CommandLine);
        Assert.AreEqual(SurlExitCode.FailedInit, result.Failure!.ExitCode);
        Assert.AreEqual("option --mqtt-hold-subscription: expected a proper numerical parameter", result.Failure.Message);
    }

    [TestMethod]
    public void Answer_HelpForTheOption_SaysHowCurlEnds()
    {
        var page = string.Join(' ', HelpText.Answer("--mqtt-hold-subscription").Output.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        StringAssert.Contains(page, "--mqtt-hold-subscription <count> Hold a subscribe open for live publishes. Default: off.");
        StringAssert.Contains(page, "so curl exits 0.");
        StringAssert.Contains(page, "curl's own --max-time ends the fetch with exit 28");
    }

    [TestMethod]
    public void Answer_Manual_SaysHowCurlEnds()
    {
        var manual = string.Join(' ', ManualText.Text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        StringAssert.Contains(manual, "--mqtt-hold-subscription <count> holds every subscription");
        StringAssert.Contains(manual, "so curl exits 0. A count of 0 is never reached.");
        StringAssert.Contains(manual, "curl's own --max-time ends the fetch with exit 28");
    }

    [TestMethod]
    public void Answer_AiHelpMqtt_SaysHowCurlEnds()
    {
        var page = AiHelpText.Answer("mqtt").Output;

        StringAssert.Contains(page, "`--mqtt-hold-subscription <count>` holds every subscription");
        StringAssert.Contains(page, "and curl exits 0; a count of 0 is never reached.");
        StringAssert.Contains(page, "curl's own `--max-time` ends the fetch with exit 28");
    }
}
