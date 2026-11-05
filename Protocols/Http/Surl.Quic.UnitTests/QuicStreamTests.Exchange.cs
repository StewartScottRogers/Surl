using System.Security.Cryptography;

namespace Surl.Quic;

/// <summary>A request and a response of 1 MiB each over one bidirectional stream, carried by credit updates alone.</summary>
public sealed partial class QuicStreamTests
{
    private const int OneMebibyte = 1 << 20;

    /// <summary>The client's own credit for stream data on a stream it opened: curl's recorded initial_max_stream_data_bidi_local.</summary>
    private const ulong RecordedClientStreamWindow = 32768;

    [TestMethod]
    public void Transmit_RequestAndResponseOfOneMebibyte_FlowThroughCreditUpdatesOnly()
    {
        using var key = RSA.Create(2048);
        using var exchange = UnitTestExchange.Completed(key);
        var request = RandomNumberGenerator.GetBytes(OneMebibyte);
        var response = RandomNumberGenerator.GetBytes(OneMebibyte);

        var (stream, received, creditUpdates) = UploadRequest(exchange, request);
        stream.Write(response, fin: true);
        var body = DownloadResponse(exchange, stream);

        CollectionAssert.AreEqual(request, received);
        Assert.IsTrue(stream.IsReadComplete);
        Assert.IsGreaterThan(2, creditUpdates);
        CollectionAssert.AreEqual(response, body);
        Assert.IsNotEmpty(Sent<QuicStreamDataBlockedFrame>(exchange));
        Assert.IsFalse(exchange.Server.IsClosed);
    }

    /// <summary>
    /// The test client sends the request in 1000-byte STREAM frames as far as the server's credit
    /// allows, and the server reads what arrives after each datagram; only the server's
    /// MAX_STREAM_DATA and MAX_DATA let the client go on.
    /// </summary>
    private static (QuicStream Stream, byte[] Received, int CreditUpdates) UploadRequest(UnitTestExchange exchange, byte[] request)
    {
        var parameters = QuicTransportParameters.Read(exchange.Client.Tls.ServerTransportParameters());
        var streamCredit = parameters.InitialMaxStreamDataBidiRemote!.Value;
        var connectionCredit = parameters.InitialMaxData!.Value;
        var received = new List<byte>();
        var creditUpdates = 0;
        var seen = 0;
        QuicStream? stream = null;
        for (var sent = 0; sent < request.Length;)
        {
            var count = (int)Math.Min((ulong)Math.Min(1000, request.Length - sent), Math.Min(streamCredit, connectionCredit) - (ulong)sent);
            Assert.IsGreaterThan(0, count, "The client ran out of the server's credit.");
            var output = exchange.Send(exchange.Client.SealOneRtt(new QuicStreamFrame(0, (ulong)sent, request.AsMemory(sent, count), sent + count == request.Length)));
            sent += count;
            stream ??= output.Streams.Single();
            var buffer = new byte[stream.ReadableLength];
            stream.Read(buffer);
            received.AddRange(buffer);
            exchange.Take(exchange.Server.Transmit());
            foreach (var frame in NewFrames(exchange, ref seen))
            {
                creditUpdates += frame is QuicMaxStreamDataFrame or QuicMaxDataFrame ? 1 : 0;
                streamCredit = frame is QuicMaxStreamDataFrame { StreamId: 0 } maxStreamData ? maxStreamData.MaximumStreamData : streamCredit;
                connectionCredit = frame is QuicMaxDataFrame maxData ? maxData.MaximumData : connectionCredit;
            }
        }

        return (stream!, [.. received], creditUpdates);
    }

    /// <summary>The test client takes the server's STREAM frames in order and grants a window past what it has taken after each round.</summary>
    private static byte[] DownloadResponse(UnitTestExchange exchange, QuicStream stream)
    {
        var seen = exchange.Client.FramesReceived[QuicPacketType.OneRtt].Count;
        exchange.Take(exchange.Server.Transmit());
        var body = new List<byte>();
        var finished = false;
        for (var round = 0; !finished; round++)
        {
            Assert.IsLessThan(200, round, "The response stalled.");
            foreach (var frame in NewFrames(exchange, ref seen).OfType<QuicStreamFrame>())
            {
                Assert.AreEqual(stream.Id, frame.StreamId);
                Assert.AreEqual((ulong)body.Count, frame.Offset);
                body.AddRange(frame.Data.ToArray());
                finished |= frame.Fin;
            }

            exchange.Send(exchange.Client.SealOneRtt(new QuicMaxStreamDataFrame(stream.Id, (ulong)body.Count + RecordedClientStreamWindow)));
        }

        return [.. body];
    }

    private static List<QuicFrame> NewFrames(UnitTestExchange exchange, ref int seen)
    {
        var frames = exchange.Client.FramesReceived[QuicPacketType.OneRtt];
        var fresh = frames.Skip(seen).ToList();
        seen = frames.Count;
        return fresh;
    }
}
