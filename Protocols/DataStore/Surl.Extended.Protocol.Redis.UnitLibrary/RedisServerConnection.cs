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
internal sealed class RedisServerConnection(
    IConnection connection,
    ExchangeContext context,
    IAuthenticationPolicy authenticationPolicy,
    long clientId)
{
    private readonly RedisCommandReader reader = new(connection, context.Limits);
    private readonly RedisLogins logins = new(authenticationPolicy, context, connection.TlsSession);

    /// <summary>Answers the connection until it ends.</summary>
    /// <returns>A task that completes when the exchange is over.</returns>
    public async Task ServeAsync()
    {
        using var firstCommandTimeout = StartHeadTimeout();
        var anonymous = await authenticationPolicy.CheckPasswordLoginAsync(
            new PasswordLogin(context.Scheme, null, null, connection.TlsSession), context.CancellationToken);
        var session = new RedisSession(clientId, anonymous == PasswordLoginVerdict.AcceptedUnchecked);

        var outcome = await ReadWithinHeadTimeoutAsync(session, firstCommandTimeout);
        while (outcome is not null && await AnswerAsync(session, outcome))
        {
            outcome = await ReadNextCommandAsync(session);
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
        if (!await reader.WaitForCommandStartAsync(context.CancellationToken))
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
            return await reader.ReadCommandAsync(context.CancellationToken);
        }

        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, headTimeout.Token);
        try
        {
            return await reader.ReadCommandAsync(cancellation.Token);
        }
        catch (OperationCanceledException) when (!context.CancellationToken.IsCancellationRequested)
        {
            context.Log.Note("No complete command within the head timeout; closed with no reply.");
            return null;
        }
    }

    // Whether the connection stays open for another command.
    private async ValueTask<bool> AnswerAsync(RedisSession session, RedisCommandReadOutcome outcome)
    {
        switch (outcome.Kind)
        {
            case RedisCommandReadOutcomeKind.Command:
                return await AnswerCommandAsync(session, outcome.Arguments);
            case RedisCommandReadOutcomeKind.ProtocolError:
                context.Log.Note("A command could not be framed; answered the protocol error and closed.");
                await connection.WriteAsync(outcome.ErrorReply!.Encode(), context.CancellationToken);
                await connection.CompleteWritesAsync(context.CancellationToken);
                return false;
            case RedisCommandReadOutcomeKind.CommandTooLarge:
                context.Log.Note("A command passed --max-filesize plus 64 KiB; closed with no reply.");
                return false;
            default:
                return false;
        }
    }

    private async ValueTask<bool> AnswerCommandAsync(RedisSession session, IReadOnlyList<byte[]> arguments)
    {
        var reply = await ReplyToAsync(session, arguments);
        await connection.WriteAsync(reply.Encode(), context.CancellationToken);
        if (!session.IsQuitting)
        {
            return true;
        }

        await connection.CompleteWritesAsync(context.CancellationToken);
        return false;
    }

    // As Redis: an unknown command and a wrong argument count are answered before the login
    // check, which comes before the command runs.
    private async ValueTask<RespValue> ReplyToAsync(RedisSession session, IReadOnlyList<byte[]> arguments)
    {
        if (!RedisCommandTable.TryFind(arguments, out var command, out var refusal))
        {
            return refusal;
        }

        if (!command.IsServedBeforeLogin && !session.MayRunEveryCommand)
        {
            return RedisErrorReplies.AuthenticationRequired;
        }

        return await command.Answer(new RedisCommandCall(arguments, session, logins));
    }
}
