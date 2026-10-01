using Surl.Content;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Rtsp;

/// <summary>
/// The one RTSP session a connection may hold (ADR-0074 decision 5): its ID, its presentation,
/// its interleaved transport, whether it is playing, and where in the presentation's bytes the
/// next RTP packet starts.
/// </summary>
/// <remarks>
/// Every file is one stream whose RTP payloads are the file's bytes, in order: packets of
/// <see cref="PayloadBytes"/> bytes, the last one fewer and marked, an empty file one empty
/// marked packet. Sequence numbers and timestamps run on from their random starts across every
/// <c>PLAY</c> of the session, the timestamp advancing by each payload's length.
/// </remarks>
internal sealed class RtspSession
{
    /// <summary>
    /// The payload bytes of every RTP packet but a presentation's last.
    /// </summary>
    public const int PayloadBytes = 1400;

    /// <summary>
    /// How long a session that is not playing lives without a request naming it: RFC 2326's
    /// default of 60 seconds, which the <c>SETUP</c> answer announces.
    /// </summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    // How many payloads one read of the file fetches, so a file is not opened per packet.
    private const int PayloadsPerRead = 46;

    private readonly MemoryStream readAhead = new();
    private int readAheadOffset;
    private long position;
    private long length;
    private uint lastTimestamp;
    private uint sessionPackets;
    private uint sessionOctets;

    /// <summary>
    /// Creates a session in the <c>Ready</c> state.
    /// </summary>
    /// <param name="id">The session ID: 16 upper-case hexadecimal digits.</param>
    /// <param name="presentation">The presentation's mapping in the content store.</param>
    /// <param name="presentationPath">The presentation's request path, for notes.</param>
    /// <param name="transport">The negotiated interleaved transport.</param>
    /// <param name="ssrc">The synchronization source of every packet.</param>
    /// <param name="firstSequenceNumber">The first packet's sequence number.</param>
    /// <param name="firstTimestamp">The first packet's RTP timestamp.</param>
    /// <param name="now">When the session was set up, from which its timeout runs.</param>
    public RtspSession(string id, ContentPathMapping presentation, string presentationPath, RtspTransport transport, uint ssrc, ushort firstSequenceNumber, uint firstTimestamp, DateTimeOffset now)
    {
        Id = id;
        Presentation = presentation;
        PresentationPath = presentationPath;
        Transport = transport;
        Ssrc = ssrc;
        NextSequenceNumber = firstSequenceNumber;
        NextTimestamp = firstTimestamp;
        LastActivity = now;
    }

    public string Id { get; }

    public ContentPathMapping Presentation { get; }

    public string PresentationPath { get; }

    /// <summary>
    /// The transport; a <c>SETUP</c> of the session while it is ready re-negotiates it.
    /// </summary>
    public RtspTransport Transport { get; set; }

    public uint Ssrc { get; }

    public bool IsPlaying { get; private set; }

    public ushort NextSequenceNumber { get; private set; }

    public uint NextTimestamp { get; private set; }

    /// <summary>
    /// The packets sent since the latest <c>PLAY</c>.
    /// </summary>
    public int PacketsThisPlay { get; private set; }

    /// <summary>
    /// The payload bytes sent since the latest <c>PLAY</c>.
    /// </summary>
    public long BytesThisPlay { get; private set; }

    /// <summary>
    /// When a request last named the session, or its playing last stopped.
    /// </summary>
    public DateTimeOffset LastActivity { get; set; }

    /// <summary>
    /// Whether the session has ended by its <see cref="Timeout"/>: not playing, and no request
    /// for that long.
    /// </summary>
    /// <param name="now">The time now.</param>
    /// <returns><see langword="true"/> when the session has timed out.</returns>
    public bool HasTimedOut(DateTimeOffset now) => !IsPlaying && now - LastActivity >= Timeout;

    /// <summary>
    /// Starts playing: from the start of a presentation <paramref name="fileLength"/> bytes long,
    /// or, after a <c>PAUSE</c>, from the next packet of the one in progress.
    /// </summary>
    /// <param name="fileLength">The presentation's length now; ignored when resuming.</param>
    public void Play(long fileLength)
    {
        if (position == 0)
        {
            length = fileLength;
        }

        IsPlaying = true;
        PacketsThisPlay = 0;
        BytesThisPlay = 0;
    }

    /// <summary>
    /// Stops playing and keeps the position, so the next <c>PLAY</c> resumes.
    /// </summary>
    public void Pause() => IsPlaying = false;

    /// <summary>
    /// Writes the next RTP packet, and after the presentation's last one the RTCP sender report
    /// and <c>BYE</c>, which leaves the session ready at the presentation's start.
    /// </summary>
    /// <param name="connection">Where the frames are written.</param>
    /// <param name="contentStore">Where the presentation's bytes are read.</param>
    /// <param name="context">The exchange: its clock and cancellation.</param>
    /// <returns>A task that completes when the frames are written.</returns>
    public async Task SendNextPacketAsync(IConnection connection, ContentStore contentStore, ExchangeContext context)
    {
        if (readAheadOffset == readAhead.Length)
        {
            await ReadAheadAsync(contentStore, context.CancellationToken);
        }

        var payloadLength = (int)Math.Min(PayloadBytes, readAhead.Length - readAheadOffset);
        var isLast = position + payloadLength >= length;
        var payload = readAhead.GetBuffer().AsSpan(readAheadOffset, payloadLength);
        var frame = RtspInterleavedFrame.Rtp(Transport.RtpChannel, NextSequenceNumber, NextTimestamp, Ssrc, isLast, payload);
        await connection.WriteAsync(frame, context.CancellationToken);

        lastTimestamp = NextTimestamp;
        NextSequenceNumber++;
        NextTimestamp = unchecked(NextTimestamp + (uint)payloadLength);
        sessionPackets = unchecked(sessionPackets + 1);
        sessionOctets = unchecked(sessionOctets + (uint)payloadLength);
        PacketsThisPlay++;
        BytesThisPlay += payloadLength;
        position += payloadLength;
        readAheadOffset += payloadLength;

        if (isLast)
        {
            await FinishPlayingAsync(connection, context);
        }
    }

    /// <summary>
    /// Lets go of the bytes read ahead when the session ends.
    /// </summary>
    public void End() => readAhead.Dispose();

    private async Task FinishPlayingAsync(IConnection connection, ExchangeContext context)
    {
        var now = context.TimeProvider.GetUtcNow();
        var report = RtspInterleavedFrame.SenderReportAndBye(Transport.RtcpChannel, Ssrc, now, lastTimestamp, sessionPackets, sessionOctets);
        await connection.WriteAsync(report, context.CancellationToken);

        IsPlaying = false;
        position = 0;
        readAhead.SetLength(0);
        readAheadOffset = 0;
        LastActivity = now;
    }

    // A file that shrank, went or cannot be read ends where its bytes ran out: the next packet
    // is then the last, empty when nothing more could be read.
    private async Task ReadAheadAsync(ContentStore contentStore, CancellationToken cancellationToken)
    {
        readAhead.SetLength(0);
        readAheadOffset = 0;
        if (position < length)
        {
            try
            {
                var range = ContentByteRange.Select(length, position, position + (PayloadBytes * PayloadsPerRead) - 1);
                await contentStore.CopyFileBytesAsync(Presentation, range, readAhead, cancellationToken);
            }
            catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
            {
                readAhead.SetLength(0);
            }
        }

        if (readAhead.Length == 0)
        {
            length = position;
        }
    }
}
