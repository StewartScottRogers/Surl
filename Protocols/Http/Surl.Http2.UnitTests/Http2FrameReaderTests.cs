using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Http2;

[TestClass]
public sealed class Http2FrameReaderTests
{
    private const int ConnectionPrefaceLength = 24;

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ReadFrameAsync_UpstreamCurlsPriorKnowledgeGet_ReadsEachFrameAsItsTypedValue(bool oneBytePerRead)
    {
        var request = Http2RecordedFixture.ReadRequestBytes("h2-prior-knowledge-get");
        Assert.AreEqual("PRI * HTTP/2.0\r\n\r\nSM\r\n\r\n", Encoding.ASCII.GetString(request, 0, ConnectionPrefaceLength));
        var frames = request[ConnectionPrefaceLength..];
        var reader = new Http2FrameReader(
            new InMemoryConnection(oneBytePerRead ? Http2RecordedFixture.OneBytePerRead(frames) : Http2RecordedFixture.Whole(frames)),
            Http2FrameReader.DefaultMaxFrameSize);

        var settings = (Http2SettingsFrame)await ReadFrameAsync(reader);
        Assert.IsFalse(settings.Ack);
        CollectionAssert.AreEqual(
            new[]
            {
                new Http2Setting(Http2SettingIdentifier.MaxConcurrentStreams, 100),
                new Http2Setting(Http2SettingIdentifier.InitialWindowSize, 65536),
                new Http2Setting(Http2SettingIdentifier.EnablePush, 0),
            },
            settings.Settings.ToArray());
        Assert.AreEqual(new Http2WindowUpdateFrame(0, 1048510465), await ReadFrameAsync(reader));

        var headers = (Http2HeadersFrame)await ReadFrameAsync(reader);
        Assert.AreEqual(1, headers.StreamId);
        Assert.IsTrue(headers.EndStream);
        Assert.IsTrue(headers.EndHeaders);
        Assert.IsNull(headers.Priority);
        Assert.IsNull(headers.PaddingLength);
        CollectionAssert.AreEqual(frames[49..80], headers.FieldBlockFragment);
        Assert.AreEqual((byte)0x82, headers.FieldBlockFragment[0]);
        Assert.AreEqual(31, headers.FieldBlockFragment.Length);

        Assert.AreEqual(new Http2WindowUpdateFrame(1, 10420225), await ReadFrameAsync(reader));
        Assert.AreEqual(new Http2WindowUpdateFrame(1, 10420225), await ReadFrameAsync(reader));
        var ack = (Http2SettingsFrame)await ReadFrameAsync(reader);
        Assert.IsTrue(ack.Ack);
        Assert.IsEmpty(ack.Settings);

        var goAway = (Http2GoAwayFrame)await ReadFrameAsync(reader);
        Assert.AreEqual(0, goAway.LastStreamId);
        Assert.AreEqual(Http2ErrorCode.NoError, goAway.ErrorCode);
        Assert.AreEqual("shutdown\0", Encoding.ASCII.GetString(goAway.DebugData));

        Assert.AreEqual(Http2FrameReadOutcome.ConnectionClosed, (await reader.ReadFrameAsync(CancellationToken.None)).Outcome);
    }

    [TestMethod]
    public async Task ReadFrameAsync_FrameOverTheMaximumSize_IsAFrameSizeConnectionErrorAndReadsNoPayloadByte()
    {
        // A DATA frame announcing 16385 bytes, one more than SETTINGS_MAX_FRAME_SIZE allows.
        var bytes = Convert.FromHexString("004001" + "00" + "00" + "00000001").Concat(new byte[16385]).ToArray();
        var connection = new ReadCountingConnection(new InMemoryConnection(Http2RecordedFixture.OneBytePerRead(bytes)));

        var result = await new Http2FrameReader(connection, Http2FrameReader.DefaultMaxFrameSize).ReadFrameAsync(CancellationToken.None);

        Assert.AreEqual(Http2FrameReadOutcome.Malformed, result.Outcome);
        Assert.AreEqual(Http2FrameMalformation.FrameTooLarge, result.Malformation);
        Assert.AreEqual(Http2ErrorCode.FrameSizeError, Http2FrameMalformations.ErrorCodeOf(result.Malformation!.Value));
        Assert.IsTrue(Http2FrameMalformations.IsConnectionError(result.Malformation.Value));
        Assert.AreEqual(new Http2FrameHeader(16385, 0, 0, 1), result.Header);
        Assert.AreEqual(Http2FrameHeader.EncodedLength, connection.BytesReadBeforeFirstWrite);
    }

