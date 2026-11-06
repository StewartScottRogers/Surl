using System.Security.Cryptography;

namespace Surl.Quic;

/// <summary>The closing and draining states (RFC 9000 section 10.2), on the test clock.</summary>
public sealed partial class QuicServerConnectionTests
{
    [TestMethod]
    public void ReceiveDatagram_InTheClosingState_AnswersWithTheSameConnectionClose()
    {
        using var key = RSA.Create(2048);
        using var exchange = UnitTestExchange.Completed(key);
        var closing = exchange.Server.Close(0x100).Datagrams.Single();

        var answer = exchange.Send(exchange.Client.SealOneRtt(new QuicPingFrame()));

        Assert.IsTrue(exchange.Server.IsClosing);
        Assert.IsFalse(exchange.Server.IsDraining);
        CollectionAssert.AreEqual(closing, answer.Datagrams.Single());
        var close = exchange.Client.FramesReceived[QuicPacketType.OneRtt].OfType<QuicConnectionCloseFrame>().Single();
        Assert.AreEqual(0x100UL, close.ErrorCode);
    }

    [TestMethod]
    public void ReceiveDatagram_InTheClosingState_AnswersOnlyThe1st2nd4thAnd8thDatagram()
    {
        using var key = RSA.Create(2048);
        using var exchange = UnitTestExchange.Completed(key);
        exchange.Take(exchange.Server.Close(0));
        var answered = new List<int>();

        for (var count = 1; count <= 10; count++)
        {
            if (exchange.Send(exchange.Client.SealOneRtt(new QuicPingFrame())).Datagrams.Count > 0)
            {
                answered.Add(count);
            }
        }

        CollectionAssert.AreEqual(new[] { 1, 2, 4, 8 }, answered);
    }

    [TestMethod]
    public void NextDeadline_InTheClosingState_IsThreeProbeTimeoutsAfterTheClose()
    {
        using var key = RSA.Create(2048);
        using var exchange = new UnitTestExchange(UnitTestCertificates.Options(key));
        exchange.Send(exchange.Client.InitialDatagrams[0]);
        var closedAt = exchange.Time.GetUtcNow();

        exchange.Take(exchange.Server.Close(0));

        // No RTT sample yet: 3 * (333 ms + 4 * 166.5 ms + the 25 ms max_ack_delay).
        Assert.AreEqual(closedAt + TimeSpan.FromMilliseconds(3072), exchange.Server.NextDeadline);
        Assert.AreEqual(3, QuicServerConnection.ClosingPeriodProbeTimeouts);
    }

    [TestMethod]
    public void HandleTimeout_BeforeTheClosingStateEnds_KeepsAnswering()
    {
        using var key = RSA.Create(2048);
        using var exchange = UnitTestExchange.Completed(key);
        exchange.Take(exchange.Server.Close(0));
        var end = exchange.Server.NextDeadline!.Value;
        exchange.Time.Advance(end - exchange.Time.GetUtcNow() - TimeSpan.FromTicks(1));

        var timeout = exchange.Server.HandleTimeout();

        Assert.IsEmpty(timeout.Datagrams);
        Assert.IsTrue(exchange.Server.IsClosing);
        Assert.AreEqual(end, exchange.Server.NextDeadline);
        Assert.HasCount(1, exchange.Send(exchange.Client.SealOneRtt(new QuicPingFrame())).Datagrams);
    }

    [TestMethod]
    public void HandleTimeout_PastThreeProbeTimeoutsOfClosing_AnswersNothingMoreAndHasNoDeadline()
    {
        using var key = RSA.Create(2048);
        using var exchange = UnitTestExchange.Completed(key);
        exchange.Take(exchange.Server.Close(0));

        var timeout = exchange.AdvanceToNextDeadline();

        Assert.IsEmpty(timeout.Datagrams);
        Assert.IsFalse(exchange.Server.IsClosing);
        Assert.IsTrue(exchange.Server.IsClosed);
        Assert.IsNull(exchange.Server.NextDeadline);
        Assert.IsEmpty(exchange.Send(exchange.Client.SealOneRtt(new QuicPingFrame())).Datagrams);
    }

