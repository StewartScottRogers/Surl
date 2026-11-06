using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Redis;

/// <summary>
/// Answers one connection to the <see cref="RedisServer"/>, command after command, until it
/// ends. <see cref="RedisServer"/> documents the answers.
/// </summary>
/// <param name="connection">The accepted connection.</param>
/// <param name="context">The exchange's context: its limits, log, clock and cancellation token.</param>
/// <param name="authenticationPolicy">Judges logins, and is asked once whether an anonymous caller is let in.</param>
/// <param name="clientId">The connection's client id.</param>
/// <param name="keyspace">The server's 16 databases, shared by every connection.</param>
/// <param name="pubSub">The server's channels and patterns, shared by every connection.</param>
internal sealed class RedisServerConnection(
    IConnection connection,
    ExchangeContext context,
    IAuthenticationPolicy authenticationPolicy,
    long clientId,
    RedisKeyspace keyspace,
    RedisPubSub pubSub)
{
    private readonly RedisCommandReader reader = new(connection, context.Limits);
    private readonly RedisLogins logins = new(authenticationPolicy, context, connection.TlsSession);
    private readonly RedisReplyWriter writer = new(connection, pubSub.MaxUnsentMessageBytes, context.CancellationToken);

    /// <summary>Answers the connection until it ends, then leaves every channel and pattern it subscribed to.</summary>
    /// <returns>A task that completes when the exchange is over.</returns>
    public async Task ServeAsync()
    {
        using var firstCommandTimeout = StartHeadTimeout();
        var anonymous = await authenticationPolicy.CheckPasswordLoginAsync(
            new PasswordLogin(context.Scheme, null, null, connection.TlsSession), context.CancellationToken);
        var subscriber = new RedisSubscriber(new RedisSession(clientId, anonymous == PasswordLoginVerdict.AcceptedUnchecked), writer);
        try
        {
            await ServeCommandsAsync(subscriber, firstCommandTimeout);
        }
        catch (OperationCanceledException) when (writer.IsClosedByWriter && !context.CancellationToken.IsCancellationRequested)
        {
            context.Log.Note("A subscriber's messages could not be sent in time; closed with no more bytes.");
        }
        finally
        {
            pubSub.UnsubscribeAll(subscriber);
        }
    }

    private async Task ServeCommandsAsync(RedisSubscriber subscriber, CancellationTokenSource? firstCommandTimeout)
    {
        var outcome = await ReadWithinHeadTimeoutAsync(subscriber.Session, firstCommandTimeout);
        while (outcome is not null && await AnswerAsync(subscriber, outcome))
        {
            outcome = await ReadNextCommandAsync(subscriber.Session);
        }
    }

    private CancellationTokenSource? StartHeadTimeout() =>
        context.Limits.HeadTimeout == Timeout.InfiniteTimeSpan
            ? null
            : new CancellationTokenSource(context.Limits.HeadTimeout, context.TimeProvider);

    // A later command is timed from its first byte (ADR-0106 decision 6); the wait for that
    // byte is the engine's idle timeout's.
    private async ValueTask<RedisCommandReadOutcome?> ReadNextCommandAsync(RedisSession session)
    {
        if (!await reader.WaitForCommandStartAsync(writer.Token))
        {
            return RedisCommandReadOutcome.ConnectionClosed;
        }

        using var commandTimeout = StartHeadTimeout();
        return await ReadWithinHeadTimeoutAsync(session, commandTimeout);
    }

    // The command, or null when the head timeout ended the read.
    private async ValueTask<RedisCommandReadOutcome?> ReadWithinHeadTimeoutAsync(RedisSession session, CancellationTokenSource? headTimeout)
    {
        reader.AppliesUnauthenticatedLimits = !session.MayRunEveryCommand;
        if (headTimeout is null)
        {
            return await reader.ReadCommandAsync(writer.Token);
        }

        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(writer.Token, headTimeout.Token);
        try
        {
            return await reader.ReadCommandAsync(cancellation.Token);
        }
        catch (OperationCanceledException) when (!writer.Token.IsCancellationRequested)
        {
            context.Log.Note("No complete command within the head timeout; closed with no reply.");
            return null;
        }
    }

    // Whether the connection stays open for another command.
    private async ValueTask<bool> AnswerAsync(RedisSubscriber subscriber, RedisCommandReadOutcome outcome)
    {
        switch (outcome.Kind)
        {
            case RedisCommandReadOutcomeKind.Command:
                return await AnswerCommandAsync(subscriber, outcome.Arguments);
            case RedisCommandReadOutcomeKind.ProtocolError:
                context.Log.Note("A command could not be framed; answered the protocol error and closed.");
                await writer.WriteAsync(outcome.ErrorReply!.Encode());
                await writer.CompleteWritesAsync();
                return false;
            case RedisCommandReadOutcomeKind.CommandTooLarge:
                context.Log.Note("A command passed --max-filesize plus 64 KiB; closed with no reply.");
                return false;
            default:
                return false;
        }
    }

    private async ValueTask<bool> AnswerCommandAsync(RedisSubscriber subscriber, IReadOnlyList<byte[]> arguments)
    {
        var reply = await ReplyToAsync(subscriber, arguments);
        await writer.WriteAsync(reply.Encode());
        if (!subscriber.Session.IsQuitting)
        {
            return true;
        }

        await writer.CompleteWritesAsync();
        return false;
    }

    // As Redis: an unknown command and a wrong argument count are answered before the login
    // check, which comes before the subscribed-context check, which comes before the command runs.
    private async ValueTask<RespValue> ReplyToAsync(RedisSubscriber subscriber, IReadOnlyList<byte[]> arguments)
    {
        if (!RedisCommandTable.TryFind(arguments, out var command, out var refusal))
        {
            return refusal;
        }

        var session = subscriber.Session;
        if (!command.IsServedBeforeLogin && !session.MayRunEveryCommand)
        {
            return RedisErrorReplies.AuthenticationRequired;
        }

        if (RedisPubSubCommands.RefusalWhileSubscribed(command, subscriber) is { } subscribedRefusal)
        {
            return subscribedRefusal;
        }

        var now = context.TimeProvider.GetUtcNow().ToUnixTimeMilliseconds();
        return await command.Answer(new RedisCommandCall(arguments, session, logins, keyspace, pubSub, subscriber, now));
    }
}
