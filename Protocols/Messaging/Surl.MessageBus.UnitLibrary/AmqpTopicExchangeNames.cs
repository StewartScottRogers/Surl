using System.Text;

namespace Surl.MessageBus;

/// <summary>
/// AMQP 0-9-1 topic-exchange keys on the bus (ADR-0100, decision 3.2; AMQP 0-9-1 section
/// 3.1.3.3): words separated by <c>.</c> become levels separated by <c>/</c>; in a binding key
/// the word <c>*</c> becomes <c>+</c> and the word <c>#</c> stays <c>#</c>. A word is
/// non-empty; one holding <c>/</c>, <c>+</c> or <c>#</c> is not mappable, and a publish with
/// such a routing key is unroutable. A routing key or binding key is a <c>shortstr</c>, at
/// most <see cref="MaxKeyBytes"/> bytes. A queue name needs no mapping: it is the bus queue's
/// name.
/// </summary>
public static class AmqpTopicExchangeNames
{
    /// <summary>The most UTF-8 bytes a routing key or binding key holds: 255, a <c>shortstr</c>.</summary>
    public const int MaxKeyBytes = 255;

    private const string SingleWordWildcard = "*";
    private const string MultiWordWildcard = "#";
    private static readonly char[] NotInWord = ['.', '/', '+', '#'];
    private static readonly Func<string, string?> WordMapping = Word;
    private static readonly Func<string, string?> BindingWordToLevelMapping = BindingWordToLevel;
    private static readonly Func<string, string?> FilterLevelToBindingWordMapping = FilterLevelToBindingWord;

    /// <summary>
    /// Maps a publish's routing key onto a canonical topic name.
    /// </summary>
    /// <param name="routingKey">The routing key.</param>
    /// <returns>The canonical topic, or <see cref="TopicNameMappingOutcome.InvalidName"/> (the
    /// publish is unroutable) when a word is empty or holds <c>/</c>, <c>+</c> or <c>#</c>.</returns>
    public static TopicNameMapping ToCanonicalTopic(string? routingKey) =>
        SeparatedNames.ToCanonical(routingKey, '.', WordMapping, isFilter: false);

    /// <summary>
    /// Maps a queue binding's binding key onto a canonical filter.
    /// </summary>
    /// <param name="bindingKey">The binding key.</param>
    /// <returns>The canonical filter, or <see cref="TopicNameMappingOutcome.InvalidName"/> when
    /// a word is empty or holds <c>/</c> or <c>+</c>, or holds <c>#</c> without being it.</returns>
    public static TopicNameMapping ToCanonicalFilter(string? bindingKey) =>
        SeparatedNames.ToCanonical(bindingKey, '.', BindingWordToLevelMapping, isFilter: true);

    /// <summary>
    /// Maps a canonical topic name onto the routing key a consumer is sent (<c>basic.deliver</c>).
    /// </summary>
    /// <param name="topic">The canonical topic name.</param>
    /// <returns>The routing key; <see cref="TopicNameMappingOutcome.Unreachable"/> when a level
    /// is empty or holds <c>.</c>, or the key would pass <see cref="MaxKeyBytes"/>; or
    /// <see cref="TopicNameMappingOutcome.InvalidName"/> when it is not canonical.</returns>
    public static TopicNameMapping FromCanonicalTopic(string? topic) =>
        CanonicalTopicName.IsValidTopic(topic)
            ? Fitting(SeparatedNames.Join(CanonicalTopicName.SplitLevels(topic), WordMapping, '.', TopicNameMappingOutcome.Unreachable))
            : TopicNameMapping.Refused(TopicNameMappingOutcome.InvalidName);

    /// <summary>
    /// Maps a canonical filter onto a binding key: <c>+</c> becomes <c>*</c>, <c>#</c> stays.
    /// </summary>
    /// <param name="filter">The canonical filter.</param>
    /// <returns>The binding key; <see cref="TopicNameMappingOutcome.Unreachable"/> when a level
    /// is empty, is a literal <c>*</c>, or holds <c>.</c>, or the key would pass
    /// <see cref="MaxKeyBytes"/>; or <see cref="TopicNameMappingOutcome.InvalidName"/> when it is
    /// not canonical.</returns>
    public static TopicNameMapping FromCanonicalFilter(string? filter) =>
        CanonicalTopicName.IsValidFilter(filter)
            ? Fitting(SeparatedNames.Join(CanonicalTopicName.SplitLevels(filter), FilterLevelToBindingWordMapping, '.', TopicNameMappingOutcome.Unreachable))
            : TopicNameMapping.Refused(TopicNameMappingOutcome.InvalidName);

    private static TopicNameMapping Fitting(TopicNameMapping mapped) =>
        mapped.IsMapped && Encoding.UTF8.GetByteCount(mapped.Name) > MaxKeyBytes
            ? TopicNameMapping.Refused(TopicNameMappingOutcome.Unreachable)
            : mapped;

    private static string? Word(string part) => part.Length > 0 && part.IndexOfAny(NotInWord) < 0 ? part : null;

    private static string? BindingWordToLevel(string word) => word switch
    {
        SingleWordWildcard => "+",
        MultiWordWildcard => MultiWordWildcard,
        _ => Word(word),
    };

    private static string? FilterLevelToBindingWord(string level) => level switch
    {
        "+" => SingleWordWildcard,
        MultiWordWildcard => MultiWordWildcard,
        SingleWordWildcard => null,
        _ => Word(level),
    };
}
