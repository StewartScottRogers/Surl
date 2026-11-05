using Surl.Tls13;

namespace Surl.Quic;

/// <summary>How the connection opens the client's packets and acts on their frames.</summary>
public sealed partial class QuicServerConnection
{
    /// <summary>NEW_TOKEN and HANDSHAKE_DONE are the server's to send; RETIRE_CONNECTION_ID would retire the one connection ID the server issued, which is in use.</summary>
    private static readonly HashSet<Type> FramesAClientMayNotSend = [typeof(QuicNewTokenFrame), typeof(QuicRetireConnectionIdFrame), typeof(QuicHandshakeDoneFrame)];

    /// <summary>Frames the connection takes without acting on them further: the server does not migrate, so the client's spare connection IDs and path responses go unused, and the client's DATA_BLOCKED and STREAMS_BLOCKED need no answer, since the server grants credit as data is read and streams close.</summary>
    private static readonly HashSet<Type> FramesNeedingNothingMore = [typeof(QuicPaddingFrame), typeof(QuicPingFrame), typeof(QuicNewConnectionIdFrame), typeof(QuicPathResponseFrame), typeof(QuicDataBlockedFrame), typeof(QuicStreamsBlockedFrame)];

    /// <summary>Reads each coalesced packet in turn; a packet whose header cannot be read drops the rest of the datagram.</summary>
    private void ReceivePackets(ReadOnlyMemory<byte> datagram)
    {
        var remaining = datagram;
        while (!remaining.IsEmpty && !IsClosed)
        {
            var packetLength = ReceivePacket(remaining);
            remaining = remaining[packetLength..];
        }
    }

    /// <summary>Opens and acts on the packet at the start of <paramref name="bytes"/> and returns how many bytes it took.</summary>
    private int ReceivePacket(ReadOnlyMemory<byte> bytes)
    {
        if (SpaceForPacket(bytes.Span) is not { } space)
        {
            return bytes.Length;
        }

        if (!CanOpen(space))
        {
            return SkippedPacketLength(bytes, space);
        }

        if (Unprotect(space, bytes) is not { } result)
        {
            return bytes.Length;
        }

        ProcessUnprotected(space, result);
        return result.PacketLength;
    }

    /// <summary>Acts on a packet that opened and is new to this connection; drops one that did not open; ends the connection on reserved bits set.</summary>
    private void ProcessUnprotected(QuicPacketNumberSpace space, QuicUnprotectResult result)
    {
        if (result.Status == QuicUnprotectStatus.ReservedBitsNotZero)
        {
            throw new QuicTransportException(QuicTransportErrorCode.ProtocolViolation, "A packet's reserved bits are not 0 (RFC 9000 section 17.2).");
        }

        if (result.Status == QuicUnprotectStatus.Unprotected && IsNewPacketForThisConnection(space, result))
        {
            ProcessPacket(space, result);
        }
    }

    /// <summary>Whether the server opens packets of the space now: it has keys, and 1-RTT packets wait for the handshake to complete (RFC 9001 section 5.7).</summary>
    private bool CanOpen(QuicPacketNumberSpace space) => space.HasKeys && (space != application || IsHandshakeComplete);

    /// <summary>Unprotects the packet, or returns null when its header cannot be located, which drops the rest of the datagram.</summary>
    private static QuicUnprotectResult? Unprotect(QuicPacketNumberSpace space, ReadOnlyMemory<byte> bytes)
    {
        try
        {
            return space.ReceiveProtection!.Unprotect(bytes, ServerConnectionIdLength, space.Received.Largest + 1 ?? 0);
        }
        catch (QuicTransportException)
        {
            return null;
        }
    }

    /// <summary>The packet number space a packet belongs to from its first byte and version; null for a packet the server never accepts (0-RTT, Retry, another version), which drops the rest of the datagram.</summary>
    private QuicPacketNumberSpace? SpaceForPacket(ReadOnlySpan<byte> bytes)
    {
        if ((bytes[0] & QuicPacketHeaderBits.HeaderForm) == 0)
        {
            return application;
        }

        if (bytes.Length < 5 || System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(bytes[1..]) != QuicVersions.Version1)
        {
            return null;
        }

        return ((bytes[0] & QuicPacketHeaderBits.LongPacketTypeMask) >> QuicPacketHeaderBits.LongPacketTypeShift) switch
        {
            (int)QuicPacketType.Initial => initial,
            (int)QuicPacketType.Handshake => handshake,
            _ => null,
        };
    }

