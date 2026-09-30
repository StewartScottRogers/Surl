using System.Globalization;
using System.Text;
using Surl.LineProtocol;
using Surl.MailStore;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Smtp;

/// <summary>
/// One SMTP connection's session (ADR-0053): the greeting, then every command line answered in
/// order until <c>QUIT</c>, the peer's close, or a limit.
/// </summary>
internal sealed class SmtpSession
{
    // A path with no domain (RFC 5321 notwithstanding, curl sends --mail-rcpt bob as <bob>) is
    // looked up as if it named surl's own domain, which the store ignores (ADR-0053, decision 4).
    private const string DomainlessPathSuffix = "@surl";

    private static readonly string[] NotImplementedVerbs = ["AUTH", "BDAT", "ETRN", "TURN", "ATRN", "SEND", "SOML", "SAML", "VERB"];

    private readonly IConnection connection;
    private readonly ExchangeContext context;
    private readonly CrlfLineReader reader;
    private readonly IAuthenticationPolicy authenticationPolicy;
    private readonly MailboxStore mailStore;
    private readonly Dictionary<string, Func<byte[]?, ValueTask<bool>>> commands;
    private byte[]? heloDomain;
    private bool isExtendedHello;
    private PasswordLoginVerdict? mailLoginVerdict;
    private SmtpMailTransaction? transaction;

    public SmtpSession(IConnection connection, ExchangeContext context, CrlfLineReader reader, IAuthenticationPolicy authenticationPolicy, MailboxStore mailStore)
    {
        this.connection = connection;
        this.context = context;
        this.reader = reader;
        this.authenticationPolicy = authenticationPolicy;
        this.mailStore = mailStore;
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
            ["STARTTLS"] = argument => ReplyAsync(AnswerStartTls(argument)),
        };
        foreach (var verb in NotImplementedVerbs)
        {
            commands[verb] = _ => ReplyAsync(SmtpReplies.NotImplemented);
        }
    }

    private CancellationToken CancellationToken => context.CancellationToken;

    // RFC 3848's word for the session, as the Received field names it.
    private string ReceivedProtocol => (isExtendedHello ? "ESMTP" : "SMTP") + (connection.TlsSession is null ? string.Empty : "S");

    /// <summary>
    /// Sends the greeting, then answers every command line until the session ends.
    /// </summary>
    /// <returns>A task that completes when the session is over.</returns>
    public async Task RunAsync()
    {
        await WriteLineAsync(SmtpReplies.Greeting, CancellationToken);
        while (await AnswerNextLineAsync())
        {
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

    // ADR-0053 decision 2's list, without STARTTLS (BL-199) and AUTH (BL-200).
    private string[] EhloReplyLines() =>
    [
        "250-surl Hello",
        "250-SIZE " + context.Limits.MaxUploadBytes.ToString(CultureInfo.InvariantCulture),
        "250-8BITMIME",
        "250-SMTPUTF8",
        "250-PIPELINING",
        "250 ENHANCEDSTATUSCODES",
    ];

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

    // Asks the policy once per session, with the login that carries no credentials (ADR-0053, decision 3).
    private async ValueTask<bool> IsMailAllowedAsync()
    {
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

    private async ValueTask<bool> ReceiveMessageAsync(SmtpMailTransaction accepted)
    {
        transaction = null;
        var traceFields = SmtpTraceFields.Build(accepted.ReversePath, heloDomain!, connection.RemoteEndPoint, ReceivedProtocol, context.TimeProvider.GetUtcNow());
        await WriteLineAsync(SmtpReplies.StartData, CancellationToken);

        var maxUploadBytes = context.Limits.MaxUploadBytes;
        await using var body = new SmtpMessageBodyBuffer(maxUploadBytes == 0 ? long.MaxValue : maxUploadBytes - traceFields.Length);
        var read = await reader.ReadDotStuffedBodyAsync(body, CancellationToken);
        if (read.Outcome == DotStuffedBodyReadOutcome.Closed)
        {
            context.Log.Note("The client closed the connection part way through a message; nothing was stored.");
            return false;
        }

        if (read.Outcome == DotStuffedBodyReadOutcome.BodyTooLarge || body.IsPastBudget)
        {
            context.Log.Note($"Message refused: past --max-filesize after {body.ReceivedBytes} bytes");
            await WriteLimitReplyAsync(SmtpReplies.MessageTooLarge);
            return false;
        }

        return await ReplyAsync(await DeliverAsync(accepted, [.. traceFields, .. body.ToArray()]));
    }

    private async ValueTask<string> DeliverAsync(SmtpMailTransaction accepted, byte[] message)
    {
        var outcome = mailStore.Deliver(accepted.Deliverable, message);
        if (outcome == MailStoreOutcome.StoreFull)
        {
            context.Log.Note("Message refused: the mail store is full");
            return SmtpReplies.StoreFull;
        }

        if (outcome == MailStoreOutcome.MessageTooLarge)
        {
            context.Log.Note($"Message refused: past --max-filesize after {message.Length} bytes");
            return SmtpReplies.MessageTooLarge;
        }

        foreach (var path in accepted.Discarded)
        {
            context.Log.Note($"Mail for {SmtpLogText.Render(Encoding.UTF8.GetBytes(path))} discarded: no such account");
        }

        context.Log.Note($"Message stored: {message.Length} bytes for {accepted.Deliverable.Count} recipients");
        await SaveMailStoreAsync();
        return SmtpReplies.MessageAccepted;
    }

    // A store that cannot be written keeps the message in memory; the next save writes it (ADR-0050, decision 7).
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

    private string AnswerStartTls(byte[]? argument) =>
        argument is not null ? SmtpReplies.TakesNoArgument("STARTTLS")
        : connection.TlsSession is not null ? SmtpReplies.AlreadyUsingTls
        : SmtpReplies.TlsNotAvailable;

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
    // aborted (ADR-0006, section 5).
    private async Task WriteLimitReplyAsync(string line)
    {
        using var deadline = new CancellationTokenSource(SmtpProtocolServer.LimitReplyWriteDeadline, context.TimeProvider);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken, deadline.Token);
        try
        {
            await WriteLineAsync(line, cancellation.Token);
            await connection.CompleteWritesAsync(cancellation.Token);
        }
        catch (OperationCanceledException) when (!CancellationToken.IsCancellationRequested)
        {
            context.Log.Note("The reply was not written within the one-second write deadline; the connection was closed.");
        }
    }
}
