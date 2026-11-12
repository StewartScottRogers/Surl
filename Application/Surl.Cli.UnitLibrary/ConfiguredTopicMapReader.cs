namespace Surl.Cli;

/// <summary>
/// Reads one <c>--topic-map &lt;scheme&gt;:&lt;name&gt;=&lt;scheme&gt;:&lt;name&gt;</c> argument (ADR-0101 decisions 3 to 5,
/// ADR-0102 decision 3.3) into a <see cref="ConfiguredTopicMap"/>: the item by <see cref="Shape"/>, then each
/// decoded side split at its first <c>:</c>, its scheme lower-cased and checked against every scheme some surl
/// server speaks (<see cref="ConfiguredMailboxReader.IsAnySurlScheme"/>), as <c>protocols=</c> is. Whether a
/// registered messaging server speaks it is <c>Surl.Composition</c>'s to check at start (BL-991).
/// </summary>
internal static class ConfiguredTopicMapReader
{
    /// <summary>The <c>--topic-map</c> item: two <c>&lt;scheme&gt;:&lt;name&gt;</c> sides joined by <c>=</c>, with no keys or flags.</summary>
    public static readonly ConfigurationItemShape Shape = new(
        "<scheme>:<name>=<scheme>:<name>",
        '=',
        ConfigurationItemValueKind.Name,
        new Dictionary<string, ConfigurationItemAttributeKind>(StringComparer.Ordinal),
        []);

    /// <summary><see cref="Read"/> with the item's argument type (ADR-0046 decision 6).</summary>
    public static readonly OptionArgumentReading<ConfiguredTopicMap> Reading = new(Read, Shape.Reading.Type);

    /// <summary>The map a refused argument leaves behind.</summary>
    public static ConfiguredTopicMap Empty { get; } = new(string.Empty, string.Empty, string.Empty, string.Empty);

    /// <summary>A source an earlier <c>--topic-map</c> already gave (ADR-0101 decision 5 row 14).</summary>
    /// <param name="source">The source, <c>&lt;scheme&gt;:&lt;name&gt;</c>.</param>
    /// <returns><c>topic map &lt;source&gt; is given twice</c>.</returns>
    public static string TopicMapGivenTwice(string source) => $"topic map {source} is given twice";

    /// <summary>Reads one <c>--topic-map</c> argument.</summary>
    /// <param name="argument">The argument as given.</param>
    /// <param name="value">The map, when accepted; <see cref="Empty"/> otherwise.</param>
    /// <returns>The refusal reason after <c>option &lt;name&gt;: </c>, or <see langword="null"/>.</returns>
    public static string? Read(string argument, out ConfiguredTopicMap value)
    {
        value = Empty;
        var failure = ConfigurationItemReader.Read(Shape, argument, out var item);
        if (failure is not null)
        {
            return failure;
        }

        var fromFailure = ReadSide(item.Name, out var fromScheme, out var fromName);
        var toFailure = ReadSide(item.Value!, out var toScheme, out var toName);
        failure = fromFailure ?? toFailure;
        if (failure is null)
        {
            value = new(fromScheme, fromName, toScheme, toName);
        }

        return failure;
    }

    /// <summary>Splits one decoded side at its first <c>:</c> into a known, lower-cased scheme and a non-empty name.</summary>
    private static string? ReadSide(string side, out string scheme, out string name)
    {
        var at = side.IndexOf(':', StringComparison.Ordinal);
        scheme = string.Empty;
        name = string.Empty;
        if (at < 0)
        {
            return ConfigurationItemReader.Expected(Shape.Argument);
        }

        scheme = side[..at].ToLowerInvariant();
        name = side[(at + 1)..];
        if (scheme.Length == 0 || name.Length == 0)
        {
            return ConfigurationItemReader.EmptyField;
        }

        return ConfiguredMailboxReader.IsAnySurlScheme(scheme) ? null : ConfiguredMailboxReader.UnknownScheme(scheme);
    }
}
