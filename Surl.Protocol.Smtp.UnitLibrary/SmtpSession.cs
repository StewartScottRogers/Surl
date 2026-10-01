using System.Globalization;
using System.Text;
using Surl.LineProtocol;
using Surl.MailStore;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Smtp;

/// <summary>
/// One SMTP connection's session (ADR-0053): the greeting, then every command line answered in
/// order until <c>QUIT</c>, the peer's close, or a limit. A <c>DATA</c> body streams, after its
/// trace fields, into a <see cref="PendingMessage"/> that is delivered or deleted, and a message
/// file that cannot be written is answered <c>451 4.3.0 Local error in processing</c>.
/// </summary>
internal sealed class SmtpSession
{
    // A path with no domain (RFC 5321 notwithstanding, curl sends --mail-rcpt bob as <bob>) is
    // looked up as if it named surl's own domain, which the store ignores (ADR-0053, decision 4).
    private const string DomainlessPathSuffix = "@surl";

    private static readonly string[] NotImplementedVerbs = ["BDAT", "ETRN", "TURN", "ATRN", "SEND", "SOML", "SAML", "VERB"];

    private readonly IConnection connection;
    private readonly ExchangeContext context;
    private readonly CrlfLineReader reader;
    private readonly IAuthenticationPolicy authenticationPolicy;
    private readonly IMailAuthenticationPolicy mailAuthenticationPolicy;
    private readonly MailboxStore mailStore;
    private readonly bool isTlsUpgradeAvailable;
    private readonly Dictionary<string, Func<byte[]?, ValueTask<bool>>> commands;
    private byte[]? heloDomain;
    private bool isExtendedHello;
    private PasswordLoginVerdict? mailLoginVerdict;
    private bool isLoggedIn;
    private SmtpMailTransaction? transaction;

    public SmtpSession(IConnection connection, ExchangeContext context, CrlfLineReader reader, IAuthenticationPolicy authenticationPolicy, IMailAuthenticationPolicy mailAuthenticationPolicy, MailboxStore mailStore, bool isTlsUpgradeAvailable)
    {
        this.connection = connection;
        this.context = context;
        this.reader = reader;
        this.authenticationPolicy = authenticationPolicy;
        this.mailAuthenticationPolicy = mailAuthenticationPolicy;
        this.mailStore = mailStore;
        this.isTlsUpgradeAvailable = isTlsUpgradeAvailable;
        commands = new(StringComparer.Ordinal)
        {
            ["EHLO"] = argument => AnswerHelloAsync(argument, isExtended: true),
            ["HELO"] = argument => AnswerHelloAsync(argument, isExtended: false),
            ["MAIL"] = AnswerMailAsync,
            ["RCPT"] = argument => ReplyAsync(AnswerRecipient(argument)),
            ["DATA"] = AnswerDataAsync,
            ["RSET"] = AnswerResetAsync,
            ["NOOP"] = _ => ReplyAsync(SmtpReplies.Ok),
            ["VRFY"] = argument => ReplyAsync(argument is null ? SmtpReplies.VrfySyntax : SmtpReplies.VrfyAnswer),
            ["EXPN"] = argument => ReplyAsync(argument is null ? SmtpReplies.ExpnSyntax : SmtpReplies.ExpnAnswer),
            ["HELP"] = _ => ReplyAsync(SmtpReplies.Help),
            ["QUIT"] = AnswerQuitAsync,
            ["STARTTLS"] = AnswerStartTlsAsync,
            ["AUTH"] = AnswerAuthAsync,
        };
        foreach (var verb in NotImplementedVerbs)
        {
            commands[verb] = _ => ReplyAsync(SmtpReplies.NotImplemented);
        }
    }

    private CancellationToken CancellationToken => context.CancellationToken;

    // RFC 3848's word for the session, as the Received field names it.
    private string ReceivedProtocol =>
        (isExtendedHello ? "ESMTP" : "SMTP") + (connection.TlsSession is null ? string.Empty : "S") + (isLoggedIn ? "A" : string.Empty);

