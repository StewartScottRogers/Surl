using System.Net;
using System.Text;
using Surl.Content;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Ftp.FtpTestExchange;

namespace Surl.Protocol.Ftp;

/// <summary>
/// Replays the directory listings recorded from pinned upstream curl (Fixtures/README.md,
/// "Listings (BL-179)") against a content store listing <c>/dir/</c> - the file <c>b.txt</c>
/// and the directory <c>sub</c> - with <c>--list-directories</c>, and asserts surl answers
/// every control line with the reply the recorder fed curl and sends, on the data connection,
/// the listing bytes curl printed.
/// </summary>
[TestClass]
public sealed class RecordedListingTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("list-directory", DisplayName = "ftp://h/dir/: CWD dir, EPSV, TYPE A, LIST")]
    [DataRow("name-list-directory", DisplayName = "-l ftp://h/dir/: CWD dir, EPSV, TYPE A, NLST")]
    public async Task Replay_DirectoryListing_SendsTheRecordedRepliesAndListing(string caseName)
    {
        var dataConnection = new InMemoryConnection([]);
        var dataConnections = new InMemoryDataConnections()
            .ScriptPassiveListener(new IPEndPoint(IPAddress.Loopback, AnnouncedPassivePort(caseName)), dataConnection);
        var control = new InMemoryConnection([RecordedFixture.ReadRequestBytes(caseName)]);
        var context = Context(new ManualTimeProvider(), TestContext.CancellationToken, dataConnections: dataConnections);

        await Server(contentStore: FtpListingTests.ListingContentStore(new ContentExposureOptions { ListDirectories = true }))
            .ServeAsync(control, context);

        Assert.AreEqual(RecordedFixture.ReadServerReplies(caseName), Text(control.WrittenBytes));
        CollectionAssert.AreEqual(RecordedFixture.ReadBytes(caseName, "stdout.bin"), dataConnection.WrittenBytes);
        Assert.IsTrue(dataConnection.WritesCompleted);
        Assert.AreEqual("0", Text(RecordedFixture.ReadBytes(caseName, "exitcode.txt")));
        Assert.IsEmpty(RecordedFixture.ReadBytes(caseName, "stderr.txt"));
    }

    private static int AnnouncedPassivePort(string caseName)
    {
        var transcript = Encoding.ASCII.GetString(RecordedFixture.ReadBytes(caseName, "transcript.txt"));
        var start = transcript.IndexOf("(|||", StringComparison.Ordinal) + 4;

        return int.Parse(transcript[start..transcript.IndexOf("|)", start, StringComparison.Ordinal)], System.Globalization.CultureInfo.InvariantCulture);
    }
}
