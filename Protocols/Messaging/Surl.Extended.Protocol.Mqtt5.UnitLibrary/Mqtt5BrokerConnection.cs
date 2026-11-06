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
    private readonly ISaslAuthenticationPolicy? saslAuthenticationPolicy;
    private readonly Mqtt5PacketReader reader;
    private readonly SemaphoreSlim writeGate = new(1, 1);
    private readonly CancellationTokenSource deliveriesCancellation = new();
    private CancellationToken headToken;
    private Mqtt5Session? session;
    private Mqtt5InboundPublishes? inbound;
    private Mqtt5OutboundDeliveries? deliveries;
    private Mqtt5EnhancedAuthentication? reauthentication;
    private Task deliveriesRunning = Task.CompletedTask;

    /// <summary>
    /// Prepares to answer <paramref name="connection"/>.
    /// </summary>
    /// <param name="connection">The accepted connection.</param>
    /// <param name="context">The exchange's context: its limits, log, clock and cancellation token.</param>
    /// <param name="state">The state the broker's connections share.</param>
    /// <param name="authenticationPolicy">Judges the user name and password of the <c>CONNECT</c>.</param>
    /// <param name="saslAuthenticationPolicy">Runs enhanced authentication; <see langword="null"/> serves no method.</param>
    public Mqtt5BrokerConnection(
        IConnection connection,
        ExchangeContext context,
        Mqtt5BrokerState state,
        IAuthenticationPolicy authenticationPolicy,
        ISaslAuthenticationPolicy? saslAuthenticationPolicy)
    {
        this.connection = connection;
        this.context = context;
        this.state = state;
        this.authenticationPolicy = authenticationPolicy;
        this.saslAuthenticationPolicy = saslAuthenticationPolicy;
        reader = new Mqtt5PacketReader(connection, context.Limits.MaxMessageBytes, context.Limits.MaxUploadBytes);
    }

    /// <summary>
    /// Answers the connection until it ends, then settles the client's session and Will.
    /// </summary>
    /// <returns>A task that completes when the exchange is over.</returns>
    public async Task ServeAsync()
    {
        if (await ConnectAsync() is not { } client)
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

    // The head timeout runs from the first byte until CONNACK, enhanced authentication included
    // (ADR-0093 decisions 2 and 8).
    private async ValueTask<Mqtt5ConnectedClient?> ConnectAsync()
    {
        using var headTimeout = new CancellationTokenSource(context.Limits.HeadTimeout, context.TimeProvider);
        using var headCancellation = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, headTimeout.Token);
        headToken = headCancellation.Token;
        if (await ReadBeforeConnackAsync("The first packet") is not { } first)
        {
            return null;
        }

        return await AcceptConnectAsync(first);
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

    private async ValueTask<Mqtt5PacketReadResult?> ReadBeforeConnackAsync(string what)
    {
        try
        {
            return await reader.ReadPacketAsync(headToken);
        }
        catch (OperationCanceledException) when (!context.CancellationToken.IsCancellationRequested)
        {
            context.Log.Note($"{what} was not complete within the head timeout; closed with no reply.");
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

        var login = Mqtt5ConnectJudge.Find(connect.Properties, Mqtt5PropertyIdentifier.AuthenticationMethod) is { } method
            ? await AuthenticateAsync(connect, method.Text)
            : await CheckLoginAsync(connect);
        if (login is null)
        {
            return null;
        }

        if (login.ReasonCode != Mqtt5ReasonCode.Success)
        {
            await RefuseConnectAsync(new(login.ReasonCode, login.Statement));
            return null;
        }

        return await AcknowledgeConnectAsync(connect, login, will);
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
    private async ValueTask<Mqtt5Login?> CheckLoginAsync(Mqtt5ConnectPacket connect)
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
            PasswordLoginVerdict.Accepted => new(Mqtt5ReasonCode.Success, "3.1.3.5", connect.UserName),
            PasswordLoginVerdict.AcceptedUnchecked => new(Mqtt5ReasonCode.Success, "3.1.3.5", null),
            PasswordLoginVerdict.RefusedCredentials => Mqtt5Login.Refused(Mqtt5ReasonCode.BadUserNameOrPassword, "3.1.3.5"),
            _ => Mqtt5Login.Refused(Mqtt5ReasonCode.NotAuthorized, "3.1.3.5"),
        };
    }

    // Section 4.12: each challenge goes in AUTH 0x18; each client AUTH must be 0x18 with the same
    // method. A DISCONNECT, a closed connection or the head timeout ends it with no CONNACK.
    private async ValueTask<Mqtt5Login?> AuthenticateAsync(Mqtt5ConnectPacket connect, string method)
    {
        if (!Mqtt5EnhancedAuthentication.IsOffered(saslAuthenticationPolicy, method, context.Scheme, connection.TlsSession))
        {
            return Mqtt5Login.Refused(Mqtt5ReasonCode.BadAuthenticationMethod, "[MQTT-4.12.0-1]");
        }

        var authentication = new Mqtt5EnhancedAuthentication(saslAuthenticationPolicy!, method, context, connection.TlsSession);
        var step = await authentication.StepAsync(AuthenticationDataOf(connect.Properties));
        while (step.Outcome == Mqtt5AuthenticationOutcome.Continue)
        {
            await WriteAsync(Authentication(Mqtt5ReasonCode.ContinueAuthentication, method, step.Data));
            var (continues, data, ended) = await ReadAuthenticationReplyAsync(method);
            if (!continues)
            {
                return ended;
            }

            step = await authentication.StepAsync(data);
        }

        return step.Outcome == Mqtt5AuthenticationOutcome.Accepted
            ? new(Mqtt5ReasonCode.Success, "4.12", step.Account, method, step.Data)
            : Mqtt5Login.Refused(Mqtt5ReasonCode.NotAuthorized, "[MQTT-4.12.0-4]");
    }

    // The client's answer to a challenge: an AUTH 0x18 that continues the exchange with its data;
    // otherwise the exchange ends, refused, or with no CONNACK at all (Ended null).
    private async ValueTask<(bool Continues, byte[]? Data, Mqtt5Login? Ended)> ReadAuthenticationReplyAsync(string method)
    {
        var reply = await ReadBeforeConnackAsync("An AUTH of the CONNECT's authentication");
        if (reply is null || (reply.Packet is null && reply.Fault is null) || IsDisconnect(reply.Packet))
        {
            return (false, null, null);
        }

        if (FaultInAuthenticationReply(reply, method) is { } fault)
        {
            return (false, null, Mqtt5Login.Refused(fault.ReasonCode, fault.Statement));
        }

        return (true, AuthenticationDataOf(((Mqtt5ReasonCodeAndPropertiesPacket)reply.Packet!).Properties), null);
    }

    private static bool IsDisconnect(Mqtt5Packet? packet) => packet?.Type == Mqtt5PacketType.Disconnect;

    // [MQTT-3.1.2-30], [MQTT-4.12.0-3]: only AUTH 0x18 with the same method continues it.
    private static Mqtt5PacketFault? FaultInAuthenticationReply(Mqtt5PacketReadResult reply, string method)
    {
        if (reply.Fault is { } fault)
        {
            return fault;
        }

        return reply.Packet is Mqtt5ReasonCodeAndPropertiesPacket { PacketType: Mqtt5PacketType.Authentication, ReasonCode: Mqtt5ReasonCode.ContinueAuthentication } authentication
            && MethodOf(authentication) == method
                ? null
                : Mqtt5PacketFault.ProtocolError("[MQTT-4.12.0-3]");
    }

    private static string? MethodOf(Mqtt5ReasonCodeAndPropertiesPacket authentication) =>
        Mqtt5ConnectJudge.Find(authentication.Properties, Mqtt5PropertyIdentifier.AuthenticationMethod)?.Text;

    private static byte[]? AuthenticationDataOf(IReadOnlyList<Mqtt5Property> properties) =>
        Mqtt5ConnectJudge.Find(properties, Mqtt5PropertyIdentifier.AuthenticationData)?.Binary;

    private static Mqtt5ReasonCodeAndPropertiesPacket Authentication(Mqtt5ReasonCode reasonCode, string method, byte[] data) =>
        new(Mqtt5PacketType.Authentication, reasonCode, AuthenticationProperties(method, data));

    private static List<Mqtt5Property> AuthenticationProperties(string method, byte[] data)
    {
        List<Mqtt5Property> properties = [Mqtt5Property.FromText(Mqtt5PropertyIdentifier.AuthenticationMethod, method)];
        if (data.Length > 0)
        {
            properties.Add(Mqtt5Property.FromBinary(Mqtt5PropertyIdentifier.AuthenticationData, data));
        }

        return properties;
    }

    private async ValueTask RefuseConnectAsync(Mqtt5PacketFault fault)
    {
        context.Log.Note($"CONNECT refused ({fault.Statement}); answered CONNACK 0x{(byte)fault.ReasonCode:X2} and closed.");
        await WriteAsync(new Mqtt5ConnectAcknowledgementPacket(false, fault.ReasonCode, []));
    }

    private async ValueTask<Mqtt5ConnectedClient?> AcknowledgeConnectAsync(Mqtt5ConnectPacket connect, Mqtt5Login login, Mqtt5ApplicationMessage? will)
    {
        var registration = RegisterClient(connect, out var assignedClientIdentifier);
        var clientIdentifier = assignedClientIdentifier ?? connect.ClientIdentifier;
        var expiry = Mqtt5ConnectAcknowledgementProperties.SessionExpiryInForce(connect);
        session = state.OpenSession(clientIdentifier, login.Account, connect.CleanStart, expiry, context.Scheme, out var resumed);
        if (session is null)
        {
            state.UnregisterClient(clientIdentifier, registration);
            await RefuseConnectAsync(new(Mqtt5ReasonCode.QuotaExceeded, "ADR-0093 decision 3"));
            return null;
        }

        state.SettlePendingWill(clientIdentifier, resumed);
        await WriteAsync(new Mqtt5ConnectAcknowledgementPacket(resumed, Mqtt5ReasonCode.Success, ConnectAcknowledgementProperties(connect, assignedClientIdentifier, login)));

        return new Mqtt5ConnectedClient(
            clientIdentifier,
            registration,
            login.Account,
            Mqtt5ConnectAcknowledgementProperties.KeepAliveInForce(connect),
            will,
            TimeSpan.FromSeconds(Mqtt5ConnectJudge.Find(connect.Will?.Properties ?? [], Mqtt5PropertyIdentifier.WillDelayInterval)?.Integer ?? 0),
            login.AuthenticationMethod)
            .WithLimitsOf(connect);
    }

    // A zero-length identifier is assigned one (section 3.1.3.1).
    private object RegisterClient(Mqtt5ConnectPacket connect, out string? assignedClientIdentifier)
    {
        if (connect.ClientIdentifier.Length == 0)
        {
            assignedClientIdentifier = state.RegisterAssignedClient(out var registration);
            return registration;
        }

        assignedClientIdentifier = null;
        return state.RegisterClient(connect.ClientIdentifier);
    }

    // [MQTT-4.12.0-5]: an enhanced authentication's CONNACK names its method and carries its last message.
    private IReadOnlyList<Mqtt5Property> ConnectAcknowledgementProperties(Mqtt5ConnectPacket connect, string? assignedClientIdentifier, Mqtt5Login login)
    {
        var properties = Mqtt5ConnectAcknowledgementProperties.For(connect, assignedClientIdentifier, context.Limits.MaxMessageBytes);
        return login.AuthenticationMethod is { } method
            ? [.. properties, .. AuthenticationProperties(method, login.AuthenticationData!)]
            : properties;
    }

    // The sending of what the session holds runs beside the reading of the client's packets.
    private void StartDeliveries(Mqtt5ConnectedClient client)
    {
        inbound = new Mqtt5InboundPublishes(state.Bus.Names, context.Scheme, session!.AwaitingRelease);
        deliveries = new Mqtt5OutboundDeliveries(session, client, state.Bus.Names, context, WriteBytesAsync);
        deliveriesRunning = deliveries.RunAsync(deliveriesCancellation.Token);
    }

    // Ends the sending, so nothing follows the broker's DISCONNECT; what is in flight stays in the session.
    private async ValueTask StopDeliveriesAsync()
    {
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
                { Packet: { } packet } => await AnswerAsync(packet, client),
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

    private ValueTask<Mqtt5ConnectionEnd> AnswerAsync(Mqtt5Packet packet, Mqtt5ConnectedClient client) => packet switch
    {
        Mqtt5PublishPacket publish => AnswerPublishAsync(publish),
        Mqtt5PublishResponsePacket { ResponseType: Mqtt5PacketType.PublishRelease } release => AnswerReleaseAsync(release),
        Mqtt5PublishResponsePacket acknowledgement => AnswerAcknowledgementAsync(acknowledgement),
        Mqtt5SubscribePacket subscribe => AnswerSubscribeAsync(subscribe),
        Mqtt5UnsubscribePacket unsubscribe => AnswerUnsubscribeAsync(unsubscribe),
        _ => AnswerControlPacketAsync(packet, client),
    };

    private ValueTask<Mqtt5ConnectionEnd> AnswerControlPacketAsync(Mqtt5Packet packet, Mqtt5ConnectedClient client)
    {
        // The decoder reads a DISCONNECT or an AUTH into this type, and nothing else.
        if (packet is Mqtt5ReasonCodeAndPropertiesPacket disconnectOrAuthentication)
        {
            return disconnectOrAuthentication.PacketType == Mqtt5PacketType.Disconnect
                ? AnswerDisconnectAsync(disconnectOrAuthentication)
                : AnswerAuthenticationAsync(disconnectOrAuthentication, client);
        }

        if (packet.Type == Mqtt5PacketType.PingRequest)
        {
            return AnswerPingAsync();
        }

        // What is left is a packet a client never sends once connected (section 3.1.0): a
        // second CONNECT and a packet only a server sends.
        return DisconnectAsync(Mqtt5ReasonCode.ProtocolError, $"A client sent {packet.Type} after CONNACK");
    }

    private async ValueTask<Mqtt5ConnectionEnd> AnswerPingAsync()
    {
        await WriteAsync(new Mqtt5PingPacket(Mqtt5PacketType.PingResponse));
        return Mqtt5ConnectionEnd.StaysOpen;
    }

    // Section 3.14.4: 0x00 discards the Will, any other code keeps it ([MQTT-3.14.4-3]). A
    // Session Expiry Interval on it changes the session's, but never from 0 (3.14.2.2.2).
    private ValueTask<Mqtt5ConnectionEnd> AnswerDisconnectAsync(Mqtt5ReasonCodeAndPropertiesPacket disconnect)
    {
        if (!TryChangeSessionExpiry(disconnect))
        {
            return DisconnectAsync(Mqtt5ReasonCode.ProtocolError, "A DISCONNECT raised a Session Expiry Interval of 0 (3.14.2.2.2)");
        }

        return ValueTask.FromResult(disconnect.ReasonCode == Mqtt5ReasonCode.Success
            ? Mqtt5ConnectionEnd.ClosedNormally
            : Mqtt5ConnectionEnd.ClosedAbnormally);
    }

    // False when the DISCONNECT raises an interval of 0, which stays 0.
    private bool TryChangeSessionExpiry(Mqtt5ReasonCodeAndPropertiesPacket disconnect)
    {
        if (Mqtt5ConnectJudge.Find(disconnect.Properties, Mqtt5PropertyIdentifier.SessionExpiryInterval) is not { } property)
        {
            return true;
        }

        if (session!.Expiry == TimeSpan.Zero && property.Integer != 0)
        {
            return false;
        }

        session.Expiry = TimeSpan.FromSeconds(Math.Min(property.Integer, Mqtt5Broker.MaxSessionExpirySeconds));
        return true;
    }

    // Section 4.12.1: re-authentication starts with AUTH 0x19 and continues with AUTH 0x18, both
    // naming the CONNECT's method, while other packets keep flowing. On a connection that used no
    // method any AUTH is a Protocol Error ([MQTT-4.12.0-7]).
    private async ValueTask<Mqtt5ConnectionEnd> AnswerAuthenticationAsync(Mqtt5ReasonCodeAndPropertiesPacket authentication, Mqtt5ConnectedClient client)
    {
        if (!ContinuesTheAuthentication(authentication, client.AuthenticationMethod))
        {
            return await DisconnectAsync(Mqtt5ReasonCode.ProtocolError, "An AUTH did not continue the client's authentication ([MQTT-4.12.0-7], [MQTT-4.12.1-1])");
        }

        var method = client.AuthenticationMethod!;
        reauthentication ??= new Mqtt5EnhancedAuthentication(saslAuthenticationPolicy!, method, context, connection.TlsSession);
        var step = await reauthentication.StepAsync(AuthenticationDataOf(authentication.Properties));
        if (step.Outcome == Mqtt5AuthenticationOutcome.Continue)
        {
            await WriteAsync(Authentication(Mqtt5ReasonCode.ContinueAuthentication, method, step.Data));
            return Mqtt5ConnectionEnd.StaysOpen;
        }

        reauthentication = null;
        return await EndReauthenticationAsync(step, client, method);
    }

    private bool ContinuesTheAuthentication(Mqtt5ReasonCodeAndPropertiesPacket authentication, string? method)
    {
        var expected = reauthentication is null ? Mqtt5ReasonCode.Reauthenticate : Mqtt5ReasonCode.ContinueAuthentication;
        return method is not null && authentication.ReasonCode == expected && MethodOf(authentication) == method;
    }

    // The session belongs to the account the CONNECT authenticated, so re-authentication must
    // authenticate the same one.
    private async ValueTask<Mqtt5ConnectionEnd> EndReauthenticationAsync(Mqtt5AuthenticationStep step, Mqtt5ConnectedClient client, string method)
    {
        if (step.Outcome == Mqtt5AuthenticationOutcome.Accepted && step.Account == client.Account)
        {
            await WriteAsync(Authentication(Mqtt5ReasonCode.Success, method, step.Data));
            return Mqtt5ConnectionEnd.StaysOpen;
        }

        return await DisconnectAsync(Mqtt5ReasonCode.NotAuthorized, "Re-authentication failed ([MQTT-4.12.1-2])");
    }

    // Section 4.3: the message is taken, then QoS 1 is answered PUBACK and QoS 2 PUBREC.
    private async ValueTask<Mqtt5ConnectionEnd> AnswerPublishAsync(Mqtt5PublishPacket publish)
    {
        var verdict = inbound!.Judge(publish);
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
        // Published through the session's own bus connection, so its No Local subscriptions miss it.
        var result = session!.BusConnection.Publish(Mqtt5BusMessages.ToBusMessage(message, context.TimeProvider.GetUtcNow()));
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
            inbound!.AwaitRelease(publish.PacketIdentifier);
        }

        var responseType = publish.QualityOfService == 1 ? Mqtt5PacketType.PublishAcknowledgement : Mqtt5PacketType.PublishReceived;
        await WriteAsync(new Mqtt5PublishResponsePacket(responseType, publish.PacketIdentifier, reasonCode, []));
        return Mqtt5ConnectionEnd.StaysOpen;
    }

    // [MQTT-4.3.3-11]: PUBREL is answered PUBCOMP; 0x92 for an identifier awaiting none.
    private async ValueTask<Mqtt5ConnectionEnd> AnswerReleaseAsync(Mqtt5PublishResponsePacket release)
    {
        var reasonCode = inbound!.Release(release.PacketIdentifier) ? Mqtt5ReasonCode.Success : Mqtt5ReasonCode.PacketIdentifierNotFound;
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
            session!.Offer(delivery.Message, delivery.QualityOfService, delivery.Retain, delivery.SubscriptionIdentifiers);
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
        inbound!.IsAwaitingRelease(subscribe.PacketIdentifier)
            ? [.. subscribe.Subscriptions.Select(_ => Mqtt5ReasonCode.PacketIdentifierInUse)]
            : [.. subscribe.Subscriptions.Select(subscription => session!.Subscriptions.Subscribe(subscription, subscriptionIdentifier, retained))];

    // Section 3.10.4: each filter identical to a subscription deletes it; UNSUBACK has one code a filter.
    private async ValueTask<Mqtt5ConnectionEnd> AnswerUnsubscribeAsync(Mqtt5UnsubscribePacket unsubscribe)
    {
        var reasonCodes = inbound!.IsAwaitingRelease(unsubscribe.PacketIdentifier)
            ? unsubscribe.TopicFilters.Select(_ => Mqtt5ReasonCode.PacketIdentifierInUse).ToList()
            : unsubscribe.TopicFilters.Select(session!.Subscriptions.Unsubscribe).ToList();
        await WriteAsync(new Mqtt5ReasonCodeListPacket(Mqtt5PacketType.UnsubscribeAcknowledgement, unsubscribe.PacketIdentifier, [], reasonCodes));
        return Mqtt5ConnectionEnd.StaysOpen;
    }

    // A refused second CONNECT is a Protocol Error whatever made it unreadable ([MQTT-3.1.0-2]).
    private ValueTask<Mqtt5ConnectionEnd> DisconnectForRefusedPacketAsync(Mqtt5PacketType? refusedPacketType, Mqtt5PacketFault fault) =>
        refusedPacketType == Mqtt5PacketType.Connect
            ? DisconnectAsync(Mqtt5ReasonCode.ProtocolError, "A second CONNECT arrived")
            : DisconnectAsync(fault.ReasonCode, $"A packet was refused ({fault.Statement})");

    // Section 4.13: the broker's DISCONNECT with the reason code, then the close.
    private async ValueTask<Mqtt5ConnectionEnd> DisconnectAsync(Mqtt5ReasonCode reasonCode, string why)
    {
        context.Log.Note($"{why}; sent DISCONNECT 0x{(byte)reasonCode:X2} and closed.");
        await StopDeliveriesAsync();
        await WriteAsync(new Mqtt5ReasonCodeAndPropertiesPacket(Mqtt5PacketType.Disconnect, reasonCode, []));
        return Mqtt5ConnectionEnd.ClosedAbnormally;
    }

    // Section 3.1.2.5: the Will is published once the lesser of its delay and the session's
    // expiry has passed, unless the client ended the connection with DISCONNECT 0x00. The
    // session is kept for its expiry, or ends now.
    private void EndConnection(Mqtt5ConnectedClient client, Mqtt5ConnectionEnd end)
    {
        state.UnregisterClient(client.ClientIdentifier, client.Registration);
        var sessionExpiry = session!.Expiry;
        state.CloseSession(session, context.TimeProvider);
        if (client.Will is not { } will || end == Mqtt5ConnectionEnd.ClosedNormally)
        {
            return;
        }

        var delay = client.WillDelay < sessionExpiry ? client.WillDelay : sessionExpiry;
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
