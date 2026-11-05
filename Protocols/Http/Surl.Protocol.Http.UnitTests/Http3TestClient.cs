using System.Security.Cryptography;
using Surl.Content;
using Surl.Hpack;
using Surl.Protocol.Abstractions;
using Surl.Qpack;
using Surl.Quic;

namespace Surl.Protocol.Http;

/// <summary>
/// An HTTP/3 client for tests, on the far side of an <see cref="InMemoryDatagramFlow"/>: its QUIC
/// connection opens with the Initials pinned upstream curl sent (Fixtures/h3-client-initial), and
/// after the handshake it sends HTTP/3 frames written from RFC 9114 and QPACK field sections written
/// with <see cref="QpackEncoder"/>. The server is <see cref="Http3ConnectionServer"/> over
/// <see cref="HttpRequestAnswerer"/>, ridden by a <see cref="QuicDatagramFlowServer"/>, on a
/// <see cref="ManualTimeProvider"/> that stands still unless a test moves it.
/// </summary>
internal sealed class Http3TestClient : IDisposable
{
    /// <summary>The client's control stream: its first unidirectional stream.</summary>
    public const ulong ControlStreamId = 2;

    /// <summary>The client's QPACK encoder stream.</summary>
    public const ulong EncoderStreamId = 6;

    /// <summary>The client's QPACK decoder stream.</summary>
    public const ulong DecoderStreamId = 10;