    [TestMethod]
    public async Task ReadFrameAsync_FrameOfTheMaximumSize_IsRead()
    {
        var bytes = Convert.FromHexString("004000" + "00" + "00" + "00000001").Concat(new byte[16384]).ToArray();

        var frame = (Http2DataFrame)await ReadFrameAsync(new Http2FrameReader(
            new InMemoryConnection(Http2RecordedFixture.Whole(bytes)), Http2FrameReader.DefaultMaxFrameSize));

        Assert.AreEqual(16384, frame.Data.Length);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ReadFrameAsync_UnknownFrameType_IsReadAndSkipped(bool oneBytePerRead)
    {
        // Type 0xFA with a 5000-byte payload, longer than the skip buffer, then a PING.
        var unknown = Convert.FromHexString("001388" + "FA" + "FF" + "00000003").Concat(Enumerable.Repeat((byte)0x55, 5000));
        var bytes = unknown.Concat(Convert.FromHexString("000008060000000000" + "0102030405060708")).ToArray();
        var connection = new ReadCountingConnection(new InMemoryConnection(
            oneBytePerRead ? Http2RecordedFixture.OneBytePerRead(bytes) : Http2RecordedFixture.Whole(bytes)));
        var reader = new Http2FrameReader(connection, Http2FrameReader.DefaultMaxFrameSize);

        var skipped = await reader.ReadFrameAsync(CancellationToken.None);

        Assert.AreEqual(Http2FrameReadOutcome.UnknownFrameSkipped, skipped.Outcome);
        Assert.IsNull(skipped.Frame);
        Assert.AreEqual(new Http2FrameHeader(5000, 0xFA, 0xFF, 3), skipped.Header);
        Assert.AreEqual(Http2FrameHeader.EncodedLength + 5000L, connection.BytesReadBeforeFirstWrite);
        Assert.AreEqual(new Http2PingFrame(false, 0x0102030405060708), await ReadFrameAsync(reader));
    }

    [TestMethod]
    public async Task ReadFrameAsync_UnknownFrameTypeWithNoPayload_IsSkipped()
    {
        var reader = new Http2FrameReader(
            new InMemoryConnection(Http2RecordedFixture.Whole(Convert.FromHexString("0000000A0000000000"))), Http2FrameReader.DefaultMaxFrameSize);

        Assert.AreEqual(Http2FrameReadOutcome.UnknownFrameSkipped, (await reader.ReadFrameAsync(CancellationToken.None)).Outcome);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("0000")]
    [DataRow("000004080000000000")]
    [DataRow("00000408000000000000")]
    [DataRow("00000AFA0000000000" + "0102")]
    public async Task ReadFrameAsync_PeerCloses_SaysWhetherItClosedBetweenFramesOrMidFrame(string hex)
    {
        var reader = new Http2FrameReader(
            new InMemoryConnection(Http2RecordedFixture.Whole(Convert.FromHexString(hex))), Http2FrameReader.DefaultMaxFrameSize);

        var result = await reader.ReadFrameAsync(CancellationToken.None);

        Assert.AreEqual(hex.Length == 0 ? Http2FrameReadOutcome.ConnectionClosed : Http2FrameReadOutcome.ConnectionClosedMidFrame, result.Outcome);
        Assert.IsNull(result.Frame);
        Assert.IsNull(result.Header);
        Assert.IsNull(result.Malformation);
    }

    [TestMethod]
    public async Task ReadFrameAsync_StreamError_LeavesTheNextFrameReadable()
    {
        var bytes = Convert.FromHexString("000004080000000001" + "00000000" + "000004080000000001" + "00000001");
        var reader = new Http2FrameReader(new InMemoryConnection(Http2RecordedFixture.Whole(bytes)), Http2FrameReader.DefaultMaxFrameSize);

        var result = await reader.ReadFrameAsync(CancellationToken.None);

        Assert.AreEqual(Http2FrameMalformation.WindowUpdateIncrementZeroOnStream, result.Malformation);
        Assert.IsFalse(Http2FrameMalformations.IsConnectionError(result.Malformation!.Value));
        Assert.AreEqual(new Http2WindowUpdateFrame(1, 1), await ReadFrameAsync(reader));
    }

    [TestMethod]
    public async Task ReadFrameAsync_LargerAdvertisedMaximum_ReadsALargerFrame()
    {
        var bytes = Convert.FromHexString("008000" + "09" + "00" + "00000001").Concat(new byte[32768]).ToArray();

        var frame = (Http2ContinuationFrame)await ReadFrameAsync(new Http2FrameReader(
            (buffer, cancellationToken) => new InMemoryConnection(Http2RecordedFixture.Whole(bytes)).ReadAsync(buffer, cancellationToken), 32768));

        Assert.AreEqual(32768, frame.FieldBlockFragment.Length);
    }

    [TestMethod]
    [DataRow(16383)]
    [DataRow(16777216)]
    public void Constructor_MaximumFrameSizeOutsideWhatRfc9113Allows_Throws(int maxFrameSize)
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new Http2FrameReader(new InMemoryConnection([]), maxFrameSize));
    }

    private static async Task<Http2Frame> ReadFrameAsync(Http2FrameReader reader)
    {
        var result = await reader.ReadFrameAsync(CancellationToken.None);
        Assert.AreEqual(Http2FrameReadOutcome.FrameRead, result.Outcome, $"Malformation: {result.Malformation}");
        Assert.IsNull(result.Header);
        Assert.IsNull(result.Malformation);

        return result.Frame!;
    }
}
