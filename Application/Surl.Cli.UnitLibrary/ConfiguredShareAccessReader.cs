namespace Surl.Cli;

/// <summary>
/// Reads one <c>--share-access &lt;share&gt;=&lt;member&gt;:&lt;r|rw&gt;,...</c> argument (ADR-0101 decisions 3 to 5,
/// ADR-0102 decision 2.2) into a <see cref="ConfiguredShareAccess"/>: the item by <see cref="Shape"/>, each list
/// member split at its last <c>:</c>, then a member granted twice in one list refused with row 14's text.
/// Whether the share is a <c>--share</c> is <see cref="ConfiguredContentConflicts"/>' to check; whether each
/// member names an account or group is <c>Surl.Composition</c>'s (BL-988).
/// </summary>
internal static class ConfiguredShareAccessReader
{
    /// <summary>The <c>--share-access</c> item: a share, <c>=</c> and grants of <c>r</c> or <c>rw</c>, with no keys or flags.</summary>
    public static readonly ConfigurationItemShape Shape = new(
        "<share>=<member>:<r|rw>,...",
        '=',
        ConfigurationItemValueKind.Grants,
        new Dictionary<string, ConfigurationItemAttributeKind>(StringComparer.Ordinal),
        ["r", "rw"]);

    /// <summary><see cref="Read"/> with the item's argument type (ADR-0046 decision 6).</summary>
    public static readonly OptionArgumentReading<ConfiguredShareAccess> Reading = new(Read, Shape.Reading.Type);

    /// <summary>Reads one <c>--share-access</c> argument.</summary>
    /// <param name="argument">The argument as given.</param>
    /// <param name="value">The share's grants, when accepted; what was read before the refusal otherwise.</param>
    /// <returns>The refusal reason after <c>option &lt;name&gt;: </c>, or <see langword="null"/>.</returns>
    public static string? Read(string argument, out ConfiguredShareAccess value)
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
