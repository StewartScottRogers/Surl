using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Stomp.UnitTests;

/// <summary>
/// Reading frames (ADR-0118 decisions 0 and 8), every input written by hand from the STOMP 1.2
/// specification's "STOMP Frames", "Value Encoding", "Repeated Header Entries", "Header
/// content-length", "Heart-beating" and "Size Limits", and its example frames.
/// </summary>
[TestClass]
public sealed class StompFrameReaderTests
{
    private const string SpecificationConnectExample = "CONNECT\naccept-version:1.2\nhost:stomp.github.org\n\n\0";
    private const string SpecificationSendExample = "SEND\ndestination:/queue/a\ncontent-type:text/plain\n\nhello queue a\n\0";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ReadFrameAsync_SpecificationConnectExample_IsReadWhole()
    {
        var frames = await ReadAllAsync(null, SpecificationConnectExample);

        var frame = frames.Single();
        Assert.AreEqual("CONNECT", frame.Command);
        Assert.AreEqual("1.2", frame.Header("accept-version"));
        Assert.AreEqual("stomp.github.org", frame.Header("host"));
        Assert.HasCount(2, frame.Headers);
        Assert.IsTrue(frame.Body.IsEmpty);
    }

    [TestMethod]
    public async Task ReadFrameAsync_SpecificationSendExample_ReadsAndWritesByteForByte()
    {
        var frames = await ReadAllAsync(StompVersion.V1_2, SpecificationSendExample);

        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes(SpecificationSendExample), StompFrameWriter.Encode(frames.Single(), StompVersion.V1_2));
        Assert.AreEqual("hello queue a\n", Encoding.UTF8.GetString(frames[0].Body.Span));
    }

    [TestMethod]
    public async Task ReadFrameAsync_FrameSplitByteByByte_IsReadWhole()
    {
        var chunks = SpecificationSendExample.Select(character => character.ToString()).ToArray();

        var frames = await ReadAllAsync(StompVersion.V1_2, chunks);

        Assert.AreEqual("/queue/a", frames.Single().Header("destination"));
    }

    [TestMethod]
    public async Task ReadFrameAsync_CrLfLineEndsAndHeartBeatEols_AreAccepted()
    {
        var frames = await ReadAllAsync(StompVersion.V1_2, "\n\r\n\nSEND\r\ndestination:/queue/a\r\n\r\nhi\0\n\nDISCONNECT\n\n\0\n");

        Assert.HasCount(2, frames);
        Assert.AreEqual("/queue/a", frames[0].Header("destination"));
        Assert.AreEqual("hi", Encoding.UTF8.GetString(frames[0].Body.Span));
        Assert.AreEqual("DISCONNECT", frames[1].Command);
    }

    [TestMethod]
    public async Task ReadFrameAsync_HeaderWithEachEscape_IsDecoded()
    {
        var frames = await ReadAllAsync(StompVersion.V1_2, "SEND\nkey\\cname:a\\rb\\nc\\cd\\\\e\n\n\0");

        Assert.AreEqual("a\rb\nc:d\\e", frames.Single().Header("key:name"));
    }

    [TestMethod]
    [DataRow("SEND\ndestination:/queue/a\\tb\n\n\0")]
    [DataRow("SEND\ndesti\\nation\\x:a\n\n\0")]
    public async Task ReadFrameAsync_UndefinedEscape_IsMalformedFrame(string frame)
    {
        var violation = await ReadViolationAsync(StompVersion.V1_2, frame);

        Assert.AreEqual(StompErrors.MalformedFrame, violation.Error.Header("message"));
    }

    [TestMethod]
    public async Task ReadFrameAsync_ConnectHeaders_AreNotUnescaped()
    {
        var frames = await ReadAllAsync(StompVersion.V1_2, "STOMP\npasscode:a\\tb:c\n\n\0CONNECT\nlogin:x\\n\n\n\0");

        Assert.AreEqual("a\\tb:c", frames[0].Header("passcode"));
        Assert.AreEqual("x\\n", frames[1].Header("login"));
    }

    [TestMethod]
    public async Task ReadFrameAsync_BeforeAVersion_NothingIsUnescaped()
    {
        var frames = await ReadAllAsync(null, "SEND\nreceipt:a\\tb\n\n\0");

        Assert.AreEqual("a\\tb", frames.Single().Header("receipt"));
    }

    [TestMethod]
    public async Task ReadFrameAsync_RepeatedHeader_KeepsTheFirstValue()
    {
        var frames = await ReadAllAsync(StompVersion.V1_2, "MESSAGE\nfoo:World\nfoo:Hello\n\n\0");

        var frame = frames.Single();
        Assert.AreEqual("World", frame.Header("foo"));
        Assert.HasCount(1, frame.Headers);
    }

    [TestMethod]
    public async Task ReadFrameAsync_BodyWithNulAndContentLength_IsReadByContentLength()
    {
        var frames = await ReadAllAsync(StompVersion.V1_2, "SEND\ncontent-length:5\ncontent-length:1\n\na\0b\0c\0");

        CollectionAssert.AreEqual("a\0b\0c"u8.ToArray(), frames.Single().Body.ToArray());
    }

    [TestMethod]
    public async Task ReadFrameAsync_BodyLargerThanTheBuffer_IsReadWhole()
    {
        var body = new string('x', 10000);

        var frames = await ReadAllAsync(StompVersion.V1_2, $"SEND\ncontent-length:10000\n\n{body}\0", $"SEND\n\n{body}\0");

        Assert.HasCount(2, frames);
        Assert.AreEqual(10000, frames[0].Body.Length);
        Assert.AreEqual(10000, frames[1].Body.Length);
    }

    [TestMethod]
    [DataRow("SEND\ncontent-length:x\n\nab\0")]
    [DataRow("SEND\ncontent-length:-1\n\nab\0")]
    [DataRow("SEND\ncontent-length:1\n\nab\0")]
    [DataRow("SEND\nno colon\n\n\0")]
    public async Task ReadFrameAsync_MalformedFrame_IsMalformedFrame(string frame)
    {
        var violation = await ReadViolationAsync(StompVersion.V1_2, frame);

        Assert.AreEqual(StompErrors.MalformedFrame, violation.Error.Header("message"));
    }

    [TestMethod]
    public async Task ReadFrameAsync_InvalidUtf8_IsMalformedFrame()
    {
        var connection = new InMemoryConnection([(ReadOnlyMemory<byte>)new byte[] { (byte)'S', 0xC3, 0x28, (byte)'\n', (byte)'\n', 0 }]);
        var reader = new StompFrameReader(connection, 8192, 1048576);

        var violation = await Assert.ThrowsExactlyAsync<StompProtocolViolationException>(
            () => reader.ReadFrameAsync(TestContext.CancellationToken).AsTask());

        Assert.AreEqual(StompErrors.MalformedFrame, violation.Error.Header("message"));
    }

    [TestMethod]
    [DataRow("SEND\ncontent-length:11\n\nhello world\0")]
    [DataRow("SEND\n\nhello world\0")]
    [DataRow("SEND\n\nhello world, and the rest")]
    public async Task ReadFrameAsync_BodyOverTheLimit_IsFrameTooLarge(string frame)
    {
        var violation = await ReadViolationAsync(StompVersion.V1_2, frame);

        Assert.AreEqual(StompErrors.FrameTooLarge, violation.Error.Header("message"));
    }

    [TestMethod]
    public async Task ReadFrameAsync_BodyAtTheLimit_IsRead()
    {
        var frames = await ReadAllAsync(StompVersion.V1_2, "SEND\n\nhello worl\0SEND\ncontent-length:10\n\nhello worl\0");

        Assert.HasCount(2, frames);
    }

    [TestMethod]
    [DataRow("SEND\nheader:0123456789012345678901234567890123456789\n\n\0")]
    [DataRow("SEND\nheader:0123456789012345678901234567890123456789")]
    public async Task ReadFrameAsync_LineOverTheLimit_IsHeaderLineTooLong(string frame)
    {
        var violation = await ReadViolationAsync(StompVersion.V1_2, frame);

        Assert.AreEqual(StompErrors.HeaderLineTooLong, violation.Error.Header("message"));
    }

    [TestMethod]
    public async Task ReadFrameAsync_MoreThan128Headers_IsTooManyHeaders()
    {
        var headers = string.Concat(Enumerable.Range(0, 129).Select(index => $"h{index}:v\n"));

        var violation = await ReadViolationAsync(StompVersion.V1_2, $"SEND\n{headers}\n\0");

        Assert.AreEqual(StompErrors.TooManyHeaders, violation.Error.Header("message"));
    }

    [TestMethod]
    public async Task ReadFrameAsync_128Headers_IsRead()
    {
        var headers = string.Concat(Enumerable.Range(0, 128).Select(index => $"h{index}:v\n"));

        var frames = await ReadAllAsync(StompVersion.V1_2, $"SEND\n{headers}\n\0");

        Assert.HasCount(128, frames.Single().Headers);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("\n\n")]
    [DataRow("SEND")]
    [DataRow("SEND\nheader:value\n")]
    [DataRow("SEND\n\nbody")]
    [DataRow("SEND\ncontent-length:4\n\nbody")]
    public async Task ReadFrameAsync_ClientClosesBeforeTheFrameEnds_IsNull(string partial)
    {
        var frames = await ReadAllAsync(StompVersion.V1_2, partial);

        Assert.IsEmpty(frames);
    }

    private async Task<List<StompFrame>> ReadAllAsync(StompVersion? version, params string[] chunks)
    {
        var reader = Reader(version, 8192, 1048576, chunks);
        var frames = new List<StompFrame>();
        while (await reader.ReadFrameAsync(TestContext.CancellationToken) is { } frame)
        {
            frames.Add(frame);
        }

        return frames;
    }

    // A 40-byte line limit and a 10-byte body limit, so the limits are reached with short frames.
    private async Task<StompProtocolViolationException> ReadViolationAsync(StompVersion? version, string frame)
    {
        var reader = Reader(version, 40, 10, frame);
        return await Assert.ThrowsExactlyAsync<StompProtocolViolationException>(
            () => reader.ReadFrameAsync(TestContext.CancellationToken).AsTask());
    }

    private static StompFrameReader Reader(StompVersion? version, long maxLineBytes, long maxBodyBytes, params string[] chunks)
    {
        var connection = new InMemoryConnection(chunks.Select(chunk => (ReadOnlyMemory<byte>)Encoding.UTF8.GetBytes(chunk)));
        return new StompFrameReader(connection, maxLineBytes, maxBodyBytes) { Version = version };
    }
}
