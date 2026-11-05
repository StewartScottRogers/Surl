using System.Runtime.ExceptionServices;
using Surl.MessageBus;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Mqtt5;

/// <summary>
/// Answers one connection to the <see cref="Mqtt5Broker"/>, from its <c>CONNECT</c> to its end.
/// <see cref="Mqtt5Broker"/> documents the answers.
/// </summary>
internal sealed class Mqtt5BrokerConnection
{
    private const double KeepAliveGraceFactor = 1.5;

    // A shared subscription's Topic Filter starts with it (section 4.8.2).
    private const string SharedSubscriptionPrefix = "$share/";

    private readonly IConnection connection;
    private readonly ExchangeContext context;
    private readonly Mqtt5BrokerState state;
    private readonly IAuthenticationPolicy authenticationPolicy;
    private readonly Mqtt5PacketReader reader;
    private readonly Mqtt5InboundPublishes inbound;
    private readonly SemaphoreSlim writeGate = new(1, 1);
    private readonly CancellationTokenSource deliveriesCancellation = new();
    private BusConnection? busConnection;
    private Mqtt5OutboundDeliveries? deliveries;
    private Mqtt5ClientSubscriptions? subscriptions;
    private Task deliveriesRunning = Task.CompletedTask;

    /// <summary>
    /// Prepares to answer <paramref name="connection"/>.
    /// </summary>
    /// <param name="connection">The accepted connection.</param>
    /// <param name="context">The exchange's context: its limits, log, clock and cancellation token.</param>
    /// <param name="state">The state the broker's connections share.</param>
    /// <param name="authenticationPolicy">Judges the login of the <c>CONNECT</c>.</param>
    public Mqtt5BrokerConnection(
        IConnection connection,
        ExchangeContext context,
        Mqtt5BrokerState state,
        IAuthenticationPolicy authenticationPolicy)
    {
        this.connection = connection;
        this.context = context;
        this.state = state;
        this.authenticationPolicy = authenticationPolicy;
        reader = new Mqtt5PacketReader(connection, context.Limits.MaxMessageBytes, context.Limits.MaxUploadBytes);
        inbound = new Mqtt5InboundPublishes(state.Bus.Names, context.Scheme);
    }

    /// <summary>
    /// Answers the connection until it ends, then settles the client's Will.
    /// </summary>
    /// <returns>A task that completes when the exchange is over.</returns>
    public async Task ServeAsync()
    {
        if (await ReadFirstPacketAsync() is not { } first || await AcceptConnectAsync(first) is not { } client)
        {
            return;
        }

        StartDeliveries(client);
        var end = Mqtt5ConnectionEnd.ClosedAbnormally;
        var failure = await CaptureFailureAsync(async () => end = await ServeConnectedAsync(client));
        await StopDeliveriesAsync();
        EndConnection(client, end);
        failure?.Throw();
    }

    // Runs work and hands back what it threw instead of throwing it, so the caller stops the
    // deliveries with an await outside any catch or finally block.
    private static async Task<ExceptionDispatchInfo?> CaptureFailureAsync(Func<Task> work)
    {
        try
        {
            await work();
            return null;
        }
        catch (Exception exception)
        {
            return ExceptionDispatchInfo.Capture(exception);
        }
    }

