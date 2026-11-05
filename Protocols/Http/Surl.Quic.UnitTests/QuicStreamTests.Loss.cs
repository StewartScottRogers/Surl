using System.Security.Cryptography;

namespace Surl.Quic;

/// <summary>What the server sends again when packets carrying stream and flow-control frames are lost (RFC 9000 section 13.3).</summary>
public sealed partial class QuicStreamTests
{
    [TestMethod]
    public void ReceiveDatagram_LostStreamAndCreditFrames_AreSentAgainAsRfc9000Section133Says()
    {
        using var key = RSA.Create(2048);
        using var exchange = UnitTestExchange.Completed(SmallCredit(key));
        exchange.AcknowledgeOneRtt();
        var opened = exchange.Server.ReceiveDatagram(exchange.Client.SealOneRtt(Data(0, 0, "a", fin: true), Data(4, 0, new string('b', 60)), Data(8, 0, new string('c', 60)), Data(2, 0, "d", fin: true))).Streams;
        opened.ToList().ForEach(stream => ReadText(stream));
        opened[0].Write("z"u8, fin: true);
        var unidirectional = exchange.Server.OpenUnidirectionalStream()!;
        unidirectional.Write("y"u8);
        var dropped = exchange.Server.Transmit().Datagrams;
        unidirectional.Reset(7);
        exchange.Server.ReceiveDatagram(exchange.Client.SealOneRtt(new QuicStreamFrame(8, 60, ReadOnlyMemory<byte>.Empty, true)));
        for (var ping = 0; ping < 3; ping++)
        {
            exchange.Send(exchange.Client.SealOneRtt(new QuicPingFrame()));
        }

        var seen = OneRttFramesCount(exchange);
        exchange.AcknowledgeOneRtt();

        var resent = exchange.Client.FramesReceived[QuicPacketType.OneRtt].Skip(seen).ToList();
        Assert.HasCount(1, dropped);
        Assert.AreEqual((ulong)321, resent.OfType<QuicMaxDataFrame>().Single().MaximumData);
        CollectionAssert.AreEquivalent(new[] { new QuicMaxStreamsFrame(true, 101), new QuicMaxStreamsFrame(false, 4) }, resent.OfType<QuicMaxStreamsFrame>().ToArray());
        Assert.AreEqual(new QuicMaxStreamDataFrame(4, 160), resent.OfType<QuicMaxStreamDataFrame>().Single(), "Stream 8 has received everything: its credit is not sent again.");
        var stream = resent.OfType<QuicStreamFrame>().Single();
        Assert.AreEqual((ulong)0, stream.StreamId, "Stream 3 was reset: its data is not sent again.");
        Assert.IsTrue(stream.Fin);
        Assert.AreEqual((ulong)3, resent.OfType<QuicResetStreamFrame>().Single().StreamId);
    }

    private static int OneRttFramesCount(UnitTestExchange exchange) => exchange.Client.FramesReceived[QuicPacketType.OneRtt].Count;
}
