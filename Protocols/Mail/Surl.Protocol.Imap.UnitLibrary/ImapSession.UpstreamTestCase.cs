using System.Globalization;
using System.Text;
using Surl.LineProtocol;

namespace Surl.Protocol.Imap;

/// <summary>
/// What an upstream test case's reply changes in a session (<see cref="ImapUpstreamTestCaseServer"/>;
/// ADR-0080 decision 2): the greeting <c>REPLY welcome</c> gives, a <c>REPLY</c>'d line answered
/// with the case's text and not run, <c>CAPABILITY</c> from the case's <c>CAPA</c> and
/// <c>AUTH</c>, <c>LOGIN</c> accepting anyone, and the mailbox commands answered from the case's
/// data with no login: <c>FETCH</c> sends it as the message, <c>LIST</c>, <c>LSUB</c>,
/// <c>EXAMINE</c>, <c>STATUS</c>, <c>SEARCH</c> and <c>UID SEARCH</c>/<c>STORE</c>/<c>COPY</c> as
/// their untagged lines, and <c>APPEND</c>'s message is recorded rather than stored. Every other
/// answer is the session's own.
/// </summary>
internal sealed partial class ImapSession
{
    private const string UidValidity = "1";

    // The UID forms answered with the selected mailbox's data.
    private static readonly HashSet<string> UidDataCommands = new(StringComparer.Ordinal) { "SEARCH", "STORE", "COPY" };

    private string caseSelectedMailbox = string.Empty;

    private void AnswerAsTheUpstreamTestCaseSays(MailUpstreamTestCaseReplies replies)
    {
        const ImapCommandState any = ImapCommandState.Any;
        commands["CAPABILITY"] = (any, arguments => RespondAsync(UpstreamTestCaseCapability(arguments, replies)));
        commands["LOGIN"] = (any, arguments => RespondAsync(UpstreamTestCaseLogin(arguments)));
        commands["SELECT"] = (any, arguments => RespondAsync(UpstreamTestCaseSelect(arguments)));
        commands["EXAMINE"] = (any, arguments => AnswerWithUpstreamTestCaseDataAsync(arguments, replies, "OK [READ-ONLY] EXAMINE completed"));
        commands["LIST"] = (any, arguments => AnswerWithUpstreamTestCaseDataAsync(arguments, replies, ImapResponses.Completed("LIST")));
        commands["LSUB"] = (any, arguments => AnswerWithUpstreamTestCaseDataAsync(arguments, replies, ImapResponses.Completed("LSUB")));
        commands["STATUS"] = (any, arguments => AnswerWithUpstreamTestCaseDataAsync(arguments, replies, ImapResponses.Completed("STATUS")));
        commands["SEARCH"] = (any, arguments => AnswerWithSelectedMailboxDataAsync(arguments, replies, ImapResponses.Completed("SEARCH")));
        commands["FETCH"] = (any, arguments => AnswerUpstreamTestCaseFetchAsync(arguments, replies, isUid: false));
        commands["STORE"] = (any, arguments => RespondAsync(UpstreamTestCaseStore(arguments)));
        commands["UID"] = (any, arguments => AnswerUpstreamTestCaseUidAsync(arguments, replies));
        foreach (var name in (string[])["CHECK", "CLOSE", "EXPUNGE", "UNSELECT", "CREATE", "DELETE", "RENAME", "SUBSCRIBE", "UNSUBSCRIBE", "COPY", "MOVE"])
        {
            commands[name] = (any, _ => RespondAsync(ImapResponse.Only(ImapResponses.Completed(name))));
        }
    }

    private async Task GreetAsync()
    {
        if (caseReplies is null)
        {
            await WriteLineAsync($"* OK [CAPABILITY {Capabilities}] {ImapResponses.Greeting}", CancellationToken);
            return;
        }

        // No capabilities in the greeting: the client asks with CAPABILITY, as upstream's server makes it.
        await (caseReplies.TakeWelcome() is { } welcome
            ? connection.WriteAsync(welcome, CancellationToken)
            : WriteLineAsync($"* OK {ImapResponses.Greeting}", CancellationToken));
    }

