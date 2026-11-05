namespace Surl.MessageBus;

/// <summary>
/// NATS subjects on the bus (ADR-0100, decision 3.2; NATS client protocol, "Protocol
/// conventions"): tokens separated by <c>.</c> become levels separated by <c>/</c>, the
/// token <c>*</c> becomes <c>+</c>, and the last token <c>&gt;</c>, which matches one or more
/// tokens, becomes <c>+/#</c>, which matches one or more levels. A token is non-empty and holds
/// no white space; one holding <c>/</c>, <c>+</c> or <c>#</c> is not mappable (the server's
/// <c>-ERR 'Invalid Subject'</c> or <c>-ERR 'Invalid Publish Subject'</c>). A token is a
/// wildcard only when it is exactly <c>*</c> or <c>&gt;</c>.
/// </summary>
public static class NatsSubjectNames
{
    private const string SingleTokenWildcard = "*";
    private const string FullWildcard = ">";
    private const string OneOrMoreLevels = "+/#";
    private static readonly char[] NotInLiteralToken = ['.', '/', '+', '#', ' ', '\t', '\r', '\n'];
    private static readonly Func<string, string?> LiteralTokenMapping = LiteralToken;
    private static readonly Func<string, string?> FilterTokenToLevelMapping = FilterTokenToLevel;
    private static readonly Func<string, string?> FilterLevelToTokenMapping = FilterLevelToToken;

    /// <summary>
    /// Maps a subject a message is published on (PUB, HPUB) onto a canonical topic name.
    /// </summary>
    /// <param name="subject">The publish subject.</param>
    /// <returns>The canonical topic, or <see cref="TopicNameMappingOutcome.InvalidName"/> when a
    /// token is empty, a wildcard, or holds white space, <c>/</c>, <c>+</c> or <c>#</c>.</returns>
    public static TopicNameMapping ToCanonicalTopic(string? subject) =>
        SeparatedNames.ToCanonical(subject, '.', LiteralTokenMapping, isFilter: false);

    /// <summary>
    /// Maps a subscription's subject (SUB) onto a canonical filter.
    /// </summary>
    /// <param name="subject">The subscription subject.</param>
    /// <returns>The canonical filter, or <see cref="TopicNameMappingOutcome.InvalidName"/> when
    /// a token is empty or holds white space, <c>/</c>, <c>+</c> or <c>#</c>, or <c>&gt;</c>
    /// stands before the last token.</returns>
    public static TopicNameMapping ToCanonicalFilter(string? subject) =>
        subject is not null && subject.Split('.')[..^1].Contains(FullWildcard)
            ? TopicNameMapping.Refused(TopicNameMappingOutcome.InvalidName)
            : SeparatedNames.ToCanonical(subject, '.', FilterTokenToLevelMapping, isFilter: true);

    /// <summary>
    /// Maps a canonical topic name onto the subject a NATS subscriber is sent (MSG, HMSG).
    /// </summary>
    /// <param name="topic">The canonical topic name.</param>
    /// <returns>The subject; <see cref="TopicNameMappingOutcome.Unreachable"/> when a level is
    /// empty, is <c>*</c> or <c>&gt;</c>, or holds <c>.</c> or white space; or
    /// <see cref="TopicNameMappingOutcome.InvalidName"/> when it is not canonical.</returns>
    public static TopicNameMapping FromCanonicalTopic(string? topic) =>
        CanonicalTopicName.IsValidTopic(topic)
            ? SeparatedNames.Join(CanonicalTopicName.SplitLevels(topic), LiteralTokenMapping, '.', TopicNameMappingOutcome.Unreachable)
            : TopicNameMapping.Refused(TopicNameMappingOutcome.InvalidName);

    /// <summary>
    /// Maps a canonical filter onto a NATS subscription subject: <c>+</c> becomes <c>*</c>, and
    /// a trailing <c>+/#</c> becomes <c>&gt;</c>.
    /// </summary>
    /// <param name="filter">The canonical filter.</param>
    /// <returns>The subject; <see cref="TopicNameMappingOutcome.Unreachable"/> when a level
    /// cannot be a token or a <c>#</c> stands anywhere but in a trailing <c>+/#</c>; or
    /// <see cref="TopicNameMappingOutcome.InvalidName"/> when it is not canonical.</returns>
    public static TopicNameMapping FromCanonicalFilter(string? filter)
    {
        if (!CanonicalTopicName.IsValidFilter(filter))
        {
            return TopicNameMapping.Refused(TopicNameMappingOutcome.InvalidName);
        }

        var levels = CanonicalTopicName.SplitLevels(filter);
        var endsWithOneOrMore = levels is [.., "+", "#"];
        var mapped = SeparatedNames.Join(endsWithOneOrMore ? levels[..^2] : levels, FilterLevelToTokenMapping, '.', TopicNameMappingOutcome.Unreachable);
        return endsWithOneOrMore && mapped.IsMapped
            ? TopicNameMapping.Of(mapped.Name.Length == 0 ? FullWildcard : mapped.Name + "." + FullWildcard)
            : mapped;
    }

    private static string? LiteralToken(string part) =>
        part.Length > 0 && part is not (SingleTokenWildcard or FullWildcard) && part.IndexOfAny(NotInLiteralToken) < 0 ? part : null;

    private static string? FilterTokenToLevel(string token) => token switch
    {
        SingleTokenWildcard => "+",
        FullWildcard => OneOrMoreLevels,
        _ => LiteralToken(token),
    };

    private static string? FilterLevelToToken(string level) => level == "+" ? SingleTokenWildcard : LiteralToken(level);
}
