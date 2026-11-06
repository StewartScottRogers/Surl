using System.Text;
using Surl.LineProtocol;

namespace Surl.Protocol.Pop3;

/// <summary>
/// What an upstream test case's reply changes in a session (<see cref="Pop3UpstreamTestCaseServer"/>;
/// ADR-0080 decision 2): the greeting <c>REPLY welcome</c> gives, a <c>REPLY</c>'d line answered
/// with the case's text and not run, <c>CAPA</c> from the case's <c>CAPA</c> and <c>AUTH</c>,
/// <c>USER</c>, <c>PASS</c> and an offered <c>APOP</c> accepting anyone, and the maildrop
/// commands answered from the case's data with no login: <c>RETR</c> and <c>TOP</c> send it as the
/// message, <c>LIST</c> and <c>UIDL</c> as the listing. Every other answer is the session's own.
/// </summary>
internal sealed partial class Pop3Session
{
    private const string ApopCapability = "APOP";

    private void AnswerAsTheUpstreamTestCaseSays(MailUpstreamTestCaseReplies replies)
    {
        commands["CAPA"] = command => AnswerUpstreamTestCaseCapabilitiesAsync(command, replies);
        commands["USER"] = command => ReplyAsync(command.Argument is null ? Pop3Replies.InvalidArguments : Pop3Replies.UserAccepted);
        commands["PASS"] = command => ReplyAsync(command.Argument is null ? Pop3Replies.InvalidArguments : Pop3Replies.LoggedIn);
        commands["APOP"] = command => AnswerUpstreamTestCaseApopAsync(command, replies);
        commands["STAT"] = command => WithWordsAsync(command, 0, _ => ReplyAsync(Pop3Replies.Status(1, replies.FindData(string.Empty).Length)));
        commands["LIST"] = command => AnswerUpstreamTestCaseListAsync(command, replies);
        commands["UIDL"] = command => WithWordsAsync(command, 0, _ => WriteUpstreamTestCaseBodyAsync(Pop3Replies.UniqueIdListingFollows, replies.FindData(string.Empty)));
        commands["RETR"] = command => WithWordsAsync(command, 1, words =>
            WriteUpstreamTestCaseMessageAsync(replies.FindData(words[0]), Pop3Replies.MessageFollows));
        commands["TOP"] = command => WithWordsAsync(command, 2, words =>
            WriteUpstreamTestCaseMessageAsync(replies.FindData(words[0]), _ => Pop3Replies.TopFollows));
        commands["DELE"] = command => WithWordsAsync(command, 1, _ => ReplyAsync(Pop3Replies.MessageDeleted));
        commands["RSET"] = command => WithWordsAsync(command, 0, _ => ReplyAsync(Pop3Replies.MaildropHas(1, replies.FindData(string.Empty).Length)));
        commands["NOOP"] = command => WithWordsAsync(command, 0, _ => ReplyAsync(Pop3Replies.Ok));
    }

    private async Task GreetAsync()
    {
        if (caseReplies?.TakeWelcome() is { } welcome)
        {
            await connection.WriteAsync(welcome, CancellationToken);
            return;
        }

        await WriteLineAsync(Greeting(), CancellationToken);
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
        return true;
    }

    // The case's capabilities but APOP, which the greeting's timestamp offers, then its SASL
    // mechanisms; with neither, CAPA is not a command (upstream's test server answers so).
    private ValueTask<bool> AnswerUpstreamTestCaseCapabilitiesAsync(Pop3CommandLine command, MailUpstreamTestCaseReplies replies)
    {
        if (!replies.OffersCapabilities)
        {
            return ReplyAsync(Pop3Replies.NotRecognized);
        }

        List<string> capabilities = [.. replies.Capabilities.Where(capability => capability != ApopCapability)];
        if (replies.AuthMechanisms.Count > 0)
        {
            capabilities.Add("SASL " + string.Join(' ', replies.AuthMechanisms));
        }

        return command.Argument is null
            ? WriteMultiLineAsync(Pop3Replies.CapabilityListFollows, capabilities)
            : ReplyAsync(Pop3Replies.InvalidArguments);
    }

    private ValueTask<bool> AnswerUpstreamTestCaseApopAsync(Pop3CommandLine command, MailUpstreamTestCaseReplies replies) =>
        !replies.Capabilities.Contains(ApopCapability) ? ReplyAsync(Pop3Replies.NotRecognized)
        : WithWordsAsync(command, 2, _ => ReplyAsync(Pop3Replies.LoggedIn));

    // LIST alone sends the case's data as the listing; LIST with a message number answers one line.
    private ValueTask<bool> AnswerUpstreamTestCaseListAsync(Pop3CommandLine command, MailUpstreamTestCaseReplies replies)
    {
        if (command.Argument is null)
        {
            var listing = replies.FindData(string.Empty);
            return WriteUpstreamTestCaseBodyAsync(Pop3Replies.ListingFollows(1, listing.Length), listing);
        }

        return WithWordsAsync(command, 1, words => ReplyAsync(Pop3Replies.OneLine(Invariant($"{words[0]} {replies.FindData(words[0]).Length}"))));
    }

    private ValueTask<bool> WithWordsAsync(Pop3CommandLine command, int wordCount, Func<string[], ValueTask<bool>> answer) =>
        command.TrySplitArguments(out var words) && words.Length == wordCount ? answer(words) : ReplyAsync(Pop3Replies.InvalidArguments);

    // A message the case has no data for does not exist.
    private ValueTask<bool> WriteUpstreamTestCaseMessageAsync(byte[] message, Func<long, string> status) =>
        message.Length == 0 ? ReplyAsync(Pop3Replies.NoSuchMessage) : WriteUpstreamTestCaseBodyAsync(status(message.Length), message);

    // The case's data is written as the case gives it, as upstream's test server writes it - the
    // case holds a message's lines as they go on the wire - then the ending "." CRLF.
    private async ValueTask<bool> WriteUpstreamTestCaseBodyAsync(string status, byte[] body)
    {
        await WriteLineAsync(status, CancellationToken);
        await connection.WriteAsync((byte[])[.. body, .. ".\r\n"u8], CancellationToken);
        return true;
    }
}