    /// <summary>
    /// Sends the greeting, then answers every command line until the session ends. An exchange
    /// the engine cancels for a limit - the idle timeout or the maximum exchange duration - is
    /// answered <c>421</c> and closed; one cancelled at shutdown ends with no farewell.
    /// </summary>
    /// <returns>A task that completes when the session is over.</returns>
    public async Task RunAsync()
    {
        try
        {
            await WriteLineAsync(SmtpReplies.Greeting, CancellationToken);
            while (await AnswerNextLineAsync())
            {
            }
        }
        catch (OperationCanceledException) when (context.IsCancelledForALimit)
        {
            context.Log.Note("The exchange was cancelled for a limit; answered 421 and closed.");
            await WriteLimitReplyAsync(SmtpReplies.TimedOut);
        }
    }

    private async ValueTask<bool> AnswerNextLineAsync()
    {
        var read = await reader.ReadLineAsync(CancellationToken);
        if (read.Line is not { } line)
        {
            return await AnswerNoLineAsync(read.Outcome);
        }

        return SmtpCommandLine.TryParse(line, out var command) && commands.TryGetValue(command!.Verb, out var answer)
            ? await answer(command.Argument)
            : await ReplyAsync(SmtpReplies.NotRecognized);
    }

    private async ValueTask<bool> AnswerNoLineAsync(CrlfLineReadOutcome outcome)
    {
        if (outcome == CrlfLineReadOutcome.LineTooLong)
        {
            context.Log.Note($"A command line was longer than {context.Limits.MaxLineBytes} bytes; answered 500 and closed.");
            await WriteLimitReplyAsync(SmtpReplies.LineTooLong);
        }
        else if (outcome == CrlfLineReadOutcome.HeadTimedOut)
        {
            context.Log.Note("A command line was not complete within the head timeout; answered 421 and closed.");
            await WriteLimitReplyAsync(SmtpReplies.HeadTimedOut);
        }

        return false;
    }

    private async ValueTask<bool> AnswerHelloAsync(byte[]? argument, bool isExtended)
    {
        if (!IsDomainArgument(argument))
        {
            return await ReplyAsync(isExtended ? SmtpReplies.EhloSyntax : SmtpReplies.HeloSyntax);
        }

        heloDomain = argument;
        isExtendedHello = isExtended;
        transaction = null;
        foreach (var line in isExtended ? EhloReplyLines() : [SmtpReplies.HeloAccepted])
        {
            await WriteLineAsync(line, CancellationToken);
        }

        return true;
    }

    // The domain is written into the Received field as sent, so it holds no control character.
    private static bool IsDomainArgument(byte[]? argument) =>
        argument is not null && argument.AsSpan().IndexOfAnyInRange((byte)0x00, (byte)0x1F) < 0 && !argument.AsSpan().Contains((byte)0x7F);

    // ADR-0053 decision 2's list: every line but the last is "250-".
    private List<string> EhloReplyLines()
    {
        List<string> capabilities =
        [
            "surl Hello",
            "SIZE " + context.Limits.MaxUploadBytes.ToString(CultureInfo.InvariantCulture),
            "8BITMIME",
            "SMTPUTF8",
            "PIPELINING",
            "ENHANCEDSTATUSCODES",
        ];
        if (CanUpgrade)
        {
            capabilities.Add("STARTTLS");
        }

        // Asked afresh for every EHLO, so the offer grows once the connection is TLS (ADR-0049, section 2).
        var mechanisms = mailAuthenticationPolicy.GetMailLoginOffer(connection.TlsSession).SaslMechanisms;
        if (mechanisms.Count > 0)
        {
            capabilities.Add("AUTH " + string.Join(' ', mechanisms));
        }

        return capabilities.Select((capability, index) => (index == capabilities.Count - 1 ? "250 " : "250-") + capability).ToList();
    }

    // STARTTLS is offered only on a plaintext connection of a server with a certificate (RFC 3207).
    private bool CanUpgrade => isTlsUpgradeAvailable && connection.TlsSession is null;

    private async ValueTask<bool> AnswerMailAsync(byte[]? argument)
    {
        var refusal = heloDomain is null ? SmtpReplies.SendHelloFirst
            : transaction is not null ? SmtpReplies.SenderAlreadyGiven
            : null;
        if (refusal is null && !await IsMailAllowedAsync())
        {
            context.Log.Note("MAIL refused: log in with AUTH first, or give --allow-anonymous");
            refusal = SmtpReplies.AuthenticationRequired;
        }

        return await ReplyAsync(refusal ?? StartTransaction(argument));
    }

