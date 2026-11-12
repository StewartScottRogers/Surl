namespace Surl.Cli;

/// <summary>
/// Reads one <c>--topic &lt;filter&gt;[;retain][;qos=&lt;0|1|2&gt;]</c> argument (ADR-0101 decisions 3 to 5, ADR-0102
/// decision 3.1) into a <see cref="ConfiguredTopic"/> by <see cref="Shape"/>; <c>qos</c> takes exactly
/// <c>0</c>, <c>1</c> or <c>2</c>. The filter's wildcard placement is the bus's to check at start (BL-991).
/// </summary>
internal static class ConfiguredTopicReader
{
    /// <summary>The flag that retains every publish on a matching topic.</summary>
    public const string RetainFlag = "retain";

    /// <summary>The key that caps the QoS a matching publish is carried at.</summary>
    public const string QosKey = "qos";

    /// <summary>The <c>--topic</c> item: a filter with no separator, the flag <c>retain</c> and the key <c>qos</c>.</summary>
    public static readonly ConfigurationItemShape Shape = new(
        "<filter>[;retain][;qos=<0|1|2>]",
        separator: null,
        ConfigurationItemValueKind.None,
        new Dictionary<string, ConfigurationItemAttributeKind>(StringComparer.Ordinal)
        {
            [RetainFlag] = ConfigurationItemAttributeKind.Flag,
            [QosKey] = ConfigurationItemAttributeKind.Value,
        },
        []);

    /// <summary><see cref="Read"/> with the item's argument type (ADR-0046 decision 6).</summary>
    public static readonly OptionArgumentReading<ConfiguredTopic> Reading = new(Read, Shape.Reading.Type);

    /// <summary>The topic a refused argument leaves behind.</summary>
    public static ConfiguredTopic Empty { get; } = new(string.Empty, IsRetained: false, MaxQos: null);

    /// <summary>
    /// A filter an earlier <c>--topic</c>, or an earlier <c>--topic-access</c>, already gave (ADR-0101 decision 5
    /// row 14, whose nouns have no separate word for an access filter).
    /// </summary>
    /// <param name="filter">The filter.</param>
    /// <returns><c>topic &lt;filter&gt; is given twice</c>.</returns>
    public static string TopicGivenTwice(string filter) => $"topic {filter} is given twice";

    /// <summary>Reads one <c>--topic</c> argument.</summary>
    /// <param name="argument">The argument as given.</param>
    /// <param name="value">The topic, when accepted; <see cref="Empty"/> otherwise.</param>
    /// <returns>The refusal reason after <c>option &lt;name&gt;: </c>, or <see langword="null"/>.</returns>
    public static string? Read(string argument, out ConfiguredTopic value)
    {
        value = Empty;
        var failure = ConfigurationItemReader.Read(Shape, argument, out var item);
        if (failure is not null)
        {
            return failure;
        }

        int? maxQos = item.Values.GetValueOrDefault(QosKey) switch
        {
            null => null,
            "0" => 0,
            "1" => 1,
            "2" => 2,
            _ => -1,
        };
        if (maxQos < 0)
        {
            return OptionArgumentReader.BadlyUsed;
        }

        value = new(item.Name, item.Flags.Contains(RetainFlag), maxQos);
        return null;
    }
}
