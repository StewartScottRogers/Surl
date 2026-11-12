namespace Surl.Cli;

/// <summary>
/// Reads one <c>--bucket &lt;name&gt;=&lt;path&gt;</c> argument (ADR-0101 decisions 3 to 5, ADR-0102 decision 2.4)
/// into a <see cref="ConfiguredBucket"/> by <see cref="Shape"/>. Whether the content path exists is
/// <c>Surl.Composition</c>'s to check at start (BL-988).
/// </summary>
internal static class ConfiguredBucketReader
{
    /// <summary>The <c>--bucket</c> item: a name, <c>=</c> and a content-store path, with no keys or flags.</summary>
    public static readonly ConfigurationItemShape Shape = new(
        "<name>=<path>",
        '=',
        ConfigurationItemValueKind.Text,
        new Dictionary<string, ConfigurationItemAttributeKind>(StringComparer.Ordinal),
        []);

    /// <summary><see cref="Read"/> with the item's argument type (ADR-0046 decision 6).</summary>
    public static readonly OptionArgumentReading<ConfiguredBucket> Reading = new(Read, Shape.Reading.Type);

    /// <summary>A bucket name an earlier <c>--bucket</c> already gave (ADR-0101 decision 5 row 14).</summary>
    /// <param name="name">The bucket's name.</param>
    /// <returns><c>bucket &lt;name&gt; is given twice</c>.</returns>
    public static string BucketGivenTwice(string name) => $"bucket {name} is given twice";

    /// <summary>Reads one <c>--bucket</c> argument.</summary>
    /// <param name="argument">The argument as given.</param>
    /// <param name="value">The bucket, when accepted; one with an empty name and path otherwise.</param>
    /// <returns>The refusal reason after <c>option &lt;name&gt;: </c>, or <see langword="null"/>.</returns>
    public static string? Read(string argument, out ConfiguredBucket value)
    {
        var failure = ConfigurationItemReader.Read(Shape, argument, out var item);
        value = failure is null ? new(item.Name, item.Value!) : new(string.Empty, string.Empty);
        return failure;
    }
}