    // Every command is recorded as upstream's server logs it, the literals' markers put back;
    // a REPLY'd one is answered and not run, the match made on the text after the tag.
    private async ValueTask<bool> AnswerFromTheUpstreamTestCaseAsync(MailUpstreamTestCaseReplies replies, ImapCommandText text, long appendLength)
    {
        var lines = text.Lines.Select((line, index) => index < text.Literals.Count || (appendLength > 0 && index == text.Lines.Count - 1)
            ? [.. line, .. Encoding.ASCII.GetBytes(LiteralMarker(index < text.Literals.Count ? text.Literals[index].Length : appendLength))]
            : line).ToList();
        for (var index = 0; index < lines.Count; index++)
        {
            replies.RecordCommandLine(index < text.Literals.Count ? [.. lines[index], .. "\r\n"u8, .. text.Literals[index]] : lines[index]);
        }

        var first = Encoding.Latin1.GetString(lines[0]);
        var space = first.IndexOf(' ');
        if (replies.TakeScriptedReply(space < 0 ? first : first[(space + 1)..]) is not { } reply)
        {
            return false;
        }

        await connection.WriteAsync(reply, CancellationToken);
        return true;
    }

    private static string LiteralMarker(long length) => "{" + length.ToString(CultureInfo.InvariantCulture) + "}";

    // The case's capabilities and its mechanisms as AUTH= words; with neither, CAPABILITY is not a
    // command, as upstream's test server answers it.
    private static ImapResponse UpstreamTestCaseCapability(ImapArguments arguments, MailUpstreamTestCaseReplies replies)
    {
        if (!replies.OffersCapabilities)
        {
            return ImapResponse.Only(ImapResponses.NotRecognized);
        }

        var words = new[] { "IMAP4rev1" }.Concat(replies.Capabilities).Concat(replies.AuthMechanisms.Select(mechanism => "AUTH=" + mechanism));
        return arguments.IsAtEnd
            ? new ImapResponse(["* CAPABILITY " + string.Join(' ', words)], ImapResponses.Completed("CAPABILITY"))
            : InvalidArguments;
    }

    private static ImapResponse UpstreamTestCaseLogin(ImapArguments arguments) =>
        arguments.ReadSpacedAString() is not null && arguments.ReadSpacedAString() is not null && arguments.IsAtEnd
            ? ImapResponse.Only(LoginCompleted)
            : InvalidArguments;

    // Any mailbox exists and holds one message, the case's data for its name.
    private ImapResponse UpstreamTestCaseSelect(ImapArguments arguments)
    {
        if (arguments.ReadSpacedAString() is not { } mailbox || !arguments.IsAtEnd)
        {
            return InvalidArguments;
        }

        caseSelectedMailbox = Encoding.Latin1.GetString(mailbox);
        return new ImapResponse(
            ["* FLAGS " + ImapResponses.SystemFlags, "* 1 EXISTS", "* 0 RECENT", $"* OK [UIDVALIDITY {UidValidity}] UIDs valid", "* OK [UIDNEXT 2] Predicted next UID"],
            "OK [READ-WRITE] SELECT completed");
    }

    // The flags +FLAGS adds, on a message that has none of its own.
    private static ImapResponse UpstreamTestCaseStore(ImapArguments arguments)
    {
        var rest = RestOfLine(arguments);
        var words = rest?.Split(' ', 3);
        return words is { Length: 3 } && words[1].StartsWith('+')
            ? new ImapResponse([$"* {words[0]} FETCH (FLAGS ({words[2].Trim('(', ')')}))"], ImapResponses.Completed("STORE"))
            : InvalidArguments;
    }