    [TestMethod]
    public void ReceiveDatagram_ClientConnectionClose_DrainsSendingNothingForThreeProbeTimeouts()
    {
        using var key = RSA.Create(2048);
        using var exchange = UnitTestExchange.Completed(key);

        var output = exchange.Send(exchange.Client.SealOneRtt(new QuicConnectionCloseFrame(true, 0x100, 0, ReadOnlyMemory<byte>.Empty)));

        Assert.IsEmpty(output.Datagrams);
        Assert.IsTrue(exchange.Server.IsClosed);
        Assert.IsTrue(exchange.Server.IsDraining);
        Assert.IsFalse(exchange.Server.IsClosing);
        Assert.IsNotNull(exchange.Server.NextDeadline);
        Assert.IsEmpty(exchange.Send(exchange.Client.SealOneRtt(new QuicPingFrame())).Datagrams);
        Assert.IsEmpty(exchange.AdvanceToNextDeadline().Datagrams);
        Assert.IsFalse(exchange.Server.IsDraining);
        Assert.IsNull(exchange.Server.NextDeadline);
        Assert.IsEmpty(exchange.Send(exchange.Client.SealOneRtt(new QuicPingFrame())).Datagrams);
    }


    [TestMethod]
    public void ReceiveDatagram_InTheClosingStateBeforeTheAddressIsValidated_KeepsTheAmplificationLimit()
    {
        using var key = RSA.Create(2048);
        using var exchange = ExchangeAtTheAmplificationLimit(key);
        var answeredAt = new List<int>();

        var closeDatagrams = exchange.Server.Close(0).Datagrams;
        for (var count = 1; count <= 1024; count++)
        {
            if (exchange.Send(new byte[1]).Datagrams.Count > 0)
            {
                answeredAt.Add(count);
            }

            Assert.IsLessThanOrEqualTo(3 * exchange.BytesToServer, exchange.BytesFromServer);
        }

        Assert.IsEmpty(closeDatagrams);
        Assert.IsGreaterThan(1, answeredAt[0]);
        Assert.IsGreaterThan(1, answeredAt.Count);
        Assert.IsTrue(answeredAt.All(count => (count & (count - 1)) == 0));
        Assert.IsTrue(exchange.Server.IsClosing);
    }

    [TestMethod]
    public void ReceiveDatagram_InTheClosingStateWhenTheAmplificationLimitHoldsTheRepeat_SendsItAtTheNextAnswer()
    {
        using var key = RSA.Create(2048);
        using var exchange = ExchangeAtTheAmplificationLimit(key);
        exchange.Server.Close(0);
        var first = exchange.Send(new byte[60]).Datagrams;

        var held = exchange.Send(new byte[1]).Datagrams;
        exchange.Send(new byte[1]);
        var repeated = exchange.Send(new byte[60]).Datagrams;

        Assert.IsNotEmpty(first);
        Assert.IsEmpty(held);
        Assert.HasCount(first.Count, repeated);
        CollectionAssert.AreEqual(first[0], repeated[0]);
        Assert.IsLessThanOrEqualTo(3 * exchange.BytesToServer, exchange.BytesFromServer);
    }

    /// <summary>
    /// A connection whose client address is not validated and whose server has sent all but a few
    /// bytes of three times what it received: a long flight, acknowledged at Initial only, and
    /// carried on by probe timeouts.
    /// </summary>
    private static UnitTestExchange ExchangeAtTheAmplificationLimit(RSA key)
    {
        var exchange = new UnitTestExchange(UnitTestCertificates.Options(key, copies: 24));
        exchange.SendRecordedInitials();
        exchange.Send(exchange.Client.SealInitial(exchange.Client.ServerConnectionId!, 1200, exchange.Client.AckFrame(QuicPacketType.Initial), new QuicPingFrame()));
        for (var round = 0; round < 20 && (3 * exchange.BytesToServer) - exchange.BytesFromServer >= 200; round++)
        {
            exchange.AdvanceToNextDeadline();
        }

        Assert.IsLessThan(200, (3 * exchange.BytesToServer) - exchange.BytesFromServer);
        return exchange;
    }
}