    /// <summary>The server's control stream: its first unidirectional stream.</summary>
    public const ulong ServerControlStreamId = 3;

    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);
    private readonly Dictionary<ulong, ulong> sendOffsets = [];
    private readonly RSA key = RSA.Create(2048);
    private int taken;

    public Http3TestClient(Func<byte[], byte[]>? editClientHello = null)
    {
        Quic = UnitTestQuicClient.FromRecording(ReadRecordedInitials(), editClientHello);
        var datagrams = Quic.InitialDatagrams.Select(datagram => (ReadOnlyMemory<byte>)datagram).ToList();
        Flow = new InMemoryDatagramFlow(datagrams[0], datagrams.Skip(1));
    }

    public UnitTestQuicClient Quic { get; }

    public InMemoryDatagramFlow Flow { get; }

    public ManualTimeProvider Time { get; } = new(HttpServerHarness.Now);

    public RecordingExchangeLog Log { get; } = new();

    /// <summary>Cancels the exchange, as shutdown or <c>--max-time</c> does.</summary>
    public CancellationTokenSource Exchange { get; } = new();

    /// <summary>The flow server's run, once <see cref="Serve"/> started it.</summary>
    public Task? Serving { get; private set; }

    public QpackEncoder Encoder { get; } = new(HpackStringCoding.Raw);

    /// <summary>A handler that answers in place of <see cref="HttpRequestAnswerer"/>, when a test sets one before starting the server.</summary>
    public IHttpRequestHandler? Handler { get; set; }

    /// <summary>
    /// When a test sets it before starting the server, the server is <see cref="Http3ProtocolServer"/>
    /// with this policy and <see cref="IdleTimeout"/>, as <c>surl --http3</c> composes it, in place of
    /// a hand-built <see cref="QuicDatagramFlowServer"/>.
    /// </summary>
    public IAuthenticationPolicy? ProtocolServerPolicy { get; set; }

    /// <summary>The idle timeout <see cref="Http3ProtocolServer"/> is given.</summary>
    public TimeSpan IdleTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Starts the server with <paramref name="limits"/> and a content store holding <c>file.txt</c>.</summary>
    public Task Serve(CancellationToken cancellationToken, ExchangeLimits? limits = null, IContentFileSystem? fileSystem = null, bool allowUploads = false)
    {
        var context = HttpServerHarness.Context(Log, Time, Exchange.Token, limits);
        var exposure = ContentExposureOptions.ServeEverythingInsideTheRoot with { AllowUploads = allowUploads };
        var store = new ContentStore(HttpServerHarness.Root, fileSystem ?? HttpServerHarness.StandardFileSystem(), exposure);
        if (ProtocolServerPolicy is { } policy)
        {
            Serving = new Http3ProtocolServer(store, policy, [SelfSigned(key)], key, IdleTimeout).ServeAsync(Flow, context);
            return Serving;
        }

        var answerer = Handler ?? new HttpRequestAnswerer(context, store, new AnonymousAuthenticationPolicy().StartHttpConnection(null));
        var options = new QuicServerConnectionOptions
        {
            CertificateChain = [SelfSigned(key)],
            CertificateKey = key,
        };
        Serving = new QuicDatagramFlowServer(options, new Http3ConnectionServer(answerer, context), Time, RandomNumberGenerator.Create())
            .ServeAsync(Flow, cancellationToken);
        return Serving;
    }

    /// <summary>Starts the server, completes the handshake, and opens the client's control stream with <paramref name="settings"/> and its two QPACK streams.</summary>
    /// <remarks>
    /// The server grants three unidirectional streams (ADR-0082 decision 4), so a test that opens
    /// another kind of stream passes <paramref name="openQpackStreams"/> false and uses stream 6 or 10.
    /// </remarks>
    public async Task StartAsync(CancellationToken cancellationToken, ExchangeLimits? limits = null, IContentFileSystem? fileSystem = null, bool allowUploads = false, byte[]? settings = null, bool openQpackStreams = true)
    {
        _ = Serve(cancellationToken, limits, fileSystem, allowUploads);
        await CompleteHandshakeAsync(cancellationToken);
        Send(ControlStreamId, [0x00, .. settings ?? Frame(Http3FrameType.Settings, [])]);
        if (openQpackStreams)
        {
            Send(EncoderStreamId, [0x02]);
            Send(DecoderStreamId, [0x03]);
        }
    }

    public async Task CompleteHandshakeAsync(CancellationToken cancellationToken)
    {
        await UntilAsync(() => Quic.ClientFinished is not null, cancellationToken);
        Flow.DeliverFromClient(Quic.FinishedDatagram());
    }

    /// <summary>Sends <paramref name="bytes"/> on <paramref name="streamId"/> after what was sent on it already.</summary>
    public void Send(ulong streamId, byte[] bytes, bool fin = false)
    {
        var offset = sendOffsets.GetValueOrDefault(streamId);
        sendOffsets[streamId] = offset + (ulong)bytes.Length;
        Flow.DeliverFromClient(Quic.SealOneRtt(new QuicStreamFrame(streamId, offset, bytes, fin)));
    }

    /// <summary>Sends <c>RESET_STREAM</c> on <paramref name="streamId"/> at what was sent on it.</summary>
    public void Reset(ulong streamId) =>
        Flow.DeliverFromClient(Quic.SealOneRtt(new QuicResetStreamFrame(streamId, 0x10c, sendOffsets.GetValueOrDefault(streamId))));

    public static byte[] Frame(ulong type, byte[] payload) => Http3FrameWriter.Frame(type, payload);

    public byte[] Headers(params (string Name, string Value)[] fields) =>
        Frame(Http3FrameType.Headers, Encoder.EncodeFieldSection(fields.Select(field => new QpackFieldLine(field.Name, field.Value))));

    /// <summary>A <c>HEADERS</c> frame for a request of <paramref name="method"/> to <paramref name="path"/>, with any extra fields.</summary>
    public byte[] Request(string method, string path, params (string Name, string Value)[] extra) =>
        Headers([(":method", method), (":scheme", "https"), (":authority", "127.0.0.1:18050"), (":path", path), .. extra]);

    /// <summary>Opens what the server has sent until <paramref name="condition"/> holds.</summary>
    public async Task UntilAsync(Func<bool> condition, CancellationToken cancellationToken)
    {
        using var patience = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        patience.CancelAfter(Patience);
        while (true)
        {
            TakeSent();
            if (condition())
            {
                return;
            }

            await Task.Delay(1, patience.Token);
        }
    }

    /// <summary>Waits for the server's run to end, then opens everything it sent.</summary>
    public async Task FinishAsync(CancellationToken cancellationToken)
    {
        await Serving!.WaitAsync(Patience, cancellationToken);
        TakeSent();
    }

    public IEnumerable<T> OneRttFrames<T>() where T : QuicFrame => Quic.FramesReceived[QuicPacketType.OneRtt].OfType<T>();

    /// <summary>The application <c>CONNECTION_CLOSE</c>'s error code, or null when none came.</summary>
    public ulong? CloseCode => OneRttFrames<QuicConnectionCloseFrame>().SingleOrDefault()?.ErrorCode;

    /// <summary>The server's <c>RESET_STREAM</c> code on <paramref name="streamId"/>, or null.</summary>
    public ulong? ResetCode(ulong streamId) => OneRttFrames<QuicResetStreamFrame>().SingleOrDefault(frame => frame.StreamId == streamId)?.ApplicationErrorCode;

    /// <summary>The server's <c>STOP_SENDING</c> code on <paramref name="streamId"/>, or null.</summary>
    public ulong? StopSendingCode(ulong streamId) => OneRttFrames<QuicStopSendingFrame>().SingleOrDefault(frame => frame.StreamId == streamId)?.ApplicationErrorCode;

    /// <summary>The bytes the server sent on <paramref name="streamId"/>, by offset, and whether its FIN came.</summary>
    public (byte[] Data, bool Fin) StreamSent(ulong streamId)
    {
        var frames = OneRttFrames<QuicStreamFrame>().Where(frame => frame.StreamId == streamId).ToList();
        var data = new byte[frames.Select(frame => (int)frame.Offset + frame.Data.Length).DefaultIfEmpty(0).Max()];
        foreach (var frame in frames)
        {
            frame.Data.CopyTo(data.AsMemory((int)frame.Offset));
        }

        return (data, frames.Any(frame => frame.Fin));
    }

    /// <summary>Whether the server has ended <paramref name="streamId"/> with FIN or a reset.</summary>
    public bool HasEnded(ulong streamId) => StreamSent(streamId).Fin || ResetCode(streamId) is not null;

    /// <summary>The server's answer on a request stream: each field section decoded, and the body.</summary>
    public Http3TestResponse Response(ulong streamId)
    {
        var (data, fin) = StreamSent(streamId);
        var response = new Http3TestResponse { Fin = fin };
        var decoder = new QpackDecoder(0, int.MaxValue);
        foreach (var (type, payload) in ReadFrames(data))
        {
            if (type == Http3FrameType.Headers)
            {
                response.FieldSections.Add(decoder.DecodeFieldSection((long)streamId, payload).FieldLines);
            }
            else
            {
                Assert.AreEqual(Http3FrameType.Data, type);
                response.Body.AddRange(payload);
            }
        }

        return response;
    }

    /// <summary>The frames in <paramref name="bytes"/>, in order.</summary>
    public static List<(ulong Type, byte[] Payload)> ReadFrames(ReadOnlySpan<byte> bytes)
    {
        var frames = new List<(ulong, byte[])>();
        while (!bytes.IsEmpty)
        {
            QuicVariableLengthInteger.TryRead(bytes, out var type, out var typeLength);
            QuicVariableLengthInteger.TryRead(bytes[typeLength..], out var length, out var lengthLength);
            var start = typeLength + lengthLength;
            frames.Add((type, bytes.Slice(start, (int)length).ToArray()));
            bytes = bytes[(start + (int)length)..];
        }

        return frames;
    }

    public void Dispose()
    {
        Quic.Dispose();
        key.Dispose();
        Exchange.Dispose();
    }

    private static byte[] SelfSigned(RSA key)
    {
        var request = new System.Security.Cryptography.X509Certificates.CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var now = DateTimeOffset.UtcNow;
        using var certificate = request.CreateSelfSigned(now.AddDays(-1), now.AddDays(1));
        return certificate.Export(System.Security.Cryptography.X509Certificates.X509ContentType.Cert);
    }

    private static List<byte[]> ReadRecordedInitials()
    {
        var bytes = RecordedFixture.ReadBytes("h3-client-initial", "static-8.21.0-win.datagrams.bin");
        var datagrams = new List<byte[]>();
        for (var offset = 0; offset < bytes.Length;)
        {
            var length = (bytes[offset] << 8) | bytes[offset + 1];
            datagrams.Add(bytes[(offset + 2)..(offset + 2 + length)]);
            offset += 2 + length;
        }

        return datagrams;
    }

    private void TakeSent()
    {
        var sent = Flow.SentDatagrams;
        Quic.Receive(sent.Skip(taken));
        taken = sent.Count;
    }
}
