using Surl.Content;
using static Surl.Protocol.Gopher.GopherTestExchange;

namespace Surl.Protocol.Gopher;

[TestClass]
public sealed class ExposureTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task DirectorySelector_WithListingsOff_AnswersExactlyAsMissing()
    {
        var missing = await MissingSelectorReplyAsync();

        var (connection, log) = await ServeAsync(Server(), RecordedFixture.ReadRequestBytes("directory-selector"), TestContext.CancellationToken);

        CollectionAssert.AreEqual(missing, connection.WrittenBytes);
        CollectionAssert.AreEqual(RecordedFixture.ReadBytes("directory-selector", "stdout.bin"), connection.WrittenBytes);
        Assert.IsTrue(connection.WritesCompleted);
        Assert.AreEqual(
            $"Selector \"/sub\": error menu, directory listings are off, so {Path.Join(Root, "sub")} is answered as absent",
            log.Notes.Single());
    }

    [TestMethod]
    [DataRow("/.hidden.txt\r\n", DisplayName = "A dot-file")]
    [DataRow("/.git\r\n", DisplayName = "A dot-directory")]
    [DataRow("/.git/config\r\n", DisplayName = "A selector under a dot-directory")]
    public async Task DotFileSelector_AnswersExactlyAsMissing(string request)
    {
        var missing = await MissingSelectorReplyAsync();

        var (connection, _) = await ServeAsync(Server(new ContentExposureOptions { ListDirectories = true }), Ascii(request), TestContext.CancellationToken);

        CollectionAssert.AreEqual(missing, connection.WrittenBytes);
    }

    [TestMethod]
    public async Task DirectoryMenu_WithListingsOn_OmitsDotFiles()
    {
        var (connection, _) = await ServeAsync(
            Server(new ContentExposureOptions { ListDirectories = true }), RecordedFixture.ReadRequestBytes("root-menu"), TestContext.CancellationToken);

        CollectionAssert.AreEqual(RecordedFixture.ReadBytes("root-menu", "stdout.bin"), connection.WrittenBytes);
        Assert.DoesNotContain(".hidden", Utf8(connection.WrittenBytes));
        Assert.DoesNotContain(".git", Utf8(connection.WrittenBytes));
    }

    [TestMethod]
    public void RecordedDirectorySelector_WasAcceptedByUpstreamCurl()
    {
        Assert.AreEqual("/sub\r\n", Utf8(RecordedFixture.ReadRequestBytes("directory-selector")));
        Assert.AreEqual("0", Utf8(RecordedFixture.ReadBytes("directory-selector", "exitcode.txt")).Trim());
        Assert.IsEmpty(RecordedFixture.ReadBytes("directory-selector", "stderr.txt"));
    }

    private async Task<byte[]> MissingSelectorReplyAsync()
    {
        var (connection, _) = await ServeAsync(Server(), RecordedFixture.ReadRequestBytes("missing-selector"), TestContext.CancellationToken);
        CollectionAssert.AreEqual(RecordedFixture.ReadBytes("missing-selector", "stdout.bin"), connection.WrittenBytes);

        return connection.WrittenBytes;
    }
}
