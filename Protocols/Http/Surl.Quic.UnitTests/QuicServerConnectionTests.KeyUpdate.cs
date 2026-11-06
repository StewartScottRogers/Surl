using System.Security.Cryptography;

namespace Surl.Quic;

/// <summary>The client's 1-RTT key updates (RFC 9001 section 6), which the server follows and never starts (ADR-0082 decision 4).</summary>
public sealed partial class QuicServerConnectionTests
{
    [TestMethod]
    public void ReceiveDatagram_KeyUpdateAfterTheServerAcknowledgedThePhase_HandsOverItsFramesAndRepliesWithTheUpdatedKeys()
    {
        using var key = RSA.Create(2048);
        using var exchange = UnitTestExchange.Completed(key);
        exchange.Send(exchange.Client.SealOneRtt(new QuicPingFrame()));
        exchange.Client.UpdateKeys();
        var updatePacketNumber = exchange.Client.NextOneRttPacketNumber;

        var output = exchange.Send(exchange.Client.SealOneRtt(new QuicStreamFrame(0, 0, new byte[] { 7, 8 }, true)));

        var stream = output.Streams.Single();
        Assert.AreEqual((ulong)0, stream.Id);
        Assert.AreEqual(2, stream.ReadableLength);
        Assert.IsTrue(exchange.Client.OneRttKeyPhasesReceived[^1], "The server's reply carries key phase bit 1.");
        Assert.AreEqual(updatePacketNumber, OneRttFrames(exchange).OfType<QuicAckFrame>().Last().LargestAcknowledged);
        Assert.IsFalse(exchange.Server.IsClosed);
    }

    [TestMethod]
    public void ReceiveDatagram_SecondKeyUpdateAfterTheFirstWasAcknowledged_IsFollowedBackToKeyPhase0()
    {
        using var key = RSA.Create(2048);
        using var exchange = UnitTestExchange.Completed(key);
        exchange.Send(exchange.Client.SealOneRtt(new QuicPingFrame()));
        exchange.Client.UpdateKeys();
        exchange.Send(exchange.Client.SealOneRtt(new QuicPingFrame()));
        exchange.Client.UpdateKeys();

        var output = exchange.Send(exchange.Client.SealOneRtt(new QuicStreamFrame(0, 0, new byte[] { 7 }, true)));

        Assert.AreEqual(1, output.Streams.Single().ReadableLength);
        Assert.IsFalse(exchange.Client.OneRttKeyPhasesReceived[^1], "The server's reply carries key phase bit 0 again.");
        Assert.IsFalse(exchange.Server.IsClosed);
    }

    [TestMethod]
    public void ReceiveDatagram_KeyUpdateBeforeTheServerAcknowledgedAPacketInThePhase_EndsWithConnectionCloseKeyUpdateError()
    {
        using var key = RSA.Create(2048);
        using var exchange = UnitTestExchange.Completed(key);
        exchange.Client.UpdateKeys();

        exchange.Send(exchange.Client.SealOneRtt(new QuicPingFrame()));

        var close = OneRttFrames(exchange).OfType<QuicConnectionCloseFrame>().Single();
        Assert.AreEqual((ulong)QuicTransportErrorCode.KeyUpdateError, close.ErrorCode);
        Assert.IsFalse(exchange.Client.OneRttKeyPhasesReceived[^1], "The server closes with the keys of the phase it is still in.");
        Assert.IsTrue(exchange.Server.IsClosed);
    }

    [TestMethod]
    public void ReceiveDatagram_SecondKeyUpdateBeforeTheFirstPhaseWasAcknowledged_EndsWithConnectionCloseKeyUpdateError()
    {
        using var key = RSA.Create(2048);
        using var exchange = UnitTestExchange.Completed(key);
        exchange.Send(exchange.Client.SealOneRtt(new QuicPingFrame()));
        exchange.Client.UpdateKeys();
        exchange.Send(exchange.Client.SealOneRtt(exchange.Client.AckFrame(QuicPacketType.OneRtt)));
        exchange.Client.UpdateKeys();

        exchange.Send(exchange.Client.SealOneRtt(new QuicPingFrame()));

        var close = OneRttFrames(exchange).OfType<QuicConnectionCloseFrame>().Single();
        Assert.AreEqual((ulong)QuicTransportErrorCode.KeyUpdateError, close.ErrorCode);
        Assert.IsTrue(exchange.Client.OneRttKeyPhasesReceived[^1], "The server closes in key phase 1, which it followed.");
    }

    [TestMethod]
    public void ReceiveDatagram_PacketOfThePreviousKeyPhaseArrivingLate_OpensWithThePreviousKeys()
    {
        using var key = RSA.Create(2048);
        using var exchange = UnitTestExchange.Completed(key);
        exchange.Send(exchange.Client.SealOneRtt(new QuicPingFrame()));
        var late = exchange.Client.SealOneRtt(new QuicStreamFrame(4, 0, new byte[] { 9 }, true));
        exchange.Client.UpdateKeys();
        exchange.Send(exchange.Client.SealOneRtt(new QuicPingFrame()));

        var output = exchange.Send(late);

        Assert.AreEqual((ulong)4, output.Streams.Single().Id);
        Assert.IsTrue(exchange.Client.OneRttKeyPhasesReceived[^1], "The late packet does not turn the server back to key phase 0.");
        Assert.IsFalse(exchange.Server.IsClosed);
    }

    [TestMethod]
    public void ReceiveDatagram_OtherKeyPhaseThatDoesNotOpenWithTheNextKeys_IsDroppedAndTheKeysKept()
    {
        using var key = RSA.Create(2048);
        using var exchange = UnitTestExchange.Completed(key);
        exchange.Send(exchange.Client.SealOneRtt(new QuicPingFrame()));
        exchange.Client.UpdateKeys();
        var forged = exchange.Client.SealOneRtt(new QuicPingFrame());
        forged[^1] ^= 1;

        var dropped = exchange.Send(forged);
        var output = exchange.Send(exchange.Client.SealOneRtt(new QuicStreamFrame(0, 0, new byte[] { 7 }, true)));

        Assert.IsEmpty(dropped.Datagrams);
        Assert.AreEqual(1, output.Streams.Single().ReadableLength);
        Assert.IsTrue(exchange.Client.OneRttKeyPhasesReceived[^1]);
    }
}
