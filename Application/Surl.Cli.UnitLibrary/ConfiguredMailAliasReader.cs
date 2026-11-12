namespace Surl.Cli;

/// <summary>
/// Reads one <c>--mail-alias &lt;address&gt;=&lt;address&gt;,...</c> argument (ADR-0101 decisions 3 to 5, ADR-0102
/// decision 1.3) into a <see cref="ConfiguredMailAlias"/>: the item by <see cref="Shape"/>, the alias and each target
/// a mail address <see cref="MailDomainNames.ReadAddress"/> accepts, then a target named twice (compared as
/// normalised) refused with row 14's text. The record keeps what was written. An alias given twice, outside
/// every <c>--mail-domain</c> or also a mailbox is <see cref="ConfiguredMailConflicts"/>' to refuse; a target
/// outside every domain, naming no mailbox, or an alias that includes itself, the mail store's (BL-994).
/// </summary>
internal static class ConfiguredMailAliasReader
{
    /// <summary>The <c>--mail-alias</c> item: an address, <c>=</c> and a list of addresses, with no keys or flags.</summary>
    public static readonly ConfigurationItemShape Shape = new(
        "<address>=<address>,...",
        '=',
        ConfigurationItemValueKind.List,
        new Dictionary<string, ConfigurationItemAttributeKind>(StringComparer.Ordinal),
        []);

    /// <summary><see cref="Read"/> with the item's argument type (ADR-0046 decision 6).</summary>
    public static readonly OptionArgumentReading<ConfiguredMailAlias> Reading = new(Read, Shape.Reading.Type);

    /// <summary>The alias a refused argument leaves behind.</summary>
    public static ConfiguredMailAlias Empty { get; } = new(string.Empty, []);

    /// <summary>An alias address an earlier <c>--mail-alias</c> already gave (ADR-0101 decision 5 row 14).</summary>
    /// <param name="address">The address as written.</param>
    /// <returns><c>alias &lt;address&gt; is given twice</c>.</returns>
    public static string AliasGivenTwice(string address) => $"alias {address} is given twice";

    /// <summary>Reads one <c>--mail-alias</c> argument.</summary>
    /// <param name="argument">The argument as given.</param>
    /// <param name="value">The alias, when accepted; <see cref="Empty"/> otherwise.</param>
    /// <returns>The refusal reason after <c>option &lt;name&gt;: </c>, or <see langword="null"/>.</returns>
    public static string? Read(string argument, out ConfiguredMailAlias value)
    {
        value = Empty;
        var failure = ConfigurationItemReader.Read(Shape, argument, out var item)
            ?? MailDomainNames.ReadAddress(item.Name, out _)
            ?? CheckTargets(item.List);
        if (failure is null)
        {
            value = new(item.Name, item.List);
        }

        return failure;
    }

    private static string? CheckTargets(IReadOnlyList<string> targets)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var target in targets)
        {
            var failure = MailDomainNames.ReadAddress(target, out var normalised);
            if (failure is not null)
            {
                return failure;
            }

            if (!seen.Add(normalised))
            {
                return ConfiguredGroupReader.MemberGivenTwice(target);
            }
        }

        return null;
    }
}
