using System.Globalization;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Kafka;

/// <summary>
/// The chosen-error fault (ADR-0124 decisions 4 and 9): Produce and Fetch ask the
/// exchange's <see cref="IFaultPlan"/> once for each topic they name, at a
/// <see cref="FaultStage.Command"/> point - the API key's name (<c>Produce</c>, <c>Fetch</c>) as its
/// command, the topic as the client named it as its path, the account the login proved - before the
/// bus is touched. A matching <see cref="FaultKind.Error"/> rule's error code 1 to 127 is answered in
/// each of that topic's partitions, in the request's own response, and each rule applied publishes
/// one <see cref="FaultAppliedEvent"/>.
/// </summary>
internal static class KafkaChosenErrorFault
{
    private const short LowestErrorCode = 1;
    private const short HighestErrorCode = 127;

    /// <summary>
    /// The error code a matching rule chooses for each of <paramref name="topics"/> that has one.
    /// </summary>
    /// <param name="apiKey">The API asking.</param>
    /// <param name="topics">The topics the request names, in its order; a repeated one is asked once.</param>
    /// <param name="account">The account the login proved, or <see langword="null"/>.</param>
    /// <param name="context">The exchange's context.</param>
    /// <returns>The chosen error code of each topic a rule answers.</returns>
    public static async ValueTask<Dictionary<string, KafkaErrorCode>> FindAsync(
        KafkaApiKey apiKey, IEnumerable<string> topics, string? account, ExchangeContext context)
    {
        var chosen = new Dictionary<string, KafkaErrorCode>(StringComparer.Ordinal);
        foreach (var topic in topics.Distinct(StringComparer.Ordinal))
        {
            if (await FindAsync(apiKey.ToString(), topic, account, context) is { } errorCode)
            {
                chosen[topic] = errorCode;
            }
        }

        return chosen;
    }

    private static async ValueTask<KafkaErrorCode?> FindAsync(string command, string topic, string? account, ExchangeContext context)
    {
        var point = new FaultPoint(FaultStage.Command)
        {
            ExchangeId = context.ExchangeId,
            ListenUrl = ReportedListenUrl.Format(context.ListenUrl),
            Scheme = context.Scheme,
            Account = account,
            Path = topic,
            Command = command,
        };
        if (await context.Faults.FindAsync(point, context.CancellationToken) is not { } rule)
        {
            return null;
        }

        var target = $"{command} {topic}";
        if (rule.Kind != FaultKind.Error || !TryReadErrorCode(rule.Reply, out var errorCode))
        {
            context.Log.Note($"{target}: fault rule {rule.Id} is not an error with a Kafka error code 1 to 127; not applied.");
            return null;
        }

        context.Log.Note($"{target} answered by fault rule {rule.Id} with error code {errorCode}.");
        context.Events.Publish(new FaultAppliedEvent(rule.Id, FaultKind.Error, FaultStage.Command, context.ExchangeId, target));
        return (KafkaErrorCode)errorCode;
    }

    private static bool TryReadErrorCode(string? reply, out short errorCode) =>
        short.TryParse(reply, NumberStyles.None, CultureInfo.InvariantCulture, out errorCode)
        && errorCode is >= LowestErrorCode and <= HighestErrorCode;
}
