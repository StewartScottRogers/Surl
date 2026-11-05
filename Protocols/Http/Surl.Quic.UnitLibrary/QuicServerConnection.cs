using System.Buffers.Binary;
using System.Security.Cryptography;
using Surl.Tls13;

namespace Surl.Quic;

/// <summary>
/// The server side of one QUIC version 1 connection (RFC 9000, RFC 9001): it takes the client's
/// datagrams and returns the server's, and does no I/O itself. It answers a first Initial
/// datagram of at least 1200 bytes, carries the TLS 1.3 handshake of <see cref="TlsServerHandshake"/>
/// in <c>CRYPTO</c> frames at the Initial and Handshake levels, sends its transport parameters,
/// acknowledges every packet number space, confirms the handshake with <c>HANDSHAKE_DONE</c>, and
/// then carries the client's and its own streams (<see cref="QuicStream"/>) in 1-RTT packets with
/// RFC 9000's flow control, granting credit as its caller reads (ADR-0096).
/// </summary>
/// <remarks>
/// Until the client's address is validated - a Handshake packet from it opens - the server sends
/// at most three times the bytes it has received (RFC 9000 section 8.1). Initial keys are
/// discarded when the first Handshake packet opens, Handshake keys when the handshake completes
/// (RFC 9001 section 4.9). An error ends the connection with <c>CONNECTION_CLOSE</c> carrying its
/// RFC 9000 code; the idle timeout ends it silently. Time comes from the injected
/// <see cref="TimeProvider"/>: call <see cref="HandleTimeout"/> at <see cref="NextDeadline"/>.
/// </remarks>
public sealed partial class QuicServerConnection : IDisposable
{
    /// <summary>The largest datagram the server sends, and the smallest that may carry a client's first Initial (RFC 9000 section 14).</summary>
    public const int MaxDatagramSize = 1200;

    /// <summary>The length of the connection ID the server chooses for itself (ADR-0082 decision 4).</summary>
    public const int ServerConnectionIdLength = 8;

    /// <summary>The most of the client's connection IDs the server keeps active: it sends no <c>active_connection_id_limit</c>, so the limit is that parameter's default, 2 (RFC 9000 section 18.2).</summary>
    public const ulong ActiveConnectionIdLimit = QuicTransportParameters.MinActiveConnectionIdLimit;

    /// <summary>The shortest Destination Connection ID a client's first Initial may carry (RFC 9000 section 7.2).</summary>
    public const int MinClientDestinationConnectionIdLength = 8;

    /// <summary>The longest frame <see cref="SendApplicationFrames"/> accepts: what fits in a 1-RTT packet of a 1200-byte datagram whatever the client's connection ID.</summary>
    public const int MaxApplicationFrameLength = MaxDatagramSize - 1 - 20 - 4 - QuicPacketProtection.AeadTagLength;

    private const byte VersionNegotiationUnusedBits = 0x40;

    private readonly QuicServerConnectionOptions options;
    private readonly TimeProvider timeProvider;
    private readonly RandomNumberGenerator random;
    private readonly QuicPacketNumberSpace initial = new(QuicPacketType.Initial, TlsEncryptionLevel.Initial);
    private readonly QuicPacketNumberSpace handshake = new(QuicPacketType.Handshake, TlsEncryptionLevel.Handshake);
    private readonly QuicPacketNumberSpace application = new(QuicPacketType.OneRtt, TlsEncryptionLevel.Application);
    private readonly QuicLossDetection recovery;
    private TlsServerHandshake? tls;
    private byte[] originalDestinationConnectionId = [];
    private byte[] clientConnectionId = [];
    private QuicPeerConnectionIds? clientConnectionIds;
    private byte[] serverConnectionId = [];
    private long bytesReceived;
    private long bytesSent;
    private bool addressValidated;
    private DateTimeOffset lastActivity;
    private bool ackElicitingSentSinceLastReceive;
    private TimeSpan? idleTimeout;
    private DateTimeOffset? handshakeDeadline;
    private QuicStreams? streams;
    private QuicKeyPhase? oneRttKeyPhase;
    private bool probePending;

