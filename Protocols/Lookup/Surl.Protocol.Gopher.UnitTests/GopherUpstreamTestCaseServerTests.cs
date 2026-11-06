using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Gopher;

[TestClass]
public sealed class GopherUpstreamTestCaseServerTests
{
    // Upstream test 1200's <data crlf="yes">, and the selector its command makes curl send.
    private static readonly byte[] Menu = GopherTestExchange.Ascii("iMenu results\t\terror.host\t1\r\n0Selector \t/bar\tbar.foo.invalid\t70\r\n.\r\n");

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Constructors_Null_Throw()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new GopherUpstreamTestCaseServer(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => new GopherUpstreamTestCaseReplies(null!));
    }

    [TestMethod]
    public async Task ServeAsync_NullArguments_Throw()
    {
        var server = new GopherUpstreamTestCaseServer(new GopherUpstreamTestCaseReplies(Menu));
        var context = GopherTestExchange.Context(TimeProvider.System, new RecordingExchangeLog(), TestContext.CancellationToken);

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.ServeAsync(null!, context));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.ServeAsync(new InMemoryConnection([]), null!));
    }

    [TestMethod]
    public void SchemesAndServerCommands_AreGopherAndGophersAndNone()
    {
        var server = new GopherUpstreamTestCaseServer(new GopherUpstreamTestCaseReplies(Menu));

        CollectionAssert.AreEqual(new[] { "gopher", "gophers" }, server.Schemes.ToArray());
        Assert.IsEmpty(GopherUpstreamTestCaseReplies.SupportedServerCommands);
    }

    [TestMethod]
    public async Task ServeAsync_Test1200Selector_SendsTheCaseDataAndRecordsTheSelector()
    {
        var replies = new GopherUpstreamTestCaseReplies(Menu);
        var connection = new InMemoryConnection([GopherTestExchange.Ascii("/1200\r\n")]);

        await new GopherUpstreamTestCaseServer(replies).ServeAsync(
            connection, GopherTestExchange.Context(TimeProvider.System, new RecordingExchangeLog(), TestContext.CancellationToken));

        CollectionAssert.AreEqual(Menu, connection.WrittenBytes);
        Assert.IsTrue(connection.WritesCompleted);
        CollectionAssert.AreEqual(GopherTestExchange.Ascii("/1200\r\n"), replies.ReceivedBytes);
    }

    [TestMethod]
    public async Task ServeAsync_ClientClosesBeforeALine_SendsNothing()
    {
        var replies = new GopherUpstreamTestCaseReplies(Menu);
        var connection = new InMemoryConnection([GopherTestExchange.Ascii("/12")]);

        await new GopherUpstreamTestCaseServer(replies).ServeAsync(
            connection, GopherTestExchange.Context(TimeProvider.System, new RecordingExchangeLog(), TestContext.CancellationToken));

        Assert.IsEmpty(connection.WrittenBytes);
        Assert.IsTrue(connection.WritesCompleted);
        CollectionAssert.AreEqual(GopherTestExchange.Ascii("/12"), replies.ReceivedBytes);
    }
}
