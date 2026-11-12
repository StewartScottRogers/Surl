using Surl.Protocol.Abstractions;

namespace Surl.Cli;

/// <summary>
/// <c>--random-seed</c> read into <see cref="SurlCommandLine.RandomSeed"/> from the command line and from a
/// <c>-K</c> file, and its refusals (ADR-0124 decision 6, BL-996).
/// </summary>
[TestClass]
public sealed class CommandLineParserRandomSeedTests
{
    private const string Url = "http://127.0.0.1:8080/";

    [TestMethod]
    public void Parse_NoRandomSeed_IsUnseeded() =>
        Assert.IsNull(Served(new FakeConfigFileSource(), Url).RandomSeed);

    [TestMethod]
    [DataRow("42", 42L)]
    [DataRow("0", 0L)]
    [DataRow("9223372036854775807", long.MaxValue)]
    public void Parse_RandomSeed_IsRead(string argument, long seed) =>
        Assert.AreEqual(seed, Served(new FakeConfigFileSource(), "--random-seed", argument, Url).RandomSeed);

    [TestMethod]
    public void Parse_RandomSeedGivenTwice_TheLastWins() =>
        Assert.AreEqual(2L, Served(new FakeConfigFileSource(), "--random-seed=1", "--random-seed=2", Url).RandomSeed);

    [TestMethod]
    public void Parse_RandomSeedInAConfigFile_IsRead() =>
        Assert.AreEqual(42L, Served(new FakeConfigFileSource(("a.cfg", "random-seed = 42\n")), "-K", "a.cfg", Url).RandomSeed);

    [TestMethod]
    [DataRow("-1")]
    [DataRow("x")]
    [DataRow("")]
    [DataRow("1.5")]
    [DataRow("+1")]
    [DataRow("9223372036854775808")]
    public void Parse_RandomSeedNotANumberInRange_IsRefused(string argument) =>
        Assert.AreEqual(
            new CommandLineFailure(SurlExitCode.FailedInit, "option --random-seed: expected a proper numerical parameter", true),
            CommandLineParser.Parse(["--random-seed", argument, Url]).Failure);

    private static SurlCommandLine Served(IConfigFileSource source, params string[] arguments)
    {
        var result = CommandLineParser.Parse(arguments, [], source);

        Assert.AreEqual(CommandLineOutcome.Serve, result.Outcome, result.Failure?.Message);
        return result.CommandLine!;
    }
}