    /// <summary>Creates a connection that waits for the client's first Initial.</summary>
    /// <param name="options">What the server serves.</param>
    /// <param name="timeProvider">The clock timeouts are measured with.</param>
    /// <param name="random">The source of the server's connection ID and its TLS randomness.</param>
    public QuicServerConnection(QuicServerConnectionOptions options, TimeProvider timeProvider, RandomNumberGenerator random)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(random);
        this.options = options;
        this.timeProvider = timeProvider;
        this.random = random;
        recovery = new QuicLossDetection([initial, handshake, application]);
    }

    /// <summary>Whether a client's first Initial has started the connection.</summary>
    public bool IsStarted => tls is not null;

    /// <summary>Whether the TLS handshake is complete, and so confirmed (RFC 9001 section 4.1.2).</summary>
    public bool IsHandshakeComplete { get; private set; }

    /// <summary>Whether the connection has ended; it then takes and sends nothing more.</summary>
    public bool IsClosed { get; private set; }

    /// <summary>The ALPN protocol agreed, or null before it is.</summary>
    public string? ApplicationProtocol => tls?.ApplicationProtocol;

    /// <summary>The TLS 1.3 cipher suite agreed, or null before it is.</summary>
    public TlsCipherSuite? CipherSuite => tls?.CipherSuite;

    /// <summary>The client's transport parameters, or null before its ClientHello is accepted.</summary>
    public QuicTransportParameters? PeerTransportParameters { get; private set; }

    /// <summary>The server's own connection ID, or empty before the connection starts.</summary>
    public ReadOnlyMemory<byte> ServerConnectionId => serverConnectionId;

    /// <summary>When <see cref="HandleTimeout"/> next has something to do, or null when nothing is timed.</summary>
    public DateTimeOffset? NextDeadline => IsClosed ? null : Earliest(Earliest(IdleDeadline, handshakeDeadline), LossDetectionDeadline);

    private DateTimeOffset? IdleDeadline => tls is not null && idleTimeout is { } timeout ? lastActivity + timeout : null;

    /// <summary>Whether the server holds keys for <paramref name="level"/>: installed and not yet discarded.</summary>
    /// <param name="level">The encryption level.</param>
    /// <returns>True while the keys are held.</returns>
    public bool HasKeys(TlsEncryptionLevel level) => SpaceOf(level).HasKeys;

    /// <summary>Takes one datagram from the client.</summary>
    /// <param name="datagram">The datagram's bytes: one packet or several coalesced.</param>
    /// <returns>The datagrams to send back and the streams the client's frames reached.</returns>
    public QuicConnectionOutput ReceiveDatagram(ReadOnlyMemory<byte> datagram)
    {
        if (IsClosed)
        {
            return QuicConnectionOutput.Empty;
        }

        if (tls is null)
        {
            var versionNegotiation = TryStart(datagram);
            if (tls is null)
            {
                return versionNegotiation is null ? QuicConnectionOutput.Empty : new QuicConnectionOutput([versionNegotiation], []);
            }
        }

        bytesReceived += datagram.Length;
        ReceivePacketsClosingOnError(datagram);
        return new QuicConnectionOutput(Flush(), TakeUpdatedStreams()) { ReceivedDatagramFrames = TakeReceivedDatagramFrames() };
    }

    /// <summary>
    /// Acts on the timer that is due: past the idle timeout the connection is discarded
    /// silently; past the handshake timeout it ends with <c>CONNECTION_CLOSE</c> <c>NO_ERROR</c>;
    /// at the loss detection timer the packets the time threshold condemns are declared lost and
    /// their frames sent again, or at the probe timeout a probe is sent (RFC 9002 section 6).
    /// </summary>
    /// <returns>The datagrams to send.</returns>
    public QuicConnectionOutput HandleTimeout()
    {
        var now = timeProvider.GetUtcNow();
        if (NextDeadline is not { } deadline || deadline > now)
        {
            return QuicConnectionOutput.Empty;
        }

        if (IdleDeadline <= now)
        {
            Discard();
            return QuicConnectionOutput.Empty;
        }

        if (handshakeDeadline <= now)
        {
            CloseWithTransportError((ulong)QuicTransportErrorCode.NoError, 0);
        }
        else
        {
            HandleLossDetectionTimeout();
        }

        return new QuicConnectionOutput(Flush(), []);
    }

    /// <summary>Seals frames in 1-RTT packets.</summary>
    /// <param name="frames">Frames a 1-RTT packet may carry, each at most <see cref="MaxApplicationFrameLength"/> bytes.</param>
    /// <returns>The datagrams to send.</returns>
    /// <exception cref="InvalidOperationException">The handshake is not complete, or the connection has ended.</exception>
    /// <exception cref="ArgumentException">A frame is longer than <see cref="MaxApplicationFrameLength"/>.</exception>
    public QuicConnectionOutput SendApplicationFrames(IEnumerable<QuicFrame> frames)
    {
        ArgumentNullException.ThrowIfNull(frames);
        RequireOneRtt();
        foreach (var frame in frames)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(frame.ToBytes().Length, MaxApplicationFrameLength, nameof(frames));
            application.FramesToSend.Add(frame);
        }

        return new QuicConnectionOutput(Flush(), []);
    }

    /// <summary>
    /// Opens the server's next unidirectional stream (IDs 3, 7, 11 and on), or returns null and
    /// sends <c>STREAMS_BLOCKED</c> with the next datagrams when the client's <c>MAX_STREAMS</c>
    /// allows no more; a later <c>MAX_STREAMS</c> lets it try again.
    /// </summary>
    /// <returns>The stream, or null.</returns>
    /// <exception cref="InvalidOperationException">The handshake is not complete, or the connection has ended.</exception>
    public QuicStream? OpenUnidirectionalStream()
    {
        RequireOneRtt();
        return streams!.OpenUnidirectional();
    }

    /// <summary>
    /// Seals what the streams have waiting: the data the client's credit allows, <c>FIN</c>,
    /// <c>RESET_STREAM</c> and <c>STOP_SENDING</c>, the credit granted as data is read and streams
    /// close, and <c>DATA_BLOCKED</c> or <c>STREAM_DATA_BLOCKED</c> when the client's credit holds
    /// data back. <see cref="ReceiveDatagram"/> does the same with its answer.
    /// </summary>
    /// <returns>The datagrams to send.</returns>
    /// <exception cref="InvalidOperationException">The handshake is not complete, or the connection has ended.</exception>
    public QuicConnectionOutput Transmit()
    {
        RequireOneRtt();
        return new QuicConnectionOutput(Flush(), []);
    }

    /// <summary>
    /// Ends the connection with an application error - for HTTP/3's shutdown, <c>H3_NO_ERROR</c>.
    /// After the handshake it is a <c>CONNECTION_CLOSE</c> of type <c>0x1d</c> in a 1-RTT packet;
    /// before, a <c>CONNECTION_CLOSE</c> of type <c>0x1c</c> carrying <c>APPLICATION_ERROR</c> at
    /// the Initial and Handshake levels (RFC 9000 section 10.2.3).
    /// </summary>
    /// <param name="applicationErrorCode">The application's error code.</param>
    /// <returns>The datagrams to send.</returns>
    public QuicConnectionOutput Close(ulong applicationErrorCode)
    {
        if (IsClosed)
        {
            return QuicConnectionOutput.Empty;
        }

        if (IsHandshakeComplete)
        {
            SendConnectionClose(new QuicConnectionCloseFrame(true, applicationErrorCode, 0, ReadOnlyMemory<byte>.Empty));
        }
        else
        {
            CloseWithTransportError((ulong)QuicTransportErrorCode.ApplicationError, 0);
        }

        return new QuicConnectionOutput(Flush(), []);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        tls?.Dispose();
        initial.Dispose();
        handshake.Dispose();
        application.Dispose();
        oneRttKeyPhase?.Dispose();
    }

    /// <summary>The streams the client's frames reached since the last call; none before the client's transport parameters are accepted.</summary>
    private IReadOnlyList<QuicStream> TakeUpdatedStreams() => streams is null ? [] : streams.TakeUpdated();

    /// <summary>Acts on the datagram's packets; a transport error or a TLS alert ends the connection with <c>CONNECTION_CLOSE</c>.</summary>
    private void ReceivePacketsClosingOnError(ReadOnlyMemory<byte> datagram)
    {
        try
        {
            ReceivePackets(datagram);
        }
        catch (QuicTransportException exception)
        {
            CloseWithTransportError((ulong)exception.ErrorCode, exception.FrameType ?? 0);
        }
        catch (TlsAlertException exception)
        {
            CloseWithTransportError((ulong)QuicTransportErrorCode.CryptoErrorBase + (ulong)exception.Alert, QuicFrameTypes.Crypto);
        }
    }

    private void RequireOneRtt()
    {
        if (!IsHandshakeComplete || IsClosed)
        {
            throw new InvalidOperationException("1-RTT frames and streams are sent only once the handshake is complete and before the connection ends.");
        }
    }

    private static DateTimeOffset? Earliest(DateTimeOffset? first, DateTimeOffset? second)
    {
        var earliest = Min(first ?? DateTimeOffset.MaxValue, second ?? DateTimeOffset.MaxValue);
        return earliest == DateTimeOffset.MaxValue ? null : earliest;
    }

    private static DateTimeOffset Min(DateTimeOffset first, DateTimeOffset second) => second < first ? second : first;

    /// <summary>The smaller of the two sides' idle timeouts, where 0 or absent means none (RFC 9000 section 10.1); a client's is capped at 2^31-1 milliseconds.</summary>
    private static TimeSpan? EffectiveIdleTimeout(TimeSpan ours, ulong? peerMilliseconds)
    {
        var own = ours > TimeSpan.Zero ? ours : TimeSpan.MaxValue;
        var peerValue = Math.Min(peerMilliseconds ?? 0, int.MaxValue);
        var peer = peerValue > 0 ? TimeSpan.FromMilliseconds(peerValue) : TimeSpan.MaxValue;
        var smaller = peer < own ? peer : own;
        return smaller == TimeSpan.MaxValue ? null : smaller;
    }

    private QuicPacketNumberSpace SpaceOf(TlsEncryptionLevel level) => level switch
    {
        TlsEncryptionLevel.Initial => initial,
        TlsEncryptionLevel.Handshake => handshake,
        _ => application,
    };

    /// <summary>Starts the connection on a client's first Initial, or returns a Version Negotiation packet for another version.</summary>
    private byte[]? TryStart(ReadOnlyMemory<byte> datagram)
    {
        if (datagram.Length < MaxDatagramSize || (datagram.Span[0] & QuicPacketHeaderBits.HeaderForm) == 0)
        {
            return null;
        }

        try
        {
            if (BinaryPrimitives.ReadUInt32BigEndian(datagram.Span[1..]) != QuicVersions.Version1)
            {
                return AnswerOtherVersion(datagram);
            }

            StartFromInitial(datagram);
        }
        catch (QuicTransportException)
        {
            // A header that cannot be read starts nothing; the datagram is dropped.
        }

        return null;
    }

    private static byte[]? AnswerOtherVersion(ReadOnlyMemory<byte> datagram) =>
        QuicPacketReader.Read(datagram, 0, out _) is QuicUnsupportedVersionPacket packet
            ? new QuicVersionNegotiationPacket(packet.SourceConnectionId, packet.DestinationConnectionId, [QuicVersions.Version1], VersionNegotiationUnusedBits).ToBytes()
            : null;

    private void StartFromInitial(ReadOnlyMemory<byte> datagram)
    {
        var reader = new QuicReader(datagram, QuicTransportErrorCode.ProtocolViolation);
        var firstByte = reader.ReadByte();
        reader.ReadUInt32();
        var destination = QuicPacketReader.ReadConnectionId(reader, QuicVersions.Version1);
        var source = QuicPacketReader.ReadConnectionId(reader, QuicVersions.Version1);
        var isInitial = (firstByte & (QuicPacketHeaderBits.FixedBit | QuicPacketHeaderBits.LongPacketTypeMask)) == QuicPacketHeaderBits.FixedBit;
        if (!isInitial || destination.Length < MinClientDestinationConnectionIdLength)
        {
            return;
        }

        originalDestinationConnectionId = destination.ToArray();
        clientConnectionId = source.ToArray();
        clientConnectionIds = new QuicPeerConnectionIds(clientConnectionId, ActiveConnectionIdLimit);
        serverConnectionId = new byte[ServerConnectionIdLength];
        random.GetBytes(serverConnectionId);
        var (client, server) = QuicInitialSecrets.DerivePacketProtection(destination.Span);
        initial.InstallKeys(client, server);
        idleTimeout = EffectiveIdleTimeout(options.IdleTimeout, null);
        lastActivity = timeProvider.GetUtcNow();
        handshakeDeadline = lastActivity + options.HandshakeTimeout;
        tls = new TlsServerHandshake(
            new TlsServerHandshakeOptions
            {
                CertificateChain = options.CertificateChain,
                CertificateKey = options.CertificateKey,
                ApplicationProtocols = options.ApplicationProtocols,
                QuicTransportParameters = ServerTransportParameters().ToBytes(),
            },
            random);
    }

    private QuicTransportParameters ServerTransportParameters() => new()
    {
        OriginalDestinationConnectionId = originalDestinationConnectionId,
        MaxIdleTimeout = options.IdleTimeout > TimeSpan.Zero ? (ulong)options.IdleTimeout.TotalMilliseconds : null,
        InitialMaxData = options.InitialMaxData,
        InitialMaxStreamDataBidiRemote = options.InitialMaxStreamDataBidiRemote,
        InitialMaxStreamDataUni = options.InitialMaxStreamDataUni,
        InitialMaxStreamsBidi = options.InitialMaxStreamsBidi,
        InitialMaxStreamsUni = options.InitialMaxStreamsUni,
        DisableActiveMigration = true,
        InitialSourceConnectionId = serverConnectionId,
        VersionInformation = new QuicVersionInformation(QuicVersions.Version1, [QuicVersions.Version1]),
        ResetStreamAt = options.ResetStreamAt,
        MaxDatagramFrameSize = options.MaxDatagramFrameSize,
    };

    /// <summary>Ends the connection with a transport error, sent as RFC 9000 section 10.2.3 says.</summary>
    private void CloseWithTransportError(ulong errorCode, ulong frameType) =>
        SendConnectionClose(new QuicConnectionCloseFrame(false, errorCode, frameType, ReadOnlyMemory<byte>.Empty));

    /// <summary>
    /// Queues <c>CONNECTION_CLOSE</c> in place of everything else: in a 1-RTT packet once the
    /// handshake is complete, before that at every level of Initial and Handshake the server has
    /// keys for, since it cannot know which the client can open. No reason phrase is sent.
    /// </summary>
    private void SendConnectionClose(QuicConnectionCloseFrame frame)
    {
        foreach (var space in (QuicPacketNumberSpace[])[initial, handshake, application])
        {
            space.ClearWhatIsToSend();
        }

        QuicPacketNumberSpace[] levels = IsHandshakeComplete ? [application] : [initial, handshake];
        foreach (var space in levels)
        {
            space.FramesToSend.Add(frame);
        }

        handshakeDeadline = null;
        IsClosed = true;
    }

    /// <summary>Ends the connection with nothing sent: the idle timeout, or the client's own <c>CONNECTION_CLOSE</c> (draining).</summary>
    private void Discard()
    {
        foreach (var space in (QuicPacketNumberSpace[])[initial, handshake, application])
        {
            space.ClearWhatIsToSend();
        }

        IsClosed = true;
    }
}
