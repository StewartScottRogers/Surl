namespace Surl.Cli;

/// <summary>
/// Reads one <c>--topic-access &lt;filter&gt;=&lt;member&gt;:&lt;pub|sub|pubsub&gt;,...</c> argument (ADR-0101 decisions
/// 3 to 5, ADR-0102 decision 3.4) into a <see cref="ConfiguredTopicAccess"/>: the item by <see cref="Shape"/>,
/// each list member split at its last <c>:</c>, then a member granted twice in one list refused with row 14's
/// text. Whether each member names an account or group is <c>Surl.Composition</c>'s to check (BL-991).
/// </summary>
internal static class ConfiguredTopicAccessReader
{
    /// <summary>The <c>--topic-access</c> item: a filter, <c>=</c> and grants of <c>pub</c>, <c>sub</c> or <c>pubsub</c>, with no keys or flags.</summary>
    public static readonly ConfigurationItemShape Shape = new(
        "<filter>=<member>:<pub|sub|pubsub>,...",
        '=',
        ConfigurationItemValueKind.Grants,
        new Dictionary<string, ConfigurationItemAttributeKind>(StringComparer.Ordinal),
        ["pub", "sub", "pubsub"]);

    /// <summary><see cref="Read"/> with the item's argument type (ADR-0046 decision 6).</summary>
    public static readonly OptionArgumentReading<ConfiguredTopicAccess> Reading = new(Read, Shape.Reading.Type);

    /// <summary>Reads one <c>--topic-access</c> argument.</summary>
    /// <param name="argument">The argument as given.</param>
    /// <param name="value">The filter's grants, when accepted; what was read before the refusal otherwise.</param>
    /// <returns>The refusal reason after <c>option &lt;name&gt;: </c>, or <see langword="null"/>.</returns>
    public static string? Read(string argument, out ConfiguredTopicAccess value)
    {
        var failure = ConfigurationItemReader.Read(Shape, argument, out var item);
        value = new(item.Name, item.Grants);
        if (failure is not null)
        {
            return failure;
        }

        var seen = new HashSet<ConfiguredMember>();
        var repeated = item.Grants.FirstOrDefault(grant => !seen.Add(grant.Member));
        return repeated is null ? null : ConfiguredGroupReader.MemberGivenTwice(repeated.Member.AsWritten);
    }
}
