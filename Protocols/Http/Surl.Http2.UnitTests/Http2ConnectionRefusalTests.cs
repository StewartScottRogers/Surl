using Surl.Hpack;
using Surl.Protocol.Abstractions;
using static Surl.Http2.Http2TestClient;

namespace Surl.Http2;

[TestClass]
public sealed class Http2ConnectionRefusalTests
{
    private static readonly HpackHeaderField[] RefusalFields = [new(":status", "503"), new("server", "surl")];

    // A frame of type 0xFA, which RFC 9113 does not define, with a 3-byte payload (section 5.5).
    private static readonly byte[] UnknownFrame = [0, 0, 3, 0xFA, 0, 0, 0, 0, 0, 1, 2, 3];

    // A SETTINGS whose 5-byte payload is not a multiple of 6: FRAME_SIZE_ERROR (section 6.5).
    private static readonly byte[] MalformedSettings = [0, 0, 5, 4, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0];

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task WriteAsync_FramesBeforeTheFirstHeaders_AreReadPastAndThatStreamIsRefused()
    {
        var client = new Http2TestClient();
        var connection = new InMemoryConnection(
            [Concat(Settings(), Frame(new Http2WindowUpdateFrame(0, 1000)), UnknownFrame, client.Get(3), client.Get(5))],
            peerHalfClosesWhenExhausted: false);

        await Http2ConnectionRefusal.WriteAsync(connection, RefusalFields, TestContext.CancellationToken);

        var frames = Frames(connection.WrittenBytes);
        Assert.HasCount(3, frames);
        Assert.IsTrue(frames[0] is Http2SettingsFrame { Ack: false, Settings.Count: 0 });
        var response = Responses(connection.WrittenBytes)[3];
        Assert.AreEqual("503", response.Status);
        Assert.AreEqual("surl", response.Fields[1].Value);
        Assert.IsTrue(response.EndedOnHeaders);
        Assert.IsTrue(frames[2] is Http2GoAwayFrame { LastStreamId: 3, ErrorCode: Http2ErrorCode.NoError, DebugData.Length: 0 });
        Assert.IsTrue(connection.WritesCompleted);
    }

    [TestMethod]
    public async Task WriteAsync_ClientClosesBeforeAnyHeaders_WritesNoBytes()
    {
        var connection = new InMemoryConnection([Settings()]);

        await Http2ConnectionRefusal.WriteAsync(connection, RefusalFields, TestContext.CancellationToken);

        Assert.IsEmpty(connection.WrittenBytes);
        Assert.IsFalse(connection.WritesCompleted);
    }

    [TestMethod]
    public async Task WriteAsync_MalformedFrameBeforeAnyHeaders_WritesNoBytes()
    {
        var connection = new InMemoryConnection([MalformedSettings], peerHalfClosesWhenExhausted: false);

        await Http2ConnectionRefusal.WriteAsync(connection, RefusalFields, TestContext.CancellationToken);

        Assert.IsEmpty(connection.WrittenBytes);
        Assert.IsFalse(connection.WritesCompleted);
    }

    [TestMethod]
    public async Task WriteAsync_NoHeadersBeforeTheDeadline_ThrowsAndWritesNoBytes()
    {
        var connection = new InMemoryConnection([Settings()], peerHalfClosesWhenExhausted: false);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var refusing = Http2ConnectionRefusal.WriteAsync(connection, RefusalFields, deadline.Token);

        await deadline.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => refusing);
        Assert.IsEmpty(connection.WrittenBytes);
    }

    [TestMethod]
    public async Task WriteAsync_NullArguments_Throw()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => Http2ConnectionRefusal.WriteAsync(null!, RefusalFields, TestContext.CancellationToken));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => Http2ConnectionRefusal.WriteAsync(new InMemoryConnection([]), null!, TestContext.CancellationToken));
    }
}
