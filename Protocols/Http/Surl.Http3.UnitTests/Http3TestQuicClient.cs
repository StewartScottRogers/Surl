using System.Security.Cryptography;
using Surl.Hpack;
using Surl.Protocol.Abstractions;
using Surl.Qpack;
using Surl.Quic;

namespace Surl.Http3;

/// <summary>
/// An HTTP/3 client for tests, on the far side of an <see cref="InMemoryDatagramFlow"/>: its QUIC
/// connection opens with the Initials pinned upstream curl sent (Fixtures/h3-client-initial), and
/// after the handshake it sends HTTP/3 frames written from RFC 9114 and QPACK field sections written
/// with <see cref="QpackEncoder"/>. Which server answers is the subclass's or the test's to start:
/// <see cref="Serve(IQuicApplicationProtocol, TimeProvider, CancellationToken)"/> rides any
/// <see cref="IQuicApplicationProtocol"/> on a <see cref="QuicDatagramFlowServer"/>.
/// </summary>
internal class Http3TestQuicClient : IDisposable
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

    /// <summary>Longer than any closing or draining state the tests reach: three probe timeouts from the initial 333 ms RTT are about 3 s.</summary>
    private static readonly TimeSpan ClosingStateBound = TimeSpan.FromMinutes(1);
    private readonly Dictionary<ulong, ulong> sendOffsets = [];
    private int taken;

    public Http3TestQuicClient(Func<byte[], byte[]>? editClientHello = null)
    {
        Quic = UnitTestQuicClient.FromRecording(ReadRecordedInitials(), editClientHello);
        var datagrams = Quic.InitialDatagrams.Select(datagram => (ReadOnlyMemory<byte>)datagram).ToList();
        Flow = new InMemoryDatagramFlow(datagrams[0], datagrams.Skip(1));
    }

    public UnitTestQuicClient Quic { get; }

    public InMemoryDatagramFlow Flow { get; }

    /// <summary>The server's key, which <see cref="SelfSignedCertificate"/> certifies.</summary>
    public RSA Key { get; } = RSA.Create(2048);

    /// <summary>The flow server's run, once a server was started.</summary>
    public Task? Serving { get; protected set; }

    public QpackEncoder Encoder { get; } = new(HpackStringCoding.Raw);

    /// <summary>
    /// Starts <paramref name="application"/> on a <see cref="QuicDatagramFlowServer"/> holding a
    /// certificate for <see cref="Key"/>, sending <c>max_datagram_frame_size</c> when
    /// <paramref name="maxDatagramFrameSize"/> names one.
    /// </summary>
    public Task Serve(IQuicApplicationProtocol application, TimeProvider time, CancellationToken cancellationToken, ulong? maxDatagramFrameSize = null)
    {
        var options = new QuicServerConnectionOptions
        {
            CertificateChain = [SelfSignedCertificate()],
            CertificateKey = Key,
            MaxDatagramFrameSize = maxDatagramFrameSize,
        };
        Clock = time as IMovableClock;
        Serving = new QuicDatagramFlowServer(options, new RecordingApplicationProtocol(this, application), time, RandomNumberGenerator.Create()).ServeAsync(Flow, cancellationToken);
        return Serving;
    }

    /// <summary>The connection the server handed its application, once it has.</summary>
    public QuicApplicationConnection? ApplicationConnection { get; private set; }

    /// <summary>The server's clock, when a test moves it: <see cref="FinishAsync"/> moves it past the closing or draining state.</summary>
    protected IMovableClock? Clock { get; set; }

    /// <summary>Completes the handshake, and opens the client's control stream with <paramref name="settings"/> and its two QPACK streams.</summary>
    /// <remarks>
    /// The server grants three unidirectional streams (ADR-0082 decision 4), so a test that opens
    /// another kind of stream passes <paramref name="openQpackStreams"/> false and uses stream 6 or 10.
    /// </remarks>
    public async Task OpenAsync(CancellationToken cancellationToken, byte[]? settings = null, bool openQpackStreams = true)
    {
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

    /// <summary>
    /// Waits for the server's run to end, then opens everything it sent. Once the connection has
    /// closed - the client has opened the server's <c>CONNECTION_CLOSE</c>, or the application was
    /// told the connection ended - it moves a <see cref="Clock"/> past the closing or draining state,
    /// which holds the run open until then (RFC 9000 section 10.2).
    /// </summary>
    public async Task FinishAsync(CancellationToken cancellationToken)
    {
        var serving = Serving!;
        await UntilAsync(() => serving.IsCompleted || HasClosed(), cancellationToken);
        await UntilAsync(
            () =>
            {
                Clock?.Advance(ClosingStateBound);
                return serving.IsCompleted;
            },
            cancellationToken);
        await serving;
        TakeSent();
    }

    /// <summary>Whether the connection has closed: the server's <c>CONNECTION_CLOSE</c> reached the client, or the application's side has ended.</summary>
    public bool HasClosed() =>
        ApplicationConnection?.Ended.IsCancellationRequested == true
        || Quic.FramesReceived.Values.Any(frames => frames.OfType<QuicConnectionCloseFrame>().Any());

    public IEnumerable<T> OneRttFrames<T>() where T : QuicFrame => Quic.FramesReceived[QuicPacketType.OneRtt].OfType<T>();

    /// <summary>The application <c>CONNECTION_CLOSE</c>'s error code, or null when none came.</summary>
    /// <remarks>The closing state may answer the client's later datagrams with the same frame again, so repeats count once.</remarks>
    public ulong? CloseCode => OneRttFrames<QuicConnectionCloseFrame>().Select(frame => (ulong?)frame.ErrorCode).Distinct().SingleOrDefault();

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

    /// <summary>A self-signed certificate for <see cref="Key"/>, DER.</summary>
    public byte[] SelfSignedCertificate()
    {
        var request = new System.Security.Cryptography.X509Certificates.CertificateRequest("CN=localhost", Key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var now = DateTimeOffset.UtcNow;
        using var certificate = request.CreateSelfSigned(now.AddDays(-1), now.AddDays(1));
        return certificate.Export(System.Security.Cryptography.X509Certificates.X509ContentType.Cert);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            Quic.Dispose();
            Key.Dispose();
        }
    }

    private static List<byte[]> ReadRecordedInitials()
    {
        const string Name = "Fixtures/h3-client-initial/static-8.21.0-win.datagrams.bin";
        using var stream = typeof(Http3TestQuicClient).Assembly.GetManifestResourceStream(Name)
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

    private void TakeSent()
    {
        var sent = Flow.SentDatagrams;
        Quic.Receive(sent.Skip(taken));
        taken = sent.Count;
    }

    /// <summary>Records the connection the server hands its application, then runs the test's application.</summary>
    private sealed class RecordingApplicationProtocol(Http3TestQuicClient client, IQuicApplicationProtocol inner) : IQuicApplicationProtocol
    {
        public Task ServeAsync(QuicApplicationConnection connection, CancellationToken cancellationToken)
        {
            client.ApplicationConnection = connection;
            return inner.ServeAsync(connection, cancellationToken);
        }
    }
}