    /// <summary>
    /// The length of a packet the server does not open: a long header's from its Length field, a
    /// short header's the rest of the datagram, and the rest of the datagram too when the header
    /// cannot be read.
    /// </summary>
    private static int SkippedPacketLength(ReadOnlyMemory<byte> bytes, QuicPacketNumberSpace space)
    {
        if (space.PacketType == QuicPacketType.OneRtt)
        {
            return bytes.Length;
        }

        try
        {
            var reader = new QuicReader(bytes, QuicTransportErrorCode.ProtocolViolation);
            reader.ReadBytes(5);
            QuicPacketReader.ReadConnectionId(reader, QuicVersions.Version1);
            QuicPacketReader.ReadConnectionId(reader, QuicVersions.Version1);
            if (space.PacketType == QuicPacketType.Initial)
            {
                reader.ReadLengthPrefixedBytes();
            }

            reader.ReadLengthPrefixedBytes();
            return reader.Position;
        }
        catch (QuicTransportException)
        {
            return bytes.Length;
        }
    }

    /// <summary>Whether a packet that opened is addressed to this connection and was not received before.</summary>
    private bool IsNewPacketForThisConnection(QuicPacketNumberSpace space, QuicUnprotectResult result)
    {
        var destination = result.Packet!.DestinationConnectionId.Span;
        var addressed = destination.SequenceEqual(serverConnectionId)
            || (space == initial && destination.SequenceEqual(originalDestinationConnectionId));
        return addressed && !space.Received.Contains(result.PacketNumber);
    }

    private void ProcessPacket(QuicPacketNumberSpace space, QuicUnprotectResult result)
    {
        space.Received.Add(result.PacketNumber);
        lastActivity = timeProvider.GetUtcNow();
        ackElicitingSentSinceLastReceive = false;
        if (space == handshake && !addressValidated)
        {
            addressValidated = true;
            DiscardSpace(initial);
        }

        var payload = result.Packet is QuicLongHeaderPacket longHeader ? longHeader.Payload : ((QuicShortHeaderPacket)result.Packet!).Payload;
        foreach (var frame in QuicFrameReader.Read(payload, space.PacketType))
        {
            space.AckPending |= IsAckEliciting(frame);
            ProcessFrame(space, frame);
            if (IsClosed)
            {
                return;
            }
        }
    }

    private static bool IsAckEliciting(QuicFrame frame) => frame is not (QuicAckFrame or QuicPaddingFrame or QuicConnectionCloseFrame);

    private void ProcessFrame(QuicPacketNumberSpace space, QuicFrame frame)
    {
        switch (frame)
        {
            case QuicAckFrame ack:
                ReceiveAck(space, ack);
                break;
            case QuicCryptoFrame crypto:
                ReceiveCrypto(space, crypto);
                break;
            case QuicConnectionCloseFrame:
                Discard();
                break;
            default:
                ProcessOtherFrame(frame);
                break;
        }
    }

    /// <summary>
    /// PATH_CHALLENGE is answered; NEW_TOKEN, RETIRE_CONNECTION_ID and HANDSHAKE_DONE are
    /// violations; PADDING, PING, NEW_CONNECTION_ID, PATH_RESPONSE, DATA_BLOCKED and STREAMS_BLOCKED
    /// need nothing more; every other frame is a stream or flow-control frame for <see cref="QuicStreams"/>.
    /// </summary>
    private void ProcessOtherFrame(QuicFrame frame)
    {
        if (frame is QuicPathChallengeFrame challenge)
        {
            application.FramesToSend.Add(new QuicPathResponseFrame(challenge.Data.ToArray()));
        }
        else if (FramesAClientMayNotSend.Contains(frame.GetType()))
        {
            throw new QuicTransportException(
                QuicTransportErrorCode.ProtocolViolation,
                "A client sends no NEW_TOKEN or HANDSHAKE_DONE, and the server's only connection ID is in use (RFC 9000 sections 19.7, 19.16 and 19.20).",
                frame.ToBytes()[0]);
        }
        else if (!FramesNeedingNothingMore.Contains(frame.GetType()))
        {
            streams!.Receive(frame);
        }
    }

