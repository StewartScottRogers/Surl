using System.Security.Cryptography;

namespace Surl.Quic;

/// <summary>
/// The 1 MiB request and response of <see cref="Transmit_RequestAndResponseOfOneMebibyte_FlowThroughCreditUpdatesOnly"/>
/// over a path that drops one datagram in twenty each way (BL-379): the server's loss detection
/// sends again what it lost, and the test client sends again what the server's ACKs show it lost.
/// </summary>
public sealed partial class QuicStreamTests
{
    private const int MaxRequestFrameData = 1000;

    [TestMethod]
    public void Transmit_OneMebibyteEachWayOverAPathDroppingFivePercent_Completes()
    {
        using var key = RSA.Create(2048);
        using var exchange = UnitTestExchange.Completed(key);
        var channel = new UnitTestLossyChannel(exchange);
        var request = RandomNumberGenerator.GetBytes(OneMebibyte);
        var response = RandomNumberGenerator.GetBytes(OneMebibyte);

        var (stream, received) = UploadRequestOverLossyChannel(exchange, channel, request);
        stream.Write(response, fin: true);
        var body = DownloadResponseOverLossyChannel(exchange, channel, stream);

        CollectionAssert.AreEqual(request, received);
        CollectionAssert.AreEqual(response, body);
        Assert.IsGreaterThan(100, channel.Dropped);
        Assert.IsFalse(exchange.Server.IsClosed);
    }

    /// <summary>
    /// The test client sends the request in STREAM frames as the server's credit allows, each packet
    /// with an ACK of the server's; a frame whose packet sits below the largest the server
    /// acknowledged, unacknowledged, is sent again, and with nothing new to send every
    /// unacknowledged frame is. When it has nothing at all to send, the server's credit is what was
    /// lost, and its loss detection timer sends it again.
    /// </summary>
    private static (QuicStream Stream, byte[] Received) UploadRequestOverLossyChannel(UnitTestExchange exchange, UnitTestLossyChannel channel, byte[] request)
    {
        var parameters = QuicTransportParameters.Read(exchange.Client.Tls.ServerTransportParameters());
        var limits = (Stream: parameters.InitialMaxStreamDataBidiRemote!.Value, Connection: parameters.InitialMaxData!.Value);
        var unacknowledged = new SortedDictionary<ulong, QuicStreamFrame>();
        var resend = new Queue<QuicStreamFrame>();
        var received = new List<byte>();
        var seen = exchange.Client.FramesReceived[QuicPacketType.OneRtt].Count;
        var next = 0;
        QuicStream? stream = null;
        for (var round = 0; stream is not { IsReadComplete: true }; round++)
        {
            Assert.IsLessThan(20000, round, "The request stalled.");
            limits = TakeAcknowledgmentsAndCredit(NewFrames(exchange, ref seen), unacknowledged, resend, limits);
            var frame = resend.Count > 0 ? resend.Dequeue() : NextRequestFrame(request, ref next, Math.Min(limits.Stream, limits.Connection));
            if (frame is null)
            {
                ResendOrWait(channel, unacknowledged, resend);
                continue;
            }

            unacknowledged[exchange.Client.NextOneRttPacketNumber] = frame;
            var output = channel.SendToServer(exchange.Client.SealOneRtt(exchange.Client.AckFrame(QuicPacketType.OneRtt), frame));
            stream ??= output.Streams.SingleOrDefault();
            received.AddRange(ReadAll(stream));
            channel.Deliver(exchange.Server.Transmit());
            exchange.Time.Advance(TimeSpan.FromMilliseconds(1));
        }

        return (stream, [.. received]);
    }

    /// <summary>Forgets the frames the server's ACKs cover, queues again those below the largest it acknowledged, and returns the limits its MAX_STREAM_DATA for stream 0 and MAX_DATA raise.</summary>
    private static (ulong Stream, ulong Connection) TakeAcknowledgmentsAndCredit(List<QuicFrame> frames, SortedDictionary<ulong, QuicStreamFrame> unacknowledged, Queue<QuicStreamFrame> resend, (ulong Stream, ulong Connection) limits)
    {
        var (streamLimit, connectionLimit) = limits;
        foreach (var frame in frames)
        {
            switch (frame)
            {
                case QuicAckFrame ack:
                    ForgetAcknowledged(ack, unacknowledged, resend);
                    break;
                case QuicMaxStreamDataFrame { StreamId: 0 } maxStreamData:
                    streamLimit = Math.Max(streamLimit, maxStreamData.MaximumStreamData);
                    break;
                case QuicMaxDataFrame maxData:
                    connectionLimit = Math.Max(connectionLimit, maxData.MaximumData);
                    break;
            }
        }

        return (streamLimit, connectionLimit);
    }

