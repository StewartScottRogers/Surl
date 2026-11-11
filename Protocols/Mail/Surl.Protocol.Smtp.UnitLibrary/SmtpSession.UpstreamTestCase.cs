using System.Globalization;
using System.Text;
using Surl.LineProtocol;

namespace Surl.Protocol.Smtp;

/// <summary>
/// What an upstream test case's reply changes in a session (<see cref="SmtpUpstreamTestCaseServer"/>;
/// ADR-0080 decision 2): the greeting <c>REPLY welcome</c> gives, a <c>REPLY</c>'d line answered
/// with the case's text and not run, <c>EHLO</c>'s extensions from <c>CAPA</c> and <c>AUTH</c>,
/// mail sent without a login, a <c>DATA</c> body recorded rather than delivered, and <c>VRFY</c>
/// and <c>EXPN</c> answered with the case's data, a <c>RCPT</c> with no at sign refused, and a <c>MAIL</c>
/// whose <c>SIZE=</c> exceeds the <c>SIZE</c> the case's <c>CAPA</c> advertised refused, as upstream's
/// <c>MAIL_smtp</c> and <c>RCPT_smtp</c> do. Every other answer is the session's own.
/// </summary>
internal sealed partial class SmtpSession
{
    // ftpserver.pl at curl-8_21_0, subs MAIL_smtp and RCPT_smtp.
    private const string UpstreamInvalidAddress = "501 Invalid address";
    private const string UpstreamMessageSizeTooLarge = "552 Message size too large";

    private void AnswerAsTheUpstreamTestCaseSays(MailUpstreamTestCaseReplies replies)
    {
        commands["MAIL"] = argument => AnswerUpstreamTestCaseMailAsync(argument, replies);
        var answerRecipient = commands["RCPT"];
        commands["RCPT"] = argument => HasAtSign(argument) ? answerRecipient(argument) : ReplyAsync(UpstreamInvalidAddress);
        commands["DATA"] = argument => AnswerUpstreamTestCaseDataAsync(argument, replies);
        commands["VRFY"] = argument => AnswerFromTheUpstreamTestCaseDataAsync(argument, replies, SmtpReplies.VrfySyntax, SmtpReplies.VrfyAnswer);
        commands["EXPN"] = argument => AnswerFromTheUpstreamTestCaseDataAsync(argument, replies, SmtpReplies.ExpnSyntax, SmtpReplies.ExpnAnswer);
    }

    private static bool HasAtSign(byte[]? argument) => argument is not null && argument.AsSpan().Contains((byte)'@');

    // MAIL_smtp: a SIZE= past the SIZE the case advertised is refused, else the session answers.
    private ValueTask<bool> AnswerUpstreamTestCaseMailAsync(byte[]? argument, MailUpstreamTestCaseReplies replies)
    {
        var advertised = replies.Capabilities.Select(capability => ReadNumberAfter(capability, "SIZE ")).FirstOrDefault(size => size is not null);
        var requested = Encoding.Latin1.GetString(argument ?? []).Split(' ').Select(parameter => ReadNumberAfter(parameter, "SIZE=")).FirstOrDefault(size => size is not null);
        return advertised < requested ? ReplyAsync(UpstreamMessageSizeTooLarge) : AnswerMailAsync(argument);
    }

    private static long? ReadNumberAfter(string text, string prefix) =>
        text.StartsWith(prefix, StringComparison.Ordinal) && long.TryParse(text.AsSpan(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : null;

    private async Task GreetAsync()
    {
        if (caseReplies?.TakeWelcome() is { } welcome)
        {
            await connection.WriteAsync(welcome, CancellationToken);
            return;
        }

        await WriteLineAsync(SmtpReplies.Greeting, CancellationToken);
    }

    // Every line is recorded as upstream's server logs it; a REPLY'd one is answered and not run.
    private async ValueTask<bool> AnswerFromTheUpstreamTestCaseAsync(MailUpstreamTestCaseReplies replies, byte[] line)
    {
        replies.RecordCommandLine(line);
        if (replies.TakeScriptedReply(Encoding.Latin1.GetString(line)) is not { } reply)
        {
            return false;
        }

        await connection.WriteAsync(reply, CancellationToken);
        NoteTheSenderOrRecipientAnUpstreamTestCaseRepliedTo(line);
        return true;
    }

    // Upstream's DATA_smtp stores the upload however MAIL and RCPT were answered, so a REPLY'd
    // MAIL opens the transaction and a REPLY'd RCPT counts as a recipient, for DATA to need.
    private void NoteTheSenderOrRecipientAnUpstreamTestCaseRepliedTo(byte[] line)
    {
        var text = Encoding.Latin1.GetString(line);
        var verb = text.Split(' ', 2)[0];
        if (verb.Equals("MAIL", StringComparison.OrdinalIgnoreCase))
        {
            transaction = new SmtpMailTransaction(string.Empty);
        }
        else if (verb.Equals("RCPT", StringComparison.OrdinalIgnoreCase))
        {
            transaction ??= new SmtpMailTransaction(string.Empty);
            transaction.AddRecipient(string.Empty, null);
        }
    }

    // The session's own hello line, then the case's capabilities and mechanisms (FILEFORMAT's CAPA and AUTH).
    private static List<string> UpstreamTestCaseEhloReplyLines(MailUpstreamTestCaseReplies replies)
    {
        List<string> capabilities = [SmtpReplies.HeloAccepted[4..], .. replies.Capabilities];
        if (replies.AuthMechanisms.Count > 0)
        {
            capabilities.Add("AUTH " + string.Join(' ', replies.AuthMechanisms));
        }

        return capabilities.Select((capability, index) => (index == capabilities.Count - 1 ? "250 " : "250-") + capability).ToList();
    }

    // The body is read as the session reads any, then recorded as it was sent rather than delivered.
    private async ValueTask<bool> AnswerUpstreamTestCaseDataAsync(byte[]? argument, MailUpstreamTestCaseReplies replies)
    {
        if (argument is not null)
        {
            return await ReplyAsync(SmtpReplies.TakesNoArgument("DATA"));
        }

        if (transaction is not { AcceptedRecipientCount: > 0 })
        {
            return await ReplyAsync(SmtpReplies.SendRecipientFirst);
        }

        transaction = null;
        await WriteLineAsync(SmtpReplies.StartData, CancellationToken);
        using var body = new MemoryStream();
        var read = await reader.ReadDotStuffedBodyAsync(body, CancellationToken);
        if (read.Outcome == DotStuffedBodyReadOutcome.Closed)
        {
            return false;
        }

        if (read.Outcome == DotStuffedBodyReadOutcome.BodyTooLarge)
        {
            await WriteLimitReplyAsync(SmtpReplies.MessageTooLarge);
            return false;
        }

        replies.RecordDotStuffedUpload(body.ToArray());
        return await ReplyAsync(SmtpReplies.MessageAccepted);
    }

    // The case's data for the client's EHLO name is the answer, as written; without any, the session's own.
    private async ValueTask<bool> AnswerFromTheUpstreamTestCaseDataAsync(byte[]? argument, MailUpstreamTestCaseReplies replies, string syntaxReply, string ownReply)
    {
        if (argument is null)
        {
            return await ReplyAsync(syntaxReply);
        }

        var data = replies.FindData(Encoding.Latin1.GetString(heloDomain ?? []));
        if (data.Length == 0)
        {
            return await ReplyAsync(ownReply);
        }

        await connection.WriteAsync(data, CancellationToken);
        return true;
    }
}
