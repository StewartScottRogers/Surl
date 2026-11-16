using System.Security.Cryptography;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Quic;

/// <summary>
/// The decrypted-bytes sink a connection reports STREAM and DATAGRAM payloads to (BL-1268, ADR-0169
/// decision 1). Frames are sealed by the test client from RFC 9000 section 19.8 and RFC 9221 section 4.
/// </summary>
[TestClass]
public sealed class QuicDecryptedBytesTests
{
    [TestMethod]
    public void ReceiveDatagram_StreamFramesOutOfOrder_ReportsEachPayloadInStreamOrderWithItsStreamId()
    {
        using var key = RSA.Create(2048);
        var sink = new RecordingSink();
        using var exchange = UnitTestExchange.Completed(Options(key, sink));

        exchange.Send(exchange.Client.SealOneRtt(Data(4, 5, " world")));
        exchange.Send(exchange.Client.SealOneRtt(Data(4, 0, "hello")));

        CollectionAssert.AreEqual(
            new[] { "R Decrypted 4 hello world" },
            sink.Reports);
    }

    [TestMethod]
    public void Transmit_FinWithoutData_ReportsNothing()
    {
        using var key = RSA.Create(2048);
        var sink = new RecordingSink();
        using var exchange = UnitTestExchange.Completed(Options(key, sink));

        exchange.Server.OpenUnidirectionalStream()!.Write([], fin: true);
        exchange.Take(exchange.Server.Transmit());
        exchange.Send(exchange.Client.SealOneRtt(new QuicStreamFrame(4, 0, Array.Empty<byte>(), true)));

        Assert.IsEmpty(sink.Reports);
    }

    [TestMethod]
    public void Transmit_StreamAndDatagramPayloads_ReportsThemAsSent()
    {
        using var key = RSA.Create(2048);
        var sink = new RecordingSink();
        using var exchange = UnitTestExchange.Completed(Options(key, sink), UnitTestClientHelloEdits.ReplaceParameters(QuicServerConnectionTests.ClientParametersOfferingDatagramFramesOf1024Bytes));

        var stream = exchange.Server.OpenUnidirectionalStream()!;
        stream.Write("settings"u8, fin: true);
        exchange.Take(exchange.Server.Transmit());
        exchange.Take(exchange.Server.SendDatagram("ping"u8.ToArray()));

        CollectionAssert.AreEqual(
            new[] { "S Decrypted 3 settings", "S Decrypted - ping" },
            sink.Reports);
    }

    [TestMethod]
    public void ReceiveDatagram_DatagramFrame_ReportsItsPayloadWithoutAStreamId()
    {
        using var key = RSA.Create(2048);
        var sink = new RecordingSink();
        var options = new QuicServerConnectionOptions
        {
            CertificateChain = [UnitTestCertificates.SelfSigned(key)],
            CertificateKey = key,
            MaxDatagramFrameSize = 7,
            DecryptedBytes = sink,
        };
        using var exchange = UnitTestExchange.Completed(options);

        exchange.Send(exchange.Client.SealOneRttPayload(Convert.FromHexString("3068656c6c6f")));

        CollectionAssert.AreEqual(new[] { "R Decrypted - hello" }, sink.Reports);
    }

    [TestMethod]
    public void ReceiveDatagram_CryptoAckPingAndFlowControlFrames_ReportNothing()
    {
        using var key = RSA.Create(2048);
        var sink = new RecordingSink();
        using var exchange = UnitTestExchange.Completed(Options(key, sink));

        exchange.Send(exchange.Client.SealOneRtt(new QuicPingFrame(), new QuicMaxDataFrame(1 << 20)));
        exchange.Take(exchange.Server.Transmit());

        Assert.IsEmpty(sink.Reports, "The handshake's CRYPTO frames, ACKs and control frames are never reported.");
    }

    [TestMethod]
    public void Transmit_RecordingAndDiscardingSink_SendTheSameFrames()
    {
        using var key = RSA.Create(2048);
        var recording = new RecordingSink();
        using var withRecording = UnitTestExchange.Completed(Options(key, recording));
        using var withDiscarding = UnitTestExchange.Completed(UnitTestCertificates.Options(key));

        foreach (var exchange in new[] { withRecording, withDiscarding })
        {
            exchange.Send(exchange.Client.SealOneRtt(Data(0, 0, "GET /")));
            exchange.Server.OpenUnidirectionalStream()!.Write("reply"u8, fin: true);
            exchange.Take(exchange.Server.Transmit());
        }

        Assert.IsNotEmpty(recording.Reports);
        var recorded = FrameBytes(withRecording).ToList();
        var discarded = FrameBytes(withDiscarding).ToList();
        Assert.AreEqual(discarded.Count, recorded.Count);
        Assert.IsTrue(recorded.Zip(discarded).All(pair => pair.First == pair.Second));
    }

    private static IEnumerable<string> FrameBytes(UnitTestExchange exchange) =>
        exchange.Client.FramesReceived[QuicPacketType.OneRtt].OfType<QuicStreamFrame>().Select(frame => Convert.ToHexString(frame.ToBytes()));

    private static QuicServerConnectionOptions Options(RSA key, IDecryptedBytesSink sink) => new()
    {
        CertificateChain = [UnitTestCertificates.SelfSigned(key)],
        CertificateKey = key,
        DecryptedBytes = sink,
    };

    private static QuicStreamFrame Data(ulong streamId, ulong offset, string text) =>
        new(streamId, offset, Encoding.ASCII.GetBytes(text), false);

    private sealed class RecordingSink : IDecryptedBytesSink
    {
        public List<string> Reports { get; } = [];

        public void Received(ExchangeBytesLayer layer, long? streamId, ReadOnlySpan<byte> bytes) => Reports.Add(Describe("R", layer, streamId, bytes));

        public void Sent(ExchangeBytesLayer layer, long? streamId, ReadOnlySpan<byte> bytes) => Reports.Add(Describe("S", layer, streamId, bytes));

        private static string Describe(string direction, ExchangeBytesLayer layer, long? streamId, ReadOnlySpan<byte> bytes) =>
            $"{direction} {layer} {(streamId?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "-")} {Encoding.ASCII.GetString(bytes)}";
    }
}