    // The data the command's first argument (a mailbox, a reference) names, as untagged lines.
    private async ValueTask<bool> AnswerWithUpstreamTestCaseDataAsync(ImapArguments arguments, MailUpstreamTestCaseReplies replies, string completion)
    {
        var rest = RestOfLine(arguments);
        if (rest is null)
        {
            return await RespondAsync(InvalidArguments);
        }

        var name = rest.Split(' ')[0].Trim('"');
        await connection.WriteAsync(replies.FindData(name), CancellationToken);
        return await ReplyAsync($"{tag} {completion}");
    }

    private async ValueTask<bool> AnswerWithSelectedMailboxDataAsync(ImapArguments arguments, MailUpstreamTestCaseReplies replies, string completion)
    {
        if (RestOfLine(arguments) is null)
        {
            return await RespondAsync(InvalidArguments);
        }

        await connection.WriteAsync(replies.FindData(caseSelectedMailbox), CancellationToken);
        return await ReplyAsync($"{tag} {completion}");
    }

    private async ValueTask<bool> AnswerUpstreamTestCaseUidAsync(ImapArguments arguments, MailUpstreamTestCaseReplies replies)
    {
        var name = arguments.TryReadSpace() ? arguments.ReadAtom() : null;
        if (name == "FETCH")
        {
            return await AnswerUpstreamTestCaseFetchAsync(arguments, replies, isUid: true);
        }

        return UidDataCommands.Contains(name ?? string.Empty)
            ? await AnswerWithSelectedMailboxDataAsync(arguments, replies, ImapResponses.Completed("UID " + name))
            : await RespondAsync(InvalidArguments);
    }

    // One message, the selected mailbox's data, as the item asked for - BODY.PEEK answered as
    // BODY, as RFC 3501 has it - then POSTFETCH's text inside the closing parenthesis.
    private async ValueTask<bool> AnswerUpstreamTestCaseFetchAsync(ImapArguments arguments, MailUpstreamTestCaseReplies replies, bool isUid)
    {
        // Upstream's ftpserver.pl takes the command's first argument as the message number and the
        // rest as the item, both echoed as sent; for UID FETCH the "arguments" begin at FETCH.
        var rest = RestOfLine(arguments);
        var words = (isUid && rest is not null ? "FETCH " + rest : rest)?.Split(' ', 2);
        if (words is not { Length: 2 })
        {
            return await RespondAsync(InvalidArguments);
        }

        var message = replies.FindData(caseSelectedMailbox);
        var head = Encoding.Latin1.GetBytes($"* {words[0]} FETCH ({words[1]} {LiteralMarker(message.Length)}\r\n");
        var tail = Encoding.Latin1.GetBytes(replies.PostFetch + ")\r\n");
        await connection.WriteAsync((byte[])[.. head, .. message, .. tail], CancellationToken);
        return await ReplyAsync($"{tag} {ImapResponses.Completed(isUid ? "UID FETCH" : "FETCH")}");
    }

    // The rest of the command line after one space, as sent; null when there is none.
    private static string? RestOfLine(ImapArguments arguments) =>
        arguments.TryReadSpace() ? arguments.ReadRun(_ => true) : null;

    // APPEND's message is read as the session reads any, and recorded rather than stored.
    private async ValueTask<bool> AnswerUpstreamTestCaseAppendAsync(MailUpstreamTestCaseReplies replies, ImapCommandReadResult read)
    {
        // ImapCommandReader gives an APPEND message's outcome only with the tag it read.
        tag = read.Tag!;
        using var message = new MemoryStream();
        var end = await commandReader.ReadAppendMessageAsync(read.MessageLength, message, CancellationToken);
        if (end.Outcome != ImapCommandReadOutcome.CommandRead)
        {
            return false;
        }

        replies.RecordUpload(message.ToArray());
        return await ReplyAsync($"{tag} {ImapResponses.Completed("APPEND")}");
    }
}
