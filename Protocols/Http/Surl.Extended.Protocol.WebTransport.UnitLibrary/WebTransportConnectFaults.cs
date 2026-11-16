using System.Globalization;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.WebTransport;

/// <summary>
/// Asks the exchange's fault plan, for each extended <c>CONNECT</c> that passed the origin, login and
/// path checks, whether to answer it with a chosen error (ADR-0124 decisions 4 and 9; BL-1065): the
/// point names the scheme (<c>webtransport</c>), <c>CONNECT</c> as <c>command</c> and the request path.
/// </summary>
/// <remarks>
/// A <see cref="FaultKind.Delay"/> rule holds the CONNECT before its response, then it is served as usual,
/// and a <see cref="FaultKind.Drop"/> rule is handed back as <see cref="ChosenAnswer.Dropped"/> for the
/// handler to reset the request stream (ADR-0141 and its amendment 1; BL-1182). A <see cref="FaultKind.Error"/> rule whose reply is a status 400 to 599 and optional text is handed
/// back as a <see cref="ChosenAnswer"/> and publishes one <see cref="FaultAppliedEvent"/>. Any other
/// kind, or an error rule without a usable reply, is noted as not applied and the request is served
/// as without it.
/// </remarks>
internal static class WebTransportConnectFaults
{
    /// <summary>The <c>command</c> an extended CONNECT is matched by.</summary>
    public const string ConnectCommand = "CONNECT";

    /// <summary>A chosen answer: the status, and the plain text sent as the body when the reply gave any.</summary>
    /// <param name="Status">The status, 400 to 599.</param>
    /// <param name="Text">The text after the status, or <see langword="null"/>.</param>
    public sealed record ChosenAnswer(int Status, string? Text)
    {
        /// <summary>The answer a drop rule gives: no status, no text; the CONNECT is ended with no response.</summary>
        public static ChosenAnswer Dropped { get; } = new(0, null);

        /// <summary>Whether this ends the CONNECT with no response.</summary>
        public bool IsDrop => Status == 0;

        /// <summary>The text as UTF-8.</summary>
        public byte[] Body => Encoding.UTF8.GetBytes(Text ?? string.Empty);
    }

    /// <summary>Finds the chosen error for one CONNECT.</summary>
    /// <param name="context">The exchange: its fault plan, event stream, listen URL and log.</param>
    /// <param name="path">The request path, query included.</param>
    /// <param name="accountName">The account that logged in, or <see langword="null"/> when none did.</param>
    /// <returns>The answer to send, or <see langword="null"/> to serve the request as usual.</returns>
    public static async ValueTask<ChosenAnswer?> FindAsync(ExchangeContext context, string path, string? accountName = null)
    {
        var point = new FaultPoint(FaultStage.Command)
        {
            ExchangeId = context.ExchangeId,
            ListenUrl = ReportedListenUrl.Format(context.ListenUrl),
            Scheme = context.Scheme,
            Path = path,
            Command = ConnectCommand,
            Account = accountName,
        };
        var rule = await CommandStageFaults.FindAndApplyAsync(context, point, path);
        if (rule is null || rule.Kind == FaultKind.Delay)
        {
            return null;
        }

        if (rule.Kind == FaultKind.Drop)
        {
            return ChosenAnswer.Dropped;
        }

        if (rule.Kind == FaultKind.Error && TryRead(rule.Reply) is { } answer)
        {
            context.Events.Publish(new FaultAppliedEvent(rule.Id, FaultKind.Error, FaultStage.Command, context.ExchangeId, path));
            context.Log.Note($"WebTransport CONNECT to {path} was answered {answer.Status} by fault rule {rule.Id}.");

            return answer;
        }

        var why = rule.Kind == FaultKind.Error ? "its reply is not a status 400 to 599" : $"a {rule.Kind} rule is not applied at a CONNECT";
        context.Log.Note($"Fault rule {rule.Id} not applied to CONNECT {path}: {why}.");

        return null;
    }

    private static ChosenAnswer? TryRead(string? reply)
    {
        var space = reply?.IndexOf(' ', StringComparison.Ordinal) ?? -1;
        var digits = space < 0 ? reply : reply![..space];
        if (digits is not { Length: 3 } || !int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var status) || status is < 400 or > 599)
        {
            return null;
        }

        return new ChosenAnswer(status, space < 0 || space == reply!.Length - 1 ? null : reply[(space + 1)..]);
    }
}
