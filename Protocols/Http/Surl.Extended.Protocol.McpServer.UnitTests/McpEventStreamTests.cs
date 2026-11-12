using System.Text;
using Surl.HttpServing;

namespace Surl.Extended.Protocol.McpServer;

[TestClass]
public sealed class McpEventStreamTests
{
    [TestMethod]
    public void Write_AResponse_IsOneDataLineThenABlankLine()
    {
        byte[] body = McpEventStream.Write("""{"jsonrpc":"2.0","id":1,"result":{}}"""u8.ToArray());

        Assert.AreEqual("data: {\"jsonrpc\":\"2.0\",\"id\":1,\"result\":{}}\n\n", Encoding.UTF8.GetString(body));
    }

    [TestMethod]
    public void Write_ACancelledCall_IsNoEvent()
    {
        byte[] body = McpEventStream.Write(null);

        Assert.IsEmpty(body);
    }

    [TestMethod]
    public async Task WriteAsync_ACallRunningPast15Seconds_WritesAKeepAliveCommentBeforeItsDataEvent()
    {
        ManualTimeProvider clock = new();
        TaskCompletionSource<byte[]?> call = new(TaskCreationOptions.RunContinuationsAsynchronously);
        List<byte> written = [];
        Task streaming = McpEventStream.WriteAsync(call.Task, Recorder(written), clock, TestContext.CancellationToken);
        await clock.TimersCreated(1);

        clock.Advance(TimeSpan.FromSeconds(15));
        await clock.TimersCreated(2);
        call.SetResult("""{"jsonrpc":"2.0","id":2,"result":{}}"""u8.ToArray());
        await streaming;

        // WHATWG HTML "Server-sent events": a line starting with a colon is a comment the client ignores.
        Assert.AreEqual(": keep-alive\n\ndata: {\"jsonrpc\":\"2.0\",\"id\":2,\"result\":{}}\n\n", Encoding.UTF8.GetString([.. written]));
    }

    [TestMethod]
    public async Task WriteAsync_ACallEndingBefore15Seconds_WritesOnlyItsDataEvent()
    {
        ManualTimeProvider clock = new();
        TaskCompletionSource<byte[]?> call = new(TaskCreationOptions.RunContinuationsAsynchronously);
        List<byte> written = [];
        Task streaming = McpEventStream.WriteAsync(call.Task, Recorder(written), clock, TestContext.CancellationToken);
        await clock.TimersCreated(1);

        clock.Advance(TimeSpan.FromSeconds(14));
        call.SetResult("""{"jsonrpc":"2.0","id":2,"result":{}}"""u8.ToArray());
        await streaming;

        Assert.AreEqual("data: {\"jsonrpc\":\"2.0\",\"id\":2,\"result\":{}}\n\n", Encoding.UTF8.GetString([.. written]));
    }

    [TestMethod]
    public async Task WriteAsync_ACancelledCall_EndsTheStreamWithNoEvent()
    {
        ManualTimeProvider clock = new();
        TaskCompletionSource<byte[]?> call = new(TaskCreationOptions.RunContinuationsAsynchronously);
        List<byte> written = [];
        Task streaming = McpEventStream.WriteAsync(call.Task, Recorder(written), clock, TestContext.CancellationToken);
        await clock.TimersCreated(1);

        clock.Advance(TimeSpan.FromSeconds(15));
        await clock.TimersCreated(2);
        call.SetResult(null);
        await streaming;

        Assert.AreEqual(": keep-alive\n\n", Encoding.UTF8.GetString([.. written]));
    }

    public TestContext TestContext { get; set; } = null!;

    private static HttpAnswerChunkWriter Recorder(List<byte> written) => (piece, _) =>
    {
        lock (written)
        {
            written.AddRange(piece.Span);
        }

        return ValueTask.CompletedTask;
    };
}
