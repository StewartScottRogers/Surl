using Surl.Protocol.Abstractions;
using static Surl.Protocol.Dict.DictTestExchange;

namespace Surl.Protocol.Dict;

[TestClass]
public sealed class LineLimitTests
{
    // "DEFINE ! " before the word and CRLF after it.
    private const int DefineFrameBytes = 11;

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task LineOfExactly8192Bytes_IsAnswered()
    {
        var connection = await ServeAsync(DefineLine(8192), ExchangeLimits.Default);

        Assert.AreEqual(Banner + "552 no match\r\n", Utf8(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task LineOf8193Bytes_Answers500AndCloses()
    {
        var connection = await ServeAsync(DefineLine(8193) + "QUIT\r\n", ExchangeLimits.Default);

        Assert.AreEqual(Banner + LineTooLongReply, Utf8(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
        Assert.IsFalse(connection.Aborted);
    }

    [TestMethod]
    public async Task RecordedOverLongLine_SendsTheRepliesUpstreamCurlAccepted()
    {
        var connection = new InMemoryConnection([RecordedFixture.ReadRequestBytes("line-too-long")]);

        await Server().ServeAsync(connection, Context(new ManualTimeProvider(), TestContext.CancellationToken));

        Assert.AreEqual("0", Utf8(RecordedFixture.ReadBytes("line-too-long", "exitcode.txt")));
        Assert.IsEmpty(RecordedFixture.ReadBytes("line-too-long", "stderr.txt"));
        CollectionAssert.AreEqual(RecordedFixture.ReadBytes("line-too-long", "stdout.bin"), connection.WrittenBytes);
        Assert.AreEqual(Banner + "250 ok\r\n" + LineTooLongReply, Utf8(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
        Assert.IsFalse(connection.Aborted);
    }

    [TestMethod]
    public async Task NoLimit_AcceptsA16KibLine()
    {
        var connection = await ServeAsync(DefineLine(16384), ExchangeLimits.Default with { MaxLineBytes = 0 });

        Assert.AreEqual(Banner + "552 no match\r\n", Utf8(connection.WrittenBytes));
    }

    private static string DefineLine(int lineBytes) => "DEFINE ! " + new string('x', lineBytes - DefineFrameBytes) + "\r\n";

    private async Task<InMemoryConnection> ServeAsync(string request, ExchangeLimits limits)
    {
        var connection = new InMemoryConnection(Ascii(request));

        await Server().ServeAsync(connection, Context(new ManualTimeProvider(), TestContext.CancellationToken, limits));

        return connection;
    }
}
