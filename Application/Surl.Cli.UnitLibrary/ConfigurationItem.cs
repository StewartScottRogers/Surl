namespace Surl.Cli;

/// <summary>
/// One layer-2 option argument read by <see cref="ConfigurationItemReader"/> (ADR-0101 decision 3):
/// the head's name and value and the item's <c>;</c>-attributes, every field percent-decoded.
/// Only the head value the option's <see cref="ConfigurationItemValueKind"/> names is filled; the
/// others are null or empty. The option task maps it to its own <c>Configured*</c> record.
/// </summary>
/// <param name="Name">The head's name, before the separator (the whole head when there is none); never empty.</param>
/// <param name="Value">The field after the separator, for <see cref="ConfigurationItemValueKind.Text"/> and <see cref="ConfigurationItemValueKind.Name"/>; otherwise null.</param>
/// <param name="List">The names after the separator, for <see cref="ConfigurationItemValueKind.List"/>; otherwise empty.</param>
/// <param name="Members">The members after the separator, for <see cref="ConfigurationItemValueKind.Members"/>; otherwise empty.</param>
/// <param name="Grants">The grants after the separator, for <see cref="ConfigurationItemValueKind.Grants"/>; otherwise empty.</param>
/// <param name="Flags">The flags given, lower-cased.</param>
/// <param name="Values">Each <see cref="ConfigurationItemAttributeKind.Value"/> key given, lower-cased, with its value.</param>
/// <param name="Lists">Each <see cref="ConfigurationItemAttributeKind.List"/> key given, lower-cased, with its names.</param>
internal sealed record ConfigurationItem(
    string Name,
    string? Value,
    IReadOnlyList<string> List,
    IReadOnlyList<ConfiguredMember> Members,
    IReadOnlyList<ConfiguredGrant> Grants,
    IReadOnlySet<string> Flags,
    IReadOnlyDictionary<string, string> Values,
    IReadOnlyDictionary<string, IReadOnlyList<string>> Lists)
{
    /// <summary>The item a refused argument leaves behind: an empty name and nothing else.</summary>
    public static ConfigurationItem Empty { get; } = new(
        string.Empty,
        null,
        [],
        [],
        [],
        new HashSet<string>(StringComparer.Ordinal),
        new Dictionary<string, string>(StringComparer.Ordinal),
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal));

    /// <summary>
    /// Names the item without its value, which may be a password (ADR-0101 decision 5), so no log
    /// or exception text can hold one.
    /// </summary>
    /// <returns><c>ConfigurationItem { Name = &lt;name&gt; }</c>.</returns>
    public override string ToString() => $"ConfigurationItem {{ Name = {Name} }}";
}
