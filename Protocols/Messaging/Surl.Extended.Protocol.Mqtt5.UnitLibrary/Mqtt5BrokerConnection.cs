using System.Collections.Frozen;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Mqtt5;

/// <summary>
/// Answers one connection to the <see cref="Mqtt5Broker"/>, from its <c>CONNECT</c> to its end.
/// <see cref="Mqtt5Broker"/> documents the answers.
/// </summary>
internal sealed class Mqtt5BrokerConnection
{
    private const double KeepAliveGraceFactor = 1.5;

    // Packets a client never sends once connected (section 3.1.0, [MQTT-4.12.0-7]): a second
    // CONNECT, the packets only a server sends, and AUTH on a connection that used no method.
    private static readonly FrozenSet<Mqtt5PacketType> ProtocolErrorsFromAClient = new[]
    {
        Mqtt5PacketType.Connect,
        Mqtt5PacketType.ConnectAcknowledgement,
        Mqtt5PacketType.SubscribeAcknowledgement,
        Mqtt5PacketType.UnsubscribeAcknowledgement,
        Mqtt5PacketType.PingResponse,
        Mqtt5PacketType.Authentication,
    }.ToFrozenSet();

    private readonly IConnection connection;
    private readonly ExchangeContext context;
    private readonly Mqtt5BrokerState state;
    private readonly IAuthenticationPolicy authenticationPolicy;
    private readonly Mqtt5PacketReader reader;

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

        var end = Mqtt5ConnectionEnd.ClosedAbnormally;
        try
        {
            end = await ServeConnectedAsync(client);
        }
        finally
        {
            EndConnection(client, end);
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
        if (Mqtt5ConnectJudge.FaultIn(connect) is { } fault)
        {
            await RefuseConnectAsync(fault);
            return null;
        }

        var loginReasonCode = await CheckLoginAsync(connect);
        if (loginReasonCode != Mqtt5ReasonCode.Success)
        {
            await RefuseConnectAsync(new(loginReasonCode, "3.1.3.5"));
            return null;
        }

        return await AcknowledgeConnectAsync(connect);
    }

    // The policy decides everything (ADR-0093 decision 2, ADR-0032): the broker passes the login
    // as sent and the connection's TLS session, null over mqtt5://.
    private async ValueTask<Mqtt5ReasonCode> CheckLoginAsync(Mqtt5ConnectPacket connect)
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
            PasswordLoginVerdict.Accepted or PasswordLoginVerdict.AcceptedUnchecked => Mqtt5ReasonCode.Success,
            PasswordLoginVerdict.RefusedCredentials => Mqtt5ReasonCode.BadUserNameOrPassword,
            _ => Mqtt5ReasonCode.NotAuthorized,
        };
    }

    private async ValueTask RefuseConnectAsync(Mqtt5PacketFault fault)
    {
        context.Log.Note($"CONNECT refused ({fault.Statement}); answered CONNACK 0x{(byte)fault.ReasonCode:X2} and closed.");
        await WriteAsync(new Mqtt5ConnectAcknowledgementPacket(false, fault.ReasonCode, []));
    }

    private async ValueTask<Mqtt5ConnectedClient> AcknowledgeConnectAsync(Mqtt5ConnectPacket connect)
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
            Mqtt5ConnectAcknowledgementProperties.KeepAliveInForce(connect),
            Mqtt5ConnectAcknowledgementProperties.SessionExpiryInForce(connect),
            connect.Will is null ? null : Mqtt5ApplicationMessage.FromWill(connect.Will),
            TimeSpan.FromSeconds(Mqtt5ConnectJudge.Find(connect.Will?.Properties ?? [], Mqtt5PropertyIdentifier.WillDelayInterval)?.Integer ?? 0));
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

    private ValueTask<Mqtt5ConnectionEnd> AnswerAsync(Mqtt5Packet packet)
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

        return ProtocolErrorsFromAClient.Contains(packet.Type)
            ? DisconnectAsync(Mqtt5ReasonCode.ProtocolError, $"A client sent {packet.Type} after CONNACK")
            : DisconnectAsync(Mqtt5ReasonCode.ImplementationSpecificError, $"{packet.Type} is not answered by this broker yet");
    }

    private async ValueTask<Mqtt5ConnectionEnd> AnswerPingAsync()
    {
        await WriteAsync(new Mqtt5PingPacket(Mqtt5PacketType.PingResponse));
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
            state.Publish(will);
        }
        else
        {
            state.ScheduleWill(client.ClientIdentifier, will, delay, context.TimeProvider);
        }
    }

    private ValueTask WriteAsync(Mqtt5Packet packet) => connection.WriteAsync(Mqtt5PacketEncoder.Encode(packet), context.CancellationToken);
}
