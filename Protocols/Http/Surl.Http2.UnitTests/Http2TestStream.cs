using Surl.Protocol.Abstractions;

namespace Surl.Http2;

/// <summary>
/// One <see cref="Http2RequestStream"/> on stream 1 of an in-memory connection, built without a
/// connection server, so a handler or what it writes can be tested on its own. A reset writes
/// <c>RST_STREAM</c> and ends the body, as the connection's does.
/// </summary>
internal sealed class Http2TestStream
{
    private readonly Http2Stream stream;

    public Http2TestStream(Http2RequestHead head, bool isBodyAnnounced, CancellationToken cancellationToken, uint? initialWindowSize = null)
    {
        Connection = new InMemoryConnection([]);
        var frames = new Http2FrameWriter(Connection);
        var windows = new Http2SendWindows();
        if (initialWindowSize is { } size)
        {
            windows.ChangeInitialStreamWindow(size);
        }

        windows.OpenStream(1);
        var body = new Http2RequestBody(isBodyAnnounced, head.ContentLength, increment =>
        {
            Replenished.Add(increment);

            return Task.CompletedTask;
        });
        stream = new Http2Stream(1, body, cancellationToken);
        Stream = new Http2RequestStream(
            stream,
            head,
            frames,
            windows,
            (code, reason) =>
            {
                Resets.Add((code, reason));
                stream.Reset();

                return frames.WriteAsync(new Http2RstStreamFrame(1, code), cancellationToken);
            },
            cancellationToken);
    }

    public InMemoryConnection Connection { get; }

    public Http2RequestStream Stream { get; }

    public List<int> Replenished { get; } = [];

    public List<(Http2ErrorCode Code, string Reason)> Resets { get; } = [];

    /// <summary>A <c>GET /</c> head with no body fields.</summary>
    public static Http2RequestHead Get(long? contentLength = null, bool expectsContinue = false) =>
        new("GET", "/", "127.0.0.1:18050", [], contentLength, expectsContinue);

    public void Receive(string text) => stream.Body.Receive(Http2TestBytes.Ascii(text));

    public void End() => stream.Body.End();

    public void Abandon() => stream.Body.Abandon();
}
