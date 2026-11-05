using System.Security.Cryptography;

namespace Surl.Quic;

/// <summary>
/// Loss recovery on a connection (BL-379, RFC 9002 and RFC 9000 section 13.3): the tests drop
/// chosen datagrams on their way to the test client and check what the server sends again.
/// </summary>
public sealed partial class QuicServerConnectionTests
{
    [TestMethod]
    public void ReceiveDatagram_AckShowingAOneRttPacketWithStreamDataDropped_SendsItsDataAgain()
    {
        using var key = RSA.Create(2048);
        using var exchange = UnitTestExchange.Completed(key);
        exchange.AcknowledgeOneRtt();
        var stream = exchange.Server.OpenUnidirectionalStream()!;
        var data = RandomNumberGenerator.GetBytes(5000);
        stream.Write(data, fin: true);
        var datagrams = exchange.Server.Transmit().Datagrams;
        exchange.Client.Receive(datagrams.Skip(1));
        var lostFrame = OneRttFrames(exchange).OfType<QuicStreamFrame>().Min(frame => frame.Offset);

        var output = exchange.AcknowledgeOneRtt();

        Assert.IsGreaterThan(3, datagrams.Count);
        Assert.AreNotEqual((ulong)0, lostFrame, "The dropped datagram held the stream's first bytes.");
        var resent = OneRttFrames(exchange).OfType<QuicStreamFrame>().Single(frame => frame.Offset == 0);
        CollectionAssert.AreEqual(data[..resent.Data.Length], resent.Data.ToArray());
        Assert.HasCount(1, output.Datagrams);
    }

    [TestMethod]
    public void HandleTimeout_LastPacketDropped_ProbeTimeoutSendsItsDataAgain()
    {
        using var key = RSA.Create(2048);
        using var exchange = UnitTestExchange.Completed(key);
        exchange.AcknowledgeOneRtt();
        var stream = exchange.Server.OpenUnidirectionalStream()!;
        stream.Write("tail"u8, fin: true);
        var dropped = exchange.Server.Transmit().Datagrams.Single();
        var sent = exchange.Time.GetUtcNow();

        var deadline = exchange.Server.NextDeadline;
        var probe = exchange.AdvanceToNextDeadline();

        Assert.IsNotEmpty(dropped);
        Assert.AreEqual(sent + TimeSpan.FromMilliseconds(1 + 25), deadline, "smoothed_rtt 0 + kGranularity + max_ack_delay 25 ms.");
        var resent = OneRttFrames(exchange).OfType<QuicStreamFrame>().Single();
        CollectionAssert.AreEqual("tail"u8.ToArray(), resent.Data.ToArray());
        Assert.IsTrue(resent.Fin);
        Assert.HasCount(1, probe.Datagrams);
    }

    [TestMethod]
    public void HandleTimeout_HandshakePacketDropped_IsSentAgainAndTheHandshakeCompletes()
    {
        using var key = RSA.Create(2048);
        using var exchange = new UnitTestExchange(UnitTestCertificates.Options(key, copies: 3));
        var flight = new List<byte[]>();
        foreach (var datagram in exchange.Client.InitialDatagrams)
        {
            // The flight answers curl's second Initial; its middle datagram holds only Handshake data.
            var answer = exchange.Server.ReceiveDatagram(datagram).Datagrams;
            flight = answer.Count > 1 ? [.. answer] : flight;
            exchange.Client.Receive(answer.Count > 1 ? answer.Where((_, index) => index != 1) : answer);
        }

        var stalled = exchange.Client.ClientFinished is null;
        exchange.Send(exchange.Client.SealHandshake(exchange.Client.AckFrame(QuicPacketType.Handshake)));

        for (var round = 0; exchange.Client.ClientFinished is null; round++)
        {
            Assert.IsLessThan(10, round, "The dropped Handshake packet was never sent again.");
            exchange.AdvanceToNextDeadline();
        }

        exchange.Send(exchange.Client.SealHandshake(exchange.Client.AckFrame(QuicPacketType.Handshake), new QuicCryptoFrame(0, exchange.Client.ClientFinished!)));

        Assert.IsGreaterThan(1, flight.Count);
        Assert.IsTrue(stalled, "Without the dropped datagram the client cannot finish.");
        Assert.IsTrue(exchange.Server.IsHandshakeComplete);
    }