    // A session logged in with AUTH may send mail; any other asks the policy once, with the login
    // that carries no credentials (ADR-0053, decision 3).
    private async ValueTask<bool> IsMailAllowedAsync()
    {
        if (isLoggedIn)
        {
            return true;
        }

        mailLoginVerdict ??= await authenticationPolicy.CheckPasswordLoginAsync(
            new PasswordLogin(context.Scheme, null, null, connection.TlsSession), CancellationToken);
        return mailLoginVerdict == PasswordLoginVerdict.AcceptedUnchecked;
    }

    private string StartTransaction(byte[]? argument)
    {
        if (!SmtpPathArgument.TryRead(argument, "FROM:", out var from) || (from!.Path.Length > 0 && LookUp(from.Path, out _) == MailRecipientLookup.InvalidAddress))
        {
            return SmtpReplies.InvalidSender;
        }

        var refusal = SmtpMailParameters.FindRefusal(from.Parameters, context.Limits.MaxUploadBytes);
        if (refusal is null)
        {
            transaction = new SmtpMailTransaction(from.Path);
        }

        return refusal ?? SmtpReplies.SenderAccepted;
    }

    private string AnswerRecipient(byte[]? argument)
    {
        if (transaction is null)
        {
            return SmtpReplies.SendMailFirst;
        }

        if (!SmtpPathArgument.TryRead(argument, "TO:", out var to) || LookUp(to!.Path, out var recipient) == MailRecipientLookup.InvalidAddress)
        {
            return SmtpReplies.InvalidRecipient;
        }

        if (to.Parameters.Count > 0)
        {
            return SmtpReplies.UnsupportedParameter;
        }

        if (transaction.AcceptedRecipientCount >= SmtpMailTransaction.MaxRecipients)
        {
            return SmtpReplies.TooManyRecipients;
        }

        transaction.AddRecipient(to.Path, recipient);
        return SmtpReplies.RecipientAccepted;
    }

    private MailRecipientLookup LookUp(string path, out MailRecipient? recipient)
    {
        var lookup = mailStore.LookUpRecipient(path, out recipient);
        return lookup == MailRecipientLookup.InvalidAddress ? mailStore.LookUpRecipient(path + DomainlessPathSuffix, out recipient) : lookup;
    }

    private async ValueTask<bool> AnswerDataAsync(byte[]? argument)
    {
        if (argument is not null)
        {
            return await ReplyAsync(SmtpReplies.TakesNoArgument("DATA"));
        }

        return transaction is { AcceptedRecipientCount: > 0 } accepted
            ? await ReceiveMessageAsync(accepted)
            : await ReplyAsync(SmtpReplies.SendRecipientFirst);
    }

    // The trace fields, then the body as it is unstuffed, are streamed into a pending message;
    // every path that does not deliver it deletes its pending file (ADR-0050, decision 7).
    private async ValueTask<bool> ReceiveMessageAsync(SmtpMailTransaction accepted)
    {
        transaction = null;
        var traceFields = SmtpTraceFields.Build(accepted.ReversePath, heloDomain!, connection.RemoteEndPoint, ReceivedProtocol, context.TimeProvider.GetUtcNow());
        await WriteLineAsync(SmtpReplies.StartData, CancellationToken);

        using var pending = mailStore.CreatePendingMessage();
        await pending.Body.WriteAsync(traceFields, CancellationToken);
        var read = await reader.ReadDotStuffedBodyAsync(pending.Body, CancellationToken);
        if (read.Outcome == DotStuffedBodyReadOutcome.Closed)
        {
            context.Log.Note("The client closed the connection part way through a message; nothing was stored.");
            return false;
        }

        if (read.Outcome == DotStuffedBodyReadOutcome.BodyTooLarge || IsPastMaxFilesize(pending.Length))
        {
            context.Log.Note($"Message refused: past --max-filesize after {read.BytesWritten} bytes");
            await WriteLimitReplyAsync(SmtpReplies.MessageTooLarge);
            return false;
        }

        return await ReplyAsync(await DeliverAsync(accepted, pending));
    }

    // --max-filesize bounds the trace fields and the body together; 0 is no limit (ADR-0053, decision 6).
    private bool IsPastMaxFilesize(long messageBytes) =>
        context.Limits.MaxUploadBytes > 0 && messageBytes > context.Limits.MaxUploadBytes;

