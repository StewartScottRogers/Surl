using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Mqtt;

/// <summary>
/// Answers the packets of one MQTT connection, one at a time, and says after each whether
/// the connection stays open. <see cref="MqttProtocolServer"/> documents the answers.
/// </summary>
internal sealed class MqttPacketResponder
{
    private const int QualityOfServiceMask = 0x06;
    private const int DuplicateFlag = 0x08;
    private const byte SubscriptionFailure = 0x80;
    private const byte GrantedQualityOfService = 0x00;

    private readonly IConnection connection;
    private readonly ExchangeContext context;
    private readonly MqttRetainedMessages retainedMessages;
    private readonly IAuthenticationPolicy authenticationPolicy;
    private bool connected;

    /// <summary>
    /// Creates a responder for one connection.
    /// </summary>
    /// <param name="connection">Where the answers go.</param>
    /// <param name="context">The exchange's context: its log and cancellation token.</param>
    /// <param name="retainedMessages">The messages publishes keep and subscribes receive.</param>
    /// <param name="authenticationPolicy">Judges the user name and password of the <c>CONNECT</c>.</param>
    public MqttPacketResponder(
        IConnection connection,
        ExchangeContext context,
        MqttRetainedMessages retainedMessages,
        IAuthenticationPolicy authenticationPolicy)
    {
        this.connection = connection;
        this.context = context;
        this.retainedMessages = retainedMessages;
        this.authenticationPolicy = authenticationPolicy;
    }

    /// <summary>
    /// Answers <paramref name="packet"/>.
    /// </summary>
    /// <param name="packet">The packet the client sent.</param>
    /// <returns>Whether the connection stays open for another packet.</returns>
    public ValueTask<bool> AnswerAsync(MqttPacket packet)
    {
        if (DescribeFixedHeaderViolation(packet) is { } violation)
        {
            return CloseForViolation(violation);
        }

        if (packet.Type == MqttPacketType.Connect)
        {
            return AnswerConnectAsync(packet);
        }

        return connected
            ? AnswerConnectedAsync(packet)
            : CloseForViolation($"The first packet was {packet.Type}, not CONNECT");
    }

    private static string? DescribeFixedHeaderViolation(MqttPacket packet)
    {
        if ((int)packet.Type is 0 or 15)
        {
            return $"A packet of reserved type {(int)packet.Type} arrived";
        }

        return HasWrongFixedHeaderFlags(packet) ? $"A {packet.Type} packet had fixed header flags {packet.Flags}" : null;
    }

    private static bool HasWrongFixedHeaderFlags(MqttPacket packet) => packet.Type switch
    {
        MqttPacketType.Publish => false,
        MqttPacketType.PublishRelease or MqttPacketType.Subscribe or MqttPacketType.Unsubscribe => packet.Flags != 0x02,
        _ => packet.Flags != 0x00,
    };

    /// <summary>
    /// QoS 3 is malformed (section 3.3.1.2), and so is DUP on a QoS 0 message (section 3.3.1.1).
    /// </summary>
    private static bool ArePublishFlagsValid(int flags, int qualityOfService) =>
        qualityOfService != 3 && (qualityOfService != 0 || (flags & DuplicateFlag) == 0);

    private static bool TryReadPublishIdentifier(MqttBodyReader reader, int qualityOfService, out ushort packetIdentifier)
    {
        packetIdentifier = 0;
        return qualityOfService == 0 || (reader.TryReadUInt16(out packetIdentifier) && packetIdentifier != 0);
    }

    private static bool TryReadTopicName(MqttBodyReader reader, out string topic) =>
        reader.TryReadString(out topic) && MqttTopicFilter.IsValidTopicName(topic);

    private static bool TryReadSubscriptions(MqttBodyReader reader, List<string> filters, List<byte> returnCodes)
    {
        while (!reader.IsAtEnd)
        {
            if (!TryReadSubscription(reader, out var filter))
            {
                return false;
            }

            var isValid = MqttTopicFilter.IsValidFilter(filter);
            returnCodes.Add(isValid ? GrantedQualityOfService : SubscriptionFailure);
            if (isValid)
            {
                filters.Add(filter);
            }
        }

        return true;
    }

    private static bool TryReadSubscription(MqttBodyReader reader, out string filter) =>
        reader.TryReadString(out filter) && reader.TryReadByte(out var requestedQualityOfService) && requestedQualityOfService <= 2;

    private static bool TryReadUnsubscribeFilters(MqttBodyReader reader)
    {
        do
        {
            if (!reader.TryReadString(out var filter) || filter.Length == 0)
            {
                return false;
            }
        }
        while (!reader.IsAtEnd);

        return true;
    }

