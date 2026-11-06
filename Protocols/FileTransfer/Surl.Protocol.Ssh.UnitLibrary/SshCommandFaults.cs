using System.Globalization;
using System.Text;
using Surl.Content;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Ssh;

/// <summary>
/// Asks the exchange's fault plan, at one SFTP request or SCP command, whether to answer it with a
/// chosen error (ADR-0124 decision 4; ADR-0135): the point names the scheme (<c>sftp</c> or
/// <c>scp</c>), the account logged in, the command (the SFTP packet name, or <c>scp</c>) and the
/// canonical path.
/// </summary>
/// <remarks>
/// Only a <see cref="FaultKind.Error"/> rule whose reply fits its scheme's error shape is applied,
/// and applying it publishes one <see cref="FaultAppliedEvent"/> naming the path, or the command
/// when the request names none. A rule of any other kind, or a reply that does not fit, is noted as
/// not applied and the request is answered as without it.
/// </remarks>
internal static class SshCommandFaults
{
    /// <summary>
    /// Finds the chosen error for an SFTP request, publishing its <see cref="FaultAppliedEvent"/>.
    /// </summary>
    /// <param name="context">The exchange: its fault plan, event stream, listen URL and log.</param>
    /// <param name="accountName">The account logged in, or <see langword="null"/> for an anonymous caller.</param>
    /// <param name="command">The SFTP packet name (<c>OPEN</c>).</param>
    /// <param name="path">The canonical path the request concerns, or <see langword="null"/> when it names none.</param>
    /// <param name="cancellationToken">Cancels the plan's search.</param>
    /// <returns>The rule's identifier, status and message, or <see langword="null"/> to answer as usual.</returns>
    public static async ValueTask<(string RuleId, SftpStatusCode Code, string Message)?> FindSftpErrorAsync(
        ExchangeContext context, string? accountName, string command, string? path, CancellationToken cancellationToken)
    {
        var rule = await FindErrorAsync(context, "sftp", accountName, command, path, reply => TryReadSftpStatus(reply, out _, out _), cancellationToken);
        if (rule is null)
        {
            return null;
        }

        TryReadSftpStatus(rule.Reply, out var code, out var message);

        return (rule.Id, code, message);
    }

    /// <summary>
    /// Finds the chosen error for an SCP command - command <c>scp</c>, the path it names
    /// canonicalised - publishing its <see cref="FaultAppliedEvent"/>.
    /// </summary>
    /// <param name="context">The exchange.</param>
    /// <param name="caller">Who logged in, on <c>scp</c>.</param>
    /// <param name="command">The SCP command.</param>
    /// <param name="cancellationToken">Cancels the plan's search.</param>
    /// <returns>The rule, whose reply is the message line, or <see langword="null"/> to answer as usual.</returns>
    public static ValueTask<FaultRule?> FindScpErrorAsync(
        ExchangeContext context, ContentCaller caller, SshScpCommand command, CancellationToken cancellationToken)
    {
        var canonical = SshContentPath.Canonicalise(Encoding.UTF8.GetBytes(command.Path));
        var path = canonical is null ? null : SshContentPath.WithoutTrailingSlash(canonical);

        return FindErrorAsync(context, "scp", caller.AccountName, "scp", path, IsScpMessageLine, cancellationToken);
    }

    /// <summary>
    /// Answers an SCP command with a rule's chosen error line - byte 1, the reply, LF - noting it,
    /// and ends the command with exit status 1, as every other SCP refusal does.
    /// </summary>
    /// <param name="channel">The channel's data both ways.</param>
    /// <param name="context">The exchange: its log.</param>
    /// <param name="subject">What the note names: <c>SCP upload /up/a</c>.</param>
    /// <param name="rule">The rule <see cref="FindScpErrorAsync"/> found.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>1, the exit status.</returns>
    public static async Task<uint> AnswerScpErrorAsync(
        ISshChannelDataStream channel, ExchangeContext context, string subject, FaultRule rule, CancellationToken cancellationToken)
    {
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

    /// <summary>
    /// The listen URL as the fault plan matches it, as the serving engine names it: the scheme, the
    /// host (an IPv6 address in brackets, its zone's <c>%</c> encoded) and the bound port.
    /// </summary>
    /// <param name="listenUrl">The exchange's listen URL.</param>
    /// <returns><c>sftp://127.0.0.1:2222/</c>.</returns>
    public static string FormatListenUrl(ListenUrl listenUrl)
    {
        var host = listenUrl.Host.Contains(':', StringComparison.Ordinal)
            ? "[" + listenUrl.Host.Replace("%", "%25", StringComparison.Ordinal) + "]"
            : listenUrl.Host;

        return $"{listenUrl.Scheme}://{host}:{listenUrl.BoundPort ?? listenUrl.Port}/";
    }

    // Asks the plan; a chosen-error rule whose reply fits is published and handed back, anything
    // else the plan found is noted as not applied.
    private static async ValueTask<FaultRule?> FindErrorAsync(
        ExchangeContext context, string scheme, string? accountName, string command, string? path, Func<string?, bool> fits, CancellationToken cancellationToken)
    {
        var point = new FaultPoint(FaultStage.Command)
        {
            ExchangeId = context.ExchangeId,
            ListenUrl = FormatListenUrl(context.ListenUrl),
            Scheme = scheme,
            Account = accountName,
            Path = path,
            Command = command,
        };
        var rule = await context.Faults.FindAsync(point, cancellationToken);
        if (rule is null)
        {
            return null;
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
