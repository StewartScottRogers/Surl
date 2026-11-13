namespace Surl.Cli;

/// <summary><c>--mqtt5-disconnect-after-retained</c> (ADR-0093 decision 9, BL-976).</summary>
[TestClass]
public sealed class Mqtt5DisconnectAfterRetainedOptionTests
{
    private static SurlCommandLine Served(params string[] arguments) =>
        CommandLineParser.Parse(arguments, ["mqtt5"]).CommandLine!;

    [TestMethod]
    public void Parse_NoFlag_IsOff() =>
        Assert.IsFalse(Served("mqtt5://127.0.0.1:0").Mqtt5DisconnectAfterRetained);

    [TestMethod]
    public void Parse_Flag_TurnsItOn() =>
        Assert.IsTrue(Served("--mqtt5-disconnect-after-retained", "mqtt5://127.0.0.1:0").Mqtt5DisconnectAfterRetained);

    [TestMethod]
    public void Parse_NegatedAfterFlag_LaterWinsAndTurnsItOff() =>
        Assert.IsFalse(Served("--mqtt5-disconnect-after-retained", "--no-mqtt5-disconnect-after-retained", "mqtt5://127.0.0.1:0").Mqtt5DisconnectAfterRetained);
}
