using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Kafka;

/// <summary>
/// Answers ListGroups v0 to v2 from the broker's <see cref="KafkaGroupCoordinator"/> and the offsets
/// <c>Surl.MessageBus</c> keeps (ADR-0145 decisions 5, 6 and 10; the Kafka protocol guide, ListGroups).
/// </summary>
/// <remarks>
/// The request is first put to the exchange's fault plan (<see cref="KafkaCommandFault"/>, command
/// <c>ListGroups</c>, no path): a drop closes with no bytes, an error rule's code is the top-level
/// <c>error_code</c> with no groups. Otherwise error 0 and every group that has a member or a
/// committed offset and that the caller may use, in ordinal order of group id, each with its protocol
/// type (empty for a group made by a simple commit or known only by its offsets). v1 and v2 start
/// with <c>throttle_time_ms</c> 0.
/// </remarks>
/// <param name="coordinator">The broker's group coordinator.</param>
/// <param name="bus">The shared message bus, which answers the group access check and keeps the offsets.</param>
public sealed class KafkaListGroupsAnswerer(KafkaGroupCoordinator coordinator, MessageBus.MessageBus bus) : IKafkaApiAnswerer
{
    /// <summary>The command a fault rule names to match ListGroups: the guide's API name.</summary>
    public const string FaultCommand = "ListGroups";

    private readonly KafkaGroupCoordinator coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
    private readonly MessageBus.MessageBus bus = bus ?? throw new ArgumentNullException(nameof(bus));

    /// <inheritdoc/>
    public KafkaApiKey ApiKey => KafkaApiKey.ListGroups;

    /// <inheritdoc/>
    public async ValueTask<KafkaAnswer> AnswerAsync(KafkaRequest request, ExchangeContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        var fault = await KafkaCommandFault.FindAsync(request, context, FaultCommand, "");
        if (fault.DropNote is { } note)
        {
            return KafkaAnswer.Close(note);
        }

        var error = fault.Error ?? KafkaErrorCode.None;
        List<KeyValuePair<string, string>> groups = error == KafkaErrorCode.None ? Listed(request.Connection.AccountName) : [];
        var body = new KafkaPrimitiveWriter();
        if (request.Header.ApiVersion >= 1)
        {
            body.WriteInt32(0);
        }

        body.WriteInt16((short)error);
        body.WriteArrayLength(groups.Count);
        foreach (var (group, protocolType) in groups)
        {
            body.WriteString(group);
            body.WriteString(protocolType);
        }

        return KafkaAnswer.Respond(body.ToArray());
    }

    // Groups with a member first, so a group with both keeps its protocol type; then those with offsets only.
    private List<KeyValuePair<string, string>> Listed(string? account)
    {
        var listed = coordinator.ListMemberedGroups().ToDictionary(group => group.Key, group => group.Value, StringComparer.Ordinal);
        foreach (var group in bus.GetCommittedGroups())
        {
            listed.TryAdd(group, "");
        }

        return [.. listed.Where(group => bus.MayUseGroup(account, group.Key)).OrderBy(group => group.Key, StringComparer.Ordinal)];
    }
}