    [TestMethod]
    public void ReceiveDatagram_AckShowingAckOnlyPacketsDropped_SendsNothingAgain()
    {
        using var key = RSA.Create(2048);
        using var exchange = UnitTestExchange.Completed(key);
        exchange.AcknowledgeOneRtt();
        var answers = Enumerable.Range(0, 4).Select(_ => exchange.Server.ReceiveDatagram(exchange.Client.SealOneRtt(new QuicPingFrame())).Datagrams.Single()).ToList();
        exchange.Client.Receive(answers.Skip(1));
        var framesBefore = OneRttFrames(exchange).Count;
        var deadline = exchange.Server.NextDeadline;

        var output = exchange.AcknowledgeOneRtt();
        exchange.Time.Advance(TimeSpan.FromSeconds(5));
        var later = exchange.Server.HandleTimeout();

        Assert.HasCount(framesBefore, OneRttFrames(exchange));
        Assert.AreEqual(exchange.Time.GetUtcNow() - TimeSpan.FromSeconds(5) + TimeSpan.FromSeconds(30), deadline, "Only the idle timeout: ACK-only packets arm no probe timeout.");
        Assert.IsEmpty(output.Datagrams);
        Assert.IsEmpty(later.Datagrams);
    }

    [TestMethod]
    public void ReceiveDatagram_DroppedHandshakePacketFoundLostBesideAnAck_SendsItsCryptoDataSplitToFit()
    {
        using var key = RSA.Create(2048);
        using var exchange = new UnitTestExchange(UnitTestCertificates.Options(key, copies: 3));
        foreach (var datagram in exchange.Client.InitialDatagrams)
        {
            var answer = exchange.Server.ReceiveDatagram(datagram).Datagrams;
            exchange.Client.Receive(answer.Count > 1 ? answer.Where((_, index) => index != 1) : answer);
        }

        var offsetsBeforeTheLoss = exchange.Client.FramesReceived[QuicPacketType.Handshake].OfType<QuicCryptoFrame>().Select(frame => frame.Offset).ToHashSet();
        exchange.Send(exchange.Client.SealHandshake(exchange.Client.AckFrame(QuicPacketType.Handshake), new QuicPingFrame()));
        exchange.Time.Advance(TimeSpan.FromSeconds(1));

        var output = exchange.Send(exchange.Client.SealHandshake(exchange.Client.AckFrame(QuicPacketType.Handshake), new QuicPingFrame()));

        var resent = exchange.Client.FramesReceived[QuicPacketType.Handshake].OfType<QuicCryptoFrame>().Where(frame => !offsetsBeforeTheLoss.Contains(frame.Offset)).ToList();
        Assert.HasCount(2, resent, "The lost CRYPTO frame no longer fits beside the ACK: its two parts.");
        Assert.HasCount(2, output.Datagrams);
        Assert.IsNotNull(exchange.Client.ClientFinished);
    }

    [TestMethod]
    public void ReceiveDatagram_InitialPacketFoundLostWhileTheWindowIsFull_WaitsForADatagramItMayPadTo1200Bytes()
    {
        using var key = RSA.Create(2048);
        using var exchange = new UnitTestExchange(UnitTestCertificates.Options(key, copies: 24));
        foreach (var datagram in exchange.Client.InitialDatagrams)
        {
            var answer = exchange.Server.ReceiveDatagram(datagram).Datagrams;
            exchange.Client.Receive(answer.Count > 1 ? answer.Skip(1) : answer);
        }

        exchange.Send(exchange.Client.SealInitial(exchange.Client.ServerConnectionId!, 1200, exchange.Client.AckFrame(QuicPacketType.Initial), new QuicPingFrame()));
        var lossFound = exchange.Send(exchange.Client.SealInitial(exchange.Client.ServerConnectionId!, 1200, exchange.Client.AckFrame(QuicPacketType.Initial), new QuicPingFrame()));
        var serverHelloHeldBack = !exchange.Client.FramesReceived[QuicPacketType.Initial].OfType<QuicCryptoFrame>().Any();
        for (var round = 0; exchange.Client.ClientFinished is null; round++)
        {
            Assert.IsLessThan(50, round, "The handshake never finished.");
            if (exchange.Client.PacketNumbersReceived[QuicPacketType.Handshake].Count > 0)
            {
                exchange.Send(exchange.Client.SealHandshake(exchange.Client.AckFrame(QuicPacketType.Handshake)));
            }

            exchange.AdvanceToNextDeadline();
        }

        exchange.SendFinished();

        Assert.IsTrue(serverHelloHeldBack, "The ServerHello found lost is not sent in a datagram the window cannot let reach 1200 bytes.");
        Assert.IsTrue(lossFound.Datagrams.All(datagram => datagram.Length < QuicServerConnection.MaxDatagramSize));
        Assert.IsTrue(exchange.Server.IsHandshakeComplete);
    }

