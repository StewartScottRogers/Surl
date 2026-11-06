using Surl.MessageBus;

namespace Surl.Extended.Protocol.Amqp1;

/// <summary>
/// The entries of a receiving link's source <c>filter</c> map (Part 3 sections 3.5.1, 3.5.3 and 3.5.8)
/// that the container applies, and the messages they let through (ADR-0146 decisions 2.1 and 2.4).
/// An entry is recognised by its descriptor from the Apache filter registry, symbol or ulong form:
/// a JMS selector, no-local (a topic link only), and the legacy AMQP direct, topic and headers
/// bindings. Every applied entry must accept a message; any other entry is left out of
/// <see cref="Applied"/>, so the client decides whether to go on.
/// </summary>
internal sealed class Amqp1SourceFilters
{
    /// <summary><c>apache.org:legacy-amqp-direct-binding:string</c>, <c>0x0000468C:0x00000000</c>.</summary>
    public const ulong DirectBinding = 0x0000468C_00000000;

    /// <summary><c>apache.org:legacy-amqp-topic-binding:string</c>, <c>0x0000468C:0x00000001</c>.</summary>
    public const ulong TopicBinding = 0x0000468C_00000001;

    /// <summary><c>apache.org:legacy-amqp-headers-binding:map</c>, <c>0x0000468C:0x00000002</c>.</summary>
    public const ulong HeadersBinding = 0x0000468C_00000002;

    /// <summary><c>apache.org:no-local-filter:list</c>, <c>0x0000468C:0x00000003</c>.</summary>
    public const ulong NoLocalFilter = 0x0000468C_00000003;

    /// <summary><c>apache.org:selector-filter:string</c>, <c>0x0000468C:0x00000004</c>.</summary>
    public const ulong SelectorFilter = 0x0000468C_00000004;

    private const int FilterFieldIndex = 7;

    private const string MatchKey = "x-match";

    private const string ReservedKeyPrefix = "x-";

    private static readonly Dictionary<string, ulong> CodesByName = new(StringComparer.Ordinal)
    {
        ["apache.org:legacy-amqp-direct-binding:string"] = DirectBinding,
        ["apache.org:legacy-amqp-topic-binding:string"] = TopicBinding,
        ["apache.org:legacy-amqp-headers-binding:map"] = HeadersBinding,
        ["apache.org:no-local-filter:list"] = NoLocalFilter,
        ["apache.org:selector-filter:string"] = SelectorFilter,
    };

    private static readonly Func<Amqp1BusSelectorMessage, string?, bool> Everything = (_, _) => true;

    private readonly List<Func<Amqp1BusSelectorMessage, string?, bool>> predicates = [];

    private readonly List<KeyValuePair<object?, object?>> applied = [];

    private Amqp1SourceFilters()
    {
    }

    /// <summary>No filter: every message is let through, and nothing is echoed.</summary>
    public static Amqp1SourceFilters None { get; } = new();

    /// <summary>Whether no-local was applied: the link is kept from messages its own connection publishes.</summary>
    public bool NoLocal { get; private set; }

    /// <summary>The applied entries, under the client's keys and as the client sent them; <see langword="null"/> when none was.</summary>
    public Amqp1Map? Applied => applied.Count == 0 ? null : new Amqp1Map([.. applied]);

    /// <summary>
    /// Reads the <c>filter</c> map of a client receiver's source.
    /// </summary>
    /// <param name="source">The source as the <c>attach</c> carried it.</param>
    /// <param name="isTopic">Whether the link's node is a topic, where no-local applies.</param>
    /// <returns>The filters applied; <see langword="null"/> when a selector entry is refused (not a
    /// string, or one <see cref="Amqp1Selector.Parse"/> refuses), which refuses the attach.</returns>
    public static Amqp1SourceFilters? Read(object? source, bool isTopic)
    {
        var filters = new Amqp1SourceFilters();
        foreach (var entry in FilterEntries(source))
        {
            if (entry.Value is Amqp1Described described && !filters.TryApply(entry, described, isTopic))
            {
                return null;
            }
        }

        return filters;
    }

    private static IReadOnlyList<KeyValuePair<object?, object?>> FilterEntries(object? source) =>
        source is Amqp1Described { Value: IReadOnlyList<object?> fields } && fields.ElementAtOrDefault(FilterFieldIndex) is Amqp1Map map
            ? map.Entries
            : [];

    /// <summary>The filter type a descriptor names: a ulong as itself, a symbol through the registry's names.</summary>
    /// <param name="descriptor">The descriptor of a filter entry's value.</param>
    /// <returns>The descriptor code; <see langword="null"/> for a symbol the registry above does not hold, or any other type.</returns>
    public static ulong? CodeOf(object? descriptor) => descriptor switch
    {
        ulong code => code,
        Amqp1Symbol symbol when CodesByName.TryGetValue(symbol.Value, out var code) => code,
        _ => null,
    };

    // False only for a selector refused; an entry not applied is left out.
    private bool TryApply(KeyValuePair<object?, object?> entry, Amqp1Described described, bool isTopic)
    {
        var code = CodeOf(described.Descriptor);
        if (code == SelectorFilter)
        {
            return TryApplySelector(entry, described.Value);
        }

        Func<Amqp1BusSelectorMessage, string?, bool>? predicate = code switch
        {
            NoLocalFilter when isTopic => NoLocalPredicate(),
            DirectBinding when described.Value is string key => (_, routingKey) => routingKey == key,
            TopicBinding when described.Value is string pattern && AmqpTopicExchangeNames.ToCanonicalFilter(pattern).IsMapped => TopicPredicate(pattern),
            HeadersBinding when described.Value is Amqp1Map arguments => HeadersPredicate(arguments),
            _ => null,
        };

        if (predicate is not null)
        {
            Apply(entry, predicate);
        }

        return true;
    }

