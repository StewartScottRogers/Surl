using System.Globalization;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Dict;

/// <summary>
/// Asks the exchange's fault plan, at each DICT command it recognizes, whether to answer it with a
/// chosen status line (ADR-0124, decision 4; ADR-0138): the point names the scheme, the command in
/// capitals and the path (<see cref="PathOf"/>). DICT has no login, so the point names no account.
/// </summary>
/// <remarks>
/// Only a <see cref="FaultKind.Error"/> rule whose reply fits (<see cref="Fits"/>) is applied, and
/// applying it publishes one <see cref="FaultAppliedEvent"/> naming the path, or the command when
/// there is none. A rule of any other kind, or a reply that does not fit, is noted as not applied
/// and the command is answered as without it.
/// </remarks>
internal static class DictCommandFaults
{
    /// <summary>
    /// Finds the chosen status line for a command, publishing its <see cref="FaultAppliedEvent"/>.
    /// </summary>
    /// <param name="context">The exchange: its fault plan, event stream and listen URL.</param>
    /// <param name="command">The command in capitals (<c>DEFINE</c>).</param>
    /// <param name="path">The word or database the command names, or <see langword="null"/>.</param>
    /// <returns>The status line to answer with, without its line ending, or <see langword="null"/> to answer as usual.</returns>
    public static async ValueTask<string?> FindErrorAsync(ExchangeContext context, string command, string? path)
    {
        var point = new FaultPoint(FaultStage.Command)
        {
            ExchangeId = context.ExchangeId,
            ListenUrl = FormatListenUrl(context.ListenUrl),
            Scheme = context.Scheme,
            Path = path,
            Command = command,
        };
        if (await context.Faults.FindAsync(point, context.CancellationToken) is not { } rule)
        {
            return null;
        }

        if (rule.Kind != FaultKind.Error || !Fits(rule.Reply))
        {
            var why = rule.Kind == FaultKind.Error ? "its reply does not fit the dict error shape" : $"a {rule.Kind} rule is not applied at a command";
            context.Log.Note($"Fault rule {rule.Id} not applied to DICT {command}: {why}.");

            return null;
        }

        context.Log.Note($"{command} answered with the chosen error of fault rule {rule.Id}.");
        context.Events.Publish(new FaultAppliedEvent(rule.Id, FaultKind.Error, FaultStage.Command, context.ExchangeId, path ?? command));

        return rule.Reply;
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

    /// <summary>
    /// The listen URL as the fault plan matches it, as the serving engine names it: the scheme, the
    /// host (an IPv6 address in brackets, its zone's <c>%</c> encoded) and the bound port.
    /// </summary>
    /// <param name="listenUrl">The exchange's listen URL.</param>
    /// <returns><c>dict://127.0.0.1:2628/</c>.</returns>
    public static string FormatListenUrl(ListenUrl listenUrl)
    {
        var host = listenUrl.Host.Contains(':', StringComparison.Ordinal)
            ? "[" + listenUrl.Host.Replace("%", "%25", StringComparison.Ordinal) + "]"
            : listenUrl.Host;

        return $"{listenUrl.Scheme}://{host}:{listenUrl.BoundPort ?? listenUrl.Port}/";
    }
}
