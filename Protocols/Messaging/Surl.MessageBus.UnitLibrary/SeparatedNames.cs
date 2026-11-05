namespace Surl.MessageBus;

/// <summary>
/// The part-by-part mapping NATS subjects and AMQP 0-9-1 topic-exchange keys share: split at
/// one separator, map each part, join at another, and refuse the whole name when one part
/// cannot be mapped.
/// </summary>
internal static class SeparatedNames
{
    /// <summary>
    /// Maps <paramref name="name"/>, split at <paramref name="separator"/>, onto canonical levels
    /// that must form a canonical filter when <paramref name="isFilter"/>, else a canonical topic.
    /// </summary>
    internal static TopicNameMapping ToCanonical(string? name, char separator, Func<string, string?> partToLevel, bool isFilter)
    {
        if (name is null)
        {
            return TopicNameMapping.Refused(TopicNameMappingOutcome.InvalidName);
        }

        var mapped = Join(name.Split(separator), partToLevel, '/', TopicNameMappingOutcome.InvalidName);
        return mapped.IsMapped && !IsCanonical(mapped.Name, isFilter) ? TopicNameMapping.Refused(TopicNameMappingOutcome.InvalidName) : mapped;
    }

    /// <summary>
    /// Maps each of <paramref name="parts"/> and joins them at <paramref name="separator"/>;
    /// <paramref name="refusal"/> when one maps to <see langword="null"/>.
    /// </summary>
    internal static TopicNameMapping Join(IReadOnlyList<string> parts, Func<string, string?> mapPart, char separator, TopicNameMappingOutcome refusal)
    {
        var mapped = new string[parts.Count];
        for (var i = 0; i < parts.Count; i++)
        {
            if (mapPart(parts[i]) is not { } part)
            {
                return TopicNameMapping.Refused(refusal);
            }

            mapped[i] = part;
        }

        return TopicNameMapping.Of(string.Join(separator, mapped));
    }

    private static bool IsCanonical(string name, bool isFilter) =>
        isFilter ? CanonicalTopicName.IsValidFilter(name) : CanonicalTopicName.IsValidTopic(name);
}
