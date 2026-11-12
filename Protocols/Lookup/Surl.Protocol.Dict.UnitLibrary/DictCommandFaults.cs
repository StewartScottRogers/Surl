using System.Globalization;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Dict;

/// <summary>
/// Asks the exchange's fault plan, at each DICT command it recognizes, whether to hold it, drop the
/// connection or answer it with a chosen status line (ADR-0124, decision 4; ADR-0138): the point
/// names the scheme, the command in capitals and the path (<see cref="PathOf"/>). DICT has no
/// login, so the point names no account.
/// </summary>
/// <remarks>
/// A <see cref="FaultKind.Delay"/> rule holds the command for its <c>ms</c> and the command is then
/// answered as usual; a <see cref="FaultKind.Drop"/> rule aborts the connection; a
/// <see cref="FaultKind.Error"/> rule whose reply fits (<see cref="Fits"/>) is answered in place of
/// the command. Each publishes one <see cref="FaultAppliedEvent"/> naming the path, or the command
/// when there is none. A rule of any other kind, or a reply that does not fit, is noted as not
/// applied and the command is answered as without it.
/// </remarks>
internal static class DictCommandFaults
{
    /// <summary>
    /// Finds and applies the fault for a command, publishing its <see cref="FaultAppliedEvent"/>.
    /// </summary>
    /// <param name="context">The exchange: its fault plan, event stream and listen URL.</param>
    /// <param name="connection">The connection a drop aborts.</param>
    /// <param name="command">The command in capitals (<c>DEFINE</c>).</param>
    /// <param name="path">The word or database the command names, or <see langword="null"/>.</param>
    /// <returns>
    /// The rule answered in place of the command - a <see cref="FaultKind.Drop"/> (send nothing and
    /// end the session) or a <see cref="FaultKind.Error"/> (send its reply) - or
    /// <see langword="null"/> to answer as usual.
    /// </returns>
    public static async ValueTask<FaultRule?> FindAndApplyAsync(
        ExchangeContext context, IConnection connection, string command, string? path)
    {
        var point = new FaultPoint(FaultStage.Command)
        {
            ExchangeId = context.ExchangeId,
            ListenUrl = ReportedListenUrl.Format(context.ListenUrl),
            Scheme = context.Scheme,
            Path = path,
            Command = command,
        };
        var rule = await CommandStageFaults.FindAndApplyAsync(context, connection, point, path ?? command);
        if (rule is null or { Kind: FaultKind.Delay or FaultKind.Drop })
        {
            return rule is { Kind: FaultKind.Drop } ? rule : null;
        }

        if (rule.Kind != FaultKind.Error || !Fits(rule.Reply))
        {
            var why = rule.Kind == FaultKind.Error ? "its reply does not fit the dict error shape" : $"a {rule.Kind} rule is not applied at a command";
            context.Log.Note($"Fault rule {rule.Id} not applied to DICT {command}: {why}.");

            return null;
        }

        context.Log.Note($"{command} answered with the chosen error of fault rule {rule.Id}.");
        context.Events.Publish(new FaultAppliedEvent(rule.Id, FaultKind.Error, FaultStage.Command, context.ExchangeId, path ?? command));

        return rule;
    }

    /// <summary>
    /// The path a fault rule's <c>path</c> is matched against: the word of a well-formed
    /// <c>DEFINE</c> or <c>MATCH</c>, the database of a well-formed <c>SHOW INFO</c>, otherwise none.
    /// </summary>
    /// <param name="command">The command in capitals.</param>
    /// <param name="words">The command line's words, the command first.</param>
    /// <returns>The word or database, or <see langword="null"/>.</returns>
    public static string? PathOf(string command, IReadOnlyList<string> words) => (command, words.Count) switch
    {
        ("DEFINE", 3) => words[2],
        ("MATCH", 4) => words[3],
        ("SHOW", 3) when string.Equals(words[1], "INFO", StringComparison.OrdinalIgnoreCase) => words[2],
        _ => null,
    };

    /// <summary>
    /// Whether a reply fits the <c>dict</c> error shape: a status 500 to 599, one space and
    /// non-empty text, with no CR or LF anywhere.
    /// </summary>
    /// <param name="reply">The rule's reply, or <see langword="null"/> when it gave none.</param>
    /// <returns>Whether the reply fits.</returns>
    public static bool Fits(string? reply) =>
        reply is { Length: > 4 }
        && reply[3] == ' '
        && int.TryParse(reply.AsSpan(0, 3), NumberStyles.None, CultureInfo.InvariantCulture, out var status)
        && status is >= 500 and <= 599
        && reply.AsSpan().IndexOfAny('\r', '\n') < 0;
}
