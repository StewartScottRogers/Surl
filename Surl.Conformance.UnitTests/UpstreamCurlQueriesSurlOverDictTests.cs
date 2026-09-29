namespace Surl.Conformance;

/// <summary>
/// The pinned upstream curl build queries a live, in-process <c>surl</c> over DICT, and its
/// exit code and stdout are the ones recorded in <c>Surl.Protocol.Dict.UnitTests/Fixtures</c>
/// for each case: surl serves <c>hello</c>, <c>help</c> and <c>world</c> as the fixtures'
/// content store does, and a fresh surl answers its first exchange with id 1, the id the
/// recorded banners carry. The fixtures are read where they were recorded, never copied.
/// Inconclusive where no pinned build is installed for the platform.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class UpstreamCurlQueriesSurlOverDictTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("define-hello", "d:hello")]
    [DataRow("match-hel", "m:hel")]
    [DataRow("bare-hello", "hello")]
    [DataRow("define-missing", "d:missing")]
    [DataRow("show-db", "show:db")]
    public async Task Query_RecordedCase_ExitsAndWritesWhatTheRecordingHolds(string fixture, string path)
    {
        var fixtureDirectory = Path.Combine(
            PinnedUpstreamCurl.RepositoryRoot(), "Surl.Protocol.Dict.UnitTests", "Fixtures", fixture);
        var expectedExitCode = int.Parse(
            await File.ReadAllTextAsync(Path.Combine(fixtureDirectory, "exitcode.txt"), TestContext.CancellationToken),
            System.Globalization.CultureInfo.InvariantCulture);
        var expectedOutput = await File.ReadAllBytesAsync(
            Path.Combine(fixtureDirectory, "stdout.bin"), TestContext.CancellationToken);
        await using var surl = await SurlOnLoopback.StartAsync(
            "dict",
            new Dictionary<string, byte[]>
            {
                ["hello"] = "A greeting.\n"u8.ToArray(),
                ["help"] = "Assistance.\n"u8.ToArray(),
                ["world"] = "The earth.\n"u8.ToArray(),
            },
            [],
            [],
            TestContext.CancellationToken);

        // Concatenated, not resolved against BaseUrl: a relative Uri would read "d:hello" as a scheme.
        var result = await PinnedUpstreamCurl.RunAsync(TestContext, "-sS", surl.BaseUrl.AbsoluteUri + path);

        Assert.AreEqual(expectedExitCode, result.ExitCode, result.StandardError);
        CollectionAssert.AreEqual(expectedOutput, result.StandardOutput);
    }
}
