using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Gopher;

/// <summary>
/// Asks the exchange's fault plan, once the selector is read, whether to answer it with a chosen
/// error item (ADR-0124, decision 4; ADR-0138): the point names the scheme (<c>gopher</c> or
/// <c>gophers</c>) and the path, the selector as the request path the content store is asked for
/// (<c>/a</c>). Gopher has no login and no command, so the point names neither.
/// </summary>
/// <remarks>
/// Only a <see cref="FaultKind.Error"/> rule whose reply fits (<see cref="Fits"/>) is applied, and
/// applying it publishes one <see cref="FaultAppliedEvent"/> naming the path. A rule of any other
/// kind, or a reply that does not fit, is noted as not applied and the selector is answered as
/// without it.
/// </remarks>
internal static class GopherSelectorFaults
{
    /// <summary>
    /// Finds the chosen error text for a selector, publishing its <see cref="FaultAppliedEvent"/>.
    /// </summary>
    /// <param name="context">The exchange: its fault plan, event stream and listen URL.</param>
    /// <param name="requestPath">The selector as a request path (<c>/a</c>).</param>
    /// <returns>The error item's text, or <see langword="null"/> to answer as usual.</returns>
    public static async ValueTask<string?> FindErrorAsync(ExchangeContext context, string requestPath)
    {
        var point = new FaultPoint(FaultStage.Command)
        {
            ExchangeId = context.ExchangeId,
            ListenUrl = FormatListenUrl(context.ListenUrl),
            Scheme = context.Scheme,
            Path = requestPath,
        };
        if (await context.Faults.FindAsync(point, context.CancellationToken) is not { } rule)
        {
            return null;
        }

        if (rule.Kind != FaultKind.Error || !Fits(rule.Reply))
        {
            var why = rule.Kind == FaultKind.Error ? "its reply does not fit the gopher error shape" : $"a {rule.Kind} rule is not applied at a selector";
            context.Log.Note($"Fault rule {rule.Id} not applied to the selector: {why}.");

            return null;
        }

        context.Log.Note($"The selector was answered with the chosen error of fault rule {rule.Id}.");
        context.Events.Publish(new FaultAppliedEvent(rule.Id, FaultKind.Error, FaultStage.Command, context.ExchangeId, requestPath));

        return rule.Reply;
    }

    /// <summary>
    /// Whether a reply fits the <c>gopher</c> error shape: non-empty text with no TAB, CR or LF,
    /// so it stays one item's display string.
    /// </summary>
    /// <param name="reply">The rule's reply, or <see langword="null"/> when it gave none.</param>
    /// <returns>Whether the reply fits.</returns>
    public static bool Fits(string? reply) =>
        reply is { Length: > 0 } && reply.AsSpan().IndexOfAny('\t', '\r', '\n') < 0;

    /// <summary>
    /// The error menu carrying <paramref name="text"/>: one item of type <c>3</c>, with the empty
    /// selector, host and port the server's own error item has, then the closing <c>.</c> line.
    /// </summary>
    /// <param name="text">The item's display string, UTF-8 encoded.</param>
    /// <returns><c>3&lt;text&gt;\t\terror.host\t1\r\n.\r\n</c>.</returns>
    public static byte[] ErrorMenu(string text) => Encoding.UTF8.GetBytes($"3{text}\t\terror.host\t1\r\n.\r\n");

    /// <summary>
    /// The listen URL as the fault plan matches it, as the serving engine names it: the scheme, the
    /// host (an IPv6 address in brackets, its zone's <c>%</c> encoded) and the bound port.
    /// </summary>
    /// <param name="listenUrl">The exchange's listen URL.</param>
    /// <returns><c>gopher://127.0.0.1:70/</c>.</returns>
    public static string FormatListenUrl(ListenUrl listenUrl)
    {
        var host = listenUrl.Host.Contains(':', StringComparison.Ordinal)
            ? "[" + listenUrl.Host.Replace("%", "%25", StringComparison.Ordinal) + "]"
            : listenUrl.Host;

        return $"{listenUrl.Scheme}://{host}:{listenUrl.BoundPort ?? listenUrl.Port}/";
    }
}
