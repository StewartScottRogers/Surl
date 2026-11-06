using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Ftp;

/// <summary>
/// The chosen-error fault (ADR-0124, decision 4): every command the server implements is asked
/// about at a <see cref="FaultStage.Command"/> point once the login check has let it through and
/// before it runs, so before the content store is touched. A matching <see cref="FaultKind.Error"/>
/// rule answers the command with the rule's reply in place of running it, so a transfer command
/// answered that way opens no data connection and changes nothing in the store.
/// </summary>
internal sealed partial class FtpCommandResponder
{
    // The commands whose argument names a file or directory, resolved against the current
    // directory as the command itself resolves it; for the others the point carries no path.
    private static readonly HashSet<string> CommandsNamingAPath = new(StringComparer.Ordinal)
    {
        "CWD", "XCWD", "SIZE", "MDTM", "RETR", "LIST", "NLST", "MLSD", "MLST", "STOR", "APPE",
        "MKD", "XMKD", "RMD", "XRMD", "DELE", "RNFR", "RNTO",
    };

    // The account a checked login named; a login --allow-anonymous accepted unchecked names none.
    private string? loggedInAccount;

    private async ValueTask<bool> AnswerUnlessAFaultAnswersAsync(FtpCommandLine commandLine, Func<byte[]?, ValueTask<bool>> answer)
    {
        var path = FaultPath(commandLine);
        var point = new FaultPoint(FaultStage.Command)
        {
            ExchangeId = context.ExchangeId,
            ListenUrl = FaultListenUrl.Text(context.ListenUrl),
            Scheme = context.Scheme,
            Account = loggedInAccount,
            Path = path,
            Command = commandLine.Command,
        };
        if (await context.Faults.FindAsync(point, context.CancellationToken) is not { Kind: FaultKind.Error, Reply: { } reply } rule)
        {
            return await answer(commandLine.Argument);
        }

        var target = path is null ? commandLine.Command : $"{commandLine.Command} {path}";
        context.Log.Note($"{target} answered by fault rule {rule.Id}: {reply}");
        context.Events.Publish(new FaultAppliedEvent(rule.Id, rule.Kind, FaultStage.Command, context.ExchangeId, target));
        return await ReplyAsync(reply);
    }

    // LIST's and NLST's ls-style options (LIST -la) are not part of the path, as when they run, and
    // a command naming no path concerns the current directory. A path that does not resolve (not
    // UTF-8, or climbing above /) leaves the point without one.
    private string? FaultPath(FtpCommandLine commandLine)
    {
        if (!CommandsNamingAPath.Contains(commandLine.Command))
        {
            return null;
        }

        var resolved = FtpPath.Resolve(currentDirectory, PathArgument(commandLine) ?? []);
        return resolved is null ? null : "/" + string.Join('/', resolved);
    }

    private static byte[]? PathArgument(FtpCommandLine commandLine) =>
        commandLine.Command is "LIST" or "NLST" ? WithoutLsOptions(commandLine.Argument) : commandLine.Argument;
}
