using System.Globalization;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Kafka;

/// <summary>
/// What the exchange's fault plan does to one consumer group request (ADR-0145 decision 10): the
/// request is put to the plan at <see cref="FaultStage.Command"/>, command the guide's API name and
/// path the group id or coordinator key, account the caller's (<see cref="CommandStageFaults"/>). A
/// delay has already been applied when this is returned; a drop has aborted the connection.
/// </summary>
/// <param name="DropNote">What the exchange log notes when a drop rule matched; otherwise null.</param>
/// <param name="Error">The top-level <c>error_code</c> an <see cref="FaultKind.Error"/> rule chose; otherwise null.</param>
internal readonly record struct KafkaCommandFault(string? DropNote, KafkaErrorCode? Error)
{
    /// <summary>Asks the fault plan about the request <paramref name="command"/> on <paramref name="path"/>.</summary>
    /// <param name="request">The request, for the caller's account and the connection a drop aborts.</param>
    /// <param name="context">The exchange's context.</param>
    /// <param name="command">The guide's API name, as a rule names it.</param>
    /// <param name="path">The group id, or FindCoordinator's key.</param>
    /// <returns>What the plan did. An <see cref="FaultKind.Error"/> rule whose reply is not a Kafka
    /// error code 1 to 127 is noted and not applied.</returns>
    public static async ValueTask<KafkaCommandFault> FindAsync(KafkaRequest request, ExchangeContext context, string command, string path)
    {
        var point = new FaultPoint(FaultStage.Command)
        {
            ExchangeId = context.ExchangeId,
            ListenUrl = ReportedListenUrl.Format(context.ListenUrl),
            Scheme = context.Scheme,
            Account = request.Connection.AccountName,
            Path = path,
            Command = command,
        };
        var rule = await CommandStageFaults.FindAndApplyAsync(context, request.Transport, point, path);
        return rule switch
        {
            { Kind: FaultKind.Drop } => new($"{command} was dropped by fault rule {rule.Id}", null),
            { Kind: FaultKind.Error } => new(null, ChosenError(rule, command, path, context)),
            _ => default,
        };
    }

    // An Error rule's code, when its reply is a Kafka error code 1 to 127 (ADR-0124 decision 4's
    // kafka row); null to answer as usual.
    private static KafkaErrorCode? ChosenError(FaultRule rule, string command, string path, ExchangeContext context)
    {
        if (!int.TryParse(rule.Reply, NumberStyles.None, CultureInfo.InvariantCulture, out var code) || code is < 1 or > 127)
        {
            context.Log.Note($"Fault rule {rule.Id} not applied to {command} of {path}: its reply is not a Kafka error code 1 to 127.");
            return null;
        }

        context.Log.Note($"{command} of {path} answered error {code} by fault rule {rule.Id}.");
        context.Events.Publish(new FaultAppliedEvent(rule.Id, FaultKind.Error, FaultStage.Command, context.ExchangeId, path));
        return (KafkaErrorCode)code;
    }
}