    private async ValueTask<string> DeliverAsync(SmtpMailTransaction accepted, PendingMessage message)
    {
        var outcome = mailStore.Deliver(accepted.Deliverable, message);
        if (outcome != MailStoreOutcome.Succeeded)
        {
            return RefuseMessage(outcome, message);
        }

        foreach (var path in accepted.Discarded)
        {
            context.Log.Note($"Mail for {SmtpLogText.Render(Encoding.UTF8.GetBytes(path))} discarded: no such account");
        }

        context.Log.Note($"Message stored: {message.Length} bytes for {accepted.Deliverable.Count} recipients");
        await SaveMailStoreAsync();
        return SmtpReplies.MessageAccepted;
    }

    // A refused message stores nothing and the session goes on; a pending file that could not be
    // written is answered 451, its reason in a note and never in the reply (ADR-0053, decision 6).
    private string RefuseMessage(MailStoreOutcome outcome, PendingMessage message)
    {
        var (note, reply) = outcome switch
        {
            MailStoreOutcome.StoreFull => ("Message refused: the mail store is full", SmtpReplies.StoreFull),
            MailStoreOutcome.MessageTooLarge => ($"Message refused: past --max-filesize after {message.Length} bytes", SmtpReplies.MessageTooLarge),
            _ => ($"Mail store: {message.StorageFailure}", SmtpReplies.StorageFailed),
        };
        context.Log.Note(note);
        return reply;
    }