    private static bool TryReadNonZeroPacketIdentifier(MqttBodyReader reader, out ushort packetIdentifier) =>
        reader.TryReadUInt16(out packetIdentifier) && packetIdentifier != 0;

    private ValueTask<bool> AnswerConnectedAsync(MqttPacket packet) => packet.Type switch
    {
        MqttPacketType.Publish => AnswerPublishAsync(packet),
        MqttPacketType.PublishRelease => AnswerPublishReleaseAsync(packet),
        MqttPacketType.Subscribe => AnswerSubscribeAsync(packet),
        MqttPacketType.Unsubscribe => AnswerUnsubscribeAsync(packet),
        _ => AnswerSessionControlAsync(packet),
    };

    private ValueTask<bool> AnswerSessionControlAsync(MqttPacket packet) => packet.Type switch
    {
        MqttPacketType.PingRequest => SendAndStayOpenAsync(MqttPacketEncoder.PingResponse),
        MqttPacketType.Disconnect => ValueTask.FromResult(false),
        _ => CloseForViolation($"A client sent {packet.Type}, which only a server sends"),
    };

    private async ValueTask<bool> AnswerConnectAsync(MqttPacket packet)
    {
        if (connected)
        {
            return await CloseForViolation("A second CONNECT arrived");
        }

        var judgement = MqttConnectJudge.Judge(packet);
        var verdict = judgement.Verdict == MqttConnectVerdict.LoginToCheck
            ? await CheckLoginAsync(judgement)
            : judgement.Verdict;

        return await AnswerConnectVerdictAsync(verdict);
    }

    // The policy decides everything (ADR-0032, sections 5 and 6): the server passes the login
    // as sent and the connection's TLS session, which is null over mqtt://.
    private async ValueTask<MqttConnectVerdict> CheckLoginAsync(MqttConnectJudgement judgement)
    {
        var login = new PasswordLogin(context.Scheme, judgement.UserName, judgement.Password, connection.TlsSession);
        var loginVerdict = await authenticationPolicy.CheckPasswordLoginAsync(login, context.CancellationToken);

        return loginVerdict switch
        {
            PasswordLoginVerdict.Accepted => MqttConnectVerdict.Accepted,
            PasswordLoginVerdict.RefusedCredentials => MqttConnectVerdict.BadUserNameOrPassword,
            _ => MqttConnectVerdict.NotAuthorized,
        };
    }

    private ValueTask<bool> AnswerConnectVerdictAsync(MqttConnectVerdict verdict)
    {
        switch (verdict)
        {
            case MqttConnectVerdict.Accepted:
                connected = true;
                return SendAndStayOpenAsync(MqttPacketEncoder.ConnectAccepted);
            case MqttConnectVerdict.UnacceptableProtocolLevel:
                context.Log.Note("CONNECT asked for a protocol level other than 4 (MQTT 3.1.1); answered CONNACK 1 and closed.");
                return SendAndCloseAsync(MqttPacketEncoder.ConnectRefusedUnacceptableProtocolLevel);
            case MqttConnectVerdict.IdentifierRejected:
                context.Log.Note("CONNECT had an empty client identifier without CleanSession; answered CONNACK 2 and closed.");
                return SendAndCloseAsync(MqttPacketEncoder.ConnectRefusedIdentifierRejected);
            case MqttConnectVerdict.BadUserNameOrPassword:
                context.Log.Note("CONNECT's user name and password match no account; answered CONNACK 4 and closed.");
                return SendAndCloseAsync(MqttPacketEncoder.ConnectRefusedBadUserNameOrPassword);
            case MqttConnectVerdict.NotAuthorized:
                context.Log.Note("CONNECT carried no user name, or a password over an unencrypted connection; answered CONNACK 5 and closed.");
                return SendAndCloseAsync(MqttPacketEncoder.ConnectRefusedNotAuthorized);
            default:
                return CloseForViolation("CONNECT was malformed");
        }
    }

    private ValueTask<bool> AnswerPublishAsync(MqttPacket packet)
    {
        var qualityOfService = (packet.Flags & QualityOfServiceMask) >> 1;
        var reader = new MqttBodyReader(packet.Body);
        if (!ArePublishFlagsValid(packet.Flags, qualityOfService) || !TryReadTopicName(reader, out var topic))
        {
            return CloseForViolation("PUBLISH was malformed");
        }

        if (!TryReadPublishIdentifier(reader, qualityOfService, out var packetIdentifier))
        {
            return CloseForViolation("PUBLISH had no packet identifier");
        }

        return retainedMessages.Retain(topic, reader.Remaining)
            ? SaveAndAcknowledgePublishAsync(qualityOfService, packetIdentifier)
            : CloseForViolation("PUBLISH would take the retained messages past their bounds");
    }