    private static void ForgetAcknowledged(QuicAckFrame ack, SortedDictionary<ulong, QuicStreamFrame> unacknowledged, Queue<QuicStreamFrame> resend)
    {
        foreach (var packetNumber in unacknowledged.Keys.ToList())
        {
            if (ack.Ranges.Any(range => range.Smallest <= packetNumber && packetNumber <= range.Largest))
            {
                unacknowledged.Remove(packetNumber);
            }
            else if (packetNumber < ack.LargestAcknowledged)
            {
                resend.Enqueue(unacknowledged[packetNumber]);
                unacknowledged.Remove(packetNumber);
            }
        }
    }

    private static QuicStreamFrame? NextRequestFrame(byte[] request, ref int next, ulong credit)
    {
        var count = (int)Math.Min((ulong)Math.Min(MaxRequestFrameData, request.Length - next), credit - (ulong)next);
        if (count <= 0)
        {
            return null;
        }

        var frame = new QuicStreamFrame(0, (ulong)next, request.AsMemory(next, count), next + count == request.Length);
        next += count;
        return frame;
    }

    private static void ResendOrWait(UnitTestLossyChannel channel, SortedDictionary<ulong, QuicStreamFrame> unacknowledged, Queue<QuicStreamFrame> resend)
    {
        if (unacknowledged.Count == 0)
        {
            channel.AdvanceToNextDeadline();
            return;
        }

        foreach (var frame in unacknowledged.Values)
        {
            resend.Enqueue(frame);
        }

        unacknowledged.Clear();
    }

    private static int StreamFramesReceived(UnitTestExchange exchange) => exchange.Client.FramesReceived[QuicPacketType.OneRtt].Count(frame => frame is QuicStreamFrame);

    private static byte[] ReadAll(QuicStream? stream)
    {
        var buffer = new byte[stream?.ReadableLength ?? 0];
        stream?.Read(buffer);
        return buffer;
    }

    /// <summary>
    /// The test client puts the server's STREAM frames in place by offset, and each round sends an
    /// ACK and a window past what it holds in order; a round that brings nothing new waits for the
    /// server's loss detection timer, unless the client's own packet was what was lost.
    /// </summary>
    private static byte[] DownloadResponseOverLossyChannel(UnitTestExchange exchange, UnitTestLossyChannel channel, QuicStream stream)
    {
        var body = new byte[OneMebibyte];
        var held = new bool[OneMebibyte];
        var inOrder = 0;
        var seen = exchange.Client.FramesReceived[QuicPacketType.OneRtt].Count;
        channel.Deliver(exchange.Server.Transmit());
        for (var round = 0; inOrder < OneMebibyte; round++)
        {
            Assert.IsLessThan(20000, round, "The response stalled.");
            foreach (var frame in NewFrames(exchange, ref seen).OfType<QuicStreamFrame>())
            {
                Assert.AreEqual(stream.Id, frame.StreamId);
                frame.Data.Span.CopyTo(body.AsSpan((int)frame.Offset));
                Array.Fill(held, true, (int)frame.Offset, frame.Data.Length);
            }

            while (inOrder < OneMebibyte && held[inOrder])
            {
                inOrder++;
            }

            var before = StreamFramesReceived(exchange);
            var credit = new QuicMaxStreamDataFrame(stream.Id, (ulong)inOrder + RecordedClientStreamWindow);
            var delivered = channel.SendToServer(exchange.Client.SealOneRtt(exchange.Client.AckFrame(QuicPacketType.OneRtt), credit)) != QuicConnectionOutput.Empty;
            if (delivered && before == StreamFramesReceived(exchange) && inOrder < OneMebibyte)
            {
                channel.AdvanceToNextDeadline();
            }

            exchange.Time.Advance(TimeSpan.FromMilliseconds(1));
        }

        return body;
    }
}
