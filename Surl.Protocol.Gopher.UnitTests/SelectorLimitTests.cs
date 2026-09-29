using Surl.Protocol.Abstractions;
using static Surl.Protocol.Gopher.GopherTestExchange;

namespace Surl.Protocol.Gopher;

[TestClass]
public sealed class SelectorLimitTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task SelectorOfExactly8192Bytes_IsAnswered()
    {
        var request = RecordedFixture.ReadRequestBytes("selector-8192-bytes");
        Assert.HasCount((int)ExchangeLimits.Default.MaxLineBytes, request);

        var (connection, _) = await ServeAsync(Server(), request, TestContext.CancellationToken);

        CollectionAssert.AreEqual(RecordedFixture.ReadBytes("selector-8192-bytes", "stdout.bin"), connection.WrittenBytes);
        Assert.IsTrue(connection.WritesCompleted);
    }

    [TestMethod]
    public async Task SelectorOf8193Bytes_ClosesWithNoBytes()
    {
        var request = RecordedFixture.ReadRequestBytes("selector-8193-bytes");
        Assert.HasCount((int)ExchangeLimits.Default.MaxLineBytes + 1, request);

        var (connection, log) = await ServeAsync(Server(), request, TestContext.CancellationToken);

        CollectionAssert.AreEqual(RecordedFixture.ReadBytes("selector-8193-bytes", "stdout.bin"), connection.WrittenBytes);
        Assert.IsEmpty(connection.WrittenBytes);
        Assert.IsTrue(connection.WritesCompleted);
        Assert.IsFalse(connection.Aborted);
        Assert.AreEqual("No selector was read (LineTooLong); the connection was closed with no reply.", log.Notes.Single());
    }

    [TestMethod]
    [DataRow("selector-8192-bytes")]
    [DataRow("selector-8193-bytes")]
    public void RecordedSelectorLimitCases_WereAcceptedByUpstreamCurl(string caseName)
    {
        Assert.AreEqual("0", Utf8(RecordedFixture.ReadBytes(caseName, "exitcode.txt")).Trim());
        Assert.IsEmpty(RecordedFixture.ReadBytes(caseName, "stderr.txt"));
    }
}