    private async ValueTask<bool> SaveAndAcknowledgePublishAsync(int qualityOfService, ushort packetIdentifier)
    {
        await SaveRetainedMessagesAsync();
        return await AcknowledgePublishAsync(qualityOfService, packetIdentifier);
    }

    // A write that fails keeps the change in memory and the connection open; the next change
    // rewrites the whole file (ADR-0031, decision 6).
    private async ValueTask SaveRetainedMessagesAsync()
    {
        try
        {
            await retainedMessages.SaveChangesAsync(context.CancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            context.Log.Note($"The retained messages could not be written to their file ({exception.Message}); the change is kept in memory.");
        }
    }

    private ValueTask<bool> AcknowledgePublishAsync(int qualityOfService, ushort packetIdentifier) => qualityOfService switch
    {
        1 => SendAndStayOpenAsync(MqttPacketEncoder.PacketIdentifierAcknowledgement(MqttPacketType.PublishAcknowledgement, packetIdentifier)),
        2 => SendAndStayOpenAsync(MqttPacketEncoder.PacketIdentifierAcknowledgement(MqttPacketType.PublishReceived, packetIdentifier)),
        _ => ValueTask.FromResult(true),
    };

    private ValueTask<bool> AnswerPublishReleaseAsync(MqttPacket packet)
    {
        var reader = new MqttBodyReader(packet.Body);
        return TryReadNonZeroPacketIdentifier(reader, out var packetIdentifier) && reader.IsAtEnd
            ? SendAndStayOpenAsync(MqttPacketEncoder.PacketIdentifierAcknowledgement(MqttPacketType.PublishComplete, packetIdentifier))
            : CloseForViolation($"{packet.Type} was malformed");
    }

    private ValueTask<bool> AnswerSubscribeAsync(MqttPacket packet)
    {
        var reader = new MqttBodyReader(packet.Body);
        if (!TryReadNonZeroPacketIdentifier(reader, out var packetIdentifier))
        {
            return CloseForViolation("SUBSCRIBE had no packet identifier");
        }

        if (reader.IsAtEnd)
        {
            return CloseForViolation("SUBSCRIBE had no topic filter");
        }

        var filters = new List<string>();
        var returnCodes = new List<byte>();
        return TryReadSubscriptions(reader, filters, returnCodes)
            ? DeliverRetainedAsync(packetIdentifier, returnCodes, filters)
            : CloseForViolation("SUBSCRIBE was malformed");
    }

    private async ValueTask<bool> DeliverRetainedAsync(ushort packetIdentifier, List<byte> returnCodes, List<string> filters)
    {
        await WriteAsync(MqttPacketEncoder.SubscribeAcknowledgement(packetIdentifier, returnCodes));
        var matching = retainedMessages.MatchingAny(filters);
        foreach (var (topic, payload) in matching)
        {
            await WriteAsync(MqttPacketEncoder.RetainedPublish(topic, payload));
        }

        context.Log.Note($"SUBSCRIBE: {matching.Count} retained messages delivered; sent DISCONNECT and closed.");
        return await SendAndCloseAsync(MqttPacketEncoder.Disconnect);
    }

    private ValueTask<bool> AnswerUnsubscribeAsync(MqttPacket packet)
    {
        var reader = new MqttBodyReader(packet.Body);
        return TryReadNonZeroPacketIdentifier(reader, out var packetIdentifier) && !reader.IsAtEnd && TryReadUnsubscribeFilters(reader)
            ? SendAndStayOpenAsync(MqttPacketEncoder.PacketIdentifierAcknowledgement(MqttPacketType.UnsubscribeAcknowledgement, packetIdentifier))
            : CloseForViolation("UNSUBSCRIBE was malformed");
    }

    private async ValueTask<bool> SendAndStayOpenAsync(byte[] packet)
    {
        await WriteAsync(packet);
        return true;
    }

    private async ValueTask<bool> SendAndCloseAsync(byte[] packet)
    {
        await WriteAsync(packet);
        return false;
    }

    private ValueTask<bool> CloseForViolation(string what)
    {
        context.Log.Note($"{what}; closed with no reply (MQTT 3.1.1, section 4.8).");
        return ValueTask.FromResult(false);
    }

    private ValueTask WriteAsync(byte[] packet) => connection.WriteAsync(packet, context.CancellationToken);
}
