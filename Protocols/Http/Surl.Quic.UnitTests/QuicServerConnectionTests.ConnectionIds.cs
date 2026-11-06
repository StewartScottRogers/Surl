using System.Security.Cryptography;

namespace Surl.Quic;

/// <summary>The client's connection IDs (RFC 9000 sections 5.1.1, 5.1.2, 19.15 and 19.16): kept up to the server's limit of 2, retired as Retire Prior To asks.</summary>
public sealed partial class QuicServerConnectionTests
{
    [TestMethod]
    public void ReceiveDatagram_NewConnectionIdMakingAThirdActive_EndsWithConnectionIdLimitError()
    {
        using var key = RSA.Create(2048);
        using var exchange = UnitTestExchange.Completed(key);

        exchange.Send(exchange.Client.SealOneRtt(NewConnectionId(exchange, 1, 0, 0xa1), NewConnectionId(exchange, 2, 0, 0xa2)));

        var close = exchange.Client.FramesReceived[QuicPacketType.OneRtt].OfType<QuicConnectionCloseFrame>().Single();
        Assert.AreEqual((ulong)QuicTransportErrorCode.ConnectionIdLimitError, close.ErrorCode);
        Assert.AreEqual((ulong)QuicFrameTypes.NewConnectionId, close.FrameType);
        Assert.IsTrue(exchange.Server.IsClosed);
    }

    [TestMethod]
    public void ReceiveDatagram_RetirePriorToCoveringTheIdInUse_RetiresItAndSendsToTheRemainingId()
    {
        using var key = RSA.Create(2048);
        using var exchange = UnitTestExchange.Completed(key);
        var replacement = NewConnectionId(exchange, 1, 1, 0xb1);

        exchange.Send(exchange.Client.SealOneRtt(replacement));
        exchange.Take(exchange.Server.SendApplicationFrames([new QuicMaxDataFrame(1)]));

        Assert.IsFalse(exchange.Server.IsClosed);
        Assert.AreEqual(new QuicRetireConnectionIdFrame(0), exchange.Client.FramesReceived[QuicPacketType.OneRtt].OfType<QuicRetireConnectionIdFrame>().Single());
        CollectionAssert.AreEqual(exchange.Client.SourceConnectionId, exchange.Client.OneRttDestinationConnectionIdsReceived[0]);
        CollectionAssert.AreEqual(replacement.ConnectionId.ToArray(), exchange.Client.OneRttDestinationConnectionIdsReceived[^1]);
    }

    [TestMethod]
    public void ReceiveDatagram_NewConnectionIdRetiringTheFirstWhileTwoAreActive_KeepsTwoAndSendsToTheLowestLeft()
    {
        using var key = RSA.Create(2048);
        using var exchange = UnitTestExchange.Completed(key);
        var first = NewConnectionId(exchange, 1, 0, 0xc1);

        exchange.Send(exchange.Client.SealOneRtt(first, NewConnectionId(exchange, 2, 1, 0xc2)));
        exchange.Take(exchange.Server.SendApplicationFrames([new QuicMaxDataFrame(1)]));

        Assert.IsFalse(exchange.Server.IsClosed);
        Assert.AreEqual(new QuicRetireConnectionIdFrame(0), exchange.Client.FramesReceived[QuicPacketType.OneRtt].OfType<QuicRetireConnectionIdFrame>().Single());
        CollectionAssert.AreEqual(first.ConnectionId.ToArray(), exchange.Client.OneRttDestinationConnectionIdsReceived[^1]);
    }

    [TestMethod]
    public void ReceiveDatagram_RepeatedNewConnectionId_IsIgnored()
    {
        using var key = RSA.Create(2048);
        using var exchange = UnitTestExchange.Completed(key);
        var frame = NewConnectionId(exchange, 1, 0, 0xd1);
        exchange.Send(exchange.Client.SealOneRtt(frame));

        exchange.Send(exchange.Client.SealOneRtt(frame));
        exchange.Take(exchange.Server.SendApplicationFrames([new QuicMaxDataFrame(1)]));

        Assert.IsFalse(exchange.Server.IsClosed);
        Assert.IsEmpty(exchange.Client.FramesReceived[QuicPacketType.OneRtt].OfType<QuicRetireConnectionIdFrame>());
        CollectionAssert.AreEqual(exchange.Client.SourceConnectionId, exchange.Client.OneRttDestinationConnectionIdsReceived[^1]);
    }

    [TestMethod]
    public void ReceiveDatagram_NewConnectionIdGivingAKnownSequenceNumberAnotherId_EndsWithProtocolViolation()
    {
        using var key = RSA.Create(2048);
        using var exchange = UnitTestExchange.Completed(key);
        exchange.Send(exchange.Client.SealOneRtt(NewConnectionId(exchange, 1, 0, 0xe1)));

        exchange.Send(exchange.Client.SealOneRtt(NewConnectionId(exchange, 1, 0, 0xe2)));

        var close = exchange.Client.FramesReceived[QuicPacketType.OneRtt].OfType<QuicConnectionCloseFrame>().Single();
        Assert.AreEqual((ulong)QuicTransportErrorCode.ProtocolViolation, close.ErrorCode);
        Assert.AreEqual((ulong)QuicFrameTypes.NewConnectionId, close.FrameType);
        Assert.IsTrue(exchange.Server.IsClosed);
    }

    /// <summary>A NEW_CONNECTION_ID whose connection ID is as long as the client's own, so the client can read the server's short headers, filled with <paramref name="fill"/>.</summary>
    private static QuicNewConnectionIdFrame NewConnectionId(UnitTestExchange exchange, ulong sequenceNumber, ulong retirePriorTo, byte fill) =>
        new(sequenceNumber, retirePriorTo, Enumerable.Repeat(fill, exchange.Client.SourceConnectionId.Length).ToArray(), new byte[QuicNewConnectionIdFrame.StatelessResetTokenLength]);
}
