namespace Surl.Cli;

/// <summary>
/// Reads one <c>--group &lt;name&gt;=&lt;member&gt;,...</c> argument (ADR-0101 decisions 3 to 5) into a
/// <see cref="ConfiguredGroup"/>: the item by <see cref="Shape"/>, then a member named twice in the
/// list refused with row 14's text. Whether each member names an account or group is
/// <c>Surl.Composition</c>'s to check at start (row 18).
/// </summary>
internal static class ConfiguredGroupReader
{
    /// <summary>The <c>--group</c> item: a name, <c>=</c> and a member list, with no keys or flags.</summary>
    public static readonly ConfigurationItemShape Shape = new(
        "<name>=<member>,...",
        '=',
        ConfigurationItemValueKind.Members,
        new Dictionary<string, ConfigurationItemAttributeKind>(StringComparer.Ordinal),
        []);

    /// <summary><see cref="Read"/> with the item's argument type (ADR-0046 decision 6).</summary>
    public static readonly OptionArgumentReading<ConfiguredGroup> Reading = new(Read, Shape.Reading.Type);

    /// <summary>A member written twice in one list (ADR-0101 decision 5 row 14).</summary>
    /// <param name="member">The member as written, <c>@</c> included for a group.</param>
    /// <returns><c>member &lt;member&gt; is given twice</c>.</returns>
    public static string MemberGivenTwice(string member) => $"member {member} is given twice";

    /// <summary>A group name an earlier <c>--group</c> already gave (ADR-0101 decision 5 row 14).</summary>
    /// <param name="name">The group's name.</param>
    /// <returns><c>group &lt;name&gt; is given twice</c>.</returns>
    public static string GroupGivenTwice(string name) => $"group {name} is given twice";

    /// <summary>Reads one <c>--group</c> argument.</summary>
    /// <param name="argument">The argument as given.</param>
    /// <param name="value">The group, when accepted; an empty group otherwise.</param>
    /// <returns>The refusal reason after <c>option &lt;name&gt;: </c>, or <see langword="null"/>.</returns>
    public static string? Read(string argument, out ConfiguredGroup value)
    {
        var failure = ConfigurationItemReader.Read(Shape, argument, out var item);
        value = new(item.Name, item.Members);
        if (failure is not null)
        {
            return failure;
        }

        var seen = new HashSet<ConfiguredMember>();
        var repeated = item.Members.FirstOrDefault(member => !seen.Add(member));
        return repeated is null ? null : MemberGivenTwice(repeated.AsWritten);
    }
}
