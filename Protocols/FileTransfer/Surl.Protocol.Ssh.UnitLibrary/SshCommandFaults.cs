using System.Globalization;
using System.Text;
using Surl.Content;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Ssh;

/// <summary>
/// Asks the exchange's fault plan, at one SFTP request or SCP command, whether to hold it, drop
/// the connection or answer it with a chosen error (ADR-0124 decision 4; ADR-0135; ADR-0141): the
/// point names the scheme (<c>sftp</c> or <c>scp</c>), the account logged in, the command (the SFTP
/// packet name, or <c>scp</c>) and the canonical path.
/// </summary>
/// <remarks>
/// A <see cref="FaultKind.Delay"/> or <see cref="FaultKind.Drop"/> rule is applied through
/// <see cref="CommandStageFaults"/>. A <see cref="FaultKind.Error"/> rule whose reply fits its
/// scheme's error shape is applied, and applying it publishes one <see cref="FaultAppliedEvent"/>
/// naming the path, or the command when the request names none. A rule of any other kind, or a
/// reply that does not fit, is noted as not applied and the request is answered as without it.
/// </remarks>
internal static class SshCommandFaults
{
    /// <summary>
    /// Finds the fault for an SFTP request, applying a delay or a drop and publishing the
    /// <see cref="FaultAppliedEvent"/> of a chosen error.
    /// </summary>
    /// <param name="context">The exchange: its fault plan, clocks, event stream, listen URL and log.</param>
    /// <param name="connection">The connection a drop aborts.</param>
    /// <param name="accountName">The account logged in, or <see langword="null"/> for an anonymous caller.</param>
    /// <param name="command">The SFTP packet name (<c>OPEN</c>).</param>
    /// <param name="path">The canonical path the request concerns, or <see langword="null"/> when it names none.</param>
    /// <returns>
    /// The error rule, or the drop rule (the connection is already aborted), or <see langword="null"/>
    /// to answer as usual - after a delay too.
    /// </returns>
    public static ValueTask<FaultRule?> FindSftpFaultAsync(
        ExchangeContext context, IConnection connection, string? accountName, string command, string? path) =>
        FindFaultAsync(context, connection, "sftp", accountName, command, path, reply => TryReadSftpStatus(reply, out _, out _));

    /// <summary>
    /// Finds the fault for an SCP command - command <c>scp</c>, the path it names canonicalised -
    /// applying a delay or a drop and publishing the <see cref="FaultAppliedEvent"/> of a chosen error.
    /// </summary>
    /// <param name="context">The exchange.</param>
    /// <param name="connection">The connection a drop aborts.</param>
    /// <param name="caller">Who logged in, on <c>scp</c>.</param>
    /// <param name="command">The SCP command.</param>
    /// <returns>
    /// The error rule, whose reply is the message line, or the drop rule (the connection is already
    /// aborted), or <see langword="null"/> to answer as usual - after a delay too.
    /// </returns>
    public static ValueTask<FaultRule?> FindScpFaultAsync(
        ExchangeContext context, IConnection connection, ContentCaller caller, SshScpCommand command)
    {
        var canonical = SshContentPath.Canonicalise(Encoding.UTF8.GetBytes(command.Path));
        var path = canonical is null ? null : SshContentPath.WithoutTrailingSlash(canonical);

        return FindFaultAsync(context, connection, "scp", caller.AccountName, "scp", path, IsScpMessageLine);
    }

    /// <summary>
    /// Answers an SCP command with a rule's chosen error line - byte 1, the reply, LF - noting it,
    /// and ends the command with exit status 1, as every other SCP refusal does. A drop rule writes
    /// nothing: the connection is already aborted.
    /// </summary>
    /// <param name="channel">The channel's data both ways.</param>
    /// <param name="context">The exchange: its log.</param>
    /// <param name="subject">What the note names: <c>SCP upload /up/a</c>.</param>
    /// <param name="rule">The rule <see cref="FindScpFaultAsync"/> found.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>1, the exit status.</returns>
    public static async Task<uint> AnswerScpFaultAsync(
        ISshChannelDataStream channel, ExchangeContext context, string subject, FaultRule rule, CancellationToken cancellationToken)
    {
        if (rule.Kind == FaultKind.Drop)
        {
            return 1;
        }

        context.Log.Note($"{subject} refused by fault rule {rule.Id}: {rule.Reply}");
        await channel.WriteAsync(ScpReply.ChosenError(rule.Reply!), cancellationToken);

        return 1;
    }

    /// <summary>
    /// Reads an <c>sftp</c> reply: a status 2 to 8, one space and non-empty text, as <c>Surl.Cli</c>
    /// checks it.
    /// </summary>
    /// <param name="reply">The rule's reply, or <see langword="null"/> when it gave none.</param>
    /// <param name="code">The status.</param>
    /// <param name="message">The text.</param>
    /// <returns>Whether the reply fits.</returns>
    public static bool TryReadSftpStatus(string? reply, out SftpStatusCode code, out string message)
    {
        code = SftpStatusCode.Failure;
        message = string.Empty;
        var space = reply?.IndexOf(' ', StringComparison.Ordinal) ?? -1;
        if (space <= 0
            || space == reply!.Length - 1
            || !uint.TryParse(reply.AsSpan(0, space), NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            || number is < 2 or > 8)
        {
            return false;
        }

        code = (SftpStatusCode)number;
        message = reply[(space + 1)..];

        return true;
    }

    /// <summary>
    /// Whether an <c>scp</c> reply can be sent as the message line after byte 1: non-empty, and
    /// holding no CR or LF, so the line ends where the reply does.
    /// </summary>
    /// <param name="reply">The rule's reply, or <see langword="null"/> when it gave none.</param>
    /// <returns>Whether the reply fits.</returns>
    public static bool IsScpMessageLine(string? reply) =>
        !string.IsNullOrEmpty(reply) && reply.AsSpan().IndexOfAny('\r', '\n') < 0;

    // Asks the plan; a delay is held and a drop aborts the connection, each published by
    // CommandStageFaults. A chosen-error rule whose reply fits is published and handed back,
    // anything else the plan found is noted as not applied.
    private static async ValueTask<FaultRule?> FindFaultAsync(
        ExchangeContext context, IConnection connection, string scheme, string? accountName, string command, string? path, Func<string?, bool> fits)
    {
        var point = new FaultPoint(FaultStage.Command)
        {
            ExchangeId = context.ExchangeId,
            ListenUrl = ReportedListenUrl.Format(context.ListenUrl),
            Scheme = scheme,
            Account = accountName,
            Path = path,
            Command = command,
        };
        var rule = await CommandStageFaults.FindAndApplyAsync(context, connection, point, path ?? command);
        if (rule is null or { Kind: FaultKind.Delay })
        {
            return null;
        }

        if (rule.Kind == FaultKind.Drop)
        {
            return rule;
        }

        if (rule.Kind != FaultKind.Error || !fits(rule.Reply))
        {
            var why = rule.Kind == FaultKind.Error ? $"its reply does not fit the {scheme} error shape" : $"a {rule.Kind} rule is not applied at a command";
            context.Log.Note($"Fault rule {rule.Id} not applied to {scheme} {command}: {why}");

            return null;
        }

        context.Events.Publish(new FaultAppliedEvent(rule.Id, rule.Kind, FaultStage.Command, context.ExchangeId, path ?? command));

        return rule;
    }
}
