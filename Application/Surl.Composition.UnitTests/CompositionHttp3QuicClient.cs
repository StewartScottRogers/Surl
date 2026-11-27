using Surl.Hpack;
using Surl.Http3;
using Surl.Protocol.Abstractions;
using Surl.Qpack;
using Surl.Quic;

namespace Surl.Composition;

/// <summary>
/// A hand-built HTTP/3 client for composition tests, on the far side of an <see cref="InMemoryDatagramFlow"/>:
/// its QUIC connection opens with the Initials pinned upstream curl sent, then it sends HTTP/3 frames
/// written from RFC 9114 and QPACK field sections written with <see cref="QpackEncoder"/>.
/// </summary>
internal sealed class CompositionHttp3QuicClient : IDisposable
{
    private const ulong ControlStreamId = 2;
    private const ulong EncoderStreamId = 6;
    private const ulong DecoderStreamId = 10;
    private const ulong SettingsFrameType = 0x04;
    private const ulong HeadersFrameType = 0x01;

    private readonly Dictionary<ulong, ulong> sendOffsets = [];
    private readonly QpackEncoder encoder = new(HpackStringCoding.Raw);
    private int taken;

    public CompositionHttp3QuicClient()
    {
        Quic = UnitTestQuicClient.FromRecording(ReadRecordedInitials());
        var datagrams = Quic.InitialDatagrams.Select(datagram => (ReadOnlyMemory<byte>)datagram).ToList();
        Flow = new InMemoryDatagramFlow(datagrams[0], datagrams.Skip(1));
    }

    /// <summary>The flow the server accepts.</summary>
    public InMemoryDatagramFlow Flow { get; }

    private UnitTestQuicClient Quic { get; }

    /// <summary>Completes the handshake, opens the control stream and the two QPACK streams, then sends a <c>GET</c> of <paramref name="path"/> on stream 0.</summary>
    public async Task GetAsync(string path, CancellationToken cancellationToken)
    {
        await UntilAsync(() => Quic.ClientFinished is not null, cancellationToken);
        Flow.DeliverFromClient(Quic.FinishedDatagram());
        Send(ControlStreamId, [0x00, .. Frame(SettingsFrameType, [])]);
        Send(EncoderStreamId, [0x02]);
        Send(DecoderStreamId, [0x03]);
        var fields = new[] { (":method", "GET"), (":scheme", "https"), (":authority", "127.0.0.1"), (":path", path) };
        Send(0, Frame(HeadersFrameType, encoder.EncodeFieldSection(fields.Select(field => new QpackFieldLine(field.Item1, field.Item2)))), fin: true);
    }

    /// <summary>
    /// Waits for the server to end stream 0, then returns the field lines of the <c>HEADERS</c> frame its answer
    /// opens with, <c>:status</c> first.
    /// </summary>
    public async Task<IReadOnlyList<QpackFieldLine>> ResponseFieldsAsync(CancellationToken cancellationToken)
    {
        await UntilAsync(() => ReceivedOnRequestStream().Any(frame => frame.Fin), cancellationToken);
        var frames = ReceivedOnRequestStream().ToList();
        var data = new byte[frames.Max(frame => (int)frame.Offset + frame.Data.Length)];
        foreach (var frame in frames)
        {
            frame.Data.CopyTo(data.AsMemory((int)frame.Offset));
        }

        Assert.IsTrue(QuicVariableLengthInteger.TryRead(data, out var type, out var typeLength));
        Assert.AreEqual(HeadersFrameType, type);
        Assert.IsTrue(QuicVariableLengthInteger.TryRead(data.AsSpan(typeLength), out var length, out var lengthLength));
        var decoded = new QpackDecoder(0, int.MaxValue).DecodeFieldSection(0, data.AsSpan(typeLength + lengthLength, (int)length));
        return decoded.FieldLines;
    }

    /// <summary>Opens what the server has sent until <paramref name="condition"/> holds, or the token is cancelled.</summary>
    public async Task UntilAsync(Func<bool> condition, CancellationToken cancellationToken)
    {
        while (true)
        {
            var sent = Flow.SentDatagrams;
            Quic.Receive(sent.Skip(taken));
            taken = sent.Count;
            if (condition())
            {
                return;
            }

            await Task.Delay(1, cancellationToken);
        }
    }

    public void Dispose() => Quic.Dispose();

    private IEnumerable<QuicStreamFrame> ReceivedOnRequestStream() =>
        Quic.FramesReceived[QuicPacketType.OneRtt].OfType<QuicStreamFrame>().Where(frame => frame.StreamId == 0);

    private static byte[] Frame(ulong type, byte[] payload) =>
        [.. QuicVariableLengthInteger.Encode(type), .. QuicVariableLengthInteger.Encode((ulong)payload.Length), .. payload];

    private void Send(ulong streamId, byte[] bytes, bool fin = false)
    {
        var offset = sendOffsets.GetValueOrDefault(streamId);
        sendOffsets[streamId] = offset + (ulong)bytes.Length;
        Flow.DeliverFromClient(Quic.SealOneRtt(new QuicStreamFrame(streamId, offset, bytes, fin)));
    }

    /// <summary>The Initial datagrams pinned upstream curl sent for an <c>h3</c> request, in order.</summary>
    internal static List<byte[]> ReadRecordedInitials()
    {
        const string Name = "Fixtures/h3-client-initial/static-8.21.0-win.datagrams.bin";
        using var stream = typeof(CompositionHttp3QuicClient).Assembly.GetManifestResourceStream(Name)
            ?? throw new InvalidOperationException($"No embedded fixture {Name}.");
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        var bytes = copy.ToArray();
        var datagrams = new List<byte[]>();
        for (var offset = 0; offset < bytes.Length;)
        {
            var length = (bytes[offset] << 8) | bytes[offset + 1];
            datagrams.Add(bytes[(offset + 2)..(offset + 2 + length)]);
            offset += 2 + length;
        }

        return datagrams;
    }
}