    // A selector that is not a string, or that does not parse, refuses the attach (ADR-0146 decision 2.2).
    private bool TryApplySelector(KeyValuePair<object?, object?> entry, object? value)
    {
        if (value is not string text || Amqp1Selector.Parse(text).Selector is not { } selector)
        {
            return false;
        }

        Apply(entry, (message, _) => selector.Selects(message));
        return true;
    }

    private void Apply(KeyValuePair<object?, object?> entry, Func<Amqp1BusSelectorMessage, string?, bool> predicate)
    {
        predicates.Add(predicate);
        applied.Add(entry);
    }

    private Func<Amqp1BusSelectorMessage, string?, bool> NoLocalPredicate()
    {
        // The bus keeps the connection's own publishes from the subscription.
        NoLocal = true;
        return Everything;
    }

    private static Func<Amqp1BusSelectorMessage, string?, bool> TopicPredicate(string pattern)
    {
        var words = pattern.Split('.');
        return (_, routingKey) => routingKey is not null && TopicMatches(words, routingKey.Split('.'));
    }

    // AMQP 0-9-1 section 3.1.3.3: "*" is one word, "#" none or more; matched by dynamic programming so
    // a pattern of many "#" stays linear in each word pair.
    private static bool TopicMatches(string[] pattern, string[] key)
    {
        var suffixMatches = new bool[key.Length + 1];
        suffixMatches[key.Length] = true;
        for (var p = pattern.Length - 1; p >= 0; p--)
        {
            var current = new bool[key.Length + 1];
            for (var k = key.Length; k >= 0; k--)
            {
                current[k] = WordMatches(pattern[p], key, k, suffixMatches, current);
            }

            suffixMatches = current;
        }

        return suffixMatches[0];
    }

    private static bool WordMatches(string word, string[] key, int k, bool[] next, bool[] current) =>
        word == "#"
            ? next[k] || (k < key.Length && current[k + 1])
            : k < key.Length && (word == "*" || word == key[k]) && next[k + 1];

    // ADR-0112 decision 5's headers match: x-match all (the default) or any; keys beginning x- are not
    // matched; a null value matches the property's presence. An x-match of any other value is not applied.
    private static Func<Amqp1BusSelectorMessage, string?, bool>? HeadersPredicate(Amqp1Map arguments)
    {
        var matchAny = arguments.Entries.FirstOrDefault(entry => entry.Key is string key && key == MatchKey).Value switch
        {
            null => false,
            string text => MatchesAny(text),
            Amqp1Symbol symbol => MatchesAny(symbol.Value),
            _ => null,
        };
        if (matchAny is not { } any)
        {
            return null;
        }

        var entries = arguments.Entries
            .Where(entry => entry.Key is string key && !key.StartsWith(ReservedKeyPrefix, StringComparison.Ordinal))
            .Select(entry => ((string)entry.Key!, entry.Value))
            .ToList();
        return any
            ? (message, _) => entries.Any(entry => PropertyMatches(message, entry))
            : (message, _) => entries.All(entry => PropertyMatches(message, entry));
    }

    private static bool? MatchesAny(string text) => text switch
    {
        "all" => false,
        "any" => true,
        _ => null,
    };

    private static bool PropertyMatches(Amqp1BusSelectorMessage message, (string Name, object? Value) entry) =>
        message.ApplicationProperty(entry.Name) is { } value
        && (entry.Value is null || (value is byte[] bytes && entry.Value is byte[] expected ? bytes.AsSpan().SequenceEqual(expected) : value.Equals(entry.Value)));

    /// <summary>The bus's consumer predicate for a queue link, or <see langword="null"/> when nothing is filtered.</summary>
    /// <param name="queueName">The queue the link consumes.</param>
    /// <returns>The predicate.</returns>
    public Func<BusMessage, bool>? QueuePredicate(string queueName) =>
        predicates.Count == 0 ? null : message => Accepts(message, queueName);

    /// <summary>
    /// Whether every applied filter accepts a message. Its routing key is its <c>subject</c>, else its bus
    /// topic as AMQP 0-9-1 writes it (<see cref="AmqpTopicExchangeNames.FromCanonicalTopic"/>); a message
    /// sent straight to the queue (its bus topic is the queue's name) with no <c>subject</c> has none.
    /// </summary>
    /// <param name="message">The bus message.</param>
    /// <param name="queueName">The queue a queue link consumes; <see langword="null"/> on a topic link.</param>
    /// <returns>Whether the link is offered the message.</returns>
    public bool Accepts(BusMessage message, string? queueName)
    {
        if (predicates.Count == 0)
        {
            return true;
        }

        var selectorMessage = Amqp1BusSelectorMessage.Read(message);
        var routingKey = selectorMessage.Properties?.Subject
            ?? (message.Topic == queueName ? null : AmqpTopicExchangeNames.FromCanonicalTopic(message.Topic).Name);
        return predicates.TrueForAll(predicate => predicate(selectorMessage, routingKey));
    }
}