    private void ReceiveAck(QuicPacketNumberSpace space, QuicAckFrame ack)
    {
        if (ack.LargestAcknowledged >= space.NextPacketNumber)
        {
            throw new QuicTransportException(
                QuicTransportErrorCode.ProtocolViolation,
                $"An ACK frame acknowledges packet {ack.LargestAcknowledged}, which was never sent (RFC 9000 section 13.1).",
                ack.EcnCounts is null ? QuicFrameTypes.Ack : QuicFrameTypes.AckWithEcn);
        }

        space.LargestAcknowledged = Math.Max(space.LargestAcknowledged ?? 0, ack.LargestAcknowledged);
        DetectLossOnAck(space, ack);
    }

    private void ReceiveCrypto(QuicPacketNumberSpace space, QuicCryptoFrame crypto)
    {
        var bytes = space.CryptoReceived.Add(crypto.Offset, crypto.Data.Span);
        if (bytes.Length == 0)
        {
            return;
        }

        var output = tls!.Receive(space.Level, bytes);
        AcceptClientTransportParameters();
        initial.CryptoToSend.AddRange(output.Initial);
        handshake.CryptoToSend.AddRange(output.Handshake);
        InstallKeys(handshake, output.HandshakeSecrets);
        InstallKeys(application, output.ApplicationSecrets);
        if (tls.IsComplete)
        {
            IsHandshakeComplete = true;
            handshakeDeadline = null;
            DiscardSpace(handshake);
            recovery.HandshakeConfirmed = true;
            application.FramesToSend.Add(new QuicHandshakeDoneFrame());
        }
    }

    private void InstallKeys(QuicPacketNumberSpace space, TlsTrafficSecrets? secrets)
    {
        if (secrets is not null)
        {
            var suite = tls!.CipherSuite!.Value;
            space.InstallKeys(
                QuicPacketProtection.FromTrafficSecret(suite, secrets.Client),
                QuicPacketProtection.FromTrafficSecret(suite, secrets.Server));
        }
    }

    /// <summary>Checks the client's transport parameters once its ClientHello is accepted (RFC 9000 sections 7.3 and 18.2, RFC 9368).</summary>
    private void AcceptClientTransportParameters()
    {
        if (PeerTransportParameters is not null || tls!.ClientHello is not { } clientHello)
        {
            return;
        }

        var parameters = QuicTransportParameters.Read(clientHello.QuicTransportParameters!);
        RequireClientParameters(parameters);
        PeerTransportParameters = parameters;
        streams = new QuicStreams(options, parameters);
        idleTimeout = EffectiveIdleTimeout(options.IdleTimeout, parameters.MaxIdleTimeout);
        recovery.MaxAckDelay = TimeSpan.FromMilliseconds((long)(parameters.MaxAckDelay ?? (ulong)QuicLossDetection.DefaultMaxAckDelay.TotalMilliseconds));
    }

    private void RequireClientParameters(QuicTransportParameters parameters)
    {
        RequireNoServerOnlyParameters(parameters);
        RequireClientSourceConnectionId(parameters);
        RequireVersion1(parameters);
    }

    private static void RequireNoServerOnlyParameters(QuicTransportParameters parameters)
    {
        ReadOnlyMemory<byte>?[] serverOnly = [parameters.OriginalDestinationConnectionId, parameters.StatelessResetToken, parameters.PreferredAddress, parameters.RetrySourceConnectionId];
        if (Array.Exists(serverOnly, parameter => parameter is not null))
        {
            throw new QuicTransportException(
                QuicTransportErrorCode.TransportParameterError, "A client sent a transport parameter only a server sends (RFC 9000 section 18.2).", QuicFrameTypes.Crypto);
        }
    }

    private void RequireClientSourceConnectionId(QuicTransportParameters parameters)
    {
        if (parameters.InitialSourceConnectionId is not { } source || !source.Span.SequenceEqual(clientConnectionId))
        {
            throw new QuicTransportException(
                QuicTransportErrorCode.TransportParameterError, "initial_source_connection_id is absent or not the client's Source Connection ID (RFC 9000 section 7.3).", QuicFrameTypes.Crypto);
        }
    }

    private static void RequireVersion1(QuicTransportParameters parameters)
    {
        if (parameters.VersionInformation is { } versions && versions.ChosenVersion != QuicVersions.Version1)
        {
            throw new QuicTransportException(
                QuicTransportErrorCode.VersionNegotiationError, $"version_information chose 0x{versions.ChosenVersion:x8}, not the version 1 the packets use (RFC 9368 section 4).", QuicFrameTypes.Crypto);
        }
    }
}