    [TestMethod]
    [DataRow(1000UL, 183.5, DisplayName = "8 ms: adjusted_rtt 92 ms")]
    [DataRow(1000000UL, 164.375, DisplayName = "capped at max_ack_delay 25 ms: adjusted_rtt 75 ms")]
    public void ReceiveDatagram_AckDelayOfTheClientsAck_IsTakenOffTheRttSample(ulong encodedAckDelay, double probeTimeoutMilliseconds)
    {
        using var key = RSA.Create(2048);
        using var exchange = UnitTestExchange.Completed(key);
        exchange.Time.Advance(TimeSpan.FromMilliseconds(40));
        exchange.AcknowledgeOneRtt();
        exchange.Take(exchange.Server.SendApplicationFrames([new QuicPingFrame()]));
        exchange.Time.Advance(TimeSpan.FromMilliseconds(100));

        exchange.Send(exchange.Client.SealOneRtt(exchange.Client.AckFrame(QuicPacketType.OneRtt) with { AckDelay = encodedAckDelay }));
        exchange.Take(exchange.Server.SendApplicationFrames([new QuicPingFrame()]));

        Assert.AreEqual(exchange.Time.GetUtcNow() + TimeSpan.FromMilliseconds(probeTimeoutMilliseconds), exchange.Server.NextDeadline, "smoothed_rtt + 4 * rttvar + max_ack_delay, from samples of 40 ms and then 100 ms.");
    }

    [TestMethod]
    public void NextDeadline_ClientMaxAckDelay_IsAddedToTheProbeTimeout()
    {
        using var key = RSA.Create(2048);
        using var exchange = UnitTestExchange.Completed(UnitTestCertificates.Options(key), UnitTestClientHelloEdits.Replace("090480040000", "0b0480000064"));
        exchange.AcknowledgeOneRtt();

        exchange.Take(exchange.Server.SendApplicationFrames([new QuicPingFrame()]));

        Assert.AreEqual((ulong)100, exchange.Server.PeerTransportParameters!.MaxAckDelay);
        Assert.AreEqual(exchange.Time.GetUtcNow() + TimeSpan.FromMilliseconds(1 + 100), exchange.Server.NextDeadline);
    }

    [TestMethod]
    public void HandleTimeout_ProbeWithNothingToSendAgain_IsAPing()
    {
        using var key = RSA.Create(2048);
        using var exchange = UnitTestExchange.Completed(key);
        exchange.AcknowledgeOneRtt();
        var dropped = exchange.Server.ReceiveDatagram(exchange.Client.SealOneRtt(new QuicPathChallengeFrame(new byte[8]))).Datagrams;

        exchange.AdvanceToNextDeadline();

        Assert.HasCount(1, dropped);
        Assert.HasCount(1, OneRttFrames(exchange).OfType<QuicPingFrame>().ToList());
        Assert.IsFalse(OneRttFrames(exchange).OfType<QuicPathResponseFrame>().Any(), "PATH_RESPONSE is never sent again (RFC 9000 section 13.3).");
    }

    [TestMethod]
    public void SendApplicationFrames_FrameNotFittingTheRoomLeft_WaitsForTheNextPacketUnlessASplitGivesItSixtyFourBytes()
    {
        using var key = RSA.Create(2048);
        using var exchange = UnitTestExchange.Completed(key);
        QuicFrame[] frames = [new QuicStreamFrame(3, 0, new byte[1100], false), new QuicStreamFrame(3, 1100, new byte[1100], false), new QuicNewTokenFrame(new byte[1100])];

        var output = exchange.Server.SendApplicationFrames(frames);
        exchange.Client.Receive(output.Datagrams);

        Assert.HasCount(3, output.Datagrams);
        CollectionAssert.AreEqual(new[] { 1100, 1100 }, OneRttFrames(exchange).OfType<QuicStreamFrame>().Select(frame => frame.Data.Length).ToArray());
        Assert.HasCount(1, OneRttFrames(exchange).OfType<QuicNewTokenFrame>().ToList());
    }

    private static List<QuicFrame> OneRttFrames(UnitTestExchange exchange) => exchange.Client.FramesReceived[QuicPacketType.OneRtt];
}
