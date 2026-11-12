namespace Surl.Cli;

/// <summary>
/// Reads one <c>--share &lt;name&gt;=&lt;path&gt;[;readonly][;protocols=&lt;scheme&gt;,...]</c> argument (ADR-0101
/// decisions 3 to 5, ADR-0102 decision 2.1) into a <see cref="ConfiguredShare"/>: the item by
/// <see cref="Shape"/>, then each scheme checked as <c>--mailbox</c> checks its own
/// (<see cref="ConfiguredMailboxReader.CheckProtocols"/>). The name's path-segment rules and the path's
/// existence are <c>Surl.Composition</c>'s to check at start (BL-988).
/// </summary>
internal static class ConfiguredShareReader
{
    /// <summary>The flag that refuses every write in the share.</summary>
    public const string ReadOnlyFlag = "readonly";

    /// <summary>The <c>--share</c> item: a name, <c>=</c> and a path, with the flag <c>readonly</c> and the list key <c>protocols</c>.</summary>
    public static readonly ConfigurationItemShape Shape = new(
        "<name>=<path>[;readonly][;protocols=<scheme>,...]",
        '=',
        ConfigurationItemValueKind.Text,
        new Dictionary<string, ConfigurationItemAttributeKind>(StringComparer.Ordinal)
        {
            [ReadOnlyFlag] = ConfigurationItemAttributeKind.Flag,
            [ConfiguredMailboxReader.ProtocolsKey] = ConfigurationItemAttributeKind.List,
        },
        []);

    /// <summary><see cref="Read"/> with the item's argument type (ADR-0046 decision 6).</summary>
    public static readonly OptionArgumentReading<ConfiguredShare> Reading = new(Read, Shape.Reading.Type);

    /// <summary>The share a refused argument leaves behind.</summary>
    public static ConfiguredShare Empty { get; } = new(string.Empty, string.Empty, IsReadOnly: false, Protocols: null);

    /// <summary>A share name an earlier <c>--share</c> already gave (ADR-0101 decision 5 row 14).</summary>
    /// <param name="name">The share's name.</param>
    /// <returns><c>share &lt;name&gt; is given twice</c>.</returns>
    public static string ShareGivenTwice(string name) => $"share {name} is given twice";

    /// <summary>Reads one <c>--share</c> argument.</summary>
    /// <param name="argument">The argument as given.</param>
    /// <param name="value">The share, when accepted; <see cref="Empty"/> otherwise.</param>
    /// <returns>The refusal reason after <c>option &lt;name&gt;: </c>, or <see langword="null"/>.</returns>
    public static string? Read(string argument, out ConfiguredShare value)
    {
        value = Empty;
        var failure = ConfigurationItemReader.Read(Shape, argument, out var item);
        if (failure is not null)
        {
            return failure;
        }

        var protocols = item.Lists.GetValueOrDefault(ConfiguredMailboxReader.ProtocolsKey);
        var schemes = new List<string>();
        failure = ConfiguredMailboxReader.CheckProtocols(protocols ?? [], ConfiguredMailboxReader.IsAnySurlScheme, schemes);
        if (failure is null)
        {
            value = new(item.Name, item.Value!, item.Flags.Contains(ReadOnlyFlag), protocols is null ? null : schemes);
        }

        return failure;
    }
}