    // The head timeout runs until the first packet is complete (ADR-0093 decision 8).
    private async ValueTask<Mqtt5PacketReadResult?> ReadFirstPacketAsync()
    {
        if (context.Limits.HeadTimeout == Timeout.InfiniteTimeSpan)
        {
            return await reader.ReadPacketAsync(context.CancellationToken);
        }

        using var headTimeout = new CancellationTokenSource(context.Limits.HeadTimeout, context.TimeProvider);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, headTimeout.Token);
        try
        {
            return await reader.ReadPacketAsync(cancellation.Token);
        }
        catch (OperationCanceledException) when (!context.CancellationToken.IsCancellationRequested)
        {
            context.Log.Note("The first packet was not complete within the head timeout; closed with no reply.");
            return null;
        }
    }

    private async ValueTask<Mqtt5ConnectedClient?> AcceptConnectAsync(Mqtt5PacketReadResult first)
    {
        if (first.Packet is Mqtt5ConnectPacket connect)
        {
            return await JudgeConnectAsync(connect);
        }

        if (first.RefusedPacketType == Mqtt5PacketType.Connect && first.Fault != Mqtt5PacketFault.UnknownProtocolName)
        {
            await RefuseConnectAsync(first.Fault!);
            return null;
        }

        NoteFirstPacketClosedWithNoReply(first);
        return null;
    }

    // [MQTT-3.1.0-1], [MQTT-3.2.0-1]: nothing but a CONNECT is answered before CONNACK.
    private void NoteFirstPacketClosedWithNoReply(Mqtt5PacketReadResult first)
    {
        var what = first.Outcome switch
        {
            Mqtt5PacketReadOutcome.PacketRead => $"The first packet was {first.Packet!.Type}, not CONNECT",
            Mqtt5PacketReadOutcome.PacketRefused when first.RefusedPacketType is null => "The first packet's fixed header was unreadable",
            Mqtt5PacketReadOutcome.PacketRefused => $"The first packet was refused and was not CONNECT, or named no MQTT protocol ({first.Fault!.Statement})",
            _ => "The client closed the connection before CONNECT",
        };
        context.Log.Note($"{what}; closed with no reply.");
    }

    private async ValueTask<Mqtt5ConnectedClient?> JudgeConnectAsync(Mqtt5ConnectPacket connect)
    {
        var fault = Mqtt5ConnectJudge.FaultIn(connect);
        Mqtt5ApplicationMessage? will = null;
        if ((fault ?? FaultInWillTopic(connect, out will)) is { } refusal)
        {
            await RefuseConnectAsync(refusal);
            return null;
        }

        var (loginReasonCode, account) = await CheckLoginAsync(connect);
        if (loginReasonCode != Mqtt5ReasonCode.Success)
        {
            await RefuseConnectAsync(new(loginReasonCode, "3.1.3.5"));
            return null;
        }

        return await AcknowledgeConnectAsync(connect, account, will);
    }

    // [MQTT-4.7.3-1]: the Will Topic is a Topic Name, mapped onto the bus as a PUBLISH's is.
    private Mqtt5PacketFault? FaultInWillTopic(Mqtt5ConnectPacket connect, out Mqtt5ApplicationMessage? will)
    {
        will = null;
        if (connect.Will is not { } connectWill)
        {
            return null;
        }

        var topic = state.Bus.Names.ToCanonicalTopic(context.Scheme, connectWill.Topic);
        if (!topic.IsMapped)
        {
            return Mqtt5PacketFault.Malformed("[MQTT-4.7.3-1]");
        }

        will = Mqtt5ApplicationMessage.FromWill(connectWill) with { Topic = topic.Name };
        return null;
    }

    // The policy decides everything (ADR-0093 decision 2, ADR-0032): the broker passes the login
    // as sent and the connection's TLS session, null over mqtt5://. A login an account matched
    // publishes and subscribes as that account; one accepted unchecked as an anonymous caller.
    private async ValueTask<(Mqtt5ReasonCode ReasonCode, string? Account)> CheckLoginAsync(Mqtt5ConnectPacket connect)
    {
        ReadOnlyMemory<byte>? password = null;
        if (connect.Password is { } passwordBytes)
        {
            password = passwordBytes;
        }

        var login = new PasswordLogin(context.Scheme, connect.UserName, password, connection.TlsSession);
        var verdict = await authenticationPolicy.CheckPasswordLoginAsync(login, context.CancellationToken);
        if (verdict is PasswordLoginVerdict.Accepted or PasswordLoginVerdict.RefusedCredentials)
        {
            context.Log.Note(new CheckedLogin(login.Scheme, login.UserName, verdict == PasswordLoginVerdict.Accepted).Note);
        }

        return verdict switch
        {
            PasswordLoginVerdict.Accepted => (Mqtt5ReasonCode.Success, connect.UserName),
            PasswordLoginVerdict.AcceptedUnchecked => (Mqtt5ReasonCode.Success, null),
            PasswordLoginVerdict.RefusedCredentials => (Mqtt5ReasonCode.BadUserNameOrPassword, null),
            _ => (Mqtt5ReasonCode.NotAuthorized, null),
        };
    }

    private async ValueTask RefuseConnectAsync(Mqtt5PacketFault fault)
    {
        context.Log.Note($"CONNECT refused ({fault.Statement}); answered CONNACK 0x{(byte)fault.ReasonCode:X2} and closed.");
        await WriteAsync(new Mqtt5ConnectAcknowledgementPacket(false, fault.ReasonCode, []));
    }

    private async ValueTask<Mqtt5ConnectedClient> AcknowledgeConnectAsync(Mqtt5ConnectPacket connect, string? account, Mqtt5ApplicationMessage? will)
    {
        string? assignedClientIdentifier = null;
        object registration;
        if (connect.ClientIdentifier.Length == 0)
        {
            assignedClientIdentifier = state.RegisterAssignedClient(out registration);
        }
        else
        {
            registration = state.RegisterClient(connect.ClientIdentifier);
        }

        var clientIdentifier = assignedClientIdentifier ?? connect.ClientIdentifier;
        state.SettlePendingWill(clientIdentifier, connect.CleanStart);
        var properties = Mqtt5ConnectAcknowledgementProperties.For(connect, assignedClientIdentifier, context.Limits.MaxMessageBytes);
        await WriteAsync(new Mqtt5ConnectAcknowledgementPacket(false, Mqtt5ReasonCode.Success, properties));

        return new Mqtt5ConnectedClient(
            clientIdentifier,
            registration,
            account,
            Mqtt5ConnectAcknowledgementProperties.KeepAliveInForce(connect),
            Mqtt5ConnectAcknowledgementProperties.SessionExpiryInForce(connect),
            will,
            TimeSpan.FromSeconds(Mqtt5ConnectJudge.Find(connect.Will?.Properties ?? [], Mqtt5PropertyIdentifier.WillDelayInterval)?.Integer ?? 0))
            .WithLimitsOf(connect);
    }

    // The client's place on the bus, made for its account, and the sending of what its
    // subscriptions receive, which runs beside the reading of its packets.
    private void StartDeliveries(Mqtt5ConnectedClient client)
    {
        busConnection = state.Bus.Connect(client.Account);
        deliveries = new Mqtt5OutboundDeliveries(client, state.Bus.Names, context, WriteBytesAsync);
        subscriptions = new Mqtt5ClientSubscriptions(busConnection, state.Bus.Names, context.Scheme, deliveries);
        deliveriesRunning = deliveries.RunAsync(deliveriesCancellation.Token);
    }

    // Ends the client's subscriptions and the sending, so nothing follows the broker's DISCONNECT.
    private async ValueTask StopDeliveriesAsync()
    {
        busConnection!.Dispose();
        await deliveriesCancellation.CancelAsync();
        await deliveriesRunning;
    }

    private async ValueTask<Mqtt5ConnectionEnd> ServeConnectedAsync(Mqtt5ConnectedClient client)
    {
        var end = Mqtt5ConnectionEnd.StaysOpen;
        while (end == Mqtt5ConnectionEnd.StaysOpen)
        {
            end = await ReadWithinKeepAliveAsync(client.KeepAlive) switch
            {
                null => await DisconnectAsync(Mqtt5ReasonCode.KeepAliveTimeout, "No packet arrived within one and a half times the Keep Alive"),
                { Packet: { } packet } => await AnswerAsync(packet),
                { Fault: { } fault } refused => await DisconnectForRefusedPacketAsync(refused.RefusedPacketType, fault),
                _ => Mqtt5ConnectionEnd.ClosedAbnormally,
            };
        }

        return end;
    }

    // [MQTT-3.1.2-22]: one and a half times the Keep Alive in force, then DISCONNECT 0x8D.
    private async ValueTask<Mqtt5PacketReadResult?> ReadWithinKeepAliveAsync(TimeSpan keepAlive)
    {
        using var keepAliveTimeout = new CancellationTokenSource(keepAlive * KeepAliveGraceFactor, context.TimeProvider);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, keepAliveTimeout.Token);
        try
        {
            return await reader.ReadPacketAsync(cancellation.Token);
        }
        catch (OperationCanceledException) when (!context.CancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    private ValueTask<Mqtt5ConnectionEnd> AnswerAsync(Mqtt5Packet packet) => packet switch
    {
        Mqtt5PublishPacket publish => AnswerPublishAsync(publish),
        Mqtt5PublishResponsePacket { ResponseType: Mqtt5PacketType.PublishRelease } release => AnswerReleaseAsync(release),
        Mqtt5PublishResponsePacket acknowledgement => AnswerAcknowledgementAsync(acknowledgement),
        Mqtt5SubscribePacket subscribe => AnswerSubscribeAsync(subscribe),
        Mqtt5UnsubscribePacket unsubscribe => AnswerUnsubscribeAsync(unsubscribe),
        _ => AnswerControlPacketAsync(packet),
    };

    private ValueTask<Mqtt5ConnectionEnd> AnswerControlPacketAsync(Mqtt5Packet packet)
    {
        if (packet.Type == Mqtt5PacketType.PingRequest)
        {
            return AnswerPingAsync();
        }

        if (packet is Mqtt5ReasonCodeAndPropertiesPacket { PacketType: Mqtt5PacketType.Disconnect } disconnect)
        {
            return ValueTask.FromResult(disconnect.ReasonCode == Mqtt5ReasonCode.Success
                ? Mqtt5ConnectionEnd.ClosedNormally
                : Mqtt5ConnectionEnd.ClosedAbnormally);
        }

        // What is left is a packet a client never sends once connected (section 3.1.0,
        // [MQTT-4.12.0-7]): a second CONNECT, a packet only a server sends, and AUTH on a
        // connection that used no method.
        return DisconnectAsync(Mqtt5ReasonCode.ProtocolError, $"A client sent {packet.Type} after CONNACK");
    }

    private async ValueTask<Mqtt5ConnectionEnd> AnswerPingAsync()
    {
        await WriteAsync(new Mqtt5PingPacket(Mqtt5PacketType.PingResponse));
        return Mqtt5ConnectionEnd.StaysOpen;
    }

    // Section 4.3: the message is taken, then QoS 1 is answered PUBACK and QoS 2 PUBREC.
    private async ValueTask<Mqtt5ConnectionEnd> AnswerPublishAsync(Mqtt5PublishPacket publish)
    {
        var verdict = inbound.Judge(publish);
        if (verdict.Fault is { } fault)
        {
            return await DisconnectAsync(fault.ReasonCode, $"A PUBLISH was refused ({fault.Statement})");
        }

        var reasonCode = verdict.Message is { } message ? await PublishAsync(message) : verdict.Answer;
        return reasonCode == Mqtt5ReasonCode.PacketTooLarge
            ? await DisconnectAsync(reasonCode, "A PUBLISH payload is larger than the message bus takes")
            : await AcknowledgePublishAsync(publish, reasonCode);
    }

    private async ValueTask<Mqtt5ReasonCode> PublishAsync(Mqtt5ApplicationMessage message)
    {
        // Published through the client's own bus connection, so its No Local subscriptions miss it.
        var result = busConnection!.Publish(Mqtt5BusMessages.ToBusMessage(message, context.TimeProvider.GetUtcNow()));
        if (result.Outcome == MessageBusOutcome.Succeeded)
        {
            await SaveBusChangesAsync();
        }

        return result.Outcome switch
        {
            MessageBusOutcome.Succeeded when result.DeliveredSubscriptionCount + result.QueuedCount == 0 => Mqtt5ReasonCode.NoMatchingSubscribers,
            MessageBusOutcome.Succeeded => Mqtt5ReasonCode.Success,
            MessageBusOutcome.AccessDenied => Mqtt5ReasonCode.NotAuthorized,
            MessageBusOutcome.MessageTooLarge => Mqtt5ReasonCode.PacketTooLarge,
            _ => Mqtt5ReasonCode.QuotaExceeded,
        };
    }

    // ADR-0100 decision 3.7: a failed write is noted; the change stays in memory and serving goes on.
    private async ValueTask SaveBusChangesAsync()
    {
        var saved = await state.Bus.SaveChangesAsync(context.CancellationToken);
        if (saved.Outcome != MessageBusOutcome.Succeeded)
        {
            context.Log.Note($"The message bus could not save a change ({saved.FailureMessage}); it stays in memory.");
        }
    }

    // A QoS 0 PUBLISH has nothing to answer: a refused one is dropped (ADR-0102 decision 3.4).
    private async ValueTask<Mqtt5ConnectionEnd> AcknowledgePublishAsync(Mqtt5PublishPacket publish, Mqtt5ReasonCode reasonCode)
    {
        var refused = reasonCode >= Mqtt5ReasonCode.UnspecifiedError;
        if (publish.QualityOfService == 0)
        {
            if (refused)
            {
                context.Log.Note($"A QoS 0 PUBLISH was refused 0x{(byte)reasonCode:X2}; dropped.");
            }

            return Mqtt5ConnectionEnd.StaysOpen;
        }

        if (publish.QualityOfService == 2 && !refused)
        {
            inbound.AwaitRelease(publish.PacketIdentifier);
        }

        var responseType = publish.QualityOfService == 1 ? Mqtt5PacketType.PublishAcknowledgement : Mqtt5PacketType.PublishReceived;
        await WriteAsync(new Mqtt5PublishResponsePacket(responseType, publish.PacketIdentifier, reasonCode, []));
        return Mqtt5ConnectionEnd.StaysOpen;
    }

    // [MQTT-4.3.3-11]: PUBREL is answered PUBCOMP; 0x92 for an identifier awaiting none.
    private async ValueTask<Mqtt5ConnectionEnd> AnswerReleaseAsync(Mqtt5PublishResponsePacket release)
    {
        var reasonCode = inbound.Release(release.PacketIdentifier) ? Mqtt5ReasonCode.Success : Mqtt5ReasonCode.PacketIdentifierNotFound;
        await WriteAsync(new Mqtt5PublishResponsePacket(Mqtt5PacketType.PublishComplete, release.PacketIdentifier, reasonCode, []));
        return Mqtt5ConnectionEnd.StaysOpen;
    }

    private async ValueTask<Mqtt5ConnectionEnd> AnswerAcknowledgementAsync(Mqtt5PublishResponsePacket acknowledgement)
    {
        if (deliveries!.Acknowledge(acknowledgement) is { } release)
        {
            await WriteAsync(release);
        }

        return Mqtt5ConnectionEnd.StaysOpen;
    }

    // Each filter is a bus subscription; its retained messages follow the SUBACK with RETAIN 1.
    // A Subscription Identifier of 0 (section 3.8.2.1.2) and No Local on a shared
    // subscription ([MQTT-3.8.3-4]) are Protocol Errors; a Packet Identifier awaiting PUBREL is in use.
    private async ValueTask<Mqtt5ConnectionEnd> AnswerSubscribeAsync(Mqtt5SubscribePacket subscribe)
    {
        var subscriptionIdentifier = Mqtt5ConnectJudge.Find(subscribe.Properties, Mqtt5PropertyIdentifier.SubscriptionIdentifier)?.Integer;
        if (ProtocolErrorIn(subscribe, subscriptionIdentifier) is { } why)
        {
            return await DisconnectAsync(Mqtt5ReasonCode.ProtocolError, why);
        }

        var retained = new List<Mqtt5Delivery>();
        var reasonCodes = SubscribeEach(subscribe, subscriptionIdentifier, retained);
        await WriteAsync(new Mqtt5ReasonCodeListPacket(Mqtt5PacketType.SubscribeAcknowledgement, subscribe.PacketIdentifier, [], reasonCodes));
        foreach (var delivery in retained)
        {
            deliveries!.Offer(delivery.Message, delivery.QualityOfService, delivery.Retain, delivery.SubscriptionIdentifiers);
        }

        return Mqtt5ConnectionEnd.StaysOpen;
    }

    private static string? ProtocolErrorIn(Mqtt5SubscribePacket subscribe, uint? subscriptionIdentifier)
    {
        if (subscriptionIdentifier == 0)
        {
            return "A SUBSCRIBE carried Subscription Identifier 0 (3.8.2.1.2)";
        }

        return subscribe.Subscriptions.Any(IsNoLocalShared)
            ? "A SUBSCRIBE set No Local on a shared subscription ([MQTT-3.8.3-4])"
            : null;
    }

    private static bool IsNoLocalShared(Mqtt5Subscription subscription) =>
        subscription.NoLocal && subscription.TopicFilter.StartsWith(SharedSubscriptionPrefix, StringComparison.Ordinal);

    private List<Mqtt5ReasonCode> SubscribeEach(Mqtt5SubscribePacket subscribe, uint? subscriptionIdentifier, List<Mqtt5Delivery> retained) =>
        inbound.IsAwaitingRelease(subscribe.PacketIdentifier)
            ? [.. subscribe.Subscriptions.Select(_ => Mqtt5ReasonCode.PacketIdentifierInUse)]
            : [.. subscribe.Subscriptions.Select(subscription => subscriptions!.Subscribe(subscription, subscriptionIdentifier, retained))];

    // Section 3.10.4: each filter identical to a subscription deletes it; UNSUBACK has one code a filter.
    private async ValueTask<Mqtt5ConnectionEnd> AnswerUnsubscribeAsync(Mqtt5UnsubscribePacket unsubscribe)
    {
        var reasonCodes = inbound.IsAwaitingRelease(unsubscribe.PacketIdentifier)
            ? unsubscribe.TopicFilters.Select(_ => Mqtt5ReasonCode.PacketIdentifierInUse).ToList()
            : unsubscribe.TopicFilters.Select(subscriptions!.Unsubscribe).ToList();
        await WriteAsync(new Mqtt5ReasonCodeListPacket(Mqtt5PacketType.UnsubscribeAcknowledgement, unsubscribe.PacketIdentifier, [], reasonCodes));
        return Mqtt5ConnectionEnd.StaysOpen;
    }

    // A refused second CONNECT is a Protocol Error whatever made it unreadable ([MQTT-3.1.0-2]).
    private ValueTask<Mqtt5ConnectionEnd> DisconnectForRefusedPacketAsync(Mqtt5PacketType? refusedPacketType, Mqtt5PacketFault fault) =>
        refusedPacketType == Mqtt5PacketType.Connect
            ? DisconnectAsync(Mqtt5ReasonCode.ProtocolError, "A second CONNECT arrived")
            : DisconnectAsync(fault.ReasonCode, $"A packet was refused ({fault.Statement})");

    private async ValueTask<Mqtt5ConnectionEnd> DisconnectAsync(Mqtt5ReasonCode reasonCode, string why)
    {
        context.Log.Note($"{why}; sent DISCONNECT 0x{(byte)reasonCode:X2} and closed.");
        await StopDeliveriesAsync();
        await WriteAsync(new Mqtt5ReasonCodeAndPropertiesPacket(Mqtt5PacketType.Disconnect, reasonCode, []));
        return Mqtt5ConnectionEnd.ClosedAbnormally;
    }

    // Section 3.1.2.5: the Will is published once the lesser of its delay and the session's
    // expiry has passed, unless the client ended the connection with DISCONNECT 0x00.
    private void EndConnection(Mqtt5ConnectedClient client, Mqtt5ConnectionEnd end)
    {
        state.UnregisterClient(client.ClientIdentifier, client.Registration);
        if (client.Will is not { } will || end == Mqtt5ConnectionEnd.ClosedNormally)
        {
            return;
        }

        var delay = client.WillDelay < client.SessionExpiry ? client.WillDelay : client.SessionExpiry;
        if (delay == TimeSpan.Zero)
        {
            state.Publish(will, client.Account, context.TimeProvider.GetUtcNow());
        }
        else
        {
            state.ScheduleWill(client.ClientIdentifier, will, client.Account, delay, context.TimeProvider);
        }
    }

    private ValueTask WriteAsync(Mqtt5Packet packet) => WriteBytesAsync(Mqtt5PacketEncoder.Encode(packet), context.CancellationToken);

    // The reading of packets and the sending of deliveries both write; one write at a time.
    private async ValueTask WriteBytesAsync(byte[] bytes, CancellationToken cancellationToken)
    {
        await writeGate.WaitAsync(cancellationToken);
        try
        {
            await connection.WriteAsync(bytes, cancellationToken);
        }
        finally
        {
            writeGate.Release();
        }
    }
}
