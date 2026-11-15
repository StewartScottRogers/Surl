using System.Globalization;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Grpc;

/// <summary>
/// Asks the exchange's fault plan, for each call that names a served method, whether to hold it, cut it
/// off or end it with a chosen status (ADR-0124 decisions 4 and 9; BL-1188): the point names the scheme
/// (<c>grpc</c> or <c>grpcs</c>), the method's name as <c>command</c> and the full <c>/&lt;service&gt;/&lt;method&gt;</c> as <c>path</c>.
/// </summary>
/// <remarks>
/// A <see cref="FaultKind.Delay"/> rule holds the call before it is served; a <see cref="FaultKind.Drop"/>
/// rule is handed back for the handler to reset the call's stream with no response (ADR-0141 amendment 1);
/// an <see cref="FaultKind.Error"/> rule whose reply is a status code 1 to 16 and an optional message is
/// handed back as the <see cref="GrpcStatus"/> the call ends with, as Trailers-Only, and publishes one
/// <see cref="FaultAppliedEvent"/>. Any other kind, or a reply that does not fit, is noted as not applied
/// and the call is served as without it.
/// </remarks>
internal static class GrpcCallFaults
{
    /// <summary>What a rule does to the call.</summary>
    /// <param name="IsDrop">Whether the call is cut off with no response.</param>
    /// <param name="Status">The status the call ends with, or <see langword="null"/> to serve it.</param>
    public readonly record struct Outcome(bool IsDrop, GrpcStatus? Status)
    {
        /// <summary>The call is served as usual.</summary>
        public static Outcome Serve { get; } = new(false, null);
    }

    /// <summary>Finds and applies the rule for one call.</summary>
    /// <param name="context">The exchange: its fault plan, event stream, clock, log and listen URL.</param>
    /// <param name="path">The call's full <c>:path</c>, <c>/&lt;service&gt;/&lt;method&gt;</c>.</param>
    /// <param name="methodName">The method's name.</param>
    /// <returns>What to do with the call.</returns>
    /// <exception cref="OperationCanceledException">The exchange was cancelled during a delay.</exception>
    public static async ValueTask<Outcome> ApplyAsync(ExchangeContext context, string path, string methodName)
    {
        var point = new FaultPoint(FaultStage.Command)
        {
            ExchangeId = context.ExchangeId,
            ListenUrl = ReportedListenUrl.Format(context.ListenUrl),
            Scheme = context.Scheme,
            Path = path,
            Command = methodName,
        };
        var rule = await CommandStageFaults.FindAndApplyAsync(context, point, path);

        return rule?.Kind switch
        {
            null or FaultKind.Delay => Outcome.Serve,
            FaultKind.Drop => new Outcome(true, null),
            _ => ChooseError(context, rule, path),
        };
    }

    /// <summary>
    /// Reads a reply in the <c>grpc</c> error shape: a status code 1 to 16, then optionally a space and the message.
    /// </summary>
    /// <param name="reply">The rule's reply, or <see langword="null"/> when it gave none.</param>
    /// <returns>The status, or <see langword="null"/> when the reply does not fit.</returns>
    public static GrpcStatus? TryRead(string? reply)
    {
        var space = reply?.IndexOf(' ', StringComparison.Ordinal) ?? -1;
        var digits = space < 0 ? reply : reply![..space];
        if (digits is null || !int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var code) || code is < 1 or > 16)
        {
            return null;
        }

        return new GrpcStatus((GrpcStatusCode)code, space < 0 ? string.Empty : reply![(space + 1)..]);
    }

    private static Outcome ChooseError(ExchangeContext context, FaultRule rule, string path)
    {
        if (rule.Kind == FaultKind.Error && TryRead(rule.Reply) is { } status)
        {
            context.Log.Note($"gRPC call {path} ended with status {status.StatusValue} by fault rule {rule.Id}.");
            context.Events.Publish(new FaultAppliedEvent(rule.Id, FaultKind.Error, FaultStage.Command, context.ExchangeId, path));

            return new Outcome(false, status);
        }

        var why = rule.Kind == FaultKind.Error ? "its reply is not a status code 1 to 16" : $"a {rule.Kind} rule is not applied at a call";
        context.Log.Note($"Fault rule {rule.Id} not applied to gRPC call {path}: {why}.");

        return Outcome.Serve;
    }
}