    // The message file is in place already; an index that cannot be written keeps the change in
    // memory, and the next save writes it (ADR-0050, decision 7).
    private async Task SaveMailStoreAsync()
    {
        try
        {
            await mailStore.SaveChangesAsync(CancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            context.Log.Note($"Mail store: {exception.Message}");
        }
    }

    private async ValueTask<bool> AnswerResetAsync(byte[]? argument)
    {
        if (argument is not null)
        {
            return await ReplyAsync(SmtpReplies.TakesNoArgument("RSET"));
        }

        transaction = null;
        return await ReplyAsync(SmtpReplies.Reset);
    }

    private async ValueTask<bool> AnswerStartTlsAsync(byte[]? argument)
    {
        var refusal = argument is not null ? SmtpReplies.TakesNoArgument("STARTTLS")
            : connection.TlsSession is not null ? SmtpReplies.AlreadyUsingTls
            : isTlsUpgradeAvailable ? null
            : SmtpReplies.TlsNotAvailable;
        return refusal is null ? await UpgradeToTlsAsync() : await ReplyAsync(refusal);
    }

    // 220, then every byte pipelined after the STARTTLS line thrown away unrun, then the
    // handshake; the session starts over (RFC 3207 section 4.2). A failed handshake throws
    // TlsHandshakeException, which the engine notes (ADR-0053, decision 5).
    private async ValueTask<bool> UpgradeToTlsAsync()
    {
        await WriteLineAsync(SmtpReplies.ReadyToStartTls, CancellationToken);
        var discarded = reader.DiscardBuffered();
        if (discarded > 0)
        {
            context.Log.Note($"Discarded {discarded} bytes sent after STARTTLS");
        }

        await connection.UpgradeToTlsAsync(CancellationToken);
        heloDomain = null;
        isExtendedHello = false;
        mailLoginVerdict = null;
        isLoggedIn = false;
        transaction = null;
        return true;
    }

    // AUTH (RFC 4954) only after EHLO, once, outside a transaction (ADR-0053, decisions 1 and 3).
    private async ValueTask<bool> AnswerAuthAsync(byte[]? argument)
    {
        if (AuthRefusal(argument) is { } refusal)
        {
            return await ReplyAsync(refusal);
        }

        return SmtpAuthArgument.TryRead(argument!, out var auth)
            ? await RunSaslExchangeAsync(auth!)
            : await ReplyAsync(SmtpReplies.CannotDecodeResponse);
    }

    private string? AuthRefusal(byte[]? argument) =>
        !isExtendedHello ? SmtpReplies.SendEhloFirst
        : isLoggedIn ? SmtpReplies.AlreadyAuthenticated
        : transaction is not null ? SmtpReplies.AuthDuringTransaction
        : argument is null ? SmtpReplies.AuthSyntax
        : null;

    // The server frames the exchange - 334 continuations, the client's base64 responses, "*" -
    // and the policy decides every step (ADR-0049, section 6).
    private async ValueTask<bool> RunSaslExchangeAsync(SmtpAuthArgument auth)
    {
        var exchange = mailAuthenticationPolicy.StartSaslExchange(
            new SaslExchangeStart(context.Scheme, auth.Mechanism, auth.InitialResponse, connection.TlsSession));
        var step = await exchange.BeginAsync(CancellationToken);
        while (true)
        {
            if (step.CheckedLogin is { } checkedLogin)
            {
                context.Log.Note(checkedLogin.Note);
            }

            if (step.RefusalNote is { } refusalNote)
            {
                context.Log.Note(refusalNote);
            }

            if (step.Outcome != SaslLoginOutcome.Challenge)
            {
                isLoggedIn = step.Outcome is SaslLoginOutcome.Accepted or SaslLoginOutcome.AcceptedUnchecked;
                return await ReplyAsync(SmtpReplies.LoginEnded(step.Outcome));
            }

            await WriteLineAsync(SmtpReplies.Continuation(step.Challenge.Span), CancellationToken);
            var read = await reader.ReadSaslContinuationAsync(CancellationToken);
            if (read.Response is null)
            {
                return await AnswerNoResponseAsync(read.Outcome);
            }

            step = await exchange.ContinueAsync(read.Response, CancellationToken);
        }
    }

    // A cancel or an undecodable response ends the exchange and the session goes on; a limit or
    // the peer's close ends the session as a command line's would.
    private ValueTask<bool> AnswerNoResponseAsync(SaslContinuationOutcome outcome) =>
        SmtpReplies.SaslExchangeAbandoned(outcome) is { } reply ? ReplyAsync(reply) : AnswerNoLineAsync(AsLineReadOutcome(outcome));

    /// <summary>
    /// The command-line read outcome that ends the session as a continuation read's does.
    /// </summary>
    /// <param name="outcome">How the continuation read ended.</param>
    /// <returns><see cref="CrlfLineReadOutcome.LineTooLong"/> or <see cref="CrlfLineReadOutcome.HeadTimedOut"/>
    /// for those limits, and <see cref="CrlfLineReadOutcome.Closed"/> for anything else.</returns>
    internal static CrlfLineReadOutcome AsLineReadOutcome(SaslContinuationOutcome outcome) => outcome switch
    {
        SaslContinuationOutcome.LineTooLong => CrlfLineReadOutcome.LineTooLong,
        SaslContinuationOutcome.HeadTimedOut => CrlfLineReadOutcome.HeadTimedOut,
        _ => CrlfLineReadOutcome.Closed,
    };

    private async ValueTask<bool> AnswerQuitAsync(byte[]? argument)
    {
        if (argument is not null)
        {
            return await ReplyAsync(SmtpReplies.TakesNoArgument("QUIT"));
        }

        await WriteLineAsync(SmtpReplies.Bye, CancellationToken);
        await connection.CompleteWritesAsync(CancellationToken);
        return false;
    }

    private async ValueTask<bool> ReplyAsync(string line)
    {
        await WriteLineAsync(line, CancellationToken);
        return true;
    }

    private ValueTask WriteLineAsync(string line, CancellationToken cancellationToken) =>
        ReplyLineWriter.WriteAsync(connection, line, cancellationToken);

    // A limit's reply gets SmtpProtocolServer.LimitReplyWriteDeadline to be written, and then
    // writes are completed; a peer that does not read it in time is closed all the same, never
    // aborted (ADR-0006, section 5). The deadline is linked to shutdown, never to the exchange's
    // token: the reply to a limit's cancellation is written after that token is cancelled (ADR-0059).
    private async Task WriteLimitReplyAsync(string line)
    {
        using var deadline = new CancellationTokenSource(SmtpProtocolServer.LimitReplyWriteDeadline, context.TimeProvider);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(context.ShutdownToken, deadline.Token);
        try
        {
            await WriteLineAsync(line, cancellation.Token);
            await connection.CompleteWritesAsync(cancellation.Token);
        }
        catch (OperationCanceledException) when (!context.ShutdownToken.IsCancellationRequested)
        {
            context.Log.Note("The reply was not written within the one-second write deadline; the connection was closed.");
        }
    }
}
