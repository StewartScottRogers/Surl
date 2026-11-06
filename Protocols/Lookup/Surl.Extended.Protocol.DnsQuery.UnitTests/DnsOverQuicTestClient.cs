using System.Security.Cryptography;
using Surl.Protocol.Abstractions;
using Surl.Quic;

namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>
/// A DNS over QUIC client for tests - the in-memory QUIC test connection - on the far side of an
/// <see cref="InMemoryDatagramFlow"/>. Its QUIC connection opens with the Initials pinned upstream
/// curl sent (Fixtures/h3-client-initial), with the ALPN list edited from <c>h3</c>, <c>h3-29</c> to <c>doq</c>, <c>h3-29</c>
/// (<see cref="OfferDoq"/>), since upstream curl has no DNS over QUIC; after the handshake it
/// sends streams written by hand from RFC 9250 section 4.2.
/// </summary>
internal sealed class DnsOverQuicTestClient : IDisposable
{
    /// <summary>
    /// The quic_transport_parameters the recorded build sends after initial_source_connection_id:
    /// bidi_local, bidi_remote, uni, max_data, streams_bidi, streams_uni, then version_information
    /// (the last ten bytes).
    /// </summary>
    private const string RecordedParametersAfterSourceConnectionId =
        "0504800080000604800080000704be8000000404be800000080480040000090480040000" + "110800000001" + "00000001";

    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);
    private readonly Dictionary<ulong, ulong> sendOffsets = [];
    private int taken;

    public DnsOverQuicTestClient()
    {
        Quic = UnitTestQuicClient.FromRecording(ReadRecordedInitials(), OfferDoq);
        var datagrams = Quic.InitialDatagrams.Select(datagram => (ReadOnlyMemory<byte>)datagram).ToList();
        Flow = new InMemoryDatagramFlow(datagrams[0], datagrams.Skip(1));
    }

    public UnitTestQuicClient Quic { get; }

    public InMemoryDatagramFlow Flow { get; }

    /// <summary>The server's key, which <see cref="SelfSignedCertificate"/> certifies.</summary>
    public RSA Key { get; } = RSA.Create(2048);

    /// <summary>The flow server's run, once a server was started.</summary>
    public Task? Serving { get; private set; }

    /// <summary>The application <c>CONNECTION_CLOSE</c>'s error code, or null when none came.</summary>
    public ulong? CloseCode => OneRttFrames<QuicConnectionCloseFrame>().SingleOrDefault(frame => frame.IsApplicationError)?.ErrorCode;

    /// <summary>The transport <c>CONNECTION_CLOSE</c>'s error code, or null when none came.</summary>
    public ulong? TransportCloseCode => Quic.FramesReceived.Values.SelectMany(frames => frames).OfType<QuicConnectionCloseFrame>().SingleOrDefault(frame => !frame.IsApplicationError)?.ErrorCode;

    /// <summary>
    /// Replaces the ALPN list's <c>h3</c> with <c>doq</c> (keeping <c>h3-29</c>, which the server does not offer), one byte longer, and keeps the
    /// ClientHello's length by putting the transport parameters' version_information (optional,
    /// RFC 9368 section 3) in a reserved parameter one byte shorter; both extensions' lengths follow.
    /// </summary>
    public static byte[] OfferDoq(byte[] hello)
    {
        byte[] h3 = Convert.FromHexString("0010000B00090268330568332D3239"); // h3, h3-29
        byte[] doq = Convert.FromHexString("0010000C000A03646F710568332D3239"); // doq, h3-29
        var parameters = Convert.FromHexString(RecordedParametersAfterSourceConnectionId);
        byte[] shorter = [.. parameters[..^10], 0x1b, 0x07, .. new byte[7]];

        var parametersAt = hello.AsSpan().IndexOf(parameters);
        Assert.IsGreaterThanOrEqualTo(0, parametersAt, "The ClientHello does not hold the recorded transport parameters.");
        var edited = hello.ToArray();
        var extensionAt = TransportParametersExtensionAt(edited, parametersAt, parameters.Length);
        var extensionLength = (edited[extensionAt + 2] << 8) | edited[extensionAt + 3];
        edited[extensionAt + 2] = (byte)((extensionLength - 1) >> 8);
        edited[extensionAt + 3] = (byte)(extensionLength - 1);
        edited = Splice(edited, parameters, shorter);
        edited = Splice(edited, h3, doq);
        Assert.HasCount(hello.Length, edited);
        return edited;
    }

    /// <summary>Starts <paramref name="application"/> on a <see cref="QuicDatagramFlowServer"/> offering <c>doq</c> with ADR-0092's transport parameters.</summary>
    public void Serve(IQuicApplicationProtocol application, CancellationToken cancellationToken, ulong maxStreamsUni = 0)
    {
        var options = new QuicServerConnectionOptions
        {
            CertificateChain = [SelfSignedCertificate()],
            CertificateKey = Key,
            ApplicationProtocols = ["doq"],
            InitialMaxStreamsBidi = 100,
            InitialMaxStreamsUni = maxStreamsUni,
        };
        Watch(new QuicDatagramFlowServer(options, application, TimeProvider.System, RandomNumberGenerator.Create()).ServeAsync(Flow, cancellationToken));
    }

    /// <summary>Keeps <paramref name="serving"/> as the run <see cref="FinishAsync"/> waits for.</summary>
    public void Watch(Task serving) => Serving = serving;

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

    /// <summary>Sends <paramref name="message"/> on <paramref name="streamId"/> after its two-byte length (RFC 9250 section 4.2).</summary>
    public void SendQuery(ulong streamId, byte[] message, bool fin = true) =>
        Send(streamId, [(byte)(message.Length >> 8), (byte)message.Length, .. message], fin);

    /// <summary>Sends <c>RESET_STREAM</c> with <c>DOQ_REQUEST_CANCELLED</c> on <paramref name="streamId"/> at what was sent on it.</summary>
    public void Reset(ulong streamId) =>
        Flow.DeliverFromClient(Quic.SealOneRtt(new QuicResetStreamFrame(streamId, (ulong)DnsOverQuicErrorCode.RequestCancelled, sendOffsets.GetValueOrDefault(streamId))));

    /// <summary>Ends the connection from the client's side with <c>CONNECTION_CLOSE</c> <c>DOQ_NO_ERROR</c>.</summary>
    public void Close() =>
        Flow.DeliverFromClient(Quic.SealOneRtt(new QuicConnectionCloseFrame(true, 0, 0, ReadOnlyMemory<byte>.Empty)));

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

    /// <summary>Whether the server has ended <paramref name="streamId"/> with FIN.</summary>
    public bool HasEnded(ulong streamId) => StreamSent(streamId).Fin;

    /// <summary>The length-prefixed messages the server sent on <paramref name="streamId"/>, read whole.</summary>
    public List<DnsMessage> Answers(ulong streamId)
    {
        var (data, _) = StreamSent(streamId);
        var messages = new List<DnsMessage>();
        for (var offset = 0; offset < data.Length;)
        {
            var length = (data[offset] << 8) | data[offset + 1];
            messages.Add(DnsTestQueries.ReadWhole(data[(offset + 2)..(offset + 2 + length)]));
            offset += 2 + length;
        }

        return messages;
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
        Quic.Dispose();
        Key.Dispose();
    }

    // The quic_transport_parameters extension (type 0x0039) whose body holds the parameters at parametersAt.
    private static int TransportParametersExtensionAt(byte[] hello, int parametersAt, int parametersLength)
    {
        for (var index = parametersAt - 4; index >= 0; index--)
        {
            var length = (hello[index + 2] << 8) | hello[index + 3];
            if (hello[index] == 0x00 && hello[index + 1] == 0x39 && index + 4 + length >= parametersAt + parametersLength)
            {
                return index;
            }
        }

        throw new AssertFailedException("The ClientHello has no quic_transport_parameters extension.");
    }

    private static byte[] Splice(byte[] bytes, byte[] from, byte[] to)
    {
        var index = bytes.AsSpan().IndexOf(from);
        Assert.IsGreaterThanOrEqualTo(0, index, $"The ClientHello does not hold {Convert.ToHexString(from)}");
        return [.. bytes[..index], .. to, .. bytes[(index + from.Length)..]];
    }

    private static List<byte[]> ReadRecordedInitials()
    {
        const string Name = "Fixtures/h3-client-initial/static-8.21.0-win.datagrams.bin";
        using var stream = typeof(DnsOverQuicTestClient).Assembly.GetManifestResourceStream(Name)
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
}
