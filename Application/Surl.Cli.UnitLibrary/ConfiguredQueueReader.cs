namespace Surl.Cli;

/// <summary>
/// Reads one <c>--queue &lt;name&gt;[;durable]</c> argument (ADR-0101 decisions 3 to 5, ADR-0102 decision 3.2) into a
/// <see cref="ConfiguredQueue"/> by <see cref="Shape"/>.
/// </summary>
internal static class ConfiguredQueueReader
{
    /// <summary>The flag that keeps the queue's messages across starts.</summary>
    public const string DurableFlag = "durable";

    /// <summary>The <c>--queue</c> item: a name with no separator and the flag <c>durable</c>.</summary>
    public static readonly ConfigurationItemShape Shape = new(
        "<name>[;durable]",
        separator: null,
        ConfigurationItemValueKind.None,
        new Dictionary<string, ConfigurationItemAttributeKind>(StringComparer.Ordinal)
        {
            [DurableFlag] = ConfigurationItemAttributeKind.Flag,
        },
        []);

    /// <summary><see cref="Read"/> with the item's argument type (ADR-0046 decision 6).</summary>
    public static readonly OptionArgumentReading<ConfiguredQueue> Reading = new(Read, Shape.Reading.Type);

    /// <summary>A queue name an earlier <c>--queue</c> already gave (ADR-0101 decision 5 row 14).</summary>
    /// <param name="name">The queue's name.</param>
    /// <returns><c>queue &lt;name&gt; is given twice</c>.</returns>
    public static string QueueGivenTwice(string name) => $"queue {name} is given twice";

    /// <summary>Reads one <c>--queue</c> argument.</summary>
    /// <param name="argument">The argument as given.</param>
    /// <param name="value">The queue, when accepted; one with an empty name otherwise.</param>
    /// <returns>The refusal reason after <c>option &lt;name&gt;: </c>, or <see langword="null"/>.</returns>
    public static string? Read(string argument, out ConfiguredQueue value)
    {
        var failure = ConfigurationItemReader.Read(Shape, argument, out var item);
        value = new(item.Name, item.Flags.Contains(DurableFlag));
        return failure;
    }
}
