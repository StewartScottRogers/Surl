using System.Net;
using System.Security.Cryptography;
using Surl.Http2;
using Surl.HttpServing;
using Surl.Protocol.Abstractions;
using static Surl.Extended.Protocol.DnsQuery.DnsHttp2TestFrames;

namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>
/// An HTTP/2 request body read as an <see cref="IHttpRequestBody"/>, through a handler of the
/// test's own on <see cref="Http2ConnectionServer"/>, from frames written by hand from RFC 9113.
/// </summary>
[TestClass]
public sealed class DnsHttp2RequestBodyTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task DiscardAsync_WithAHash_HashesEveryPieceAndEndsTheBody()
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var handler = new BodyHandler(body => body.DiscardAsync(hash, CancellationToken.None));

        await ServeAsync(handler, Ascii("abc"), Ascii("def"));

        Assert.AreEqual(HttpRequestBodyDiscardOutcome.Discarded, handler.Outcome);
        Assert.IsTrue(handler.WasAnnounced);
        Assert.IsFalse(handler.IsAnnouncedAfter);
        Assert.AreEqual(6L, handler.AnnouncedLength);
        Assert.IsTrue(handler.IsReadable);
        CollectionAssert.AreEqual(SHA256.HashData(Ascii("abcdef")), hash.GetHashAndReset());
    }

    [TestMethod]
    public async Task DiscardAsync_WithNoHash_ReadsTheBodyToItsEnd()
    {
        var handler = new BodyHandler(body => body.DiscardAsync(null, CancellationToken.None));

        await ServeAsync(handler, Ascii("abc"), Ascii("def"));

        Assert.AreEqual(HttpRequestBodyDiscardOutcome.Discarded, handler.Outcome);
    }

    [TestMethod]
    public async Task CopyToAsync_ClientClosingBeforeTheEnd_IsIncompleteWithWhatArrived()
    {
        using var destination = new MemoryStream();
        var handler = new BodyHandler(body => body.CopyToAsync(destination, CancellationToken.None));
        var client = new DnsHttp2TestFrames();

        await ServeFramesAsync(handler, client.Request(1, "POST", "/dns-query", endStream: false), Data(1, Ascii("abc"), endStream: false));

        Assert.AreEqual(HttpRequestBodyDiscardOutcome.Incomplete, handler.Outcome);
        CollectionAssert.AreEqual(Ascii("abc"), destination.ToArray());
    }

    [TestMethod]
    public async Task CopyToAsync_NullDestination_Throws()
    {
        var handler = new BodyHandler(body => body.CopyToAsync(null!, CancellationToken.None));

        await ServeAsync(handler, Ascii("abc"));

        Assert.IsInstanceOfType<ArgumentNullException>(handler.Failure);
    }

    private async Task ServeAsync(BodyHandler handler, params byte[][] pieces)
    {
        var client = new DnsHttp2TestFrames();
        var length = pieces.Sum(piece => piece.Length).ToString(System.Globalization.CultureInfo.InvariantCulture);
        byte[][] data = [.. pieces.Select((piece, index) => Data(1, piece, endStream: index == pieces.Length - 1))];

        await ServeFramesAsync(handler, [client.Request(1, "POST", "/dns-query", endStream: false, ("content-length", length)), .. data]);
    }

    private async Task ServeFramesAsync(BodyHandler handler, params byte[][] frames)
    {
        var connection = new InMemoryConnection([(byte[])[.. Start, .. frames.SelectMany(frame => frame)]]);
        var context = new ExchangeContext(
            1,
            new ListenUrl("https", "127.0.0.1", 443).WithBoundPort(443),
            new IPEndPoint(IPAddress.Loopback, 443),
            new IPEndPoint(IPAddress.Loopback, 50000),
            new RecordingExchangeLog(),
            TimeProvider.System,
            TestContext.CancellationToken);

        await new Http2ConnectionServer(handler, context, connection).ServeAsync();
    }

    private sealed class BodyHandler(Func<IHttpRequestBody, Task<HttpRequestBodyDiscardOutcome>> read) : IHttp2StreamHandler
    {
        public HttpRequestBodyDiscardOutcome? Outcome { get; private set; }

        public bool WasAnnounced { get; private set; }

        public bool IsAnnouncedAfter { get; private set; }

        public long? AnnouncedLength { get; private set; }

        public bool IsReadable { get; private set; }

        public Exception? Failure { get; private set; }

        public async Task ServeStreamAsync(Http2RequestStream stream, CancellationToken cancellationToken)
        {
            var body = new DnsHttp2RequestBody(stream);
            WasAnnounced = body.IsAnnounced;
            AnnouncedLength = body.AnnouncedLength;
            IsReadable = body.IsReadable;
            try
            {
                Outcome = await read(body);
            }
            catch (ArgumentNullException failure)
            {
                Failure = failure;
            }

            IsAnnouncedAfter = body.IsAnnounced;
            await stream.WriteHeadersAsync([new(":status", "204")], endStream: true);
        }
    }
}
