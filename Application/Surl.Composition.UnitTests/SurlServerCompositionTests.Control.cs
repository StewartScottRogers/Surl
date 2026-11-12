using Surl.Cli;
using Surl.Control;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

public sealed partial class SurlServerCompositionTests
{
    private const string GoodToken = "0123456789abcdef0123456789abcdef";

    private static SurlCommandLine ControlCommandLine(params string[] extra) =>
        StartTestSeams.Parse(["--control", "http://127.0.0.1:0", "--control-token-file", "token.txt", .. extra, "http://127.0.0.1:0"]);

    private static IReadOnlyList<KeyValuePair<string, string>> Bearer(string token) =>
        [new KeyValuePair<string, string>("Authorization", "Bearer " + token)];

    [TestMethod]
    public async Task ComposeAsync_AnUnreadableControlTokenFile_IsRefused37BeforeTheLockIsTaken()
    {
        var startSeams = new StartTestSeams();
        var seams = startSeams.Seams with { ReadStartFile = path => throw new FileNotFoundException(path) };

        var start = await SurlServerComposition.ComposeAsync(
            ControlCommandLine("--directory", "served"), seams, TestContext.CancellationToken);

        Assert.IsNull(start.Serving);
        Assert.AreEqual(SurlExitCode.CouldNotReadFile, start.Failure!.ExitCode);
        CollectionAssert.AreEqual(new[] { "(37) Could not read control token file token.txt" }, start.Failure.Lines.ToArray());
        Assert.IsFalse(startSeams.IsHeld("served"));
    }

    [TestMethod]
    [DataRow("short")]
    [DataRow("tooLong")]
    [DataRow("nonAscii")]
    public async Task ComposeAsync_AControlTokenOutsideItsBounds_IsRefused2WithoutTheToken(string kind)
    {
        var token = kind switch
        {
            "short" => new string('a', ControlToken.MinLength - 1),
            "tooLong" => new string('a', ControlToken.MaxLength + 1),
            _ => new string('a', ControlToken.MinLength - 1) + "é",
        };
        var seams = new StartTestSeams().Seams with { ReadStartFile = _ => System.Text.Encoding.Latin1.GetBytes(token + "\n") };

        var start = await SurlServerComposition.ComposeAsync(ControlCommandLine(), seams, TestContext.CancellationToken);

        Assert.IsNull(start.Serving);
        Assert.AreEqual(SurlExitCode.FailedInit, start.Failure!.ExitCode);
        CollectionAssert.AreEqual(
            new[] { "(2) Control token file token.txt must hold 32 to 1024 visible ASCII bytes on its first line" },
            start.Failure.Lines.ToArray());
    }

    [TestMethod]
    [DataRow("\n")]
    [DataRow("\r\n")]
    [DataRow("")]
    [DataRow("\nsecond line")]
    public async Task ComposeAsync_AControlTokenFile_AcceptsItsFirstLineWithoutTheLineEndAsTheToken(string lineEnd)
    {
        var seams = new StartTestSeams().Seams with { ReadStartFile = _ => System.Text.Encoding.ASCII.GetBytes(GoodToken + lineEnd) };

        var start = await SurlServerComposition.ComposeAsync(ControlCommandLine(), seams, TestContext.CancellationToken);
        await using var serving = start.Serving!;

        Assert.IsNull(start.Failure);
        var (token, _, _) = ControlComposition.ReadToken(ControlCommandLine(), _ => System.Text.Encoding.ASCII.GetBytes(GoodToken + lineEnd));
        Assert.IsTrue(token!.IsPresentedIn(Bearer(GoodToken)));
        Assert.IsFalse(token.IsPresentedIn(Bearer(GoodToken + "\r")));
    }

    [TestMethod]
    public async Task ComposeAsync_WithoutControl_ReadsNoControlTokenFile()
    {
        var seams = new StartTestSeams().Seams with { ReadStartFile = path => throw new AssertFailedException(path) };

        var start = await SurlServerComposition.ComposeAsync(
            StartTestSeams.Parse("http://127.0.0.1:0"), seams, TestContext.CancellationToken);
        await using var serving = start.Serving!;

        Assert.IsNull(start.Failure);
    }
}
