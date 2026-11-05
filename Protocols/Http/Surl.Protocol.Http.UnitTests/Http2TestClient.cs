using Surl.Hpack;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Http;

/// <summary>
/// What the HTTP/2 tests send and how they read what the server wrote: client frames encoded
/// with the codec under test's encoder, request header blocks encoded with one HPACK encoder per
/// connection, and the server's frames parsed back into typed frames and decoded responses.
/// </summary>
internal sealed class Http2TestClient
{
    private readonly HpackEncoder encoder = new();

    public static byte[] Preface => Http2ConnectionPreface.Preface.ToArray();

    /// <summary>The server's first frame with the default limits: ADR-0081 decision 5's bytes.</summary>
    public static byte[] DefaultServerSettings =>
        [0x00, 0x00, 0x0C, 0x04, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x03, 0x00, 0x00, 0x00, 0x64, 0x00, 0x06, 0x00, 0x10, 0x00, 0x00];

    public static byte[] Frame(Http2Frame frame) => Http2FrameEncoder.Encode(frame);

    public static byte[] Settings(params Http2Setting[] settings) => Frame(new Http2SettingsFrame(false, settings));

    /// <summary>The preface and an empty <c>SETTINGS</c>: how every test connection starts.</summary>
    public static byte[] Start(params Http2Setting[] settings) => [.. Preface, .. Settings(settings)];

    public static byte[] Data(int streamId, string text, bool endStream) => Frame(new Http2DataFrame(streamId, HttpServerHarness.Ascii(text), endStream));

    public static byte[] Concat(params byte[][] parts) => [.. parts.SelectMany(part => part)];

    public static IReadOnlyList<HpackHeaderField> RequestFields(string method, string path, params (string Name, string Value)[] fields) =>
    [
        new(":method", method),
        new(":scheme", "http"),
        new(":authority", "127.0.0.1:18050"),
        new(":path", path),
        .. fields.Select(field => new HpackHeaderField(field.Name, field.Value)),
    ];

    public byte[] Block(IEnumerable<HpackHeaderField> fields) => encoder.Encode(fields);

    public byte[] Headers(int streamId, IEnumerable<HpackHeaderField> fields, bool endStream, Http2Priority? priority = null) =>
        Frame(new Http2HeadersFrame(streamId, Block(fields), endStream, EndHeaders: true, priority));

    public byte[] Get(int streamId, string path = "/file.txt", bool endStream = true, params (string Name, string Value)[] fields) =>
        Headers(streamId, RequestFields("GET", path, fields), endStream);

    /// <summary>Parses every frame the server wrote.</summary>
    public static IReadOnlyList<Http2Frame> Frames(byte[] written)
    {
        var frames = new List<Http2Frame>();
        for (var offset = 0; offset < written.Length;)
        {
            var header = Http2FrameHeader.Read(written.AsSpan(offset));
            var payload = written[(offset + Http2FrameHeader.EncodedLength)..(offset + Http2FrameHeader.EncodedLength + header.Length)];
            var result = Http2FrameParser.Parse(header, payload);
            Assert.AreEqual(Http2FrameReadOutcome.FrameRead, result.Outcome, $"The server wrote a malformed frame at byte {offset}.");
            frames.Add(result.Frame!);
            offset += Http2FrameHeader.EncodedLength + header.Length;
        }

        return frames;
    }

    /// <summary>
    /// Decodes the streams the server answered, header blocks in the order written with one
    /// decoder, as a client does.
    /// </summary>
    public static Dictionary<int, Http2TestResponse> Responses(byte[] written)
    {
        var decoder = new HpackDecoder(HpackDecoder.DefaultMaximumTableSize, int.MaxValue);
        var responses = new Dictionary<int, Http2TestResponse>();
        var block = new List<byte>();
        foreach (var frame in Frames(written))
        {
            var response = frame.StreamId == 0 ? null : Response(responses, frame.StreamId);
            switch (frame)
            {
                case Http2HeadersFrame headers:
                    block.AddRange(headers.FieldBlockFragment);
                    response!.EndedOnHeaders |= headers.EndStream;
                    EndBlock(decoder, block, response, headers.EndHeaders);
                    break;
                case Http2ContinuationFrame continuation:
                    block.AddRange(continuation.FieldBlockFragment);
                    EndBlock(decoder, block, response!, continuation.EndHeaders);
                    break;
                case Http2DataFrame data:
                    response!.Body.AddRange(data.Data);
                    response.DataFrameLengths.Add(data.Data.Length);
                    response.EndedOnData |= data.EndStream;
                    break;
                case Http2RstStreamFrame reset:
                    response!.ResetCode = reset.ErrorCode;
                    break;
            }
        }

        return responses;
    }

    public static Http2GoAwayFrame? GoAway(byte[] written) => Frames(written).OfType<Http2GoAwayFrame>().SingleOrDefault();

    /// <summary>Waits, for at most ten seconds of real time, until <paramref name="condition"/> holds.</summary>
    public static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(1));
        }

        Assert.IsTrue(condition(), "The condition did not hold within ten seconds.");
    }

    /// <summary>Whether <paramref name="connection"/> holds the end of stream <paramref name="streamId"/>'s answer.</summary>
    public static bool HasEnded(InMemoryConnection connection, int streamId) =>
        Responses(connection.WrittenBytes).TryGetValue(streamId, out var response) && (response.EndedOnHeaders || response.EndedOnData || response.ResetCode is not null);

    private static Http2TestResponse Response(Dictionary<int, Http2TestResponse> responses, int streamId)
    {
        if (!responses.TryGetValue(streamId, out var response))
        {
            response = new Http2TestResponse();
            responses[streamId] = response;
        }

        return response;
    }

    private static void EndBlock(HpackDecoder decoder, List<byte> block, Http2TestResponse response, bool endHeaders)
    {
        if (!endHeaders)
        {
            return;
        }

        var decoded = decoder.Decode(block.ToArray());
        Assert.IsTrue(decoded.IsDecoded, $"The server wrote a header block that does not decode: {decoded.Error}.");
        response.HeaderBlocks.Add(decoded.Fields);
        block.Clear();
    }
}
